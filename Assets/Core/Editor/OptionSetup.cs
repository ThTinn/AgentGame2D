using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Items;
using Game.Map.Editor;
using Game.Players;
using Game.Skills;

namespace Game.EditorTools
{
    /// <summary>
    /// Dựng Assets/Data/Player/OptionDatabase.asset: mỗi <see cref="PlayerStat"/> một option (tên song ngữ, mô tả, cách ghi
    /// số, trần, thứ tự hiện). Chạy lại an toàn: option đã có (theo id) giữ nguyên mọi chỉnh tay, chỉ thêm cái còn thiếu.
    /// Kèm menu kiểm tra: đối chiếu bảng với enum, với cây nâng cấp nhân vật và với option của mọi vật phẩm.
    /// </summary>
    public static class OptionSetup
    {
        public const string DataPath = "Assets/_Project/Core/Data/Player/OptionDatabase.asset";
        const string TreePath = "Assets/_Project/Core/Data/Player/PlayerUpgradeTree.asset";
        const string WeaponPath = MapPaths.Root + "/Skills/WeaponDatabase.asset";
        const string SkillPath = MapPaths.Root + "/Skills/SkillDatabase.asset";
        static readonly PlayerStat[] WeaponNeeds = { PlayerStat.AttackDamage, PlayerStat.AttackCooldown, PlayerStat.AttackRange };

        struct Seed
        {
            public PlayerStat stat;
            public string name, displayName, description;
            public StatFormat format;
            public StatCap capKind;
            public float cap;
        }

        static Seed S(PlayerStat stat, string name, string displayName, StatFormat format, string description,
                      StatCap capKind = StatCap.None, float cap = 0f) =>
            new Seed { stat = stat, name = name, displayName = displayName, format = format, description = description,
                       capKind = capKind, cap = cap };

        // Thứ tự mảng = thứ tự hiện trong bảng tổng (order = chỉ số x 10, chừa chỗ chèn): chiến đấu, sinh tồn, tốc độ,
        // vũ khí, kinh tế. Trần lấy từ các số mặc định của PlayerStats để bảng mới dựng cho kết quả y như trước.
        static readonly Seed[] Seeds =
        {
            S(PlayerStat.Damage, "damage", "Sát thương|Damage", StatFormat.Percent,
              "Tăng % sát thương mọi đòn (đánh thường lẫn skill).|+% damage of every hit (basic attacks and skills)."),
            S(PlayerStat.Crit, "crit", "Chí mạng|Crit chance", StatFormat.Percent,
              "Cộng vào tỉ lệ chí mạng gốc.|Added to the base critical chance.", StatCap.Total, PlayerStats.DefaultMaxCritChance),
            S(PlayerStat.CritDamage, "crit_damage", "ST chí mạng|Crit damage", StatFormat.Percent,
              "Đòn chí mạng gốc gây x2; mỗi 100% cộng thêm là thêm x1.|Crits deal x2 base; every +100% adds x1."),
            S(PlayerStat.MaxHp, "max_hp", "Máu tối đa|Max HP", StatFormat.Flat,
              "Cộng thẳng vào máu tối đa.|Added to maximum HP."),
            S(PlayerStat.Defense, "defense", "Giáp|Armor", StatFormat.Percent,
              "Giảm % sát thương mỗi đòn phải nhận (luôn mất ít nhất 1 máu).|Cuts % of every hit taken (always at least 1).",
              StatCap.Bonus, PlayerStats.DefaultMaxDefense),
            S(PlayerStat.HpRegen, "hp_regen", "Hồi máu|HP regen", StatFormat.PerSecond,
              "Máu hồi mỗi giây khi còn sống và chưa đầy.|HP regenerated per second while alive and not full."),
            S(PlayerStat.MoveSpeed, "move_speed", "Tốc chạy|Move speed", StatFormat.Percent,
              "Tăng % tốc độ chạy.|+% running speed.", StatCap.Bonus, PlayerStats.DefaultMaxMoveSpeed),
            S(PlayerStat.AttackSpeed, "attack_speed", "Hồi đòn thường|Attack cooldown", StatFormat.ReducePercent,
              "Giảm % hồi chiêu đánh thường, cộng chung trần với nâng cấp tốc đánh của vũ khí.|"
              + "Cuts basic attack cooldown; shares its cap with the weapon's attack speed upgrades.",
              StatCap.SharedWithWeapon, PlayerStats.DefaultMaxAttackSpeedCut),
            S(PlayerStat.SkillHaste, "skill_haste", "Hồi chiêu skill|Skill cooldown", StatFormat.ReducePercent,
              "Giảm % hồi chiêu của skill ô 1-4.|Cuts cooldown of skills 1-4.", StatCap.Bonus, PlayerStats.DefaultMaxSkillHaste),
            S(PlayerStat.DashHaste, "dash_haste", "Hồi chiêu lướt|Dash cooldown", StatFormat.ReducePercent,
              "Giảm % hồi chiêu lướt.|Cuts dash cooldown.", StatCap.Bonus, PlayerStats.DefaultMaxDashHaste),
            S(PlayerStat.WeaponPower, "weapon_power", "ST đòn thường|Basic attack damage", StatFormat.Percent,
              "Tăng % sát thương đòn đánh thường (đòn của vũ khí).|+% damage of basic (weapon) attacks."),
            S(PlayerStat.UpgradeBoost, "upgrade_boost", "Hiệu quả nâng cấp VK|Weapon upgrade effect", StatFormat.Percent,
              "Phóng to các nút nâng cấp vũ khí: sát thương, nổ, tầm, góc chém.|Scales weapon upgrades: damage, blast, range, arc."),
            S(PlayerStat.Range, "range", "Tầm đòn thường|Basic attack range", StatFormat.Percent,
              "Tăng % tầm đòn đánh thường.|+% basic attack range."),
            S(PlayerStat.ExpBonus, "exp_bonus", "EXP hạ quái|Kill EXP", StatFormat.Percent,
              "Tăng % EXP nhận khi hạ quái.|+% EXP from kills."),
            S(PlayerStat.HitExp, "hit_exp", "EXP theo đòn (% ST)|EXP per hit (% dmg)", StatFormat.Percent,
              "Đổi % sát thương gây ra thành EXP.|Turns % of damage dealt into EXP."),
            S(PlayerStat.GoldFind, "gold_find", "Vàng nhặt được|Gold find", StatFormat.Percent,
              "Tăng % vàng rơi ra khi hạ quái.|+% gold dropped by monsters."),
            S(PlayerStat.ItemFind, "item_find", "Cơ hội rơi ngọc|Gem drop chance", StatFormat.Percent,
              "Nhân thêm % vào tỉ lệ quái rơi ngọc.|Multiplies the monster gem drop chance."),
            S(PlayerStat.MaxMp, "max_mp", "Mana tối đa|Max MP", StatFormat.Flat,
              "Cộng thẳng vào mana tối đa.|Added to maximum MP."),
            S(PlayerStat.RestoreHp, "restore_hp", "Hồi máu khi dùng|Restores HP", StatFormat.Flat,
              "Máu hồi ngay khi uống.|HP restored on use."),
            S(PlayerStat.RestoreMp, "restore_mp", "Hồi mana khi dùng|Restores MP", StatFormat.Flat,
              "Mana hồi ngay khi uống.|MP restored on use."),
            S(PlayerStat.AttackDamage, "attack_damage", "Sát thương đòn|Attack damage", StatFormat.Percent,
              "Sát thương gốc của đòn đánh thường, tính theo % sát thương cơ bản. Ghi ở item vũ khí.|"
              + "Base damage of the basic attack, as % of base damage. Set on the weapon item."),
            S(PlayerStat.AttackCooldown, "attack_cooldown", "Hồi đòn|Attack cooldown", StatFormat.Seconds,
              "Giây hồi gốc của đòn đánh thường. Ghi ở item vũ khí.|Base cooldown of the basic attack. Set on the weapon item."),
            S(PlayerStat.AttackRange, "attack_range", "Tầm đòn|Attack range", StatFormat.Cells,
              "Tầm đòn đánh thường. Item vũ khí ghi gốc, nút nâng cấp cộng thêm.|"
              + "Basic attack range. The weapon item sets the base, upgrades add to it."),
            S(PlayerStat.AttackTargets, "attack_targets", "Mục tiêu|Targets", StatFormat.Flat,
              "Số mục tiêu hoặc số kẻ đạn xuyên. Item vũ khí không ghi thì trúng mọi kẻ trong tầm.|"
              + "Targets hit or pierced. A weapon item without this line hits everyone in range."),
            S(PlayerStat.WeaponDamage, "weapon_damage", "Sát thương vũ khí|Weapon damage", StatFormat.Percent,
              "Nút nâng cấp vũ khí: tăng % sát thương mọi đòn.|Weapon upgrade: +% damage of every hit."),
            S(PlayerStat.Projectiles, "projectiles", "Số đạn|Projectiles", StatFormat.Flat,
              "Nút nâng cấp vũ khí: thêm đạn mỗi phát.|Weapon upgrade: extra projectiles per shot."),
            S(PlayerStat.ExplosionRadius, "explosion_radius", "Bán kính nổ|Blast radius", StatFormat.Cells,
              "Nút nâng cấp vũ khí: đòn trúng thì nổ trong bán kính này.|Weapon upgrade: hits explode within this radius."),
            S(PlayerStat.ExplosionDamage, "explosion_damage", "Sát thương nổ|Blast damage", StatFormat.Percent,
              "Nút nâng cấp vũ khí: tăng % sát thương vụ nổ.|Weapon upgrade: +% blast damage."),
            S(PlayerStat.Arc, "arc", "Góc chém|Swing arc", StatFormat.Degrees,
              "Nút nâng cấp vũ khí: tăng nửa góc chém.|Weapon upgrade: widens half of the swing arc."),
        };

        [MenuItem("Tools/Core/Player/Build Option Database")]
        public static void BuildMenu()
        {
            OptionDatabase db = Build();
            Selection.activeObject = db;
            Validate();
        }

        /// <summary>Tạo / bổ sung bảng rồi gán vào GameController của scene đang mở (nếu ô còn trống).</summary>
        public static OptionDatabase Build()
        {
            var db = AssetDatabase.LoadAssetAtPath<OptionDatabase>(DataPath);
            if (db == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DataPath));
                db = ScriptableObject.CreateInstance<OptionDatabase>();
                AssetDatabase.CreateAsset(db, DataPath);
            }

            db.schemaVersion = OptionDatabase.CurrentSchema;
            int added = 0;
            for (int i = 0; i < Seeds.Length; i++)
            {
                Seed s = Seeds[i];
                if (db.Get(s.stat) != null) continue;
                db.options.Add(new OptionTemplate
                {
                    id = (int)s.stat, name = s.name, displayName = s.displayName, description = s.description,
                    order = (i + 1) * 10, format = s.format, capKind = s.capKind, cap = s.cap,
                });
                db.ClearCache();
                added++;
            }

            db.ClearCache();
            EditorUtility.SetDirty(db);
            AssignToScene(db);
            AssetDatabase.SaveAssets();
            Debug.Log("OptionDatabase: thêm " + added + " option, tổng " + db.options.Count + ".", db);
            return db;
        }

        /// <summary>
        /// Đối chiếu toàn bộ: bảng option với enum PlayerStat (thiếu / thừa / trùng id, thiếu tên, trần sai), rồi mọi chỗ cộng
        /// chỉ số (nút và Chuyên tinh của cây nhân vật, option của vật phẩm) xem có trỏ vào option không có trong bảng hay
        /// giá trị bằng 0 không. In một báo cáo; lỗi thì LogWarning.
        /// </summary>
        [MenuItem("Tools/Core/Player/Validate Options")]
        public static void Validate()
        {
            var db = AssetDatabase.LoadAssetAtPath<OptionDatabase>(DataPath);
            var report = new StringBuilder();
            int problems = 0;

            void Problem(string line)
            {
                report.Append("  ✗ ").Append(line).Append('\n');
                problems++;
            }

            if (db == null)
            {
                Debug.LogWarning("Validate Options: chưa có " + DataPath + ", chạy Tools/Core/Player/Build Option Database trước.");
                return;
            }

            // ---- bảng với enum ----
            var seen = new HashSet<int>();
            foreach (OptionTemplate o in db.options)
            {
                if (!seen.Add(o.id)) Problem("option id " + o.id + " bị trùng.");
                if (o.id < 0 || o.id > byte.MaxValue || !System.Enum.IsDefined(typeof(PlayerStat), (byte)o.id))
                    Problem("option id " + o.id + " (" + o.name + ") không ứng với PlayerStat nào: không có tác dụng trong game.");
                if (string.IsNullOrEmpty(o.name)) Problem("option id " + o.id + " thiếu tên khóa.");
                if (string.IsNullOrEmpty(o.displayName)) Problem("option " + o.name + " thiếu tên hiển thị.");
                if (o.capKind != StatCap.None && o.cap <= 0f) Problem("option " + o.name + " có capKind " + o.capKind + " mà cap <= 0.");
            }
            foreach (PlayerStat s in (PlayerStat[])System.Enum.GetValues(typeof(PlayerStat)))
                if (db.Get(s) == null) Problem("PlayerStat." + s + " chưa có option trong bảng (UI sẽ hiện tên khóa).");

            // ---- cây nâng cấp nhân vật ----
            int treeRefs = 0;
            var tree = AssetDatabase.LoadAssetAtPath<PlayerUpgradeTree>(TreePath);
            if (tree != null)
            {
                foreach (PlayerUpgradeNode n in tree.nodes)
                    treeRefs += Check(db, n.effects, "cây/" + n.name, Problem);
                if (tree.masteries != null)
                    foreach (BranchMastery m in tree.masteries)
                        treeRefs += Check(db, m.effects, "Chuyên tinh hướng " + m.branch + " (" + m.points + " điểm)", Problem);
            }
            else report.Append("  - chưa có ").Append(TreePath).Append(", bỏ qua phần cây.\n");

            // ---- vật phẩm ----
            int itemRefs = 0;
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ChestTools.ItemDatabasePath);
            if (items != null)
            {
                foreach (ItemTemplate it in items.items)
                {
                    if (it.options == null || it.options.Count == 0) continue;
                    itemRefs += Check(db, it.options, "vật phẩm/" + it.name, Problem);
                    if (!it.Equippable && !it.Usable && it.kind != ItemKind.Jewel)
                        Problem("vật phẩm/" + it.name + " có option mà không mặc, dùng hay khảm được: option không bao giờ được tính.");
                }
            }

            // ---- vũ khí: nút nâng cấp, chỉ số gốc ở item, mỗi đòn đánh thường đúng một vũ khí ----
            int weaponRefs = 0;
            var weapons = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(WeaponPath);
            if (weapons != null)
            {
                foreach (WeaponTemplate w in weapons.weapons)
                {
                    foreach (WeaponUpgrade u in w.upgrades)
                        weaponRefs += Check(db, u.effects, "vũ khí/" + w.name + "/" + u.name, Problem);
                    ItemTemplate it = items != null ? items.FindWeapon(w.id) : null;
                    if (it == null) Problem("vũ khí/" + w.name + " chưa có vật phẩm.");
                    else
                        foreach (PlayerStat need in WeaponNeeds)
                            if (it.OptionValue(need) <= 0f) Problem("vật phẩm/" + it.name + " thiếu option " + need + ".");
                }
                var skills = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillPath);
                if (skills != null)
                    foreach (SkillTemplate t in skills.skills)
                    {
                        if (t.kind != SkillKind.Basic) continue;
                        int owners = weapons.weapons.FindAll(w => w.basicSkill == t.id).Count;
                        if (owners != 1) Problem("đòn đánh thường " + t.name + " thuộc " + owners + " vũ khí, phải đúng 1 (chỉ số gốc lấy từ item vũ khí).");
                    }
            }

            string head = "Validate Options: " + db.options.Count + " option, " + treeRefs + " lượt dùng trên cây, "
                          + weaponRefs + " lượt dùng trên nút vũ khí, " + itemRefs + " lượt dùng trên vật phẩm. ";
            if (problems == 0) Debug.Log(head + "Không có lỗi.\n" + report, db);
            else Debug.LogWarning(head + problems + " lỗi:\n" + report, db);
        }

        static int Check(OptionDatabase db, List<PlayerStatEffect> effects, string where, System.Action<string> problem)
        {
            if (effects == null) return 0;
            foreach (PlayerStatEffect e in effects)
            {
                if (db.Get(e.stat) == null) problem(where + ": chỉ số " + e.stat + " không có trong bảng option.");
                if (e.value == 0f) problem(where + ": " + e.stat + " cộng 0.");
            }
            return effects.Count;
        }

        /// <summary>Gán bảng vào LocalPlaySettings nếu ô còn trống (Game: thay cho GameController 2D), để OptionDatabase.Current có giá trị.</summary>
        static void AssignToScene(OptionDatabase db) => CoreHostAsset.Bind("options", db, onlyIfEmpty: true);
    }
}
