using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Players
{
    /// <summary>
    /// Chỉ số của nhân vật: cây nâng cấp và đồ mặc cộng vào đây. Giá trị cố định vì được ghi xuống file.
    /// Tên, mô tả, cách ghi số và trần nằm ở <see cref="OptionDatabase"/> (id option = giá trị enum này).
    /// Chỉ số có trần thì cộng quá trần cũng không hơn. Thêm chỉ số: thêm giá trị ở đây, trường + case trong
    /// <see cref="PlayerStats"/>, chỗ gameplay đọc nó, rồi một dòng trong bảng seed của OptionSetup.
    /// </summary>
    public enum PlayerStat : byte
    {
        MaxHp = 0,          // +máu tối đa
        MaxMp = 1,          // +mana tối đa (chưa skill nào tốn mana: cây mặc định không dùng)
        Damage = 2,         // +% sát thương mọi đòn (đánh thường lẫn skill)
        Crit = 3,           // +% tỉ lệ chí mạng (cộng vào tỉ lệ gốc của GameController, trần MaxCritChance)
        Defense = 4,        // giảm % sát thương mỗi đòn phải nhận (trần MaxDefense, luôn mất ít nhất 1 máu)
        MoveSpeed = 5,      // +% tốc độ chạy (trần MaxMoveSpeed)
        HpRegen = 6,        // +máu hồi mỗi giây
        ExpBonus = 7,       // +% EXP khi hạ quái
        GoldFind = 8,       // +% vàng rơi ra khi hạ quái
        ItemFind = 9,       // +% cơ hội quái rơi ngọc (nhân vào tỉ lệ gốc)
        WeaponPower = 10,   // +% sát thương đòn đánh thường (đòn của vũ khí)
        AttackSpeed = 11,   // -% hồi chiêu đánh thường, cộng chung trần với nâng cấp tốc đánh của vũ khí
        SkillHaste = 12,    // -% hồi chiêu skill ô 1-4 (trần MaxSkillHaste)
        DashHaste = 13,     // -% hồi chiêu lướt (trần MaxDashHaste)
        CritDamage = 14,    // +% sát thương chí mạng (gốc x2 = +100%)
        HitExp = 15,        // % sát thương gây ra được đổi thành EXP
        UpgradeBoost = 16,  // +% hiệu quả các nút nâng cấp vũ khí (sát thương, nổ, tầm, góc chém)
        Range = 17,         // +% tầm đòn đánh thường
        // từ đây là option của vật phẩm dùng một lần: không cộng vào nhân vật, không hiện ở các bảng tổng (PlayerStatText.Order)
        RestoreHp = 18,     // máu hồi ngay khi uống
        RestoreMp = 19,     // mana hồi ngay khi uống
        // từ đây là option của vũ khí: gốc đòn đánh thường ghi ở item vũ khí (SkillTemplate.Stats), phần cộng ghi ở nút nâng cấp
        // (WeaponStats). Không cộng vào nhân vật, không hiện ở các bảng tổng, cường hóa không nhân lên
        AttackDamage = 20,    // % sát thương gốc của đòn, 100 = bằng đòn thường
        AttackCooldown = 21,  // giây hồi gốc của đòn
        AttackRange = 22,     // tầm đòn (ô)
        AttackTargets = 23,   // số mục tiêu / xuyên; item không có dòng này thì trúng mọi kẻ trong tầm
        WeaponDamage = 24,    // +% sát thương mọi đòn khi cầm vũ khí đã nâng
        Projectiles = 25,     // +số đạn mỗi phát, xòe hình quạt
        ExplosionRadius = 26, // +bán kính nổ (ô) khi trúng; > 0 là đòn có nổ
        ExplosionDamage = 27, // +% sát thương của vụ nổ (tính theo sát thương đòn)
        Arc = 28,             // +độ nửa góc chém (cận chiến)
    }

    /// <summary>
    /// Một option: chỉ số và lượng cộng thêm. Nút cây nâng cấp (mỗi cấp), bậc Chuyên tinh và đồ mặc (ItemTemplate.options)
    /// dùng chung.
    /// </summary>
    [Serializable]
    public struct PlayerStatEffect
    {
        public PlayerStat stat;
        public float value;

        public PlayerStatEffect(PlayerStat stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }
    }

    /// <summary>
    /// Một nút của cây nâng cấp nhân vật. Nâng một cấp tốn <see cref="cost"/> điểm tiềm năng, cần nhân vật đạt
    /// <see cref="requiredLevel"/> và nút cha (nếu có) đã có ít nhất một cấp. Id cố định, không dùng lại.
    /// </summary>
    [Serializable]
    public class PlayerUpgradeNode
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd vitality).")]
        public string name;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Tên nhánh con (vd Chí mạng), hiện trong bảng chi tiết. Trống = thân của hướng.")]
        public string group;
        public UpgradeNodeKind kind;
        [Tooltip("Id nút phải mở trước. 0 = mở được ngay.")]
        public int parent;
        [Min(1)] public int maxRank = 1;
        [Tooltip("Điểm tiềm năng cho mỗi cấp.")]
        [Min(1)] public int cost = 1;
        [Tooltip("Cấp nhân vật tối thiểu để nâng nút này.")]
        [Min(1)] public int requiredLevel = 1;
        [Header("Vị trí trên cây (toạ độ cực quanh nút gốc)")]
        [Tooltip("Hướng toả ra: 0 thẳng lên, rồi theo chiều kim đồng hồ, mỗi hướng cách nhau 360 / số nhánh độ.")]
        [Min(0)] public int branch;
        [Tooltip("Vòng tính từ tâm: 0 = nút gốc ở giữa, 1 = vòng đầu tiên...")]
        [Min(0)] public int ring;
        [Tooltip("Lệch ngang so với trục của nhánh, tính bằng ô (âm = lệch trái khi nhìn từ tâm ra). Dùng khi nhánh rẽ đôi.")]
        public float lateral;
        public List<PlayerStatEffect> effects = new List<PlayerStatEffect>();

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    /// <summary>
    /// Chỉ số cộng thêm gộp từ mọi nút đã mở của cây và đồ đang mặc. Tính lại khi nâng / đặt lại cây hay mặc / tháo đồ,
    /// không tính mỗi đòn. Trường giữ tổng thô; chỗ dùng đọc qua các thuộc tính đã kẹp trần (vd <see cref="DefenseCut"/>)
    /// để thêm nút / thêm đồ cũng không phá cân bằng. Trần đọc từ <see cref="OptionDatabase"/>; số Default* ở đây chỉ dùng
    /// khi scene chưa có bảng.
    /// </summary>
    public struct PlayerStats
    {
        public const float DefaultMaxCritChance = 60f;
        public const float DefaultMaxDefense = 50f;
        public const float DefaultMaxMoveSpeed = 40f;
        public const float DefaultMaxSkillHaste = 40f;
        public const float DefaultMaxDashHaste = 50f;
        public const float DefaultMaxAttackSpeedCut = 50f;

        /// <summary>% tỉ lệ chí mạng tổng (gốc + cộng thêm).</summary>
        public static float MaxCritChance => OptionDatabase.CapOf(PlayerStat.Crit, DefaultMaxCritChance);
        /// <summary>% sát thương nhận được giảm.</summary>
        public static float MaxDefense => OptionDatabase.CapOf(PlayerStat.Defense, DefaultMaxDefense);
        /// <summary>+% tốc chạy.</summary>
        public static float MaxMoveSpeed => OptionDatabase.CapOf(PlayerStat.MoveSpeed, DefaultMaxMoveSpeed);
        /// <summary>-% hồi chiêu skill.</summary>
        public static float MaxSkillHaste => OptionDatabase.CapOf(PlayerStat.SkillHaste, DefaultMaxSkillHaste);
        /// <summary>-% hồi chiêu lướt.</summary>
        public static float MaxDashHaste => OptionDatabase.CapOf(PlayerStat.DashHaste, DefaultMaxDashHaste);
        /// <summary>% hồi chiêu đòn thường bị cắt, cộng chung nâng cấp vũ khí + cây + đồ: 50% = đánh nhanh gấp đôi.</summary>
        public static float MaxAttackSpeedCut => OptionDatabase.CapOf(PlayerStat.AttackSpeed, DefaultMaxAttackSpeedCut);

        public long maxHp;
        public long maxMp;
        public float damage;        // +% sát thương mọi đòn
        public float crit;          // +% chí mạng
        public float critDamage;    // +% sát thương chí mạng
        public float defense;       // % giảm sát thương nhận
        public float hpRegen;       // máu / giây
        public float moveSpeed;     // +% tốc chạy
        public float attackSpeed;   // -% hồi chiêu đánh thường
        public float skillHaste;    // -% hồi chiêu skill
        public float dashHaste;     // -% hồi chiêu lướt
        public float expBonus;      // +% EXP
        public float hitExp;        // % sát thương đổi thành EXP
        public float goldFind;      // +% vàng
        public float itemFind;      // +% tỉ lệ rơi đồ (nhân vào từng dòng của bảng rơi đồ)
        public float weaponPower;   // +% sát thương đòn đánh thường
        public float upgradeBoost;  // +% hiệu quả nâng cấp vũ khí
        public float range;         // +% tầm đòn đánh thường

        /// <summary>Phần sát thương nhận bị cắt, 0..0.5.</summary>
        public float DefenseCut => Mathf.Clamp(defense, 0f, MaxDefense) / 100f;
        /// <summary>Hệ số tốc chạy, 1..1.4.</summary>
        public float MoveSpeedScale => 1f + Mathf.Clamp(moveSpeed, 0f, MaxMoveSpeed) / 100f;
        /// <summary>Hệ số hồi chiêu skill, 0.6..1.</summary>
        public float SkillCooldownScale => 1f - Mathf.Clamp(skillHaste, 0f, MaxSkillHaste) / 100f;
        /// <summary>Hệ số hồi chiêu lướt, 0.5..1.</summary>
        public float DashCooldownScale => 1f - Mathf.Clamp(dashHaste, 0f, MaxDashHaste) / 100f;

        public void Add(PlayerStatEffect e, int times)
        {
            float v = e.value * times;
            switch (e.stat)
            {
                case PlayerStat.MaxHp: maxHp += Mathf.RoundToInt(v); break;
                case PlayerStat.MaxMp: maxMp += Mathf.RoundToInt(v); break;
                case PlayerStat.Damage: damage += v; break;
                case PlayerStat.Crit: crit += v; break;
                case PlayerStat.Defense: defense += v; break;
                case PlayerStat.MoveSpeed: moveSpeed += v; break;
                case PlayerStat.HpRegen: hpRegen += v; break;
                case PlayerStat.ExpBonus: expBonus += v; break;
                case PlayerStat.GoldFind: goldFind += v; break;
                case PlayerStat.ItemFind: itemFind += v; break;
                case PlayerStat.WeaponPower: weaponPower += v; break;
                case PlayerStat.AttackSpeed: attackSpeed += v; break;
                case PlayerStat.SkillHaste: skillHaste += v; break;
                case PlayerStat.DashHaste: dashHaste += v; break;
                case PlayerStat.CritDamage: critDamage += v; break;
                case PlayerStat.HitExp: hitExp += v; break;
                case PlayerStat.UpgradeBoost: upgradeBoost += v; break;
                case PlayerStat.Range: range += v; break;
            }
        }

        /// <summary>Cộng dồn một bộ chỉ số khác (cây + đồ mặc).</summary>
        public void Add(in PlayerStats o)
        {
            maxHp += o.maxHp;
            maxMp += o.maxMp;
            damage += o.damage;
            crit += o.crit;
            critDamage += o.critDamage;
            defense += o.defense;
            hpRegen += o.hpRegen;
            moveSpeed += o.moveSpeed;
            attackSpeed += o.attackSpeed;
            skillHaste += o.skillHaste;
            dashHaste += o.dashHaste;
            expBonus += o.expBonus;
            hitExp += o.hitExp;
            goldFind += o.goldFind;
            itemFind += o.itemFind;
            weaponPower += o.weaponPower;
            upgradeBoost += o.upgradeBoost;
            range += o.range;
        }

        /// <summary>Tổng thô của một chỉ số (UI in ra, công cụ kiểm tra).</summary>
        public float Get(PlayerStat s)
        {
            switch (s)
            {
                case PlayerStat.MaxHp: return maxHp;
                case PlayerStat.MaxMp: return maxMp;
                case PlayerStat.Damage: return damage;
                case PlayerStat.Crit: return crit;
                case PlayerStat.CritDamage: return critDamage;
                case PlayerStat.Defense: return defense;
                case PlayerStat.HpRegen: return hpRegen;
                case PlayerStat.MoveSpeed: return moveSpeed;
                case PlayerStat.AttackSpeed: return attackSpeed;
                case PlayerStat.SkillHaste: return skillHaste;
                case PlayerStat.DashHaste: return dashHaste;
                case PlayerStat.WeaponPower: return weaponPower;
                case PlayerStat.UpgradeBoost: return upgradeBoost;
                case PlayerStat.Range: return range;
                case PlayerStat.ExpBonus: return expBonus;
                case PlayerStat.HitExp: return hitExp;
                case PlayerStat.GoldFind: return goldFind;
                case PlayerStat.ItemFind: return itemFind;
                default: return 0f;
            }
        }
    }

    /// <summary>Loại nút: quyết định cỡ nút trên cây và nhãn trong bảng chi tiết. Giá trị cố định (ghi xuống file).</summary>
    public enum UpgradeNodeKind : byte
    {
        Normal = 0,        // nút thường, nâng nhiều cấp
        Breakthrough = 1,  // Đột phá: một cấp, mở ở mốc cấp 10 / 30, tăng mạnh
        Capstone = 2,      // Đỉnh nhánh: một cấp, mở ở mốc cấp 40
        Mastery = 3,       // Tinh thông: nâng rất nhiều cấp, hiệu quả thấp — chỗ tiêu điểm giữa các mốc
    }

    /// <summary>
    /// Một bậc Chuyên tinh: tiêu đủ <see cref="points"/> điểm trong hướng <see cref="branch"/> (mọi nút trừ nút gốc) thì
    /// được cộng <see cref="effects"/>. Bậc cao chỉ người dồn gần hết điểm vào một hướng mới tới: thưởng cho lối đi sâu.
    /// </summary>
    [Serializable]
    public class BranchMastery
    {
        public int branch;
        [Min(1)] public int points;
        public List<PlayerStatEffect> effects = new List<PlayerStatEffect>();
    }

    /// <summary>Một dòng lưu: nút id của cây nhân vật đang ở cấp rank.</summary>
    [Serializable]
    public class NodeRank
    {
        public int id;
        public int rank;
    }

    /// <summary>
    /// Tiến trình của người chơi: cấp, EXP, điểm tiềm năng và cấp các nút của cây nhân vật. Chỉ giữ số; luật (đường cong
    /// EXP, giá nút, nút cha, cấp yêu cầu) đọc từ <see cref="PlayerUpgradeTree"/>. Chủ dữ liệu (PlayerData, sau này là
    /// server) nạp vào qua <see cref="Load"/> và nghe các event để ghi lại.
    /// </summary>
    public class PlayerProgress
    {
        readonly Dictionary<int, int> ranks = new Dictionary<int, int>();
        readonly List<int> scratch = new List<int>();

        public int Level { get; private set; } = 1;
        /// <summary>EXP đã có trong cấp hiện tại (về 0 mỗi lần lên cấp, phần dư chuyển sang cấp sau).</summary>
        public long Exp { get; private set; }
        /// <summary>Điểm tiềm năng chưa tiêu.</summary>
        public int Points { get; private set; }

        /// <summary>EXP hoặc điểm vừa đổi (nhận EXP, lên cấp, tiêu / hoàn điểm). UI vẽ lại theo event này.</summary>
        public event Action Changed;
        /// <summary>Vừa nhận EXP: lượng đã cộng (sau thưởng %).</summary>
        public event Action<long> ExpGained;
        /// <summary>Vừa lên cấp: (cấp mới, số cấp lên trong lần này).</summary>
        public event Action<int, int> LeveledUp;
        /// <summary>Một nút vừa đổi cấp: (id nút, cấp mới). Đặt lại cây thì bắn một lần cho mỗi nút về 0.</summary>
        public event Action<int, int> RankChanged;

        static PlayerUpgradeTree Tree => PlayerUpgradeTree.Current;

        /// <summary>EXP cần cho cấp tiếp theo; 0 khi đã tối đa hoặc chưa có cây.</summary>
        public long ExpToNext => Tree != null && !IsMaxLevel ? Tree.ExpToNext(Level) : 0;
        public bool IsMaxLevel => Tree != null && Level >= Tree.maxLevel;
        public float ExpRatio => ExpToNext > 0 ? Mathf.Clamp01((float)Exp / ExpToNext) : 1f;

        public int Rank(int nodeId) => ranks.TryGetValue(nodeId, out int r) ? r : 0;

        public void Load(int level, long exp, int points, IEnumerable<NodeRank> saved)
        {
            Level = Mathf.Max(1, level);
            Exp = Math.Max(0, exp);
            Points = Mathf.Max(0, points);
            ranks.Clear();
            if (saved != null)
                foreach (NodeRank r in saved)
                    if (r != null && r.rank > 0) ranks[r.id] = r.rank;
            Changed?.Invoke();
        }

        /// <summary>
        /// Cộng EXP (có thưởng % của cây). Mỗi cấp lên được điểm tiềm năng theo <see cref="PlayerUpgradeTree.PointsForLevel"/>
        /// (cấp mốc được thêm điểm đột phá).
        /// </summary>
        /// <param name="announce">False: không bắn <see cref="ExpGained"/> (EXP lặt vặt theo từng đòn, không hiện chữ bay).</param>
        public void AddExp(long amount, float bonusPercent = 0f, bool announce = true)
        {
            PlayerUpgradeTree tree = Tree;
            if (amount <= 0 || tree == null || IsMaxLevel) return;

            amount += (long)(amount * Mathf.Max(0f, bonusPercent) / 100f);
            Exp += amount;
            int gained = 0;
            while (Level < tree.maxLevel && Exp >= tree.ExpToNext(Level))
            {
                Exp -= tree.ExpToNext(Level);
                Level++;
                gained++;
                Points += tree.PointsForLevel(Level);
            }
            if (Level >= tree.maxLevel) Exp = 0;

            if (announce) ExpGained?.Invoke(amount);
            if (gained > 0) LeveledUp?.Invoke(Level, gained);
            Changed?.Invoke();
        }

        /// <summary>Lý do chưa nâng được nút (UI hiện cho người chơi); None là nâng được.</summary>
        public enum Block : byte { None, NoNode, Maxed, NeedParent, NeedLevel, NeedPoints }

        public Block Check(PlayerUpgradeNode node)
        {
            if (node == null) return Block.NoNode;
            if (Rank(node.id) >= node.maxRank) return Block.Maxed;
            if (node.parent != 0 && Rank(node.parent) == 0) return Block.NeedParent;
            if (Level < node.requiredLevel) return Block.NeedLevel;
            if (Points < node.cost) return Block.NeedPoints;
            return Block.None;
        }

        public bool CanUpgrade(PlayerUpgradeNode node) => Check(node) == Block.None;

        /// <summary>Nâng nút một cấp, trừ điểm tiềm năng.</summary>
        public bool Upgrade(int nodeId)
        {
            PlayerUpgradeNode node = Tree != null ? Tree.Get(nodeId) : null;
            if (!CanUpgrade(node)) return false;

            Points -= node.cost;
            int rank = Rank(nodeId) + 1;
            ranks[nodeId] = rank;
            RankChanged?.Invoke(nodeId, rank);
            Changed?.Invoke();
            Game.Core.NetSync.Send?.UpgradeNode(nodeId);
            return true;
        }

        /// <summary>Đưa cả cây về 0 và hoàn lại toàn bộ điểm đã tiêu.</summary>
        public void ResetTree()
        {
            PlayerUpgradeTree tree = Tree;
            if (tree == null) return;

            scratch.Clear();
            foreach (var pair in ranks)
            {
                PlayerUpgradeNode node = tree.Get(pair.Key);
                if (node != null) Points += node.cost * Mathf.Min(pair.Value, node.maxRank);
                scratch.Add(pair.Key);
            }
            ranks.Clear();
            foreach (int id in scratch) RankChanged?.Invoke(id, 0);
            Changed?.Invoke();
            Game.Core.NetSync.Send?.ResetTree();
        }

        /// <summary>Tổng điểm đã tiêu trên cây.</summary>
        public int Spent()
        {
            PlayerUpgradeTree tree = Tree;
            int n = 0;
            if (tree == null) return n;
            foreach (var pair in ranks)
            {
                PlayerUpgradeNode node = tree.Get(pair.Key);
                if (node != null) n += node.cost * Mathf.Min(pair.Value, node.maxRank);
            }
            return n;
        }

        /// <summary>Điểm đã tiêu trong một hướng (mọi nút của hướng đó, không tính nút gốc): tính bậc Chuyên tinh.</summary>
        public int Spent(int branch)
        {
            PlayerUpgradeTree tree = Tree;
            int n = 0;
            if (tree == null) return n;
            foreach (var pair in ranks)
            {
                PlayerUpgradeNode node = tree.Get(pair.Key);
                if (node != null && node.ring > 0 && node.branch == branch) n += node.cost * Mathf.Min(pair.Value, node.maxRank);
            }
            return n;
        }

        /// <summary>Số bậc Chuyên tinh đã đạt ở hướng branch (0 = chưa bậc nào).</summary>
        public int MasteryTier(int branch)
        {
            PlayerUpgradeTree tree = Tree;
            if (tree == null || tree.masteries == null) return 0;
            int spent = Spent(branch), tier = 0;
            foreach (BranchMastery m in tree.masteries)
                if (m.branch == branch && spent >= m.points) tier++;
            return tier;
        }

        /// <summary>Gộp chỉ số của mọi nút đã mở và các bậc Chuyên tinh đã đạt.</summary>
        public PlayerStats Compute()
        {
            var stats = new PlayerStats();
            PlayerUpgradeTree tree = Tree;
            if (tree == null) return stats;
            foreach (PlayerUpgradeNode node in tree.nodes)
            {
                int rank = Mathf.Min(Rank(node.id), node.maxRank);
                if (rank <= 0) continue;
                foreach (PlayerStatEffect e in node.effects) stats.Add(e, rank);
            }
            if (tree.masteries != null && tree.masteries.Count > 0)
            {
                for (int b = 0; b < tree.BranchCount; b++)
                {
                    int spent = Spent(b);
                    foreach (BranchMastery m in tree.masteries)
                        if (m.branch == b && spent >= m.points)
                            foreach (PlayerStatEffect e in m.effects) stats.Add(e, 1);
                }
            }
            return stats;
        }

        /// <summary>Chép cấp các nút ra danh sách để lưu.</summary>
        public void Save(List<NodeRank> into)
        {
            into.Clear();
            foreach (var pair in ranks) into.Add(new NodeRank { id = pair.Key, rank = pair.Value });
        }
    }
}
