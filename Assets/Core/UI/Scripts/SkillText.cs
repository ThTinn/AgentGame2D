using System.Text;
using Game.Players;
using Game.Skills;

namespace Game.UI
{
    /// <summary>Chữ mô tả skill / vũ khí dùng chung cho bảng skill và popup thông tin vật phẩm.</summary>
    public static class SkillText
    {
        /// <summary>Tên kiểu đòn.</summary>
        public static string TypeLabel(SkillType t)
        {
            switch (t)
            {
                case SkillType.Melee: return Lang.GetSplitText("Cận chiến|Melee");
                case SkillType.Projectile: return Lang.GetSplitText("Bắn xa|Ranged");
                case SkillType.Area: return Lang.GetSplitText("Vùng|Area");
                case SkillType.Dash: return Lang.GetSplitText("Lướt|Dash");
                default: return Lang.GetSplitText("Hỗ trợ|Buff");
            }
        }

        /// <summary>
        /// Chỉ số đòn đánh thường của vũ khí ở cấp người chơi đang có (kiểu đòn, sát thương, tầm, hồi, mục tiêu), rồi
        /// phần cộng từ cây nâng cấp nếu đã nâng.
        /// </summary>
        public static void AppendWeaponStats(StringBuilder info, WeaponTemplate weapon, SkillTemplate skill, PlayerProfile player)
        {
            SkillInfo s = skill?.Stats(player != null && player.BasicSkill != null ? player.BasicSkill.level : 1);
            if (s != null)
            {
                info.Append(Lang.GetSplitText("Kiểu đòn|Type")).Append(": ").Append(TypeLabel(skill.type)).Append('\n');
                info.Append(Lang.GetSplitText("Sát thương|Damage")).Append(": ").Append(s.damage).Append("%\n");
                info.Append(Lang.GetSplitText("Tầm|Range")).Append(": ").Append(s.range.ToString("0.#"))
                    .Append(Lang.GetSplitText(" ô| cells")).Append('\n');
                info.Append(Lang.GetSplitText("Hồi|Cooldown")).Append(": ").Append(s.cooldown.ToString("0.##")).Append("s\n");
                info.Append(Lang.GetSplitText("Mục tiêu|Targets")).Append(": ")
                    .Append(s.maxTarget > 0 ? s.maxTarget.ToString() : Lang.GetSplitText("mọi kẻ trong tầm|all in range"));
            }
            if (weapon != null && player != null && player.UpgradesSpent(weapon) > 0)
                info.Append("\n\n").Append(WeaponUpgradePanel.StatsText(player.StatsOf(weapon), player.UpgradesSpent(weapon)));
        }
    }
}
