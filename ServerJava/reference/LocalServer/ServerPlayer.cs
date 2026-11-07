using System;
using System.IO;
using UnityEngine;
using Game.Players;

namespace Game.ServerSim
{
    /// <summary>
    /// Một người chơi của server giả lập: tài khoản, hồ sơ Core (PlayerData lưu file + PlayerProfile dựng từ đó, luật chạy
    /// nguyên văn qua CoreSystems.AsServer), chỗ đứng, và những phần hồ sơ vừa đổi chờ gửi về client (<see cref="dirty"/>).
    /// </summary>
    public sealed class ServerPlayer
    {
        public readonly int id;
        public readonly string account;
        public ServerConnection conn;

        public PlayerData data;
        public PlayerProfile profile;

        public short mapId = -1;
        public Vector3 pos;              // vị trí theo gói MOVE (y thật của client); vừa vào map thì y = CoreNet.GroundY
        public int dir;
        public byte action, subAct;
        public bool mapReady;            // client đã dựng xong map (MAP_READY)
        public float nextReachCheck;

        // sống / chết, hồi chiêu, hồi máu-mana (ServerMonsters / ServerCombat)
        public bool dead;
        public float reviveAt;
        public float hpCarry, mpCarry;   // phần lẻ của máu / mana hồi, đủ 1 mới cộng
        public float nextDenyToast;
        public readonly System.Collections.Generic.Dictionary<short, float> readyAt = new System.Collections.Generic.Dictionary<short, float>();

        /// <summary>Skill_Template dựng theo hồ sơ này (ServerWorld.SkillOf), theo id gửi trong ATTACK.</summary>
        public struct CachedSkill
        {
            public Game.Skills.PlayerSkill skill;
            public int level;
            public Skill_Template template;
        }
        public readonly System.Collections.Generic.Dictionary<short, CachedSkill> skillCache = new System.Collections.Generic.Dictionary<short, CachedSkill>();

        /// <summary>Phần hồ sơ (ProfileSync.Story / Bag / Stats / Build) đổi từ lần gửi trước.</summary>
        public byte dirty;
        bool unsaved;

        // PLAYER_STATE đã báo người cùng map lần trước (ServerWorld.TickPlayerState so để biết khi nào gửi lại)
        public int sentLevel = -1, sentClan = -2;
        public long sentHp = -1, sentMaxHp = -1;

        public string Name => profile != null ? profile.DisplayName : account;

        public ServerPlayer(int id, string account)
        {
            this.id = id;
            this.account = account;
        }

        // ---------------- tài khoản ----------------

        public static string Folder => Path.Combine(Application.persistentDataPath, "local_server");

        static string FileOf(string account)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) account = account.Replace(c, '_');
            return Path.Combine(Folder, account + ".json");
        }

        /// <summary>Nạp hồ sơ của tài khoản từ file (chưa có thì dựng từ mẫu) rồi dựng PlayerProfile.</summary>
        public void Load(PlayerData template)
        {
            data = template != null ? UnityEngine.Object.Instantiate(template) : ScriptableObject.CreateInstance<PlayerData>();
            data.hideFlags = HideFlags.DontSave;
            data.name = "Server " + account;
            string path = FileOf(account);
            if (File.Exists(path))
            {
                try { JsonUtility.FromJsonOverwrite(File.ReadAllText(path), data); }
                catch (Exception e) { Debug.LogError("LocalServer: không đọc được " + path + ": " + e.Message); }
            }
            data.id = id;
            data.displayName = account;

            // dựng trong vùng server: các hàm nạp (ô dùng nhanh...) không gửi ngược gói lên server như client online
            using (CoreSystems.AsServer(null)) profile = CoreSystems.BuildProfile(data);
            PlayerProfile pr = profile;
            // luật CoreSystems.Begin nối cho người chơi mình, ở đây nối cho hồ sơ của server
            profile.Story.BecameEnemy += npc => { using (CoreSystems.AsServer(pr)) Game.Story.QuestSystem.OnBecameEnemy(npc); };
            profile.Progress.LeveledUp += (_, __) => { if (PlayerUpgradeTree.Current != null && PlayerUpgradeTree.Current.refillOnLevelUp) pr.Refill(); };
            profile.Story.Changed += () => { dirty |= ProfileSync.Story; unsaved = true; };
            profile.Inventory.Changed += () => { dirty |= ProfileSync.Bag; unsaved = true; };
            profile.Equipment.Changed += _ => { dirty |= ProfileSync.Bag; unsaved = true; };
            profile.OnQuickSlotChanged += (_, __) => { dirty |= ProfileSync.Bag; unsaved = true; };
            profile.Progress.Changed += () => { dirty |= ProfileSync.Stats; unsaved = true; };
            profile.OnGoldChanged += _ => { dirty |= ProfileSync.Stats; unsaved = true; };
            profile.StatChanged += (_, __, ___) => dirty |= ProfileSync.Stats;   // máu / mana: bị đánh, tốn mana, hồi
            // skill / nâng cấp vũ khí / cây nhân vật: ghi thẳng vào PlayerData (như CoreSystems.Save của chơi local)
            profile.OnSkillChanged += (slot, s) => { data.SetSkillSlot(slot, s); dirty |= ProfileSync.Build; unsaved = true; };
            profile.OnBasicSkillChanged += s => { data.SetBasicSkill(s); unsaved = true; };
            profile.OnUpgradeChanged += (w, up, rank) => { data.SetUpgradeRank(w, up, rank); dirty |= ProfileSync.Build; unsaved = true; };
            profile.Progress.RankChanged += (_, __) => { dirty |= ProfileSync.Build; unsaved = true; };
            dirty = 0;
        }

        /// <summary>Bảng tư thế của client (ANIM_SET): lưu tư thế vào hồ sơ, lần đăng nhập sau PLAYER_DATA mang về.</summary>
        public bool SetAnimVariant(int slot, int motion, int variantId)
        {
            if (data == null || slot < 0 || slot >= Game.Anim.AnimLayout.SlotCount || motion < 0) return false;
            var s = (Game.Anim.AnimSlot)slot;
            int index = Game.Anim.AnimLayout.MotionIndex(s, motion);
            if (index < 0 || index >= Game.Anim.AnimLayout.MotionTotal) return false;
            // motion vượt số clip của hành động sẽ rơi sang ô của hành động sau: chỉ nhận khi còn nằm trong hành động này
            if (slot + 1 < Game.Anim.AnimLayout.SlotCount && index >= Game.Anim.AnimLayout.MotionIndex((Game.Anim.AnimSlot)(slot + 1), 0)) return false;
            data.SetAnimVariant(s, motion, variantId);
            unsaved = true;
            return true;
        }

        /// <summary>Chép hồ sơ đang chơi vào PlayerData (để gửi client lúc vào game, hoặc ghi file).</summary>
        public void CopyToData()
        {
            data.SaveProgress(profile.Progress);
            data.gold = profile.Gold;
            data.SaveStory(profile.Story);
            data.SaveEquipment(profile.Equipment);
            data.SetWeapon(profile.WeaponId);
            data.SaveInventory(profile.Inventory);
            data.SaveQuickItems(profile);
        }

        /// <summary>
        /// Rời server (đã ghi file xong): hủy PlayerData dựng lúc đăng nhập. Nó mang HideFlags.DontSave nên Unity không tự dọn,
        /// mỗi lần đăng nhập trong Editor sẽ để lại một bản nằm mãi tới khi tắt Editor.
        /// </summary>
        public void Release()
        {
            if (data != null) UnityEngine.Object.Destroy(data);
            data = null;
        }

        /// <summary>Ghi file nếu có gì đổi (force: ghi luôn).</summary>
        public void Save(bool force = false)
        {
            if (profile == null || data == null || (!unsaved && !force)) return;
            unsaved = false;
            try
            {
                CopyToData();
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FileOf(account), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError("LocalServer: không ghi được hồ sơ " + account + ": " + e.Message);
            }
        }
    }
}
