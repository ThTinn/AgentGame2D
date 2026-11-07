using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Players;

namespace Game.Skills
{
    /// <summary>
    /// Một nút của cây nâng cấp vũ khí. Mở được khi nút cha đã có ít nhất một cấp (parent 0 = gốc).
    /// Mỗi cấp cộng toàn bộ <see cref="effects"/> một lần. Id cố định trong vũ khí, không dùng lại.
    /// </summary>
    [Serializable]
    public class WeaponUpgrade
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd multi_shot).")]
        public string name;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Id nút phải mở trước. 0 = mở được ngay.")]
        public int parent;
        [Min(1)] public int maxRank = 1;
        [Tooltip("Chi phí mỗi cấp. Chưa dùng: nâng cấp đang miễn phí.")]
        public int cost;
        [Tooltip("Vị trí trên cây ở UI: cột (nhánh) và hàng (bậc, 0 ở trên cùng).")]
        public int column, row;
        [Tooltip("Option cộng thêm mỗi cấp. Dùng các chỉ số WeaponStats.Add đọc, chỉ số khác không có tác dụng.")]
        public List<PlayerStatEffect> effects = new List<PlayerStatEffect>();

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    /// <summary>
    /// Chỉ số cộng thêm của vũ khí, gộp từ mọi nút đã mở. Tính lại khi đổi vũ khí hoặc nâng cấp, không tính mỗi đòn.
    /// </summary>
    public struct WeaponStats
    {
        public int damage;             // +% sát thương
        public int projectiles;        // +số đạn
        public int targets;            // +mục tiêu / xuyên
        public float explosionRadius;  // ô, 0 = không nổ
        public int explosionDamage;    // % sát thương vụ nổ
        public float range;            // +ô
        public float attackSpeed;      // phần hồi chiêu bị cắt, 0..MaxCooldownCut
        public float arc;              // +độ nửa góc chém

        /// <summary>Hồi chiêu giảm tối đa chừng này, để nâng hết cỡ vẫn còn nhịp đánh.</summary>
        public const float MaxCooldownCut = 0.6f;
        /// <summary>Vụ nổ không có nút tăng sát thương nổ thì gây chừng này % sát thương đòn.</summary>
        public const int BaseExplosionDamage = 50;

        public void Add(PlayerStatEffect e, int times)
        {
            float v = e.value * times;
            switch (e.stat)
            {
                case PlayerStat.WeaponDamage: damage += Mathf.RoundToInt(v); break;
                case PlayerStat.Projectiles: projectiles += Mathf.RoundToInt(v); break;
                case PlayerStat.AttackTargets: targets += Mathf.RoundToInt(v); break;
                case PlayerStat.ExplosionRadius: explosionRadius += v; break;
                case PlayerStat.ExplosionDamage: explosionDamage += Mathf.RoundToInt(v); break;
                case PlayerStat.AttackRange: range += v; break;
                case PlayerStat.AttackSpeed: attackSpeed = Mathf.Min(MaxCooldownCut, attackSpeed + v / 100f); break;
                case PlayerStat.Arc: arc += v; break;
            }
        }

        /// <summary>% sát thương của vụ nổ: mức gốc cộng phần nâng thêm.</summary>
        public int ExplosionPercent => explosionRadius > 0f ? BaseExplosionDamage + explosionDamage : 0;
    }

    /// <summary>Một dòng lưu: nút upgradeId của vũ khí weaponId đang ở cấp rank.</summary>
    [Serializable]
    public class UpgradeRank
    {
        public int weaponId;
        public int upgradeId;
        public int rank;
    }

    /// <summary>
    /// Cấp các nút nâng cấp của mọi vũ khí mà người chơi đã mở. Chỉ giữ số; luật mở (nút cha, cấp tối đa) đọc từ
    /// WeaponTemplate. Chủ dữ liệu (PlayerData, sau này là server) nạp vào qua <see cref="Load"/> và nghe <see cref="Changed"/>.
    /// </summary>
    public class WeaponUpgradeState
    {
        readonly Dictionary<int, int> ranks = new Dictionary<int, int>();
        readonly List<int> scratch = new List<int>();

        /// <summary>(weaponId, upgradeId, rank mới). Đặt lại cây thì bắn một lần cho mỗi nút về 0.</summary>
        public event Action<int, int, int> Changed;

        static int Key(int weaponId, int upgradeId) => (weaponId << 16) | (upgradeId & 0xFFFF);

        public int Rank(int weaponId, int upgradeId) => ranks.TryGetValue(Key(weaponId, upgradeId), out int r) ? r : 0;

        public void Load(IEnumerable<UpgradeRank> saved)
        {
            ranks.Clear();
            if (saved == null) return;
            foreach (UpgradeRank r in saved)
                if (r != null && r.rank > 0) ranks[Key(r.weaponId, r.upgradeId)] = r.rank;
        }

        /// <summary>Chép các nút đã mở (cấp &gt; 0) ra danh sách.</summary>
        public void Save(List<UpgradeRank> into)
        {
            into.Clear();
            foreach (var pair in ranks)
                into.Add(new UpgradeRank { weaponId = pair.Key >> 16, upgradeId = pair.Key & 0xFFFF, rank = pair.Value });
        }

        /// <summary>Còn nâng được: chưa tối đa và nút cha (nếu có) đã mở.</summary>
        public bool CanUpgrade(WeaponTemplate weapon, WeaponUpgrade up)
        {
            if (weapon == null || up == null) return false;
            if (Rank(weapon.id, up.id) >= up.maxRank) return false;
            return up.parent == 0 || Rank(weapon.id, up.parent) > 0;
        }

        /// <summary>Nâng một cấp. Miễn phí: chi phí (WeaponUpgrade.cost) chưa được trừ.</summary>
        public bool Upgrade(WeaponTemplate weapon, int upgradeId)
        {
            WeaponUpgrade up = weapon?.GetUpgrade(upgradeId);
            if (!CanUpgrade(weapon, up)) return false;
            int rank = Rank(weapon.id, upgradeId) + 1;
            ranks[Key(weapon.id, upgradeId)] = rank;
            Changed?.Invoke(weapon.id, upgradeId, rank);
            return true;
        }

        /// <summary>Đưa cả cây của vũ khí về 0.</summary>
        public void Reset(WeaponTemplate weapon)
        {
            if (weapon == null) return;
            scratch.Clear();
            foreach (WeaponUpgrade up in weapon.upgrades)
                if (ranks.Remove(Key(weapon.id, up.id))) scratch.Add(up.id);
            foreach (int id in scratch) Changed?.Invoke(weapon.id, id, 0);
        }

        /// <summary>Tổng số cấp đã mở trên cây của vũ khí.</summary>
        public int Spent(WeaponTemplate weapon)
        {
            int n = 0;
            if (weapon != null) foreach (WeaponUpgrade up in weapon.upgrades) n += Rank(weapon.id, up.id);
            return n;
        }

        /// <summary>Gộp chỉ số của mọi nút đã mở trên cây của vũ khí.</summary>
        public WeaponStats Compute(WeaponTemplate weapon)
        {
            var stats = new WeaponStats();
            if (weapon == null) return stats;
            foreach (WeaponUpgrade up in weapon.upgrades)
            {
                int rank = Mathf.Min(Rank(weapon.id, up.id), up.maxRank);
                if (rank <= 0) continue;
                foreach (PlayerStatEffect e in up.effects) stats.Add(e, rank);
            }
            return stats;
        }
    }
}
