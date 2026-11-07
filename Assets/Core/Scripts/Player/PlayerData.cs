using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Anim;
using Game.Core;
using Game.Items;
using Game.Skills;

namespace Game.Players
{
    /// <summary>
    /// Data tạm của người chơi mình khi chạy local (chưa có server, chưa có save). Một asset ở
    /// Assets/Data/Player/PlayerData.asset, GameController đọc lúc tạo nhân vật.
    /// Online thì không dùng: server gửi EntityData qua ME_INFO.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerData", menuName = "Core/Player Data")]
    public class PlayerData : ScriptableObject
    {
        /// <summary>Một dòng vật phẩm có sẵn trong hành trang lúc vào game.</summary>
        [Serializable]
        public class StartItem
        {
            [Tooltip("Id trong ItemDatabase.")]
            public int itemId;
            [Min(1)] public int amount = 1;
        }

        [Header("Thông tin")]
        [Tooltip("Người chơi bắt đầu từ 1 (quái từ 1000).")]
        public int id = 1;
        [Tooltip("Tên hiển thị, tối đa 20 ký tự như luật của server.")]
        public string displayName = "Hero";

        [Header("Chỉ số")]
        [Min(1)] public long maxHp = 100;
        [Min(0)] public long maxMp = 50;

        [Header("Tiến trình")]
        [Min(1)] public int level = 1;
        [Tooltip("EXP đã có trong cấp hiện tại.")]
        [Min(0)] public long exp;
        [Tooltip("Điểm tiềm năng chưa tiêu.")]
        [Min(0)] public int potentialPoints;
        [Tooltip("Vàng đang có (hạ quái rơi ra).")]
        [Min(0)] public long gold;
        [Tooltip("Cấp các nút của cây nâng cấp nhân vật (PlayerUpgradeTree). Bảng nhân vật ghi vào đây lúc chơi.")]
        public List<NodeRank> playerUpgrades = new List<NodeRank>();

        [Header("Cốt truyện")]
        [Tooltip("Độ thân thiện với từng NPC (theo NpcTemplate.name). NPC chưa có dòng nào thì lấy startAffinity của nó.")]
        public List<Game.Story.AffinityRecord> npcAffinity = new List<Game.Story.AffinityRecord>();
        [Tooltip("Nhiệm vụ đang làm và đã trả (theo QuestTemplate.name). Hộp thoại NPC ghi vào đây lúc chơi.")]
        public List<Game.Story.QuestRecord> quests = new List<Game.Story.QuestRecord>();
        [Tooltip("Cờ cốt truyện: lựa chọn đã chọn, ai đã thành kẻ thù (enemy:<npc>), việc đã trả (done:<quest>), kết cục.")]
        public List<string> storyFlags = new List<string>();
        [Tooltip("Nhiệm vụ đang theo dõi / chỉ đường (QuestTemplate.name). Trống = tự chọn.")]
        public string trackedQuest;

        [Header("Hành trang")]
        public List<StartItem> startItems = new List<StartItem>();
        [Tooltip("Id vật phẩm đồ mặc (ItemKind.Armor) đang mặc, mỗi ô một món. Vũ khí lưu riêng ở weaponId. "
                 + "Bảng hành trang ghi vào đây lúc chơi.")]
        public List<int> equipped = new List<int>();
        [Tooltip("Cấp cường hóa, lỗ và đá khảm của món đang mặc / cầm, chỉ số = EquipSlot (kể cả vũ khí). Bảng hành trang ghi vào đây lúc chơi.")]
        public List<ItemStack> equipGear = new List<ItemStack>();
        [Tooltip("Id vật phẩm gán vào hai ô dùng nhanh trên HUD (bình máu / mana, thuốc tăng chỉ số), theo thứ tự ô. "
                 + "0 = ô trống. HUD ghi vào đây lúc chơi.")]
        public List<int> quickItems = new List<int>();

        [Header("Anim")]
        [Tooltip("Id bộ anim trong AnimData (AnimSet.id).")]
        public int animSetId;
        [Tooltip("Id tư thế (AnimVariant.id) đang mặc của từng clip (idle0-2, run, dash, attack1-4, ...), "
                 + "chỉ số theo AnimLayout.MotionIndex. 0 = bộ mặc định. Server lưu tư thế đã chọn (ANIM_SET).")]
        [AnimSlotLabels] public int[] animVariants = new int[AnimLayout.MotionTotal];

        [Header("Vũ khí")]
        [Tooltip("Id vũ khí đang cầm trong WeaponDatabase. Vũ khí quyết định skill đánh thường. 0 = không cầm "
                 + "(đánh thường theo basicSkill bên dưới).")]
        public int weaponId;
        [Tooltip("Cấp các nút nâng cấp đã mở, của mọi vũ khí. Bảng nâng cấp ghi vào đây lúc chơi.")]
        public List<UpgradeRank> weaponUpgrades = new List<UpgradeRank>();

        [Header("Skill")]
        [Tooltip("Skill đánh thường và cấp. Có vũ khí thì skill lấy theo vũ khí, chỉ cấp ở đây là còn dùng. "
                 + "templateId 0 = đánh thường theo chỉ số Local combat của GameController.")]
        public PlayerSkill basicSkill = new PlayerSkill();
        [Tooltip("Bốn ô skill (phím 1-4): id skill trong SkillDatabase và cấp. templateId 0 = ô trống. "
                 + "Bảng trang bị ghi vào đây lúc chơi.")]
        public PlayerSkill[] skillSlots = NewSkillSlots();

        static PlayerSkill[] NewSkillSlots()
        {
            var slots = new PlayerSkill[AnimLayout.SkillCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = new PlayerSkill();
            return slots;
        }

        /// <summary>EntityData để spawn, máu và mana đầy.</summary>
        public EntityData ToEntityData(Vector2 pos) =>
            new EntityData(id, displayName, maxHp, maxHp, maxMp, maxMp, pos);

        /// <summary>Bỏ vật phẩm khởi đầu vào hành trang.</summary>
        [Header("Lưu game (tự ghi lúc chơi)")]
        [Tooltip("Các ô hành trang đã lưu. Chỉ dùng khi bagSaved = true; chưa có thì túi dựng từ startItems.")]
        public List<ItemStack> bag = new List<ItemStack>();
        public bool bagSaved;

        /// <summary>Dựng túi: từ bản lưu nếu có, không thì từ đồ khởi đầu. Trả true khi lấy từ bản lưu.</summary>
        public bool FillInventory(Inventory inventory)
        {
            if (inventory == null) return false;
            if (bagSaved && bag != null)
            {
                inventory.Load(bag);
                return true;
            }
            foreach (var it in startItems)
                if (it != null) inventory.Add(it.itemId, it.amount);
            return false;
        }

        public void SaveInventory(Inventory inventory)
        {
            if (inventory == null) return;
            if (bag == null) bag = new List<ItemStack>();
            bag.Clear();
            bag.AddRange(inventory.Slots);
            bagSaved = true;
        }

        public void SetSkillSlot(int slot, PlayerSkill skill)
        {
            skillSlots[slot] = skill != null ? new PlayerSkill(skill.templateId, skill.level) : new PlayerSkill();
        }

        public void SetBasicSkill(PlayerSkill skill)
        {
            basicSkill = skill != null ? new PlayerSkill(skill.templateId, skill.level) : new PlayerSkill();
        }

        public void SetWeapon(int id) => weaponId = id;

        /// <summary>Chép đồ mặc hiện tại để lưu (vũ khí đi đường SetWeapon).</summary>
        public void SaveEquipment(Equipment equipment)
        {
            if (equipment == null) return;
            if (equipped == null) equipped = new List<int>();
            if (equipGear == null) equipGear = new List<ItemStack>();
            equipment.SaveArmor(equipped);
            equipment.SaveGear(equipGear);
        }

        /// <summary>Chép hai ô dùng nhanh để lưu.</summary>
        public void SaveQuickItems(PlayerProfile profile)
        {
            if (profile == null) return;
            if (quickItems == null) quickItems = new List<int>();
            profile.SaveQuickItems(quickItems);
        }

        /// <summary>Chép cấp, EXP, điểm và cây nhân vật hiện tại để lưu.</summary>
        public void SaveProgress(PlayerProgress progress)
        {
            if (progress == null) return;
            level = progress.Level;
            exp = progress.Exp;
            potentialPoints = progress.Points;
            if (playerUpgrades == null) playerUpgrades = new List<NodeRank>();
            progress.Save(playerUpgrades);
        }

        /// <summary>Chép độ thân thiện, nhiệm vụ và cờ cốt truyện hiện tại để lưu.</summary>
        public void SaveStory(Game.Story.StoryState story)
        {
            if (story == null) return;
            if (npcAffinity == null) npcAffinity = new List<Game.Story.AffinityRecord>();
            if (quests == null) quests = new List<Game.Story.QuestRecord>();
            if (storyFlags == null) storyFlags = new List<string>();
            story.Save(npcAffinity, quests, storyFlags);
            trackedQuest = story.Tracked;
        }

        /// <summary>Ghi cấp một nút nâng cấp; rank 0 thì xóa dòng.</summary>
        public void SetUpgradeRank(int weapon, int upgradeId, int rank)
        {
            if (weaponUpgrades == null) weaponUpgrades = new List<UpgradeRank>();
            for (int i = 0; i < weaponUpgrades.Count; i++)
            {
                UpgradeRank r = weaponUpgrades[i];
                if (r.weaponId != weapon || r.upgradeId != upgradeId) continue;
                if (rank > 0) r.rank = rank;
                else weaponUpgrades.RemoveAt(i);
                return;
            }
            if (rank > 0) weaponUpgrades.Add(new UpgradeRank { weaponId = weapon, upgradeId = upgradeId, rank = rank });
        }

        // mảng luôn đủ một ô cho mỗi clip / ô skill, kể cả khi bố cục thêm ô mới. Mảng cũ theo AnimSlot (7 ô) được
        // trải ra: mọi clip của hành động nhận id của hành động đó.
        void OnValidate()
        {
            if (animVariants == null || animVariants.Length != AnimLayout.MotionTotal)
                animVariants = AnimLayout.PerMotion(animVariants);
            if (skillSlots == null || skillSlots.Length != AnimLayout.SkillCount)
                Array.Resize(ref skillSlots, AnimLayout.SkillCount);
        }
    }
}
