using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Enemies;
using Game.Items;
using Game.Map.Editor;

namespace Game.EditorTools
{
    /// <summary>
    /// Bảng rơi đồ (Assets/Data/Items/DropDatabase.asset):
    /// <list type="number">
    /// <item>tạo asset nếu chưa có, thêm bảng mẫu cho mọi loại quái (MonsterDatabase), 4 kiểu rương và bảng chung của quái
    /// (<see cref="DropDatabase.MonsterDefaultTable"/>) còn thiếu bảng (bảng đã có thì giữ nguyên);</item>
    /// <item>gán vào GameController của scene đang mở;</item>
    /// <item>xuất drops.bin cho server (cùng items.bin, options.bin...).</item>
    /// </list>
    /// Sau đó game design sửa ở sheet "Rơi đồ" của Docs/Items_Data.xlsx (Export Items Excel để có sheet) rồi Build Data From Excel.
    /// </summary>
    public static class DropTools
    {
        public const string DatabasePath = MapPaths.Items + "/DropDatabase.asset";

        // (vật phẩm, tỉ lệ %, min, max). Rương: dòng đầu 100% để mở rương không bao giờ trống.
        static readonly Dictionary<string, (string item, float chance, int min, int max)[]> Defaults =
            new Dictionary<string, (string, float, int, int)[]>
            {
                ["slime_green"] = new[]
                {
                    ("gem_green", 10f, 1, 1), ("gem_blue", 5f, 1, 1), ("key_small", 3f, 1, 1),
                    ("armor_head", 2f, 1, 1), ("armor_feet", 2f, 1, 1), ("weapon_sword", 1f, 1, 1),
                },
                ["slime_fire"] = new[]
                {
                    ("gem_red", 12f, 1, 2), ("gem_orange", 8f, 1, 1), ("key_small", 4f, 1, 1), ("key_iron", 2f, 1, 1),
                    ("armor_body", 2.5f, 1, 1), ("armor_hands", 1.5f, 1, 1), ("weapon_hammer", 1f, 1, 1), ("armor_ring", 0.5f, 1, 1),
                },
                [DropDatabase.ChestTable(ChestKind.Small)] = new[]
                {
                    ("gem_blue", 100f, 1, 2), ("gem_green", 60f, 1, 2), ("gem_yellow", 40f, 1, 1), ("key_iron", 10f, 1, 1),
                    ("armor_head", 10f, 1, 1), ("armor_legs", 10f, 1, 1), ("armor_feet", 10f, 1, 1),
                },
                [DropDatabase.ChestTable(ChestKind.Iron)] = new[]
                {
                    ("gem_red", 100f, 1, 2), ("gem_orange", 60f, 1, 2), ("gem_purple", 40f, 1, 1), ("key_gold", 10f, 1, 1),
                    ("armor_body", 15f, 1, 1), ("armor_hands", 12f, 1, 1), ("weapon_hammer", 6f, 1, 1), ("weapon_bow", 6f, 1, 1),
                },
                [DropDatabase.ChestTable(ChestKind.Gold)] = new[]
                {
                    ("gem_purple", 100f, 2, 3), ("gem_yellow", 80f, 2, 3), ("key_large", 8f, 1, 1), ("armor_neck", 15f, 1, 1),
                    ("armor_ring", 15f, 1, 1), ("weapon_scythe", 8f, 1, 1), ("weapon_gun", 8f, 1, 1), ("skin_b", 4f, 1, 1), ("skin_c", 4f, 1, 1),
                },
                [DropDatabase.ChestTable(ChestKind.Large)] = new[]
                {
                    ("gem_red", 100f, 2, 4), ("gem_blue", 100f, 2, 4), ("gem_purple", 60f, 1, 3), ("armor_neck", 25f, 1, 1),
                    ("armor_ring", 25f, 1, 1), ("weapon_rifle", 10f, 1, 1), ("skin_d", 6f, 1, 1), ("skin_e", 3f, 1, 1),
                },
            };

        // quái 3D của Game (MonsterSOData, không có trong MonsterDatabase): bảng theo id mẫu (DropDatabase.MonsterIdTable).
        // Bình / ngọc như bảng chung nhưng hay rơi hơn, thêm ít trang bị theo độ khó: Slime (cấp 2) đồ nhẹ, Rùa gai (cấp 10)
        // giáp nặng + búa / cung, Beholder (phép) dây chuyền / nhẫn + lưỡi hái / súng.
        static readonly (int id, string label, (string item, float chance, int min, int max)[] rows)[] Monsters3D =
        {
            (1, "Slime_Poly", new[]
            {
                ("potion_hp_small", 15f, 1, 2), ("potion_mp_small", 15f, 1, 2), ("gem_green", 3f, 1, 1), ("gem_blue", 2f, 1, 1),
                ("key_small", 2f, 1, 1), ("armor_head", 2f, 1, 1), ("armor_feet", 2f, 1, 1), ("armor_hands", 1.5f, 1, 1),
                ("weapon_sword", 1f, 1, 1),
            }),
            (2, "TurtleShell_Poly", new[]
            {
                ("potion_hp_small", 10f, 1, 2), ("potion_hp_medium", 10f, 1, 1), ("potion_mp_medium", 8f, 1, 1),
                ("gem_blue", 4f, 1, 1), ("gem_yellow", 3f, 1, 1), ("key_iron", 2f, 1, 1), ("buff_power", 3f, 1, 1),
                ("armor_body", 3f, 1, 1), ("armor_legs", 2.5f, 1, 1), ("armor_head", 2f, 1, 1),
                ("weapon_hammer", 1.5f, 1, 1), ("weapon_bow", 1f, 1, 1),
            }),
            (5, "Beholder_Poly", new[]
            {
                ("potion_hp_small", 10f, 1, 1), ("potion_mp_small", 15f, 1, 2), ("potion_mp_medium", 8f, 1, 1),
                ("gem_purple", 4f, 1, 1), ("gem_red", 3f, 1, 1), ("key_small", 3f, 1, 1),
                ("buff_attack_speed", 3f, 1, 1), ("buff_move_speed", 2f, 1, 1),
                ("armor_neck", 2f, 1, 1), ("armor_ring", 2f, 1, 1), ("armor_hands", 2f, 1, 1),
                ("weapon_scythe", 1f, 1, 1), ("weapon_gun", 1f, 1, 1),
            }),
        };

        /// <summary>Tên mẫu quái 3D có bảng mẫu (cho cột Nguồn của sheet Rơi đồ); null khi không có trong <see cref="Monsters3D"/>.</summary>
        public static string Monster3DLabel(int id)
        {
            foreach (var m in Monsters3D) if (m.id == id) return m.label;
            return null;
        }

        // quái mới chưa có bảng mẫu riêng: ít ngọc, hiếm khi có chìa
        static readonly (string item, float chance, int min, int max)[] GenericMonster =
        {
            ("gem_blue", 2f, 1, 1), ("gem_green", 2f, 1, 1), ("gem_red", 2f, 1, 1), ("key_small", 1f, 1, 1),
        };

        // bảng chung (DropDatabase.MonsterDefaultTable) của quái chưa có bảng riêng, vd quái 3D lúc chơi local:
        // chút ngọc, bình máu / mana (bình nhỏ hay rơi, bình lớn hiếm), thuốc tăng chỉ số
        internal static readonly (string item, float chance, int min, int max)[] MonsterDefault =
        {
            ("gem_blue", 1f, 1, 1), ("gem_green", 1f, 1, 1), ("gem_red", 1f, 1, 1), ("gem_yellow", 1f, 1, 1),
            ("potion_hp_small", 12f, 1, 2), ("potion_hp_medium", 4f, 1, 1), ("potion_hp_large", 1f, 1, 1),
            ("potion_mp_small", 15f, 1, 2), ("potion_mp_medium", 5f, 1, 1), ("potion_mp_large", 1.2f, 1, 1),
            ("buff_attack_speed", 2f, 1, 1), ("buff_move_speed", 2f, 1, 1), ("buff_power", 1.5f, 1, 1),
        };

        public static DropDatabase EnsureDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<DropDatabase>(DatabasePath);
            if (db != null) return db;
            System.IO.Directory.CreateDirectory(MapPaths.Items);
            db = ScriptableObject.CreateInstance<DropDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
            return db;
        }

        [MenuItem("Tools/Core/Items/Build Drop Tables")]
        public static void Run()
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ChestTools.ItemDatabasePath);
            if (items == null)
            {
                Debug.LogError("DropTools: chưa có " + ChestTools.ItemDatabasePath + ".");
                return;
            }
            var monsters = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterTools.DatabasePath);
            DropDatabase db = EnsureDatabase();
            Undo.RecordObject(db, "Build drop tables");

            var added = new List<string>();
            void Add(string table, (string item, float chance, int min, int max)[] rows)
            {
                if (db.Find(table) != null) return;
                var t = new DropTable { name = table };
                foreach (var r in rows)
                {
                    if (items.Find(r.item) == null) { Debug.LogWarning("DropTools: bỏ " + table + "/" + r.item + " (không có trong ItemDatabase)."); continue; }
                    t.entries.Add(new DropEntry { item = r.item, chance = r.chance, min = r.min, max = r.max });
                }
                db.tables.Add(t);
                added.Add(table);
            }

            if (monsters != null)
                foreach (MonsterTemplate m in monsters.monsters)
                    if (m != null && !string.IsNullOrEmpty(m.name))
                        Add(m.name, Defaults.TryGetValue(m.name, out var rows) ? rows : GenericMonster);
            foreach (var m in Monsters3D) Add(DropDatabase.MonsterIdTable(m.id), m.rows);
            foreach (ChestKind kind in new[] { ChestKind.Small, ChestKind.Iron, ChestKind.Gold, ChestKind.Large })
                Add(DropDatabase.ChestTable(kind), Defaults[DropDatabase.ChestTable(kind)]);
            Add(DropDatabase.MonsterDefaultTable, MonsterDefault);

            db.schemaVersion = DropDatabase.CurrentSchema;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();

            List<string> problems = db.Validate(name => items.Find(name) != null);
            foreach (string p in problems) Debug.LogWarning("DropDatabase: " + p, db);

            BindGameController(db);
            ItemsExcelImport.ExportServerData();
            Debug.Log($"DropTools: {db.tables.Count} bảng rơi đồ" + (added.Count > 0 ? ", thêm: " + string.Join(", ", added) : ", không thêm bảng nào")
                      + ". Chạy Tools/Core/Items/Export Items Excel để có sheet Rơi đồ trong Items_Data.xlsx.", db);
        }

        /// <summary>Gán bảng vào trường drops của LocalPlaySettings (Game: thay cho GameController 2D).</summary>
        static void BindGameController(DropDatabase db) => CoreHostAsset.Bind("drops", db, onlyIfEmpty: false);
    }
}
