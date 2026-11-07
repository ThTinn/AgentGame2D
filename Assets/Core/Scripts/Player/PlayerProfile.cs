using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Items;
using Game.Skills;

namespace Game.Players
{
    /// <summary>
    /// Hồ sơ của người chơi mình: túi đồ, đồ đang mặc, vàng, cấp / EXP / cây nhân vật, cốt truyện, vũ khí + cây nâng cấp
    /// vũ khí, ô skill, máu / mana, chỉ số cộng thêm, vật phẩm dùng được (bình máu / mana, thuốc tăng chỉ số) và hai ô
    /// dùng nhanh. C# thuần, không phải MonoBehaviour.
    /// </summary>
    /// <remarks>
    /// Game: tách từ Player của GAME_2D (MonoBehaviour 2D gồm cả di chuyển, anim, đánh nhau). Ở đây chỉ giữ phần dữ liệu
    /// và luật mà QuestSystem / NpcSystem / cây nâng cấp cần; code giữ đúng như bản gốc. Nhân vật 3D của Game nghe các event
    /// (<see cref="StatChanged"/>, <see cref="OnBonusChanged"/>, <see cref="OnWeaponChanged"/>...) để áp lên model của mình.
    /// </remarks>
    public class PlayerProfile
    {
        /// <summary>Số ô skill (AnimLayout.SkillCount).</summary>
        public const int SkillSlots = Game.Anim.AnimLayout.SkillCount;

        public Inventory Inventory { get; } = new Inventory();

        /// <summary>Đồ đang mặc / cầm, mỗi ô một id vật phẩm.</summary>
        public Equipment Equipment { get; } = new Equipment();

        /// <summary>Cấp, EXP, điểm tiềm năng và cấp từng nút của cây nhân vật.</summary>
        public PlayerProgress Progress { get; } = new PlayerProgress();

        /// <summary>Thân thiện với NPC, nhiệm vụ, cờ cốt truyện.</summary>
        public Game.Story.StoryState Story { get; } = new Game.Story.StoryState();

        /// <summary>Id, tên, máu / mana hiện tại và tối đa. Null tới khi <see cref="SetInfo"/>.</summary>
        public EntityData data;

        /// <summary>
        /// Anim của nhân vật 3D đang mang hồ sơ này (CharacterAnimator), giống Player.CharAnim của GAME_2D: UI đọc bộ anim
        /// và tư thế đang mặc để vẽ xem trước. Null khi chưa có nhân vật.
        /// </summary>
        public Game.Anim.IAnimView Anim { get; set; }

        public bool isDie;

        /// <summary>Máu hoặc mana vừa đổi: (loại, hiện tại, tối đa).</summary>
        public event Action<TypeHP_KI, long, long> StatChanged;
        /// <summary>Vàng vừa đổi: (lượng đổi, âm là tiêu). Nạp lúc vào game bắn với 0.</summary>
        public event Action<long> OnGoldChanged;
        /// <summary>Vừa nhặt được một vật phẩm (id trong ItemDatabase).</summary>
        public event Action<int> OnItemLooted;
        /// <summary>Ô skill (0-3) vừa đổi; skill null = ô trống.</summary>
        public event Action<int, PlayerSkill> OnSkillChanged;
        /// <summary>Skill đánh thường vừa đổi; null = không có, đánh thường theo chỉ số mặc định.</summary>
        public event Action<PlayerSkill> OnBasicSkillChanged;
        /// <summary>Vũ khí vừa đổi (id trong WeaponDatabase, 0 = không cầm).</summary>
        public event Action<int> OnWeaponChanged;
        /// <summary>Một nút nâng cấp vừa đổi cấp: (weaponId, upgradeId, rank mới).</summary>
        public event Action<int, int, int> OnUpgradeChanged;
        /// <summary>Chỉ số cộng thêm (cây nhân vật + đồ đang mặc + thuốc đang có tác dụng) vừa tính lại (<see cref="Bonus"/>).</summary>
        public event Action OnBonusChanged;
        /// <summary>Vừa dùng một vật phẩm (id trong ItemDatabase): bình máu / mana, thuốc tăng chỉ số.</summary>
        public event Action<int> OnItemUsed;
        /// <summary>Thuốc tăng chỉ số vừa có / hết tác dụng (<see cref="Buffs"/>).</summary>
        public event Action OnBuffsChanged;
        /// <summary>Ô dùng nhanh vừa đổi: (ô, id vật phẩm; 0 = ô trống).</summary>
        public event Action<int, int> OnQuickSlotChanged;
        /// <summary>Có kết quả cường hóa: (kết quả, cấp hiện tại của món). Online: đến sau bản hồ sơ server gửi về.</summary>
        public event Action<GearOutcome, int> OnGearUpgraded;
        /// <summary>Có kết quả khoét lỗ: (kết quả, số lỗ hiện tại của món). Online: đến sau bản hồ sơ server gửi về.</summary>
        public event Action<GearOutcome, int> OnSocketOpened;

        readonly PlayerSkill[] skills = new PlayerSkill[SkillSlots];
        PlayerSkill basicSkill;
        int weaponId;
        readonly WeaponUpgradeState upgrades = new WeaponUpgradeState();
        WeaponStats weaponStats;   // gộp sẵn từ cây nâng cấp của vũ khí đang cầm
        PlayerStats bonus;         // gộp sẵn: cây nâng cấp nhân vật + đồ đang mặc
        PlayerStats gearBonus;     // riêng phần đồ đang mặc (UI tách "đồ +x")
        PlayerStats buffBonus;     // riêng phần thuốc tăng chỉ số đang có tác dụng
        long baseMaxHp, baseMaxMp; // máu / mana tối đa gốc (chưa cộng cây), chốt ở lần áp đầu tiên
        bool hasBaseStats;

        public PlayerProfile()
        {
            upgrades.Changed += HandleUpgradeChanged;
            Progress.RankChanged += HandleProgressRankChanged;
            Equipment.Changed += HandleEquipmentChanged;
        }

        /// <summary>Gán id / tên / máu / mana (PlayerData.ToEntityData từ hồ sơ server gửi).</summary>
        public void SetInfo(EntityData info)
        {
            data = info;
            hasBaseStats = false;
            ApplyBonus();
        }

        public string DisplayName => data != null ? data.nameObject : "";

        // ---------------- máu / mana ----------------

        public void UpdateStats(TypeHP_KI type, long cur, long max = -1)
        {
            if (data == null) return;
            switch (type)
            {
                case TypeHP_KI.Type_Hp:
                    if (max > 0) data.maxHp = max;
                    data.curHp = Math.Clamp(cur, 0, data.maxHp);
                    StatChanged?.Invoke(type, data.curHp, data.maxHp);
                    break;
                case TypeHP_KI.Type_MP:
                    if (max > 0) data.maxMp = max;
                    data.curMp = Math.Clamp(cur, 0, data.maxMp);
                    StatChanged?.Invoke(type, data.curMp, data.maxMp);
                    break;
            }
        }

        /// <summary>Hồi đầy máu và mana (lên cấp).</summary>
        public void Refill()
        {
            if (data == null || isDie) return;
            UpdateStats(TypeHP_KI.Type_Hp, data.maxHp);
            UpdateStats(TypeHP_KI.Type_MP, data.maxMp);
        }

        /// <summary>
        /// Trừ mana để tung skill. false (không trừ gì) khi không đủ. amount &lt;= 0 hoặc chưa có data thì luôn được.
        /// </summary>
        public bool TrySpendMana(int amount)
        {
            if (amount <= 0 || data == null) return true;
            if (data.curMp < amount) return false;
            UpdateStats(TypeHP_KI.Type_MP, data.curMp - amount);
            return true;
        }

        // ---------------- dùng vật phẩm (bình máu / mana, thuốc tăng chỉ số) ----------------

        /// <summary>
        /// Giây chờ giữa hai lần dùng: mọi bình máu chung một lượt chờ, mọi bình mana chung một lượt, thuốc tăng chỉ số thì
        /// từng loại thuốc riêng (uống liền thuốc tốc đánh rồi thuốc tốc chạy được).
        /// </summary>
        public const float UseCooldown = 1f;

        /// <summary>Số ô dùng nhanh trên HUD.</summary>
        public const int QuickSlotCount = 2;

        // Time.time lúc dùng lại được, theo nhóm chờ (CooldownGroup)
        readonly Dictionary<int, float> useReadyAt = new Dictionary<int, float>();
        readonly int[] quickItems = new int[QuickSlotCount];

        // bình máu / bình mana: một nhóm mỗi loại (số âm); thuốc: theo id vật phẩm
        static int CooldownGroup(ItemTemplate item) => item.kind == ItemKind.Buff ? item.id : -(int)item.kind;

        /// <summary>Giây còn phải chờ mới dùng lại được <paramref name="item"/>; 0 = dùng được.</summary>
        public float UseCooldownLeft(ItemTemplate item)
        {
            if (item == null || !useReadyAt.TryGetValue(CooldownGroup(item), out float ready)) return 0f;
            return Mathf.Max(0f, ready - Time.time);
        }

        /// <summary>
        /// Dùng một cái <paramref name="itemId"/> lấy từ hành trang: bình máu / mana hồi ngay, thuốc tăng chỉ số có tác dụng
        /// <see cref="ItemTemplate.BuffSeconds"/> giây (uống lại cùng loại thì tính lại từ đầu, không cộng dồn).
        /// false (không mất gì) khi không dùng được: hết vật phẩm, đang chờ, đã chết, máu / mana đang đầy.
        /// </summary>
        public bool UseItem(int itemId)
        {
            ItemDatabase table = ItemDatabase.Current;
            ItemTemplate item = table != null ? table.Get(itemId) : null;
            if (item == null || !item.Usable || data == null || isDie) return false;
            // đang chờ: im lặng (ô dùng nhanh đã có vòng hồi), kiểm trước để bấm liên tục không ra chuỗi thông báo
            if (UseCooldownLeft(item) > 0f) return false;
            if (!Inventory.Has(itemId))
            {
                Notify.Toast(Game.Story.StoryText.L("Hết {0}|Out of {0}", Game.Story.StoryText.T(item.Label)));
                return false;
            }

            // tác dụng theo loại; máu / mana đầy thì không uống (khỏi phí bình)
            switch (item.kind)
            {
                case ItemKind.HpPotion:
                    if (data.curHp >= data.maxHp)
                    {
                        Notify.Toast(Game.Story.StoryText.L("Máu đang đầy|HP is full"));
                        return false;
                    }
                    UpdateStats(TypeHP_KI.Type_Hp, Math.Min(data.maxHp, data.curHp + item.RestoreHp));
                    break;
                case ItemKind.MpPotion:
                    if (data.curMp >= data.maxMp)
                    {
                        Notify.Toast(Game.Story.StoryText.L("Mana đang đầy|MP is full"));
                        return false;
                    }
                    UpdateStats(TypeHP_KI.Type_MP, Math.Min(data.maxMp, data.curMp + item.RestoreMp));
                    break;
                case ItemKind.Buff:
                    AddBuff(item);
                    break;
            }

            // dùng được: mất một cái, bắt đầu lượt chờ của nhóm
            Inventory.Remove(itemId);
            useReadyAt[CooldownGroup(item)] = Time.time + UseCooldown;
            OnItemUsed?.Invoke(itemId);
            NetSync.Send?.UseItem(itemId);
            return true;
        }

        // ---------------- ô dùng nhanh ----------------

        /// <summary>Id vật phẩm gán ở ô dùng nhanh <paramref name="slot"/>; 0 = ô trống.</summary>
        public int GetQuickItem(int slot) => slot >= 0 && slot < quickItems.Length ? quickItems[slot] : 0;

        /// <summary>
        /// Gán vật phẩm dùng được vào ô dùng nhanh (0 = bỏ trống). Ô giữ id vật phẩm chứ không giữ ô hành trang, nên hết
        /// rồi nhặt thêm vẫn dùng được. Vật phẩm đã nằm ở ô khác thì hai ô đổi chỗ cho nhau.
        /// </summary>
        public void SetQuickItem(int slot, int itemId)
        {
            if (slot < 0 || slot >= quickItems.Length) return;
            ItemTemplate item = itemId > 0 && ItemDatabase.Current != null ? ItemDatabase.Current.Get(itemId) : null;
            if (item == null || !item.Usable) itemId = 0;
            int old = quickItems[slot];
            if (old == itemId) return;

            // món này đang ở ô khác: ô đó nhận món cũ của ô này (đổi chỗ), để một món không nằm hai ô
            if (itemId > 0)
                for (int i = 0; i < quickItems.Length; i++)
                    if (i != slot && quickItems[i] == itemId)
                    {
                        quickItems[i] = old;
                        OnQuickSlotChanged?.Invoke(i, old);
                    }
            quickItems[slot] = itemId;
            OnQuickSlotChanged?.Invoke(slot, itemId);
            NetSync.Send?.SetQuickItem(slot, itemId);
        }

        /// <summary>Dùng vật phẩm ở ô dùng nhanh. false khi ô trống hoặc không dùng được (xem <see cref="UseItem"/>).</summary>
        public bool UseQuickSlot(int slot)
        {
            int id = GetQuickItem(slot);
            return id > 0 && UseItem(id);
        }

        /// <summary>Nạp ô dùng nhanh đã lưu (PlayerData.quickItems), theo thứ tự ô.</summary>
        public void LoadQuickItems(IList<int> saved)
        {
            for (int i = 0; i < quickItems.Length; i++) SetQuickItem(i, saved != null && i < saved.Count ? saved[i] : 0);
        }

        /// <summary>Chép ô dùng nhanh để lưu.</summary>
        public void SaveQuickItems(List<int> into)
        {
            into.Clear();
            into.AddRange(quickItems);
        }

        // ---------------- thuốc tăng chỉ số ----------------

        /// <summary>Một thuốc tăng chỉ số đang có tác dụng.</summary>
        public struct ActiveBuff
        {
            public int itemId;
            /// <summary>Giây còn lại.</summary>
            public float remaining;
            /// <summary>Tổng số giây lúc uống (UI vẽ thanh thời gian).</summary>
            public float duration;
        }

        readonly List<ActiveBuff> buffs = new List<ActiveBuff>();

        /// <summary>Thuốc tăng chỉ số đang có tác dụng, theo thứ tự uống.</summary>
        public IReadOnlyList<ActiveBuff> Buffs => buffs;

        /// <summary>Riêng phần chỉ số từ thuốc đang có tác dụng (đã nằm trong <see cref="Bonus"/>).</summary>
        public PlayerStats BuffBonus => buffBonus;

        // Uống thuốc: thêm vào danh sách (đã có cùng loại thì đặt lại thời gian, không cộng dồn chỉ số) rồi tính lại Bonus.
        void AddBuff(ItemTemplate item)
        {
            float seconds = item.BuffSeconds;
            var buff = new ActiveBuff { itemId = item.id, remaining = seconds, duration = seconds };
            for (int i = 0; i < buffs.Count; i++)   // vòng for thay cho FindIndex(lambda): khỏi tạo closure mỗi lần uống
            {
                if (buffs[i].itemId != item.id) continue;
                buffs[i] = buff;
                OnBuffsChanged?.Invoke();   // chỉ số không đổi (cùng thuốc), chỉ thời gian: không cần ApplyBonus
                return;
            }
            buffs.Add(buff);
            ApplyBonus();
            OnBuffsChanged?.Invoke();
        }

        /// <summary>Đếm lùi thời gian thuốc; hết thì bỏ và tính lại chỉ số. CoreSystems.Tick gọi mỗi frame.</summary>
        public void TickBuffs(float deltaTime)
        {
            if (buffs.Count == 0) return;
            bool expired = false;
            // duyệt ngược để RemoveAt không làm lệch chỉ số; ActiveBuff là struct nên phải ghi lại vào list sau khi trừ
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff b = buffs[i];
                b.remaining -= deltaTime;
                if (b.remaining > 0f)
                {
                    buffs[i] = b;
                    continue;
                }
                buffs.RemoveAt(i);
                expired = true;
                ItemTemplate item = ItemDatabase.Current != null ? ItemDatabase.Current.Get(b.itemId) : null;
                if (item != null) Notify.Toast(Game.Story.StoryText.L("{0} đã hết tác dụng|{0} wore off", Game.Story.StoryText.T(item.Label)));
            }
            // chỉ tính lại chỉ số khi có thuốc hết hạn (không tính mỗi frame)
            if (!expired) return;
            ApplyBonus();
            OnBuffsChanged?.Invoke();
        }

        /// <summary>Bỏ mọi thuốc đang có tác dụng (bị hạ gục).</summary>
        public void ClearBuffs()
        {
            if (buffs.Count == 0) return;
            buffs.Clear();
            ApplyBonus();
            OnBuffsChanged?.Invoke();
        }

        // ---------------- skill ----------------

        public PlayerSkill GetSkill(int slot) => slot >= 0 && slot < skills.Length ? skills[slot] : null;

        /// <summary>Skill đánh thường, null khi không có (đánh thường theo chỉ số mặc định).</summary>
        public PlayerSkill BasicSkill => basicSkill;

        /// <summary>Gắn skill vào ô slot (0-3). templateId 0 = gỡ ô.</summary>
        public void SetSkill(int slot, int templateId, int level = 1)
        {
            if (slot < 0 || slot >= skills.Length) return;
            PlayerSkill skill = templateId > 0 ? new PlayerSkill(templateId, Mathf.Max(1, level)) : null;
            if (skill != null) skill.cooldownScale = bonus.SkillCooldownScale;
            skills[slot] = skill;
            OnSkillChanged?.Invoke(slot, skill);
        }

        /// <summary>
        /// Người chơi gắn skill templateId vào ô slot (0 = gỡ ô). Một skill chỉ nằm ở một ô: đang ở ô khác thì gỡ khỏi ô đó,
        /// giữ nguyên cấp. Online: báo server (một gói cho cả việc chuyển ô).
        /// </summary>
        public bool EquipSkill(int slot, int templateId)
        {
            if (slot < 0 || slot >= skills.Length) return false;
            int level = 1;
            for (int i = 0; i < skills.Length; i++)
            {
                PlayerSkill s = skills[i];
                if (templateId == 0 || i == slot || s == null || s.templateId != templateId) continue;
                level = s.level;
                SetSkill(i, 0);
            }
            SetSkill(slot, templateId, level);
            NetSync.Send?.SetSkill(slot, templateId);
            return true;
        }

        /// <summary>Đổi skill đánh thường. Thường gọi qua <see cref="SetWeapon"/>. templateId 0 = bỏ.</summary>
        public void SetBasicSkill(int templateId, int level = 1)
        {
            basicSkill = templateId > 0 ? new PlayerSkill(templateId, Mathf.Max(1, level)) : null;
            OnBasicSkillChanged?.Invoke(basicSkill);
        }

        // ---------------- vũ khí ----------------

        /// <summary>Vũ khí đang cầm (id trong WeaponDatabase), 0 = không cầm.</summary>
        public int WeaponId => weaponId;

        /// <summary>
        /// Cầm vũ khí: đòn đánh thường đổi sang skill đánh thường của vũ khí (giữ cấp đang có).
        /// id không có trong bảng thì bỏ vũ khí, đánh thường theo chỉ số mặc định.
        /// </summary>
        public void SetWeapon(int id)
        {
            WeaponTemplate w = WeaponDatabase.Current != null ? WeaponDatabase.Current.Get(id) : null;
            weaponId = w != null ? w.id : 0;
            weaponStats = upgrades.Compute(w);
            SetBasicSkill(w != null ? w.basicSkill : 0, basicSkill?.level ?? 1);
            OnWeaponChanged?.Invoke(weaponId);
        }

        // ---------------- trang bị ----------------

        /// <summary>
        /// Mặc/cầm món ở ô <paramref name="bagSlot"/> của hành trang vào ô trang bị của nó. Món đang ở ô trang bị đó (nếu có)
        /// về lại hành trang, nằm đúng chỗ món mới vừa rời. Vũ khí thì đổi luôn đòn đánh thường (<see cref="SetWeapon"/>).
        /// Trả false khi món không trang bị được hoặc không còn chỗ cho món cũ.
        /// </summary>
        public bool Equip(int bagSlot)
        {
            ItemDatabase table = ItemDatabase.Current;
            ItemStack s = Inventory[bagSlot];
            ItemTemplate item = table != null && !s.IsEmpty ? table.Get(s.itemId) : null;
            if (item == null || !item.Equippable) return false;

            EquipSlot slot = item.Slot;
            ItemStack old = Equipment.Stack(slot);
            if (old.IsEmpty || !Inventory.ReplaceAt(bagSlot, old))
            {
                Inventory.RemoveAt(bagSlot, 1);
                // món mặc lên nằm trong một chồng nhiều cái: ô không trống ra, món cũ phải tìm chỗ khác
                if (!old.IsEmpty && !Inventory.AddGear(old))
                {
                    Inventory.AddGear(s);
                    Notify.Toast("Hành trang đầy");
                    return false;
                }
            }

            Equipment.Set(slot, s);
            if (slot == EquipSlot.Weapon) SetWeapon(item.WeaponId);
            NetSync.Send?.Equip(bagSlot);
            return true;
        }

        /// <summary>Tháo món ở ô trang bị về hành trang. Tháo vũ khí là đánh tay không. Trả false khi ô trống hoặc túi đầy.</summary>
        public bool Unequip(EquipSlot slot)
        {
            int id = Equipment[slot];
            if (id <= 0) return false;
            if (!Inventory.AddGear(Equipment.Stack(slot)))
            {
                Notify.Toast("Hành trang đầy");
                return false;
            }

            Equipment.Set(slot, 0);
            if (slot == EquipSlot.Weapon) SetWeapon(0);
            NetSync.Send?.Unequip((int)slot);
            return true;
        }

        /// <summary>Sắp xếp hành trang (gộp chồng, xếp theo loại).</summary>
        public void SortBag()
        {
            Inventory.Sort();
            NetSync.Send?.SortBag();
        }

        /// <summary>Bỏ <paramref name="count"/> cái ở ô hành trang <paramref name="bagSlot"/>. false khi ô không đủ.</summary>
        public bool Discard(int bagSlot, int count)
        {
            if (!Inventory.RemoveAt(bagSlot, count)) return false;
            NetSync.Send?.Discard(bagSlot, count);
            return true;
        }

        /// <summary>
        /// Nạp đồ đang mặc lúc vào game: vũ khí theo <see cref="WeaponId"/> (gọi sau SetWeapon), đồ mặc theo danh sách id đã lưu.
        /// Món nào đang có trong hành trang thì lấy ra (để không thành hai cái: một trong túi, một trên người).
        /// </summary>
        /// <param name="removeFromBag">true khi túi được dựng từ đồ khởi đầu (đồ đang mặc nằm lẫn trong đó, phải rút ra);
        /// false khi túi nạp từ file lưu (đã không chứa đồ đang mặc, rút nữa sẽ mất món trùng).</param>
        /// <param name="gear">Cấp, lỗ và đá khảm theo ô (chỉ số = (int)EquipSlot); null hoặc thiếu ô thì đồ thường.</param>
        public void LoadEquipment(IEnumerable<int> armor, bool removeFromBag = true, IList<ItemStack> gear = null)
        {
            ItemDatabase table = ItemDatabase.Current;
            if (table == null) return;

            ItemTemplate weapon = weaponId > 0 ? table.FindWeapon(weaponId) : null;
            if (weapon != null) Wear(weapon);

            if (armor == null) return;
            foreach (int id in armor)
            {
                ItemTemplate item = table.Get(id);
                if (item != null && item.Equippable && item.Slot != EquipSlot.Weapon) Wear(item);
            }

            void Wear(ItemTemplate item)
            {
                if (removeFromBag) Inventory.Remove(item.id);   // không có trong túi cũng được: đồ đã lưu là đang mặc
                int at = (int)item.Slot;
                ItemStack stack = gear != null && at < gear.Count ? gear[at] : default;
                stack.itemId = item.id;
                Equipment.Set(item.Slot, stack);
            }
        }

        // ---------------- cường hóa trang bị ----------------

        /// <summary>Chỗ của món đang mặc ở ô <paramref name="slot"/>, theo quy ước của <see cref="GearAt"/>.</summary>
        public static int GearSpot(EquipSlot slot) => -(int)slot;

        /// <summary>Món ở một chỗ: <paramref name="spot"/> >= 0 là ô hành trang, âm là ô trang bị (<see cref="GearSpot"/>).</summary>
        public ItemStack GearAt(int spot)
        {
            return spot >= 0 ? Inventory[spot] : Equipment.Stack((EquipSlot)(-spot));
        }

        void StoreGear(int spot, ItemStack gear)
        {
            if (spot >= 0) Inventory.SetGear(spot, gear);
            else Equipment.Set((EquipSlot)(-spot), gear);
        }

        /// <summary>Kiểm tra cường hóa được không; None là được. Đá bảo vệ chỉ cần khi lần nâng này có thể tụt cấp.</summary>
        public GearBlock CheckGearUpgrade(int spot, bool lucky, bool protect)
        {
            GearUpgradeTable rules = GearUpgradeTable.Current;
            ItemDatabase table = ItemDatabase.Current;
            if (rules == null || table == null) return GearBlock.NoTable;

            ItemStack gear = GearAt(spot);
            ItemTemplate item = gear.IsEmpty ? null : table.Get(gear.itemId);
            if (item == null) return GearBlock.NoItem;
            if (!item.Upgradable) return GearBlock.NotGear;

            GearUpgradeStep step = rules.Step(gear.level);
            if (step == null) return GearBlock.Maxed;
            if (!HasStone(table, GearStone.Upgrade, step.stones)) return GearBlock.NeedStone;
            if (lucky && !HasStone(table, GearStone.Lucky, 1)) return GearBlock.NeedLucky;
            if (protect && step.dropOnFail && !HasStone(table, GearStone.Protect, step.protectStones)) return GearBlock.NeedProtect;
            return Gold < step.gold ? GearBlock.NeedGold : GearBlock.None;
        }

        bool HasStone(ItemDatabase table, GearStone stone, int amount)
        {
            ItemTemplate it = table.FindStone(stone);
            return it != null && Inventory.Has(it.id, amount);
        }

        /// <summary>
        /// Cường hóa món ở <paramref name="spot"/> một lần: mất đá nâng cấp, vàng, đá may mắn (nếu dùng) rồi quay tỉ lệ. Đá bảo
        /// vệ chỉ mất khi cứu món khỏi tụt cấp. Tỉ lệ không dự đoán được nên client online chỉ gửi lên server và trả Pending,
        /// kết quả về qua <see cref="ReceiveGearResult"/>. Server (và test) thì tính ngay.
        /// </summary>
        public GearOutcome UpgradeGear(int spot, bool lucky, bool protect)
        {
            if (CheckGearUpgrade(spot, lucky, protect) != GearBlock.None) return GearOutcome.Refused;

            ICoreRemote remote = NetSync.Send;
            if (remote != null)
            {
                remote.UpgradeGear(spot, lucky, protect);
                return GearOutcome.Pending;
            }

            GearUpgradeTable rules = GearUpgradeTable.Current;
            ItemDatabase table = ItemDatabase.Current;
            ItemStack gear = GearAt(spot);
            int level = gear.level;
            GearUpgradeStep step = rules.Step(level);

            Inventory.Remove(table.FindStone(GearStone.Upgrade).id, step.stones);
            if (lucky) Inventory.Remove(table.FindStone(GearStone.Lucky).id);
            AddGold(-step.gold);

            GearOutcome outcome = GearOutcome.Failed;
            if (UnityEngine.Random.value * 100f < rules.ChanceOf(step, lucky))
            {
                outcome = GearOutcome.Success;
                level++;
            }
            else if (step.dropOnFail && level > 0)
            {
                if (protect)
                {
                    Inventory.Remove(table.FindStone(GearStone.Protect).id, step.protectStones);
                    outcome = GearOutcome.Protected;
                }
                else
                {
                    outcome = GearOutcome.Dropped;
                    level--;
                }
            }

            gear.level = level;
            StoreGear(spot, gear);
            OnGearUpgraded?.Invoke(outcome, level);
            return outcome;
        }

        /// <summary>Client online: server báo kết quả cường hóa (hồ sơ đã được ProfileSync cập nhật trước đó).</summary>
        public void ReceiveGearResult(GearOutcome outcome, int level) => OnGearUpgraded?.Invoke(outcome, level);

        // ---------------- khoét lỗ, khảm đá ----------------

        /// <summary>Kiểm tra khoét thêm lỗ được không; None là được.</summary>
        public GearBlock CheckOpenSocket(int spot)
        {
            GearUpgradeTable rules = GearUpgradeTable.Current;
            ItemDatabase table = ItemDatabase.Current;
            if (rules == null || table == null) return GearBlock.NoTable;

            ItemStack gear = GearAt(spot);
            ItemTemplate item = gear.IsEmpty ? null : table.Get(gear.itemId);
            if (item == null) return GearBlock.NoItem;
            if (!item.Upgradable) return GearBlock.NotGear;

            GearSocketStep step = rules.SocketStep(gear.sockets);
            if (step == null) return GearBlock.Maxed;
            if (!HasStone(table, GearStone.Drill, step.stones)) return GearBlock.NeedStone;
            return Gold < step.gold ? GearBlock.NeedGold : GearBlock.None;
        }

        /// <summary>
        /// Khoét thêm một lỗ trên món ở <paramref name="spot"/>: mất đá khoét lỗ và vàng rồi quay tỉ lệ, trượt thì món giữ
        /// nguyên. Như <see cref="UpgradeGear"/>, client online chỉ gửi lên server và trả Pending, kết quả về qua
        /// <see cref="ReceiveSocketResult"/>.
        /// </summary>
        public GearOutcome OpenSocket(int spot)
        {
            if (CheckOpenSocket(spot) != GearBlock.None) return GearOutcome.Refused;

            ICoreRemote remote = NetSync.Send;
            if (remote != null)
            {
                remote.OpenSocket(spot);
                return GearOutcome.Pending;
            }

            GearUpgradeTable rules = GearUpgradeTable.Current;
            ItemStack gear = GearAt(spot);
            GearSocketStep step = rules.SocketStep(gear.sockets);
            Inventory.Remove(ItemDatabase.Current.FindStone(GearStone.Drill).id, step.stones);
            AddGold(-step.gold);

            GearOutcome outcome = GearOutcome.Failed;
            if (UnityEngine.Random.value * 100f < step.chance)
            {
                outcome = GearOutcome.Success;
                gear.sockets++;
                StoreGear(spot, gear);
            }
            OnSocketOpened?.Invoke(outcome, gear.sockets);
            return outcome;
        }

        /// <summary>Client online: server báo kết quả khoét lỗ (hồ sơ đã được ProfileSync cập nhật trước đó).</summary>
        public void ReceiveSocketResult(GearOutcome outcome, int sockets) => OnSocketOpened?.Invoke(outcome, sockets);

        /// <summary>Kiểm tra khảm đá <paramref name="gemId"/> vào lỗ <paramref name="index"/> được không; None là được.</summary>
        public GearBlock CheckInlay(int spot, int index, int gemId)
        {
            ItemDatabase table = ItemDatabase.Current;
            if (table == null) return GearBlock.NoTable;

            ItemStack gear = GearAt(spot);
            ItemTemplate item = gear.IsEmpty ? null : table.Get(gear.itemId);
            if (item == null) return GearBlock.NoItem;
            if (!item.Upgradable) return GearBlock.NotGear;
            if (index < 0 || index >= gear.sockets) return GearBlock.NoSocket;
            if (gear.Gem(index) > 0) return GearBlock.SocketFull;

            ItemTemplate gem = table.Get(gemId);
            if (gem == null || gem.kind != ItemKind.Jewel) return GearBlock.NotJewel;
            if (!Inventory.Has(gemId)) return GearBlock.NeedJewel;
            // mỗi món một viên mỗi loại: không cho dồn cả ba lỗ vào một chỉ số
            for (int i = 0; i < gear.sockets; i++)
                if (gem.SameJewel(table.Get(gear.Gem(i)))) return GearBlock.SameJewel;
            return GearBlock.None;
        }

        /// <summary>Khảm đá vào lỗ trống: đá rời túi, luôn thành công. false (không mất gì) khi không khảm được.</summary>
        public bool InlayGem(int spot, int index, int gemId)
        {
            if (CheckInlay(spot, index, gemId) != GearBlock.None) return false;

            ItemStack gear = GearAt(spot);
            Inventory.Remove(gemId);
            gear.SetGem(index, gemId);
            StoreGear(spot, gear);
            NetSync.Send?.InlayGem(spot, index, gemId);
            return true;
        }

        /// <summary>Kiểm tra gỡ đá ở lỗ <paramref name="index"/> được không; None là được.</summary>
        public GearBlock CheckRemoveGem(int spot, int index)
        {
            GearUpgradeTable rules = GearUpgradeTable.Current;
            if (rules == null) return GearBlock.NoTable;
            ItemStack gear = GearAt(spot);
            if (gear.IsEmpty) return GearBlock.NoItem;
            if (index < 0 || index >= gear.sockets) return GearBlock.NoSocket;
            if (gear.Gem(index) <= 0) return GearBlock.SocketEmpty;
            if (Gold < rules.removeGemGold) return GearBlock.NeedGold;
            return Inventory.Has(gear.Gem(index)) || Inventory.UsedSlots < Inventory.Capacity ? GearBlock.None : GearBlock.BagFull;
        }

        /// <summary>Gỡ đá khảm về túi, mất vàng theo bảng luật. false (không mất gì) khi không gỡ được.</summary>
        public bool RemoveGem(int spot, int index)
        {
            if (CheckRemoveGem(spot, index) != GearBlock.None) return false;

            ItemStack gear = GearAt(spot);
            int gemId = gear.Gem(index);
            if (Inventory.Add(gemId) > 0) return false;   // chồng đá đã đầy và không còn ô trống
            AddGold(-GearUpgradeTable.Current.removeGemGold);
            gear.SetGem(index, 0);
            StoreGear(spot, gear);
            NetSync.Send?.RemoveGem(spot, index);
            return true;
        }

        // ---------------- nâng cấp vũ khí ----------------

        /// <summary>Chỉ số cộng thêm từ cây nâng cấp của vũ khí đang cầm.</summary>
        public WeaponStats WeaponStats => weaponStats;

        public int UpgradeRank(int weapon, int upgradeId) => upgrades.Rank(weapon, upgradeId);

        public bool CanUpgrade(WeaponTemplate weapon, WeaponUpgrade up) => upgrades.CanUpgrade(weapon, up);

        /// <summary>Tổng số cấp đã mở trên cây của vũ khí.</summary>
        public int UpgradesSpent(WeaponTemplate weapon) => upgrades.Spent(weapon);

        /// <summary>Chỉ số gộp của cây một vũ khí bất kỳ (UI xem trước vũ khí chưa cầm).</summary>
        public WeaponStats StatsOf(WeaponTemplate weapon) => upgrades.Compute(weapon);

        /// <summary>Nâng nút upgradeId của vũ khí một cấp (miễn phí). false khi chưa mở được / đã tối đa.</summary>
        public bool Upgrade(WeaponTemplate weapon, int upgradeId)
        {
            if (!upgrades.Upgrade(weapon, upgradeId)) return false;
            NetSync.Send?.UpgradeWeapon(weapon.id, upgradeId);
            return true;
        }

        /// <summary>Đưa cả cây nâng cấp của vũ khí về 0.</summary>
        public void ResetUpgrades(WeaponTemplate weapon)
        {
            if (weapon == null) return;
            upgrades.Reset(weapon);
            NetSync.Send?.ResetWeapon(weapon.id);
        }

        /// <summary>Chép cấp các nút nâng cấp vũ khí ra danh sách (lưu / gửi đồng bộ).</summary>
        public void SaveUpgrades(List<UpgradeRank> into) => upgrades.Save(into);

        /// <summary>
        /// Nạp bản chuẩn của server (ProfileSync): nút nào khác cấp thì đổi và báo <see cref="OnUpgradeChanged"/> để UI vẽ lại,
        /// chỉ số vũ khí đang cầm tính lại.
        /// </summary>
        public void ApplyUpgrades(IReadOnlyList<UpgradeRank> server)
        {
            upgrades.Save(upgradeBuffer);
            upgrades.Load(server);
            weaponStats = upgrades.Compute(WeaponDatabase.Current != null ? WeaponDatabase.Current.Get(weaponId) : null);
            foreach (UpgradeRank r in server)
                if (r.rank != RankIn(upgradeBuffer, r.weaponId, r.upgradeId)) OnUpgradeChanged?.Invoke(r.weaponId, r.upgradeId, r.rank);
            foreach (UpgradeRank r in upgradeBuffer)
                if (upgrades.Rank(r.weaponId, r.upgradeId) == 0) OnUpgradeChanged?.Invoke(r.weaponId, r.upgradeId, 0);
        }

        readonly List<UpgradeRank> upgradeBuffer = new List<UpgradeRank>();

        static int RankIn(List<UpgradeRank> list, int weapon, int upgradeId)
        {
            foreach (UpgradeRank r in list) if (r.weaponId == weapon && r.upgradeId == upgradeId) return r.rank;
            return 0;
        }

        /// <summary>Nạp cấp các nút đã lưu (PlayerData / server). Gọi trước SetWeapon để chỉ số đúng ngay từ đầu.</summary>
        public void LoadUpgrades(IEnumerable<UpgradeRank> saved)
        {
            upgrades.Load(saved);
            weaponStats = upgrades.Compute(WeaponDatabase.Current != null ? WeaponDatabase.Current.Get(weaponId) : null);
        }

        void HandleUpgradeChanged(int weapon, int upgradeId, int rank)
        {
            if (weapon == weaponId)
                weaponStats = upgrades.Compute(WeaponDatabase.Current != null ? WeaponDatabase.Current.Get(weaponId) : null);
            OnUpgradeChanged?.Invoke(weapon, upgradeId, rank);
        }

        // ---------------- tiến trình nhân vật ----------------

        /// <summary>Chỉ số cộng thêm: cây nâng cấp nhân vật + option của đồ đang mặc + thuốc tăng chỉ số đang có tác dụng.</summary>
        public PlayerStats Bonus => bonus;

        /// <summary>Riêng phần chỉ số từ đồ đang mặc (đã nằm trong <see cref="Bonus"/>).</summary>
        public PlayerStats GearBonus => gearBonus;

        /// <summary>
        /// Phần hồi chiêu đánh thường bị cắt: nâng cấp tốc đánh của vũ khí cộng tốc đánh của cây, chung một trần
        /// (<see cref="PlayerStats.MaxAttackSpeedCut"/>, 50% = đánh nhanh gấp đôi) để hai nguồn cộng lại không phá cân bằng.
        /// </summary>
        public float AttackSpeedCut => Mathf.Min(PlayerStats.MaxAttackSpeedCut / 100f, weaponStats.attackSpeed + bonus.attackSpeed / 100f);

        /// <summary>
        /// Chỉ số nâng cấp vũ khí đang cầm sau khi nhân "hiệu quả nâng cấp" của cây: phần cộng % (sát thương, nổ) và cộng
        /// liên tục (tầm, bán kính nổ, góc chém) được phóng theo %; phần đếm (số đạn, số mục tiêu) và tốc đánh giữ nguyên.
        /// </summary>
        public WeaponStats BoostedWeaponStats
        {
            get
            {
                WeaponStats w = weaponStats;
                float k = 1f + Mathf.Max(0f, bonus.upgradeBoost) / 100f;
                if (k == 1f) return w;
                w.damage = Mathf.RoundToInt(w.damage * k);
                w.explosionDamage = Mathf.RoundToInt(w.explosionDamage * k);
                w.explosionRadius *= k;
                w.range *= k;
                w.arc *= k;
                return w;
            }
        }

        // ---------------- vàng ----------------

        /// <summary>Vàng đang có. Hạ quái rơi vàng, cây nhân vật tăng % vàng nhặt được.</summary>
        public long Gold { get; private set; }

        public void SetGold(long amount)
        {
            Gold = Math.Max(0, amount);
            OnGoldChanged?.Invoke(0);
        }

        public void AddGold(long amount)
        {
            if (amount == 0) return;
            Gold = Math.Max(0, Gold + amount);
            OnGoldChanged?.Invoke(amount);
        }

        /// <summary>
        /// Bỏ vật phẩm nhặt được vào hành trang và báo UI. Hết ô thì báo "Hành trang đầy" và trả về số cái không vào được
        /// (người gọi quyết định phần thừa: thưởng nhiệm vụ thì mất).
        /// </summary>
        public int Loot(int itemId, int amount = 1)
        {
            int lost = Inventory.Add(itemId, amount);
            if (lost < amount) OnItemLooted?.Invoke(itemId);
            if (lost > 0) Notify.Toast(Game.Story.StoryText.L("Hành trang đầy|Inventory is full"));
            return lost;
        }

        /// <summary>
        /// Nạp tiến trình đã lưu (PlayerData / server) rồi áp chỉ số của cây. Gọi sau <see cref="SetInfo"/>: máu / mana tối đa
        /// lúc đó được chốt làm mức gốc để cây cộng lên.
        /// </summary>
        public void LoadProgress(int level, long exp, int points, IEnumerable<NodeRank> saved)
        {
            Progress.Load(level, exp, points, saved);
            ApplyBonus();
        }

        void HandleProgressRankChanged(int nodeId, int rank) => ApplyBonus();

        void HandleEquipmentChanged(EquipSlot slot) => ApplyBonus();

        /// <summary>Gộp option của mọi món đang mặc/cầm.</summary>
        PlayerStats ComputeGear()
        {
            var stats = new PlayerStats();
            ItemDatabase table = ItemDatabase.Current;
            if (table == null) return stats;
            for (int i = 1; i <= Equipment.SlotCount; i++)
            {
                var slot = (EquipSlot)i;
                ItemStack gear = Equipment.Stack(slot);
                GearUpgradeTable.EffectsOf(table.Get(gear.itemId), gear.level, gearEffects);
                GearUpgradeTable.AddGemEffects(gear, gearEffects);
                foreach (PlayerStatEffect e in gearEffects) stats.Add(e, 1);
            }
            return stats;
        }

        readonly List<PlayerStatEffect> gearEffects = new List<PlayerStatEffect>();

        /// <summary>Gộp option của mọi thuốc tăng chỉ số đang có tác dụng.</summary>
        PlayerStats ComputeBuffs()
        {
            var stats = new PlayerStats();
            ItemDatabase table = ItemDatabase.Current;
            if (table == null) return stats;
            foreach (ActiveBuff b in buffs)
            {
                ItemTemplate item = table.Get(b.itemId);
                if (item == null || item.options == null) continue;
                foreach (PlayerStatEffect e in item.options) stats.Add(e, 1);
            }
            return stats;
        }

        /// <summary>
        /// Tính lại chỉ số (cây + đồ đang mặc + thuốc): hồi chiêu skill, máu / mana tối đa (tốc chạy, % sát thương, chí mạng, giáp,
        /// hồi máu thì phía nhân vật đọc thẳng từ <see cref="Bonus"/>). Tối đa tăng bao nhiêu thì máu / mana hiện tại cộng
        /// bấy nhiêu; tháo đồ thì máu hiện tại chỉ bị kẹp xuống mức tối đa mới.
        /// </summary>
        void ApplyBonus()
        {
            gearBonus = ComputeGear();
            buffBonus = ComputeBuffs();
            bonus = Progress.Compute();
            bonus.Add(gearBonus);
            bonus.Add(buffBonus);
            for (int i = 0; i < skills.Length; i++) if (skills[i] != null) skills[i].cooldownScale = bonus.SkillCooldownScale;

            if (data != null)
            {
                if (!hasBaseStats)
                {
                    baseMaxHp = data.maxHp;
                    baseMaxMp = data.maxMp;
                    hasBaseStats = true;
                }
                ApplyMax(TypeHP_KI.Type_Hp, data.curHp, data.maxHp, baseMaxHp + bonus.maxHp);
                ApplyMax(TypeHP_KI.Type_MP, data.curMp, data.maxMp, baseMaxMp + bonus.maxMp);
            }
            OnBonusChanged?.Invoke();
        }

        void ApplyMax(TypeHP_KI type, long cur, long oldMax, long newMax)
        {
            if (newMax == oldMax) return;
            if (!isDie && newMax > oldMax) cur += newMax - oldMax;
            UpdateStats(type, Math.Clamp(cur, 0, newMax), newMax);
        }
    }
}
