using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Enemies;
using Game.Items;
using Game.Npcs;
using Game.Players;
using Game.Skills;
using Game.Story;

namespace Game
{
    /// <summary>
    /// Chỗ nối các hệ thống chép từ GAME_2D (túi đồ, trang bị, cây nhân vật, nâng cấp vũ khí, nhiệm vụ, NPC) với game.
    /// Giữ người chơi mình (<see cref="Me"/>), dựng nó từ PlayerData và nối sự kiện giữa các hệ thống.
    /// </summary>
    /// <remarks>
    /// Game: thay cho phần GameController của GAME_2D lo những việc này (đăng ký bảng, GrantKillRewards, hồi máu). Bỏ phần
    /// spawn nhân vật, anim, map, quái 2D và đồ rơi trên đất. OnlineCore (client) đăng ký bảng từ Resources/LocalPlaySettings
    /// rồi gọi vào đây; tiến trình do server giữ.
    /// </remarks>
    public static class CoreSystems
    {
        /// <summary>Người chơi mình; null khi chưa <see cref="Begin"/> / <see cref="StartOnline"/>.</summary>
        public static PlayerProfile Me { get; private set; }

        /// <summary>Người chơi mình vừa đổi (Begin / End). UI nghe để nối lại vào Me mới.</summary>
        public static event Action<PlayerProfile> MeChanged;

        /// <summary>Hạ quái xong: (id loại quái, EXP, vàng, đồ vào túi). UI nghe để hiện chữ / thông báo.</summary>
        public static event Action<int, long, long, List<DropRoll>> KillRewarded;

        static readonly List<DropRoll> rolls = new List<DropRoll>(8);
        static float regenCarry;   // phần lẻ của máu hồi, dồn tới đủ 1 mới cộng
        static float mpRegenCarry; // như trên, cho mana

        /// <summary>
        /// Mana hồi mỗi giây (skill tốn mana, cây nhân vật chưa có nút hồi mana). 0 = không hồi, chỉ bình mana.
        /// </summary>
        public static float BaseMpRegen { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Me = null;
            MeChanged = null;
            KillRewarded = null;
            regenCarry = 0f;
            mpRegenCarry = 0f;
            BaseMpRegen = 0f;
        }

        /// <summary>Đăng ký các bảng cho cả game (X.Current), giống GameController.Awake của GAME_2D. Null thì bỏ qua bảng đó.</summary>
        public static void Use(ItemDatabase items, SkillDatabase skills, WeaponDatabase weapons,
                               MonsterDatabase monsters, PlayerUpgradeTree upgradeTree, OptionDatabase options,
                               NpcDatabase npcs, QuestDatabase quests, DropDatabase drops, GearUpgradeTable gearUpgrades)
        {
            ItemDatabase.Use(items);
            SkillDatabase.Use(skills);
            WeaponDatabase.Use(weapons);
            MonsterDatabase.Use(monsters);
            PlayerUpgradeTree.Use(upgradeTree);
            OptionDatabase.Use(options);
            NpcDatabase.Use(npcs);
            QuestDatabase.Use(quests);
            DropDatabase.Use(drops);
            GearUpgradeTable.Use(gearUpgrades);
        }

        // ---------------- người chơi ----------------

        /// <summary>
        /// Hồ sơ server gửi về (gói PLAYER_DATA) thành người chơi mình. Server giữ tiến trình.
        /// </summary>
        public static PlayerProfile StartOnline(PlayerData fromServer)
        {
            PlayerProfile me;
            NetSync.Suppress(true);   // đang dựng lại đúng bản của server: đừng gửi ngược (ô dùng nhanh...) lên server
            try { me = BuildProfile(fromServer); }
            finally { NetSync.Suppress(false); }
            Begin(me);
            return me;
        }

        /// <summary>
        /// Dựng một PlayerProfile từ PlayerData (giống GameController.StartLocalGame của GAME_2D, bỏ phần spawn / anim). Dùng
        /// chung cho client online và server giả lập (mỗi người chơi của server một hồ sơ). Không nối vào hệ thống.
        /// </summary>
        public static PlayerProfile BuildProfile(PlayerData playerData)
        {
            var me = new PlayerProfile();
            me.SetInfo(playerData.ToEntityData(Vector2.zero));
            bool bagFromSave = playerData.FillInventory(me.Inventory);
            for (int i = 0; i < playerData.skillSlots.Length && i < PlayerProfile.SkillSlots; i++)
            {
                PlayerSkill s = playerData.skillSlots[i];
                if (s != null) me.SetSkill(i, s.templateId, s.level);
            }
            if (playerData.basicSkill != null) me.SetBasicSkill(playerData.basicSkill.templateId, playerData.basicSkill.level);
            // vũ khí quyết định skill đánh thường: áp sau để đè skill vừa nạp, cấp giữ theo PlayerData.
            // Cây nâng cấp nạp trước để chỉ số vũ khí tính đúng ngay lúc cầm.
            me.LoadUpgrades(playerData.weaponUpgrades);
            if (playerData.weaponId > 0) me.SetWeapon(playerData.weaponId);
            // đồ đang mặc (kể cả vũ khí vừa cầm) rời hành trang lên ô trang bị
            me.LoadEquipment(playerData.equipped, removeFromBag: !bagFromSave, gear: playerData.equipGear);
            me.LoadQuickItems(playerData.quickItems);
            // cấp / EXP / cây nhân vật: nạp sau SetInfo để máu tối đa của PlayerData làm mức gốc cho cây cộng lên
            me.LoadProgress(playerData.level, playerData.exp, playerData.potentialPoints, playerData.playerUpgrades);
            me.SetGold(playerData.gold);
            me.Story.Load(playerData.npcAffinity, playerData.quests, playerData.storyFlags, playerData.trackedQuest);
            return me;
        }

        // ---------------- server giả lập ----------------

        /// <summary>
        /// Chạy code của server giả lập trên hồ sơ <paramref name="player"/> (một người chơi của server): trong khối using,
        /// <see cref="Me"/> là hồ sơ đó nên QuestSystem / NpcSystem / thưởng hạ quái... chạy nguyên văn trên dữ liệu của server;
        /// không gửi gì lên mạng; thông báo (toast) vào <paramref name="toasts"/> thay vì hiện lên UI của client cùng máy.
        /// Chỉ gọi trên main thread, không lồng hai người chơi khác nhau.
        /// </summary>
        public static ServerScope AsServer(PlayerProfile player, List<string> toasts = null) => new ServerScope(player, toasts);

        public struct ServerScope : IDisposable
        {
            readonly PlayerProfile previousMe;
            readonly List<string> previousToasts;
            bool open;

            internal ServerScope(PlayerProfile player, List<string> toasts)
            {
                previousMe = Me;
                Me = player;
                previousToasts = NetSync.Enter(toasts);
                open = true;
            }

            public void Dispose()
            {
                if (!open) return;
                open = false;
                Me = previousMe;
                NetSync.Exit(previousToasts);
            }
        }

        /// <summary>
        /// Lấy <paramref name="me"/> làm người chơi mình (đã nạp sẵn từ nguồn nào cũng được) và nối các hệ thống vào nó.
        /// Người chơi cũ (nếu có) được tháo ra trước.
        /// </summary>
        public static void Begin(PlayerProfile me)
        {
            if (me == Me) return;
            End();
            if (me == null) return;

            Me = me;
            me.Progress.LeveledUp += HandleLevelUp;
            me.Story.BecameEnemy += QuestSystem.OnBecameEnemy;   // việc đang làm với kẻ thù tự hủy
            me.Story.Changed += QuestSystem.NotifyChanged;
            me.Inventory.Changed += QuestSystem.NotifyChanged;
            QuestSystem.Retrack();   // bản lưu cũ chưa theo dõi việc nào: theo việc nên làm tiếp để có chỉ đường ngay
            MeChanged?.Invoke(me);
            QuestSystem.NotifyChanged();   // NPC dựng trước người chơi thì giờ mới vẽ được dấu
        }

        /// <summary>Tháo người chơi mình khỏi các hệ thống (rời game, đổi nhân vật).</summary>
        public static void End()
        {
            PlayerProfile me = Me;
            if (me == null) return;

            me.Progress.LeveledUp -= HandleLevelUp;
            me.Story.BecameEnemy -= QuestSystem.OnBecameEnemy;
            me.Story.Changed -= QuestSystem.NotifyChanged;
            me.Inventory.Changed -= QuestSystem.NotifyChanged;
            Me = null;
            regenCarry = 0f;
            mpRegenCarry = 0f;
            MeChanged?.Invoke(null);
        }

        // ---------------- sự kiện game ----------------

        /// <summary>
        /// Người chơi mình vừa hạ quái loại <paramref name="templateId"/> (id trong MonsterDatabase): đếm mục tiêu nhiệm vụ,
        /// cộng EXP (thưởng % EXP của cây), vàng ±20% (thưởng % vàng), và đồ theo bảng rơi đồ của loại quái (tỉ lệ nhân
        /// % Tìm đồ; quái 3D không có trong MonsterDatabase thì tìm bảng theo id mẫu, mob_&lt;templateId&gt;; quái chưa có bảng
        /// riêng thì dùng bảng chung <see cref="DropDatabase.MonsterDefaultTable"/>, không có cả
        /// bảng chung thì <paramref name="gemDropChance"/> ra một viên ngọc).
        /// Loại quái không có trong bảng: EXP / vàng theo <paramref name="level"/> (bãi quái có khai cấp,
        /// <see cref="MonsterTemplate.ExpAtLevel"/>), không có cấp thì theo <paramref name="maxHp"/>.
        /// </summary>
        /// <param name="groundDrops">null: đồ vào thẳng túi. Khác null: đồ không vào túi mà được thêm vào danh sách này để
        /// người gọi thả xuống đất; người chơi nhặt thì mới vào túi.</param>
        public static void GrantKillRewards(int templateId, long maxHp = 0, float gemDropChance = 0.06f, List<DropRoll> groundDrops = null,
                                            int level = 0)
        {
            PlayerProfile me = Me;
            if (me == null) return;
            QuestSystem.OnMonsterKilled(templateId);

            PlayerStats bonus = me.Bonus;
            MonsterDatabase table = MonsterDatabase.Current;
            MonsterTemplate t = table != null && templateId > 0 ? table.Get(templateId) : null;

            long exp = t != null ? t.ExpReward : level > 0 ? MonsterTemplate.ExpAtLevel(level) : MonsterTemplate.DefaultExp(maxHp);
            me.Progress.AddExp(exp, bonus.expBonus);

            long gold = t != null ? t.GoldReward : level > 0 ? MonsterTemplate.GoldAtLevel(level) : MonsterTemplate.DefaultGold(maxHp);
            float goldScale = UnityEngine.Random.Range(0.8f, 1.2f) * (1f + Mathf.Max(0f, bonus.goldFind) / 100f);
            gold = Math.Max(1, (long)Math.Round(gold * goldScale));
            me.AddGold(gold);

            rolls.Clear();
            float find = Mathf.Max(0f, bonus.itemFind);
            ItemDatabase items = ItemDatabase.Current;
            DropTable drops = DropDatabase.Current != null ? DropDatabase.Current.ForMonster(t != null ? t.name : null, templateId) : null;
            if (drops != null)
            {
                DropDatabase.Roll(drops, find, Random01, rolls);
            }
            else if (items != null && UnityEngine.Random.value < Mathf.Min(0.9f, gemDropChance * (1f + find / 100f)))
            {
                // chưa có bảng cho loại quái này (cả bảng chung): một cơ hội rơi ngọc ngẫu nhiên
                List<ItemTemplate> gems = items.OfKind(ItemKind.Gem);
                if (gems.Count > 0) rolls.Add(new DropRoll { item = gems[UnityEngine.Random.Range(0, gems.Count)].name, count = 1 });
            }

            for (int i = rolls.Count - 1; i >= 0; i--)
            {
                ItemTemplate item = items != null ? items.Find(rolls[i].item) : null;
                if (item == null)
                {
                    Debug.LogWarning("CoreSystems: bảng rơi đồ có vật phẩm '" + rolls[i].item + "' không có trong ItemDatabase.");
                    rolls.RemoveAt(i);
                    continue;
                }
                if (groundDrops != null) groundDrops.Add(rolls[i]);
                else me.Loot(item.id, rolls[i].count);
            }
            // server giả lập chạy chung process: chữ thưởng là việc của client (nó nhận qua gói đồng bộ), đừng bắn vào UI ở đây
            if (!NetSync.InServer) KillRewarded?.Invoke(templateId, exp, gold, rolls);
        }

        /// <summary>
        /// Chỉ khi online và còn sống (dự đoán tại máy, PROFILE_SYNC của server ghi đè): hồi máu theo nút hồi phục của cây (máu / giây), hồi mana theo
        /// <see cref="BaseMpRegen"/>, đếm lùi thuốc tăng chỉ số. Gọi mỗi frame.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            PlayerProfile me = Me;
            if (!NetSync.Online || me == null || me.isDie || me.data == null) return;   // online: tính trước tại máy như mọi thứ khác

            me.TickBuffs(deltaTime);
            Regen(me, TypeHP_KI.Type_Hp, me.Bonus.hpRegen, me.data.curHp, me.data.maxHp, ref regenCarry, deltaTime);
            Regen(me, TypeHP_KI.Type_MP, BaseMpRegen, me.data.curMp, me.data.maxMp, ref mpRegenCarry, deltaTime);
        }

        // hồi rate mỗi giây, chưa đầy mới hồi; phần lẻ dồn vào carry tới đủ 1 mới cộng
        static void Regen(PlayerProfile me, TypeHP_KI type, float rate, long cur, long max, ref float carry, float deltaTime)
        {
            if (rate <= 0f || cur >= max)
            {
                carry = 0f;
                return;
            }

            carry += rate * deltaTime;
            if (carry < 1f) return;
            long add = (long)carry;
            carry -= add;
            me.UpdateStats(type, Math.Min(max, cur + add));
        }

        static void HandleLevelUp(int level, int gained)
        {
            PlayerUpgradeTree tree = PlayerUpgradeTree.Current;
            if (tree != null && tree.refillOnLevelUp) Me?.Refill();
        }

        static float Random01() => UnityEngine.Random.value;
    }
}
