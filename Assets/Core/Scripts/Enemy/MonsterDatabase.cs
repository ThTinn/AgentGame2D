using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Một loại quái: chỉ số và prefab. Map chỉ lưu id loại quái và chỗ đứng (MapData.monsters); server gửi id này
    /// trong gói ADD_MONSTER (trường template).
    /// </summary>
    [Serializable]
    public class MonsterTemplate
    {
        public int id;
        [Tooltip("Tên trong dữ liệu, duy nhất (không dấu).")]
        public string name;
        [Tooltip("Tên hiện trên đầu quái.")]
        public string displayName;
        public long maxHp = 30;
        [Tooltip("Sát thương mỗi đòn của quái (chơi local; online do server tính).")]
        public long damage = 5;
        [Tooltip("Tốc độ đi (cho map chuẩn ô 2.56, tự nhân theo cỡ ô như mọi thông số của Entity).")]
        public float moveSpeed = 2f;
        [Tooltip("Bán kính vùng canh quanh chỗ đứng (chơi local): người chơi vào vùng thì quái đuổi đánh, ra khỏi vùng thì quái về chỗ. "
                 + "0 = dùng giá trị trên prefab.")]
        public float aggroRange = 4f;
        [Tooltip("Tầm đánh (chơi local): khoảng hở giữa collider của quái và của mục tiêu. 0 = dùng giá trị trên prefab.")]
        public float attackRange = 0.3f;
        [Tooltip("Giây giữa hai đòn, tính từ lúc bắt đầu đòn (chơi local). 0 = dùng giá trị trên prefab.")]
        public float attackCooldown = 1.2f;
        [Tooltip("Chết bao lâu thì hồi sinh (chơi local).")]
        public float respawnSeconds = 5f;
        [Tooltip("EXP người chơi nhận khi hạ quái này (chơi local). 0 = tự tính theo máu (xem ExpReward).")]
        [Min(0)] public long exp;
        [Tooltip("Vàng rơi ra khi hạ quái này (chơi local), dao động ±20%. 0 = tự tính theo máu (xem GoldReward).")]
        [Min(0)] public long gold;
        // Game: bản GAME_2D là "Enemy prefab" (component 2D chưa chép sang); tạm giữ GameObject, ref cũ trong asset thành Missing.
        public GameObject prefab;

        /// <summary>EXP khi hạ quái: exp nếu có khai, không thì bằng nửa máu tối đa (quái trâu hơn cho nhiều hơn).</summary>
        public long ExpReward => exp > 0 ? exp : DefaultExp(maxHp);

        /// <summary>EXP mặc định của quái có maxHp máu (cũng dùng cho quái không có MonsterTemplate).</summary>
        public static long DefaultExp(long maxHp) => Math.Max(1, maxHp / 2);

        /// <summary>Vàng khi hạ quái (trước dao động ±20%): gold nếu có khai, không thì một phần ba máu tối đa.</summary>
        public long GoldReward => gold > 0 ? gold : DefaultGold(maxHp);

        public static long DefaultGold(long maxHp) => Math.Max(1, maxHp / 3);

        /// <summary>
        /// EXP khi hạ quái cấp level (bãi quái có khai cấp, không có MonsterTemplate): một phần mười EXP người chơi cần để
        /// lên từ cấp đó (PlayerUpgradeTree.ExpToNext), nên đánh quái cùng cấp chừng 10 con thì lên một cấp; EXP chính vẫn
        /// từ nhiệm vụ.
        /// </summary>
        public static long ExpAtLevel(int level)
        {
            level = Math.Max(1, level);
            Game.Players.PlayerUpgradeTree tree = Game.Players.PlayerUpgradeTree.Current;
            long toNext = tree != null ? tree.ExpToNext(level) : (long)Math.Round(60.0 * Math.Pow(level, 1.35));
            return Math.Max(1, toNext / 10);
        }

        /// <summary>Vàng khi hạ quái cấp level (trước dao động ±20%).</summary>
        public static long GoldAtLevel(int level) => 10 + 6L * Math.Max(1, level);
    }

    /// <summary>
    /// Danh mục loại quái. Một asset ở Assets/Data/Monsters/MonsterDatabase.asset, dựng bằng
    /// Tools/Core/Monsters/Build Monsters. Cùng khuôn với ItemDatabase: GameController đăng ký lúc Awake,
    /// ai cần thì đọc <see cref="Current"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Core/Monster Database")]
    public class MonsterDatabase : ScriptableObject
    {
        public List<MonsterTemplate> monsters = new List<MonsterTemplate>();

        [NonSerialized] Dictionary<int, MonsterTemplate> byId;

        static MonsterDatabase current;

        /// <summary>Bảng loại quái của game; null khi scene chưa có ai giữ bảng.</summary>
        public static MonsterDatabase Current => current;

        /// <summary>Đăng ký bảng cho cả game (GameController.Awake). Gán chồng bằng bảng khác sẽ có cảnh báo.</summary>
        public static void Use(MonsterDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("MonsterDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public MonsterTemplate Get(int id)
        {
            if (byId == null || byId.Count != monsters.Count)
            {
                byId = new Dictionary<int, MonsterTemplate>(monsters.Count);
                foreach (var m in monsters) byId[m.id] = m;
            }
            return byId.TryGetValue(id, out MonsterTemplate t) ? t : null;
        }

        public MonsterTemplate Find(string name)
        {
            foreach (var m in monsters) if (m.name == name) return m;
            return null;
        }

        /// <summary>Thêm loại mới, hoặc trả về loại cũ nếu tên đã có (id không bao giờ dùng lại).</summary>
        public MonsterTemplate Add(MonsterTemplate t)
        {
            MonsterTemplate old = Find(t.name);
            if (old != null) return old;
            int max = 0;
            foreach (var m in monsters) if (m.id > max) max = m.id;
            t.id = max + 1;
            monsters.Add(t);
            byId = null;
            return t;
        }
    }
}
