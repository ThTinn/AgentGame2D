using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Anim;
using Game.Map.Editor;
using Game.Skills;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Core/Skills/Import Game Skills: đưa 17 skill của Game (bảng thử <c>Skill_Template.LoadData()</c>, mỗi skill một
    /// prefab hiệu ứng <c>SkillPref/Skill_&lt;idModel&gt;_...</c>) vào SkillDatabase làm skill ô 1-4, thay 6 skill ô chép từ GAME_2D.
    /// Đòn đánh thường theo vũ khí (kind Basic) giữ nguyên.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Lấy từ bảng của Game: hiệu ứng (<see cref="SkillTemplate.model3D"/> = idModel), loại (Bullet_Nor → Projectile, Melee,
    /// Cast → Area, Cast2 → Area tầm 0 = tại chỗ), tầm / bán kính (rangeSkill / rangeDame, mét → ô theo
    /// <see cref="CellSize"/>; cận chiến đánh trong rangeDame). Tên theo tên đang dùng cho hiệu ứng (Skill_Template.ModelNames).</item>
    /// <item>Bảng Game không có (hồi chiêu đều 5 giây, không % sát thương / mana / mô tả / tư thế): đặt theo loại skill, 5 cấp.</item>
    /// <item>Id: skill GAME_2D cũ ở ô nào thì skill Game dùng đúng hiệu ứng nó đang mượn lấy lại id đó (1 Chém kiếm → Chém điện,
    /// 2 Ném bom → Nổ năng lượng, 3 Đập búa → Chém đá, 4 Quét lưỡi hái → Chém băng, 5 Lướt → Lướt chém, 8 Mũi tên xuyên → Pha lê
    /// lục), để PlayerData / file lưu / hồ sơ server giả lập đang gắn id cũ vẫn ra skill cùng hình đánh. Skill còn lại: id 13-23.</item>
    /// </list>
    /// Chạy lại được (ghi đè theo id). Chạy xong thì chạy Build Skill Icons để vẽ icon cho tên mới.
    /// </remarks>
    public static class GameSkillSetup
    {
        const string SkillPath = MapPaths.Root + "/Skills/SkillDatabase.asset";
        const float CellSize = 2f;   // LocalPlaySettings.game2DCellSize: mét mỗi ô
        const int Levels = 5;

        // tư thế tung (AnimVariant.id trong hành động Attack / Dash của Data/Anim/AnimData)
        const int SwordA = 5, SwordB = 6, SwordC = 7, SwordD = 8;
        const int Magic1hA = 9, Magic1hB = 10, Magic1hC = 11;
        const int Magic2hA = 12, Magic2hB = 13, Magic2hC = 14, Magic2hD = 15, Magic2hE = 16;
        const int MagicAreaA = 17, MagicAreaB = 18, Cast1h = 19, Cast2h = 20;

        struct Def
        {
            public int id, model;
            public string name, display, description;
            public SkillType type;
            public AnimSlot animSlot;
            public int idAnim;
            public float hitDelay;
            public bool canMove;
            public float rangeM, radiusM;   // mét, như bảng Game (cận chiến: rangeM = rangeDame, tầm đánh hình quạt)
            public float speed;             // ô / giây (đạn)
            public int maxTarget;
            public float cooldown;
            public int damage, mana, levelRequire;
            public bool pierceGrows;        // mỗi cấp xuyên thêm một mục tiêu
        }

        static Def Shot(int id, int model, string name, string display, string desc, int anim, float rangeM, float radiusM,
            float speed, int maxTarget, float cd, int dmg, int mana, int req) => new Def
        {
            id = id, model = model, name = name, display = display, description = desc, type = SkillType.Projectile,
            animSlot = AnimSlot.Attack, idAnim = anim, hitDelay = 0.25f, canMove = true, rangeM = rangeM, radiusM = radiusM,
            speed = speed, maxTarget = maxTarget, cooldown = cd, damage = dmg, mana = mana, levelRequire = req,
        };

        static Def Slash(int id, int model, string name, string display, string desc, int anim, float rangeM, int maxTarget,
            float cd, int dmg, int mana, int req) => new Def
        {
            id = id, model = model, name = name, display = display, description = desc, type = SkillType.Melee,
            animSlot = AnimSlot.Attack, idAnim = anim, hitDelay = 0.33f, rangeM = rangeM, maxTarget = maxTarget,
            cooldown = cd, damage = dmg, mana = mana, levelRequire = req,
        };

        // rangeM 0 = phép tại chỗ (Cast2): nổ quanh người, chạm là tung
        static Def Zone(int id, int model, string name, string display, string desc, int anim, float rangeM, float radiusM,
            float cd, int dmg, int mana, int req) => new Def
        {
            id = id, model = model, name = name, display = display, description = desc, type = SkillType.Area,
            animSlot = AnimSlot.Attack, idAnim = anim, hitDelay = 0.4f, rangeM = rangeM, radiusM = radiusM,
            cooldown = cd, damage = dmg, mana = mana, levelRequire = req,
        };

        // theo idModel 0-16; tầm / bán kính (mét) chép từ Skill_Template.LoadData()
        static readonly Def[] Defs =
        {
            Shot(13, 0, "energy_bolt", "Đạn năng lượng", "Bắn một viên đạn năng lượng bay thẳng, trúng kẻ địch đầu tiên trên đường.",
                 Magic1hA, 8f, 2f, 14f, 1, 2f, 130, 6, 1),
            new Def
            {
                id = 8, model = 1, name = "green_crystal", display = "Pha lê lục", type = SkillType.Projectile,
                description = "Phóng mũi pha lê xanh xuyên qua nhiều kẻ địch trên một đường thẳng.",
                animSlot = AnimSlot.Attack, idAnim = Magic1hB, hitDelay = 0.25f, canMove = true, rangeM = 8f, radiusM = 2f,
                speed = 12f, maxTarget = 2, cooldown = 4f, damage = 150, mana = 10, levelRequire = 4, pierceGrows = true,
            },
            Shot(14, 2, "red_crystal", "Pha lê đỏ", "Phóng khối pha lê đỏ nặng, gây sát thương lớn cho kẻ địch đầu tiên trúng phải.",
                 Magic1hC, 8f, 2f, 10f, 1, 5f, 220, 14, 8),
            Slash(1, 3, "electro_slash", "Chém điện", "Chém một nhát tích điện về phía trước, trúng tối đa ba kẻ địch.",
                  SwordA, 2f, 3, 2f, 120, 6, 1),
            Slash(4, 4, "frost_slash", "Chém băng", "Vung lưỡi băng rộng phía trước, trúng mọi kẻ địch trong tầm.",
                  SwordB, 2f, 0, 3f, 110, 9, 3),
            Slash(3, 5, "stone_slash", "Chém đá", "Bổ một nhát nặng như đá, dồn sát thương lớn vào một mục tiêu.",
                  SwordC, 2f, 1, 5f, 200, 12, 5),
            Zone(15, 6, "snowstorm", "Bão tuyết", "Gọi bão tuyết xuống vùng nhắm, gây sát thương mọi kẻ địch trong vùng.",
                 MagicAreaA, 10f, 2f, 6f, 150, 14, 6),
            Zone(16, 7, "meteor_shower", "Mưa thiên thạch", "Gọi mưa thiên thạch trút xuống vùng nhắm, sát thương rất lớn.",
                 MagicAreaB, 10f, 2f, 8f, 200, 18, 10),
            Zone(17, 8, "laser", "Tia laser", "Chiếu tia laser xuống điểm nhắm, đốt kẻ địch đứng trong tia.",
                 Magic2hA, 10f, 2f, 5f, 170, 14, 9),
            Zone(18, 9, "freeze", "Đóng băng", "Đóng băng một vùng tại điểm nhắm, gây sát thương kẻ địch trong vùng.",
                 Magic2hB, 10f, 2f, 6f, 140, 12, 7),
            // hiệu ứng Skill_10_AOE_Healing; skill chưa có hồi máu nên là phép vùng gây sát thương như bảng Game
            Zone(19, 10, "holy_light", "Vầng sáng", "Vầng sáng bùng lên tại điểm nhắm, gây sát thương kẻ địch trong vùng.",
                 Cast1h, 10f, 2f, 6f, 130, 12, 5),
            Zone(2, 11, "energy_blast", "Nổ năng lượng", "Kích nổ một khối năng lượng tại điểm nhắm, sát thương lan ra xung quanh.",
                 Magic2hC, 10f, 2f, 6f, 150, 14, 3),
            // bảng Game: Bullet_Nor (vệt chém bay), không phải lướt người
            new Def
            {
                id = 5, model = 12, name = "charge_slash", display = "Lướt chém", type = SkillType.Projectile,
                description = "Phóng một vệt chém bay thẳng theo hướng nhắm, xuyên qua mọi kẻ địch trên đường.",
                animSlot = AnimSlot.Attack, idAnim = SwordD, hitDelay = 0.3f, rangeM = 10f, radiusM = 2f, speed = 16f,
                cooldown = 3f, damage = 110, mana = 8, levelRequire = 2,
            },
            Zone(20, 13, "blast_zone", "Vùng nổ", "Tạo vùng nổ lớn tại điểm nhắm, trúng mọi kẻ địch trong vùng rộng.",
                 Cast2h, 10f, 4f, 8f, 170, 18, 12),
            Shot(21, 14, "stone_throw", "Ném đá", "Ném tảng đá lớn bay thẳng, đè lên mọi kẻ địch trên đường rơi.",
                 Magic2hD, 10f, 4f, 10f, 0, 6f, 180, 16, 11),
            Zone(22, 15, "bullet_hell", "Mưa đạn", "Xả mưa đạn quanh người, gây sát thương mọi kẻ địch đứng gần.",
                 Magic2hE, 0f, 3f, 10f, 200, 20, 15),
            Zone(23, 16, "frost_nova", "Băng giá", "Bùng phát băng giá rộng tại điểm nhắm, trúng mọi kẻ địch trong vùng.",
                 MagicAreaA, 10f, 4f, 9f, 190, 18, 14),
        };

        [MenuItem("Tools/Core/Skills/Import Game Skills")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillPath);
            if (db == null) { Debug.LogError("GameSkillSetup: không thấy " + SkillPath); return; }

            var ids = new HashSet<int>();
            foreach (Def d in Defs) ids.Add(d.id);

            var removed = new List<string>();
            var active = new List<SkillTemplate>();
            var basics = new List<SkillTemplate>();
            foreach (SkillTemplate t in db.skills)
            {
                if (t.kind == SkillKind.Basic) basics.Add(t);
                else if (!ids.Contains(t.id)) removed.Add(t.id + " " + t.name);   // skill ô của GAME_2D không có bản Game
            }
            foreach (Def d in Defs) active.Add(Build(d, db.Get(d.id)));

            // skill ô theo thứ tự hiệu ứng của Game (lưới bảng Kỹ năng hiện theo thứ tự này), đánh thường sau cùng
            db.skills.Clear();
            db.skills.AddRange(active);
            db.skills.AddRange(basics);
            db.ClearCache();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            Debug.Log($"GameSkillSetup: {active.Count} skill của Game vào {SkillPath}, giữ {basics.Count} đòn đánh thường"
                      + (removed.Count > 0 ? ", bỏ: " + string.Join(", ", removed) : "") + ". Chạy Build Skill Icons để vẽ icon.", db);
        }

        // giây tới khung vung tay nhanh nhất của clip tung (đo bằng tốc độ bàn tay trên clip thật): mốc hiệu ứng ra và gây sát thương.
        // Đổi tư thế của skill thì đo lại.
        static readonly Dictionary<string, float> SwingAt = new Dictionary<string, float>
        {
            ["energy_bolt"] = 0.8f, ["green_crystal"] = 0.5f, ["red_crystal"] = 0.8f,
            ["electro_slash"] = 1.23f, ["frost_slash"] = 0.6f, ["stone_slash"] = 0.77f, ["charge_slash"] = 0.43f,
            ["snowstorm"] = 1.27f, ["meteor_shower"] = 1.8f, ["laser"] = 1.13f, ["freeze"] = 1.3f, ["holy_light"] = 0.6f,
            ["energy_blast"] = 1f, ["blast_zone"] = 0.63f, ["stone_throw"] = 0.93f, ["bullet_hell"] = 2.03f, ["frost_nova"] = 1.27f,
        };

        // ghi đè skill cùng id (giữ icon cũ nếu có, Build Skill Icons sẽ vẽ lại theo tên; âm thanh cũng giữ, Generate Skill SFX
        // gán theo tên)
        static SkillTemplate Build(Def d, SkillTemplate old)
        {
            bool same = old != null && old.name == d.name;
            var t = new SkillTemplate
            {
                id = d.id,
                name = d.name,
                displayName = d.display,
                description = d.description,
                kind = SkillKind.Active,
                type = d.type,
                animSlot = d.animSlot,
                idAnim = d.idAnim,
                hitDelay = SwingAt.TryGetValue(d.name, out float swing) ? swing : d.hitDelay,
                canMoveWhileCasting = d.canMove,
                idIcon = same ? old.idIcon : 0,
                model3D = d.model,
                castSfx = same ? old.castSfx : 0,
                fireSfx = same ? old.fireSfx : 0,
                hitSfx = same ? old.hitSfx : 0,
            };
            float range = d.rangeM / CellSize, radius = d.radiusM / CellSize;
            int step = Mathf.Max(5, Mathf.RoundToInt(d.damage * 0.1f / 5f) * 5);   // +~10% sát thương mỗi cấp, chẵn 5

            for (int i = 0; i < Levels; i++)
            {
                int target = d.pierceGrows ? d.maxTarget + i : d.maxTarget;
                t.levels.Add(new SkillInfo
                {
                    damage = d.damage + step * i,
                    manaUse = d.mana + i,
                    cooldown = d.cooldown,
                    range = range,
                    radius = radius,
                    speed = d.speed,
                    maxTarget = target,
                    levelRequire = d.levelRequire + i * 3,
                    moreInfo = d.pierceGrows ? "Xuyên " + target + " mục tiêu" : "",
                });
            }
            t.Renumber();
            return t;
        }
    }
}
