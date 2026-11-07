using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;

namespace Game.Players
{
    /// <summary>
    /// Cây nâng cấp nhân vật và đường cong lên cấp. Một asset ở Assets/Data/Player/PlayerUpgradeTree.asset, dựng bằng
    /// Tools/Core/Player/Build Upgrade Tree. Cùng khuôn với WeaponDatabase: GameController đăng ký lúc Awake,
    /// ai cần thì đọc <see cref="Current"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerUpgradeTree", menuName = "Core/Player Upgrade Tree")]
    public class PlayerUpgradeTree : ScriptableObject
    {
        [Header("Lên cấp")]
        [Min(1)] public int maxLevel = 50;
        [Tooltip("EXP cần để từ cấp 1 lên cấp 2.")]
        [Min(1)] public long expBase = 60;
        [Tooltip("EXP để lên cấp L+1 = expBase x L^expGrowth.")]
        [Min(1f)] public float expGrowth = 1.35f;
        [Tooltip("Điểm tiềm năng nhận mỗi lần lên cấp.")]
        [Min(0)] public int pointsPerLevel = 3;
        [Tooltip("Cứ mỗi chừng này cấp là một mốc đột phá (10 = cấp 10, 20, 30...). 0 = không có mốc.")]
        [Min(0)] public int milestoneEvery = 10;
        [Tooltip("Điểm tiềm năng thưởng thêm ở cấp mốc: đủ mua ngay nút Đột phá / Đỉnh vừa mở ở mốc đó.")]
        [Min(0)] public int milestoneBonusPoints = 6;
        [Tooltip("Lên cấp thì hồi đầy máu và mana.")]
        public bool refillOnLevelUp = true;

        [Header("Cây")]
        [Tooltip("Tên các nhánh theo hướng (PlayerUpgradeNode.branch). Số phần tử = số hướng cây toả ra quanh nút gốc.")]
        public string[] branchNames = { "Sức mạnh|Might", "Tốc độ|Speed", "Kinh nghiệm|Wisdom", "Tài lộc|Fortune", "Sức mạnh đồ|Gear" };
        public List<PlayerUpgradeNode> nodes = new List<PlayerUpgradeNode>();
        [Tooltip("Bậc Chuyên tinh của từng hướng: tiêu đủ số điểm trong hướng thì được cộng thêm. Xếp theo điểm tăng dần.")]
        public List<BranchMastery> masteries = new List<BranchMastery>();

        /// <summary>Cấp level có phải mốc đột phá không.</summary>
        public bool IsMilestone(int level) => milestoneEvery > 0 && level % milestoneEvery == 0;

        /// <summary>Điểm tiềm năng nhận khi vừa lên tới cấp level: điểm mỗi cấp, cấp mốc cộng thêm điểm đột phá.</summary>
        public int PointsForLevel(int level) => pointsPerLevel + (IsMilestone(level) ? milestoneBonusPoints : 0);

        /// <summary>Tổng điểm có được khi ở cấp level (từ cấp 1).</summary>
        public int TotalPointsAt(int level)
        {
            int n = 0;
            for (int l = 2; l <= level; l++) n += PointsForLevel(l);
            return n;
        }

        /// <summary>Mốc đột phá kế tiếp sau cấp level; 0 khi không còn.</summary>
        public int NextMilestone(int level)
        {
            if (milestoneEvery <= 0) return 0;
            int next = (level / milestoneEvery + 1) * milestoneEvery;
            return next <= maxLevel ? next : 0;
        }

        /// <summary>Số hướng cây toả ra (ít nhất 1).</summary>
        public int BranchCount => Mathf.Max(1, branchNames != null ? branchNames.Length : 0);

        /// <summary>
        /// Hướng (vector đơn vị) của nhánh: nhánh 0 thẳng lên, các nhánh sau theo chiều kim đồng hồ, chia đều 360 độ.
        /// </summary>
        public Vector2 BranchDirection(int branch)
        {
            float angle = (90f - branch * 360f / BranchCount) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        /// <summary>Vị trí nút trên cây, đơn vị UI, gốc ở tâm: ring x ringStep dọc theo nhánh, lateral x lateralStep sang ngang.</summary>
        public Vector2 NodePosition(PlayerUpgradeNode node, float ringStep, float lateralStep)
        {
            if (node.ring <= 0) return Vector2.zero;
            Vector2 dir = BranchDirection(node.branch);
            Vector2 side = new Vector2(dir.y, -dir.x);   // vuông góc, lệch phải khi nhìn từ tâm ra
            return dir * (node.ring * ringStep) + side * (node.lateral * lateralStep);
        }

        public const int CurrentSchema = 1;

        [NonSerialized] Dictionary<int, PlayerUpgradeNode> byId;

        static PlayerUpgradeTree current;

        /// <summary>Cây của game; null khi scene chưa có ai giữ.</summary>
        public static PlayerUpgradeTree Current => current;

        /// <summary>Đăng ký cây cho cả game (GameController.Awake).</summary>
        public static void Use(PlayerUpgradeTree tree)
        {
            if (tree == null || tree == current) return;
            if (current != null) Debug.LogWarning("PlayerUpgradeTree: đang dùng " + current.name + ", bị thay bằng " + tree.name + ".", tree);
            current = tree;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        void OnValidate() => byId = null;

        /// <summary>EXP cần để từ cấp level lên cấp level + 1.</summary>
        public long ExpToNext(int level) =>
            (long)Math.Round(expBase * Math.Pow(Mathf.Max(1, level), expGrowth));

        public PlayerUpgradeNode Get(int id)
        {
            if (byId == null || byId.Count != nodes.Count)
            {
                byId = new Dictionary<int, PlayerUpgradeNode>(nodes.Count);
                foreach (var n in nodes) byId[n.id] = n;
            }
            return byId.TryGetValue(id, out PlayerUpgradeNode node) ? node : null;
        }

        public PlayerUpgradeNode Find(string nodeName)
        {
            foreach (var n in nodes) if (n.name == nodeName) return n;
            return null;
        }

        // ---------------- file cho server ----------------

        // Binary (MyWriter/MyReader, big-endian), file tree.bin gửi cho server. Chỉ phần logic: KHÔNG có displayName,
        // description, group, ring, lateral, branchNames (những thứ đó chỉ client dùng).
        //   short schema, short maxLevel, long expBase, float expGrowth, short pointsPerLevel, short milestoneEvery,
        //   short milestoneBonusPoints, bool refillOnLevelUp,
        //   short nodeCount { short id, UTF name, byte kind, short parent, short maxRank, short cost, short requiredLevel,
        //                     byte branch, effects },
        //   byte masteryCount { byte branch, short points, effects }
        //   effects: byte count { byte stat, float value }
        public byte[] ToBytes()
        {
            var w = new MyWriter(4 * 1024);
            w.WriteShort(CurrentSchema);
            w.WriteShort(checked((short)maxLevel));
            w.WriteLong(expBase);
            w.WriteFloat(expGrowth);
            w.WriteShort(checked((short)pointsPerLevel));
            w.WriteShort(checked((short)milestoneEvery));
            w.WriteShort(checked((short)milestoneBonusPoints));
            w.WriteBool(refillOnLevelUp);
            w.WriteShort(checked((short)nodes.Count));
            foreach (PlayerUpgradeNode n in nodes)
            {
                w.WriteShort(checked((short)n.id));
                w.WriteUTF(n.name ?? "");
                w.WriteByte((byte)n.kind);
                w.WriteShort(checked((short)n.parent));
                w.WriteShort(checked((short)n.maxRank));
                w.WriteShort(checked((short)n.cost));
                w.WriteShort(checked((short)n.requiredLevel));
                w.WriteByte(checked((byte)n.branch));
                WriteEffects(w, n.effects);
            }
            w.WriteByte(checked((byte)masteries.Count));
            foreach (BranchMastery m in masteries)
            {
                w.WriteByte(checked((byte)m.branch));
                w.WriteShort(checked((short)m.points));
                WriteEffects(w, m.effects);
            }
            return w.ToArray();
        }

        static void WriteEffects(MyWriter w, List<PlayerStatEffect> effects)
        {
            w.WriteByte(checked((byte)effects.Count));
            foreach (PlayerStatEffect e in effects)
            {
                w.WriteByte((byte)e.stat);
                w.WriteFloat(e.value);
            }
        }

        static List<PlayerStatEffect> ReadEffects(MyReader r)
        {
            int count = r.ReadByte();
            var effects = new List<PlayerStatEffect>(count);
            for (int i = 0; i < count; i++) effects.Add(new PlayerStatEffect((PlayerStat)r.ReadByte(), r.ReadFloat()));
            return effects;
        }

        /// <summary>Đọc cây từ <see cref="ToBytes"/> vào <paramref name="into"/>. False và lý do khi dữ liệu hỏng, schema mới hơn hay id trùng.</summary>
        public static bool Read(byte[] bytes, PlayerUpgradeTree into, out string error)
        {
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty tree data"; return false; }
            try
            {
                var r = new MyReader(bytes);
                int schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "tree schema too new (" + schema + ")"; return false; }
                into.maxLevel = r.ReadShort();
                into.expBase = r.ReadLong();
                into.expGrowth = r.ReadFloat();
                into.pointsPerLevel = r.ReadShort();
                into.milestoneEvery = r.ReadShort();
                into.milestoneBonusPoints = r.ReadShort();
                into.refillOnLevelUp = r.ReadBool();
                into.nodes.Clear();
                into.byId = null;
                var ids = new HashSet<int>();
                int count = r.ReadShort();
                for (int i = 0; i < count; i++)
                {
                    var n = new PlayerUpgradeNode
                    {
                        id = r.ReadShort(), name = r.ReadUTF(), kind = (UpgradeNodeKind)r.ReadByte(), parent = r.ReadShort(),
                        maxRank = r.ReadShort(), cost = r.ReadShort(), requiredLevel = r.ReadShort(), branch = r.ReadByte(),
                        effects = ReadEffects(r),
                    };
                    if (!ids.Add(n.id)) { error = "duplicate node id " + n.id; return false; }
                    into.nodes.Add(n);
                }
                into.masteries.Clear();
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                    into.masteries.Add(new BranchMastery { branch = r.ReadByte(), points = r.ReadShort(), effects = ReadEffects(r) });
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad tree data: " + e.Message; return false; }
            return true;
        }
    }
}
