using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Players;

namespace Game.EditorTools
{
    /// <summary>
    /// Dựng Assets/Data/Player/PlayerUpgradeTree.asset: đường cong lên cấp, điểm đột phá ở mốc, bậc Chuyên tinh và cây nâng
    /// cấp nhân vật bắt đầu từ một nút gốc ở tâm, toả ra 5 hướng (Sức mạnh, Tốc độ, Kinh nghiệm, Tài lộc, Sức mạnh đồ).
    /// Mỗi hướng: thân 2 vòng + nút Tinh thông cạnh thân, rồi 3 nhánh con (trái / giữa / phải) từ vòng 3 tới Đỉnh ở vòng 7:
    /// vòng 3 thường (cấp 5) · vòng 4 Đột phá (cấp 10) · vòng 5 thường (cấp 20) · vòng 6 Đột phá (cấp 30) · vòng 7 Đỉnh (cấp 40).
    /// </summary>
    /// <remarks>
    /// Số liệu SINH RA từ bộ mô phỏng cân bằng (tree_v2.py: ngân sách mỗi nút = giá x hệ số vòng, đổi ra chỉ số theo bảng quy
    /// đổi chung; kiểm bằng build 1 / 2 / 3 hướng, lai, farm ở các mốc cấp). Chỉnh tay trong asset được; chỉnh ở đây thì chạy
    /// "Rebuild". Chi tiết: Docs/03-Stats.md mục 8 và file Excel danh sách nút.
    /// "Build" an toàn: nút đã có (theo name) giữ nguyên id và số liệu chỉnh tay, chỉ thêm nút còn thiếu (bậc Chuyên tinh chỉ
    /// nạp khi asset chưa có). "Rebuild" xoá hết nút + bậc rồi dựng lại, đặt lại thông số lên cấp (id đánh lại từ 1).
    /// </remarks>
    public static class PlayerUpgradeSetup
    {
        const string DataPath = "Assets/_Project/Core/Data/Player/PlayerUpgradeTree.asset";

        const int PointsPerLevel = 3, MilestoneEvery = 10, MilestoneBonus = 6;

        static readonly string[] BranchNames =
            { "Sức mạnh|Might", "Tốc độ|Speed", "Kinh nghiệm|Wisdom", "Tài lộc|Fortune", "Sức mạnh đồ|Gear" };

        /// <summary>Một nút mẫu: nút cha theo tên để không phụ thuộc id.</summary>
        class Node
        {
            public string name, displayName, description, parent, group;
            public int branch, ring, cost, maxRank, requiredLevel;
            public float lateral;
            public UpgradeNodeKind kind;
            public PlayerStatEffect[] effects;
        }

        static Node Nd(string name, string label, string group, string parent, int branch, int ring, float lateral,
                       UpgradeNodeKind kind, int cost, int maxRank, int requiredLevel, string description,
                       params PlayerStatEffect[] effects) =>
            new Node
            {
                name = name, displayName = label, group = group, parent = parent, branch = branch, ring = ring,
                lateral = lateral, kind = kind, cost = cost, maxRank = maxRank, requiredLevel = requiredLevel,
                description = description, effects = effects,
            };

        static BranchMastery Ms(int branch, int points, params PlayerStatEffect[] effects) =>
            new BranchMastery { branch = branch, points = points, effects = new List<PlayerStatEffect>(effects) };

        static PlayerStatEffect E(PlayerStat stat, float value) => new PlayerStatEffect(stat, value);

        // Sinh từ mô phỏng — thứ tự: nút gốc, rồi mỗi hướng: thân 1, thân 2, 3 nhánh con (vòng 3 → 7), Tinh thông.
        static readonly Node[] Tree =
        {
            Nd("origin", "Khởi nguồn", "", null, 0, 0, 0, UpgradeNodeKind.Normal, 1, 1, 1, "Điểm bắt đầu của mọi hướng.", E(PlayerStat.Damage, 1.5f), E(PlayerStat.MaxHp, 10)),
            Nd("might", "Sức mạnh", "Sức mạnh", "origin", 0, 1, 0, UpgradeNodeKind.Normal, 1, 5, 1, "", E(PlayerStat.Damage, 1.5f)),
            Nd("constitution", "Thể phách", "Sức mạnh", "might", 0, 2, 0, UpgradeNodeKind.Normal, 1, 5, 3, "", E(PlayerStat.MaxHp, 10)),
            Nd("keen", "Tinh mắt", "Chí mạng", "constitution", 0, 3, -1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.Crit, 2)),
            Nd("lethal", "Chí tử", "Chí mạng", "keen", 0, 4, -1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.CritDamage, 45)),
            Nd("blade_master", "Bậc thầy binh khí", "Chí mạng", "lethal", 0, 5, -1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Sức mạnh đồ.", E(PlayerStat.Crit, 2), E(PlayerStat.WeaponPower, 2)),
            Nd("assassin", "Sát thủ", "Chí mạng", "blade_master", 0, 6, -1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.CritDamage, 28), E(PlayerStat.Crit, 5.5f)),
            Nd("slayer", "Sát thần", "Chí mạng", "assassin", 0, 7, -2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Chí mạng. Xen Sức mạnh đồ.", E(PlayerStat.Crit, 7), E(PlayerStat.CritDamage, 35), E(PlayerStat.WeaponPower, 7.5f)),
            Nd("power", "Cường lực", "Công phá", "constitution", 0, 3, 0, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.Damage, 3.5f)),
            Nd("vigor", "Khí huyết", "Công phá", "power", 0, 4, 0, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.Damage, 9), E(PlayerStat.MaxHp, 30)),
            Nd("fury", "Cuồng nộ", "Công phá", "vigor", 0, 5, 0, UpgradeNodeKind.Normal, 2, 3, 20, "", E(PlayerStat.Damage, 4)),
            Nd("berserker", "Cuồng chiến", "Công phá", "fury", 0, 6, 0, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.Damage, 17)),
            Nd("titan", "Bá vương", "Công phá", "berserker", 0, 7, 0, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Công phá.", E(PlayerStat.Damage, 20), E(PlayerStat.MaxHp, 44)),
            Nd("iron_skin", "Da thép", "Phòng thủ", "constitution", 0, 3, 1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.Defense, 2)),
            Nd("recovery", "Hồi phục", "Phòng thủ", "iron_skin", 0, 4, 1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.HpRegen, 1.1f), E(PlayerStat.MaxHp, 45)),
            Nd("steady_step", "Bộ pháp vững", "Phòng thủ", "recovery", 0, 5, 1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Tốc độ.", E(PlayerStat.Defense, 1.5f), E(PlayerStat.MoveSpeed, 1.5f)),
            Nd("fortress", "Thành lũy", "Phòng thủ", "steady_step", 0, 6, 1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.MaxHp, 57), E(PlayerStat.Defense, 4.5f)),
            Nd("undying", "Bất khuất", "Phòng thủ", "fortress", 0, 7, 2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Phòng thủ. Xen Tốc độ.", E(PlayerStat.Defense, 7), E(PlayerStat.HpRegen, 1.1f), E(PlayerStat.MoveSpeed, 6.5f)),
            Nd("might_mastery", "Tinh thông Sức mạnh", "Sức mạnh", "constitution", 0, 2, 1.35f, UpgradeNodeKind.Mastery, 2, 20, 20, "Nâng được nhiều lần, hiệu quả thấp: chỗ tiêu điểm giữa các mốc.", E(PlayerStat.Damage, 1.5f)),
            Nd("swift", "Nhanh nhẹn", "Tốc độ", "origin", 1, 1, 0, UpgradeNodeKind.Normal, 1, 5, 1, "", E(PlayerStat.MoveSpeed, 1.5f)),
            Nd("quick_hands", "Tay nhanh", "Tốc độ", "swift", 1, 2, 0, UpgradeNodeKind.Normal, 1, 5, 3, "", E(PlayerStat.AttackSpeed, 1)),
            Nd("quick_strike", "Ra đòn nhanh", "Đòn nhanh", "quick_hands", 1, 3, -1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.AttackSpeed, 2)),
            Nd("flurry", "Liên hoàn", "Đòn nhanh", "quick_strike", 1, 4, -1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.AttackSpeed, 7)),
            Nd("swift_blade", "Kiếm tốc", "Đòn nhanh", "flurry", 1, 5, -1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Sức mạnh.", E(PlayerStat.AttackSpeed, 1.5f), E(PlayerStat.Damage, 1.5f)),
            Nd("frenzy", "Cuồng phong", "Đòn nhanh", "swift_blade", 1, 6, -1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.AttackSpeed, 9)),
            Nd("tempest", "Bão tố", "Đòn nhanh", "frenzy", 1, 7, -2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Đòn nhanh. Xen Sức mạnh.", E(PlayerStat.AttackSpeed, 7), E(PlayerStat.Damage, 13)),
            Nd("dash", "Lướt nhanh", "Thân pháp", "quick_hands", 1, 3, 0, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.DashHaste, 4.5f)),
            Nd("sprinter", "Nước rút", "Thân pháp", "dash", 1, 4, 0, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen sinh tồn.", E(PlayerStat.MoveSpeed, 7), E(PlayerStat.MaxHp, 45)),
            Nd("wind_step", "Bước gió", "Thân pháp", "sprinter", 1, 5, 0, UpgradeNodeKind.Normal, 2, 3, 20, "", E(PlayerStat.DashHaste, 2.5f), E(PlayerStat.MoveSpeed, 2)),
            Nd("blink", "Chớp nhoáng", "Thân pháp", "wind_step", 1, 6, 0, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30. Xen sinh tồn.", E(PlayerStat.DashHaste, 11), E(PlayerStat.Defense, 4.5f)),
            Nd("phantom", "Bóng ma", "Thân pháp", "blink", 1, 7, 0, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Thân pháp. Xen sinh tồn.", E(PlayerStat.MoveSpeed, 9), E(PlayerStat.DashHaste, 12), E(PlayerStat.Defense, 4.5f)),
            Nd("focus", "Tập trung", "Tâm tốc", "quick_hands", 1, 3, 1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.SkillHaste, 2)),
            Nd("channel", "Vận khí", "Tâm tốc", "focus", 1, 4, 1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.SkillHaste, 9)),
            Nd("quick_study", "Học nhanh", "Tâm tốc", "channel", 1, 5, 1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Kinh nghiệm.", E(PlayerStat.SkillHaste, 2), E(PlayerStat.ExpBonus, 2.5f)),
            Nd("zen", "Nhập định", "Tâm tốc", "quick_study", 1, 6, 1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30. Xen Sức mạnh.", E(PlayerStat.SkillHaste, 5.5f), E(PlayerStat.Damage, 8.5f)),
            Nd("time_warp", "Ngưng đọng", "Tâm tốc", "zen", 1, 7, 2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Tâm tốc. Xen Kinh nghiệm, Sức mạnh.", E(PlayerStat.SkillHaste, 6), E(PlayerStat.ExpBonus, 18), E(PlayerStat.Damage, 9)),
            Nd("speed_mastery", "Tinh thông Tốc độ", "Tốc độ", "quick_hands", 1, 2, 1.35f, UpgradeNodeKind.Mastery, 2, 20, 20, "Nâng được nhiều lần, hiệu quả thấp: chỗ tiêu điểm giữa các mốc. Xen Sức mạnh.", E(PlayerStat.Damage, 1), E(PlayerStat.MoveSpeed, 1)),
            Nd("study", "Ham học", "Kinh nghiệm", "origin", 2, 1, 0, UpgradeNodeKind.Normal, 1, 5, 1, "", E(PlayerStat.ExpBonus, 3)),
            Nd("training", "Rèn luyện", "Kinh nghiệm", "study", 2, 2, 0, UpgradeNodeKind.Normal, 1, 5, 3, "", E(PlayerStat.HitExp, 1)),
            Nd("practice", "Luyện tập", "Khổ luyện", "training", 2, 3, -1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.HitExp, 2)),
            Nd("sparring", "Đối luyện", "Khổ luyện", "practice", 2, 4, -1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen Sức mạnh.", E(PlayerStat.HitExp, 4.5f), E(PlayerStat.Damage, 7)),
            Nd("agile_mind", "Linh hoạt", "Khổ luyện", "sparring", 2, 5, -1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Tốc độ.", E(PlayerStat.HitExp, 2), E(PlayerStat.MoveSpeed, 1.5f)),
            Nd("discipline", "Kỷ luật", "Khổ luyện", "agile_mind", 2, 6, -1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30. Xen Sức mạnh.", E(PlayerStat.HitExp, 5.5f), E(PlayerStat.Damage, 8.5f)),
            Nd("grandmaster", "Tông sư", "Khổ luyện", "discipline", 2, 7, -2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Khổ luyện. Xen Tốc độ.", E(PlayerStat.HitExp, 12), E(PlayerStat.AttackSpeed, 4.5f)),
            Nd("scholar", "Học giả", "Uyên bác", "training", 2, 3, 0, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.ExpBonus, 6.5f)),
            Nd("meditation", "Thiền định", "Uyên bác", "scholar", 2, 4, 0, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen sinh tồn.", E(PlayerStat.ExpBonus, 14), E(PlayerStat.HpRegen, 1.1f)),
            Nd("wisdom", "Thông thái", "Uyên bác", "meditation", 2, 5, 0, UpgradeNodeKind.Normal, 2, 3, 20, "", E(PlayerStat.ExpBonus, 8)),
            Nd("enlightened", "Giác ngộ", "Uyên bác", "wisdom", 2, 6, 0, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.ExpBonus, 34)),
            Nd("sage", "Hiền triết", "Uyên bác", "enlightened", 2, 7, 0, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Uyên bác. Xen sinh tồn.", E(PlayerStat.ExpBonus, 35), E(PlayerStat.HpRegen, 1.45f)),
            Nd("hunter", "Thợ săn", "Săn thưởng", "training", 2, 3, 1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.ExpBonus, 6.5f)),
            Nd("tracker", "Truy dấu", "Săn thưởng", "hunter", 2, 4, 1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen Sức mạnh.", E(PlayerStat.ExpBonus, 14), E(PlayerStat.Damage, 7)),
            Nd("bounty", "Tiền thưởng", "Săn thưởng", "tracker", 2, 5, 1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Tài lộc.", E(PlayerStat.ExpBonus, 5.5f), E(PlayerStat.GoldFind, 5.5f)),
            Nd("veteran", "Lão luyện", "Săn thưởng", "bounty", 2, 6, 1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.ExpBonus, 34)),
            Nd("legend", "Huyền thoại", "Săn thưởng", "veteran", 2, 7, 2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Săn thưởng. Xen Tài lộc.", E(PlayerStat.ExpBonus, 26), E(PlayerStat.GoldFind, 53)),
            Nd("wisdom_mastery", "Tinh thông Kinh nghiệm", "Kinh nghiệm", "training", 2, 2, 1.35f, UpgradeNodeKind.Mastery, 2, 20, 20, "Nâng được nhiều lần, hiệu quả thấp: chỗ tiêu điểm giữa các mốc.", E(PlayerStat.ExpBonus, 3.5f)),
            Nd("greed", "Hám lợi", "Tài lộc", "origin", 3, 1, 0, UpgradeNodeKind.Normal, 1, 5, 1, "", E(PlayerStat.GoldFind, 6)),
            Nd("lucky", "May mắn", "Tài lộc", "greed", 3, 2, 0, UpgradeNodeKind.Normal, 1, 5, 3, "", E(PlayerStat.ItemFind, 12)),
            Nd("coin", "Nhặt xu", "Thương nhân", "lucky", 3, 3, -1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.GoldFind, 13)),
            Nd("haggle", "Mặc cả", "Thương nhân", "coin", 3, 4, -1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen sinh tồn.", E(PlayerStat.GoldFind, 27), E(PlayerStat.MaxHp, 45)),
            Nd("wise_trade", "Buôn khôn", "Thương nhân", "haggle", 3, 5, -1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Kinh nghiệm.", E(PlayerStat.GoldFind, 11), E(PlayerStat.ExpBonus, 2.5f)),
            Nd("treasurer", "Thủ quỹ", "Thương nhân", "wise_trade", 3, 6, -1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.GoldFind, 68)),
            Nd("tycoon", "Phú hộ", "Thương nhân", "treasurer", 3, 7, -2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Thương nhân. Xen Kinh nghiệm.", E(PlayerStat.GoldFind, 53), E(PlayerStat.ExpBonus, 26)),
            Nd("treasure", "Săn báu", "Săn kho báu", "lucky", 3, 3, 0, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.ItemFind, 26)),
            Nd("lucky_charm", "Bùa may", "Săn kho báu", "treasure", 3, 4, 0, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen sinh tồn.", E(PlayerStat.ItemFind, 54), E(PlayerStat.MaxHp, 45)),
            Nd("fortune", "Vận may", "Săn kho báu", "lucky_charm", 3, 5, 0, UpgradeNodeKind.Normal, 2, 3, 20, "", E(PlayerStat.ItemFind, 32)),
            Nd("jackpot", "Trúng mánh", "Săn kho báu", "fortune", 3, 6, 0, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30. Xen Sức mạnh.", E(PlayerStat.ItemFind, 68), E(PlayerStat.Damage, 8.5f)),
            Nd("golden_touch", "Bàn tay vàng", "Săn kho báu", "jackpot", 3, 7, 0, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Săn kho báu.", E(PlayerStat.ItemFind, 106), E(PlayerStat.GoldFind, 53)),
            Nd("plunder", "Cướp bóc", "Chiến lợi phẩm", "lucky", 3, 3, 1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.GoldFind, 13)),
            Nd("mercenary", "Lính đánh thuê", "Chiến lợi phẩm", "plunder", 3, 4, 1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen Sức mạnh.", E(PlayerStat.GoldFind, 27), E(PlayerStat.Damage, 7)),
            Nd("armory", "Kho vũ khí", "Chiến lợi phẩm", "mercenary", 3, 5, 1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Sức mạnh đồ.", E(PlayerStat.GoldFind, 11), E(PlayerStat.WeaponPower, 2)),
            Nd("war_chest", "Rương chiến", "Chiến lợi phẩm", "armory", 3, 6, 1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30. Xen Sức mạnh.", E(PlayerStat.GoldFind, 34), E(PlayerStat.Damage, 8.5f)),
            Nd("royal_hoard", "Kho báu hoàng gia", "Chiến lợi phẩm", "war_chest", 3, 7, 2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Chiến lợi phẩm. Xen Sức mạnh đồ.", E(PlayerStat.GoldFind, 53), E(PlayerStat.WeaponPower, 19)),
            Nd("fortune_mastery", "Tinh thông Tài lộc", "Tài lộc", "lucky", 3, 2, 1.35f, UpgradeNodeKind.Mastery, 2, 20, 20, "Nâng được nhiều lần, hiệu quả thấp: chỗ tiêu điểm giữa các mốc.", E(PlayerStat.GoldFind, 6.5f)),
            Nd("honing", "Mài giũa", "Sức mạnh đồ", "origin", 4, 1, 0, UpgradeNodeKind.Normal, 1, 5, 1, "", E(PlayerStat.WeaponPower, 2)),
            Nd("tempering", "Tôi luyện", "Sức mạnh đồ", "honing", 4, 2, 0, UpgradeNodeKind.Normal, 1, 5, 3, "", E(PlayerStat.UpgradeBoost, 7)),
            Nd("forging", "Rèn đúc", "Rèn đúc", "tempering", 4, 3, -1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.UpgradeBoost, 15)),
            Nd("refine", "Tinh luyện", "Rèn đúc", "forging", 4, 4, -1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.UpgradeBoost, 63)),
            Nd("appraiser", "Thẩm định", "Rèn đúc", "refine", 4, 5, -1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Tài lộc.", E(PlayerStat.UpgradeBoost, 13), E(PlayerStat.GoldFind, 5.5f)),
            Nd("artisan", "Nghệ nhân", "Rèn đúc", "appraiser", 4, 6, -1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.UpgradeBoost, 80)),
            Nd("masterwork", "Kiệt tác", "Rèn đúc", "artisan", 4, 7, -2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Rèn đúc. Xen Tài lộc.", E(PlayerStat.UpgradeBoost, 62), E(PlayerStat.GoldFind, 53)),
            Nd("sharpen", "Lưỡi bén", "Binh khí", "tempering", 4, 3, 0, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.WeaponPower, 5)),
            Nd("grip", "Chuôi chắc", "Binh khí", "sharpen", 4, 4, 0, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10. Xen sinh tồn.", E(PlayerStat.WeaponPower, 10), E(PlayerStat.Defense, 3.5f)),
            Nd("heavy", "Trọng binh", "Binh khí", "grip", 4, 5, 0, UpgradeNodeKind.Normal, 2, 3, 20, "", E(PlayerStat.WeaponPower, 6)),
            Nd("divine_arms", "Thần binh", "Binh khí", "heavy", 4, 6, 0, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30.", E(PlayerStat.WeaponPower, 25)),
            Nd("artifact", "Thần khí", "Binh khí", "divine_arms", 4, 7, 0, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Binh khí. Xen sinh tồn.", E(PlayerStat.WeaponPower, 26), E(PlayerStat.Defense, 4.5f)),
            Nd("reach", "Tầm với", "Tầm xa", "tempering", 4, 3, 1, UpgradeNodeKind.Normal, 2, 3, 5, "", E(PlayerStat.Range, 3.5f)),
            Nd("long_arm", "Vươn xa", "Tầm xa", "reach", 4, 4, 1.25f, UpgradeNodeKind.Breakthrough, 5, 1, 10, "Đột phá mốc cấp 10.", E(PlayerStat.Range, 7), E(PlayerStat.WeaponPower, 10)),
            Nd("piercing", "Xuyên phá", "Tầm xa", "long_arm", 4, 5, 1.5f, UpgradeNodeKind.Normal, 2, 3, 20, "Xen Sức mạnh.", E(PlayerStat.Range, 2), E(PlayerStat.Crit, 1.5f)),
            Nd("sniper", "Thiện xạ", "Tầm xa", "piercing", 4, 6, 1.75f, UpgradeNodeKind.Breakthrough, 6, 1, 30, "Đột phá mốc cấp 30. Xen Sức mạnh.", E(PlayerStat.Range, 8.5f), E(PlayerStat.CritDamage, 28)),
            Nd("far_strike", "Vạn dặm", "Tầm xa", "sniper", 4, 7, 2, UpgradeNodeKind.Capstone, 8, 1, 40, "Đỉnh nhánh Tầm xa. Xen Sức mạnh.", E(PlayerStat.Range, 9), E(PlayerStat.Crit, 6), E(PlayerStat.CritDamage, 29)),
            Nd("gear_mastery", "Tinh thông Sức mạnh đồ", "Sức mạnh đồ", "tempering", 4, 2, 1.35f, UpgradeNodeKind.Mastery, 2, 20, 20, "Nâng được nhiều lần, hiệu quả thấp: chỗ tiêu điểm giữa các mốc.", E(PlayerStat.WeaponPower, 2.5f)),
        };

        // Chuyên tinh: 15 / 35 / 60 / 90 / 120 điểm trong một hướng. Bậc 5 chỉ người dồn gần hết điểm vào hướng đó mới tới.
        static readonly BranchMastery[] Masteries =
        {
            Ms(0, 15, E(PlayerStat.Damage, 4), E(PlayerStat.MaxHp, 13)),
            Ms(0, 35, E(PlayerStat.Damage, 8), E(PlayerStat.MaxHp, 27)),
            Ms(0, 60, E(PlayerStat.Damage, 14), E(PlayerStat.MaxHp, 47)),
            Ms(0, 90, E(PlayerStat.Damage, 22), E(PlayerStat.MaxHp, 73)),
            Ms(0, 120, E(PlayerStat.Damage, 46), E(PlayerStat.MaxHp, 153)),
            Ms(1, 15, E(PlayerStat.AttackSpeed, 1), E(PlayerStat.MoveSpeed, 2), E(PlayerStat.MaxHp, 13)),
            Ms(1, 35, E(PlayerStat.AttackSpeed, 2), E(PlayerStat.MoveSpeed, 4), E(PlayerStat.MaxHp, 27)),
            Ms(1, 60, E(PlayerStat.AttackSpeed, 3.5f), E(PlayerStat.MoveSpeed, 7), E(PlayerStat.MaxHp, 47)),
            Ms(1, 90, E(PlayerStat.AttackSpeed, 6), E(PlayerStat.MoveSpeed, 11), E(PlayerStat.MaxHp, 73)),
            Ms(1, 120, E(PlayerStat.AttackSpeed, 12), E(PlayerStat.MoveSpeed, 23), E(PlayerStat.MaxHp, 153)),
            Ms(2, 15, E(PlayerStat.ExpBonus, 4), E(PlayerStat.Damage, 2), E(PlayerStat.MaxHp, 13)),
            Ms(2, 35, E(PlayerStat.ExpBonus, 8), E(PlayerStat.Damage, 4), E(PlayerStat.MaxHp, 27)),
            Ms(2, 60, E(PlayerStat.ExpBonus, 14), E(PlayerStat.Damage, 7), E(PlayerStat.MaxHp, 47)),
            Ms(2, 90, E(PlayerStat.ExpBonus, 22), E(PlayerStat.Damage, 11), E(PlayerStat.MaxHp, 73)),
            Ms(2, 120, E(PlayerStat.ExpBonus, 46), E(PlayerStat.Damage, 23), E(PlayerStat.MaxHp, 153)),
            Ms(3, 15, E(PlayerStat.GoldFind, 8), E(PlayerStat.ItemFind, 16), E(PlayerStat.Damage, 2)),
            Ms(3, 35, E(PlayerStat.GoldFind, 16), E(PlayerStat.ItemFind, 32), E(PlayerStat.Damage, 4)),
            Ms(3, 60, E(PlayerStat.GoldFind, 28), E(PlayerStat.ItemFind, 56), E(PlayerStat.Damage, 7)),
            Ms(3, 90, E(PlayerStat.GoldFind, 44), E(PlayerStat.ItemFind, 88), E(PlayerStat.Damage, 11)),
            Ms(3, 120, E(PlayerStat.GoldFind, 92), E(PlayerStat.ItemFind, 184), E(PlayerStat.Damage, 23)),
            Ms(4, 15, E(PlayerStat.WeaponPower, 3), E(PlayerStat.UpgradeBoost, 9.5f), E(PlayerStat.Defense, 1)),
            Ms(4, 35, E(PlayerStat.WeaponPower, 6), E(PlayerStat.UpgradeBoost, 19), E(PlayerStat.Defense, 2)),
            Ms(4, 60, E(PlayerStat.WeaponPower, 10), E(PlayerStat.UpgradeBoost, 33), E(PlayerStat.Defense, 3.5f)),
            Ms(4, 90, E(PlayerStat.WeaponPower, 16), E(PlayerStat.UpgradeBoost, 51), E(PlayerStat.Defense, 6)),
            Ms(4, 120, E(PlayerStat.WeaponPower, 34), E(PlayerStat.UpgradeBoost, 107), E(PlayerStat.Defense, 12)),
        };

        /// <summary>Xoá mọi nút và bậc Chuyên tinh rồi dựng lại cây mẫu; đặt lại thông số lên cấp và tên hướng. Id đánh lại từ 1.</summary>
        [MenuItem("Tools/Core/Player/Rebuild Upgrade Tree (xoá nút cũ)")]
        public static void Rebuild()
        {
            PlayerUpgradeTree tree = LoadOrCreate();
            tree.nodes.Clear();
            tree.masteries.Clear();
            tree.pointsPerLevel = PointsPerLevel;
            tree.milestoneEvery = MilestoneEvery;
            tree.milestoneBonusPoints = MilestoneBonus;
            tree.branchNames = (string[])BranchNames.Clone();
            EditorUtility.SetDirty(tree);
            Build();
        }

        [MenuItem("Tools/Core/Player/Build Upgrade Tree")]
        public static void Build()
        {
            PlayerUpgradeTree tree = LoadOrCreate();

            int added = 0;
            foreach (Node n in Tree)
            {
                if (tree.Find(n.name) != null) continue;
                PlayerUpgradeNode parent = n.parent != null ? tree.Find(n.parent) : null;
                if (n.parent != null && parent == null)
                {
                    Debug.LogWarning("PlayerUpgradeSetup: " + n.name + " thiếu nút cha " + n.parent + ".");
                    continue;
                }
                tree.nodes.Add(new PlayerUpgradeNode
                {
                    id = NextId(tree), name = n.name, displayName = n.displayName, description = n.description,
                    group = n.group, kind = n.kind, parent = parent != null ? parent.id : 0,
                    maxRank = n.maxRank, cost = n.cost, requiredLevel = n.requiredLevel,
                    branch = n.branch, ring = n.ring, lateral = n.lateral,
                    effects = new List<PlayerStatEffect>(n.effects),
                });
                added++;
            }
            if (tree.masteries.Count == 0)
                foreach (BranchMastery m in Masteries)
                    tree.masteries.Add(new BranchMastery { branch = m.branch, points = m.points, effects = new List<PlayerStatEffect>(m.effects) });

            EditorUtility.SetDirty(tree);
            AssignToScene(tree);
            AssetDatabase.SaveAssets();
            Selection.activeObject = tree;

            int total = 0;
            foreach (PlayerUpgradeNode n in tree.nodes) total += n.cost * n.maxRank;
            Debug.Log($"PlayerUpgradeTree: thêm {added} nút, tổng {tree.nodes.Count} nút, {tree.masteries.Count} bậc Chuyên tinh, "
                      + $"cả cây {total} điểm, tới cấp {tree.maxLevel} được {tree.TotalPointsAt(tree.maxLevel)} điểm.", tree);
        }

        static PlayerUpgradeTree LoadOrCreate()
        {
            var tree = AssetDatabase.LoadAssetAtPath<PlayerUpgradeTree>(DataPath);
            if (tree != null) return tree;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DataPath));
            tree = ScriptableObject.CreateInstance<PlayerUpgradeTree>();
            tree.pointsPerLevel = PointsPerLevel;
            AssetDatabase.CreateAsset(tree, DataPath);
            return tree;
        }

        // id không bao giờ dùng lại: lớn nhất đang có + 1
        static int NextId(PlayerUpgradeTree tree)
        {
            int max = 0;
            foreach (PlayerUpgradeNode n in tree.nodes) if (n.id > max) max = n.id;
            return max + 1;
        }

        /// <summary>Gán cây vào LocalPlaySettings nếu ô còn trống (Game: thay cho GameController 2D), để PlayerUpgradeTree.Current có giá trị.</summary>
        static void AssignToScene(PlayerUpgradeTree tree) => CoreHostAsset.Bind("upgradeTree", tree, onlyIfEmpty: true);
    }
}
