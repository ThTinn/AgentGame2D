using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Items;
using Game.Map;
using Game.Map.Editor;
using Game.Players;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Core/Items/Build Potion Items: dựng nhóm vật phẩm dùng được.
    /// <list type="number">
    /// <item>vẽ icon 128x128 (bình tròn cho máu / mana ba cỡ, chai vuông có ký hiệu cho thuốc tăng chỉ số) vào
    /// Data/Sprites/ItemIcons, nhập làm Sprite và thêm vào SpriteData (loại Icon);</item>
    /// <item>thêm / cập nhật vật phẩm trong ItemDatabase (giữ id nếu đã có, theo name);</item>
    /// <item>bảng rơi đồ: thêm bảng chung của quái (<see cref="DropDatabase.MonsterDefaultTable"/>) nếu chưa có, chêm bình
    /// vào bảng quái / rương đã có (dòng đã có thì giữ nguyên);</item>
    /// <item>PlayerData: thêm ít bình vào đồ khởi đầu, gán sẵn hai ô dùng nhanh nếu đang trống.</item>
    /// </list>
    /// Chạy lại bao nhiêu lần cũng được. Sửa số (lượng hồi, giây, chỉ số) về sau thì qua Excel (Export / Build Data From Excel).
    /// </summary>
    public static class PotionItemSetup
    {
        const string IconFolder = MapPaths.Sprites + "/ItemIcons";
        const string PlayerDataPath = MapPaths.Root + "/Player/PlayerData.asset";

        enum Shape { Round, Square }
        enum Symbol { None, Bolt, Wind, Star }

        sealed class Def
        {
            public string name, display, description;
            public ItemKind kind;
            public ItemRarity rarity;
            public int value, maxStack;
            public PlayerStatEffect[] options = Array.Empty<PlayerStatEffect>();
            // icon
            public Shape shape;
            public float size;      // 0..1: bình nhỏ / vừa / lớn
            public Color liquid;
            public Symbol symbol;
        }

        static readonly Color Red = new Color(0.91f, 0.2f, 0.24f), Blue = new Color(0.2f, 0.48f, 1f);
        static readonly Color Orange = new Color(1f, 0.62f, 0.12f), Green = new Color(0.24f, 0.84f, 0.44f), Purple = new Color(0.69f, 0.36f, 1f);

        static readonly Def[] Defs =
        {
            Potion("potion_hp_small", "Bình máu nhỏ", ItemKind.HpPotion, ItemRarity.Common, 50, 0f, Red),
            Potion("potion_hp_medium", "Bình máu vừa", ItemKind.HpPotion, ItemRarity.Uncommon, 150, 0.5f, Red),
            Potion("potion_hp_large", "Bình máu lớn", ItemKind.HpPotion, ItemRarity.Rare, 400, 1f, Red),
            Potion("potion_mp_small", "Bình mana nhỏ", ItemKind.MpPotion, ItemRarity.Common, 30, 0f, Blue),
            Potion("potion_mp_medium", "Bình mana vừa", ItemKind.MpPotion, ItemRarity.Uncommon, 80, 0.5f, Blue),
            Potion("potion_mp_large", "Bình mana lớn", ItemKind.MpPotion, ItemRarity.Rare, 200, 1f, Blue),
            Buff("buff_attack_speed", "Thuốc cuồng nộ", ItemRarity.Rare, 60, PlayerStat.AttackSpeed, 25f, "tốc đánh", Orange, Symbol.Bolt),
            Buff("buff_move_speed", "Thuốc gió", ItemRarity.Uncommon, 60, PlayerStat.MoveSpeed, 25f, "tốc chạy", Green, Symbol.Wind),
            Buff("buff_power", "Thuốc sức mạnh", ItemRarity.Rare, 60, PlayerStat.Damage, 20f, "sát thương", Purple, Symbol.Star),
        };

        static Def Potion(string name, string display, ItemKind kind, ItemRarity rarity, int value, float size, Color liquid) => new Def
        {
            name = name, display = display, kind = kind, rarity = rarity, maxStack = 99,
            options = new[] { new PlayerStatEffect(kind == ItemKind.HpPotion ? PlayerStat.RestoreHp : PlayerStat.RestoreMp, value) },
            description = (kind == ItemKind.HpPotion ? "Uống hồi ngay " + value + " máu." : "Uống hồi ngay " + value + " mana.")
                          + " Gán vào ô dùng nhanh để dùng giữa trận.",
            shape = Shape.Round, size = size, liquid = liquid,
        };

        static Def Buff(string name, string display, ItemRarity rarity, int seconds, PlayerStat stat, float amount, string what,
            Color liquid, Symbol symbol) => new Def
        {
            name = name, display = display, kind = ItemKind.Buff, rarity = rarity, value = seconds, maxStack = 20,
            options = new[] { new PlayerStatEffect(stat, amount) },
            description = "Tăng " + amount + "% " + what + " trong " + seconds + " giây. Uống lại thì tính lại từ đầu.",
            shape = Shape.Square, size = 0.5f, liquid = liquid, symbol = symbol,
        };

        // bình chen vào các bảng đã có: (bảng, vật phẩm, tỉ lệ %, min, max)
        static readonly (string table, string item, float chance, int min, int max)[] ExtraDrops =
        {
            ("slime_green", "potion_hp_small", 10f, 1, 1), ("slime_green", "potion_mp_small", 12f, 1, 1),
            ("slime_fire", "potion_hp_small", 10f, 1, 2), ("slime_fire", "potion_mp_medium", 5f, 1, 1),
            ("slime_fire", "buff_attack_speed", 2f, 1, 1),
            ("chest_small", "potion_hp_small", 50f, 1, 3), ("chest_small", "potion_mp_small", 50f, 1, 3),
            ("chest_iron", "potion_hp_medium", 40f, 1, 2), ("chest_iron", "potion_mp_medium", 40f, 1, 2),
            ("chest_iron", "buff_move_speed", 15f, 1, 1),
            ("chest_gold", "potion_hp_large", 30f, 1, 2), ("chest_gold", "potion_mp_large", 30f, 1, 2),
            ("chest_gold", "buff_attack_speed", 20f, 1, 1), ("chest_gold", "buff_power", 15f, 1, 1),
            ("chest_large", "potion_hp_large", 50f, 2, 3), ("chest_large", "potion_mp_large", 50f, 2, 3),
            ("chest_large", "buff_power", 30f, 1, 2),
        };

        // đồ khởi đầu thêm vào PlayerData (chỉ áp cho bản lưu mới) và hai ô dùng nhanh mặc định
        static readonly (string item, int amount)[] StartItems = { ("potion_hp_small", 5), ("potion_mp_small", 5) };
        static readonly string[] DefaultQuickItems = { "potion_hp_small", "potion_mp_small" };

        [MenuItem("Tools/Core/Items/Build Potion Items")]
        public static void Run()
        {
            ItemDatabase items = ChestTools.EnsureItems();
            SpriteData sprites = items.sprites != null ? items.sprites : MapExport.LoadOrCreateAssets().sprites;
            if (items.sprites == null) items.sprites = sprites;
            MapPaths.EnsureFolder(IconFolder);

            int added = 0;
            foreach (Def d in Defs)
            {
                Sprite icon = WriteIcon(d);
                int iconId = Register(sprites, "icon_" + d.name, icon);
                if (Upsert(items, d, iconId)) added++;
            }
            items.ClearCache();
            EditorUtility.SetDirty(items);
            EditorUtility.SetDirty(sprites);

            int drops = FillDrops(items);
            bool player = FillPlayerData(items);
            AssetDatabase.SaveAssets();
            ItemIconAtlas.Build();   // icon mới vào atlas chung
            ItemsExcelImport.ExportServerData();

            Debug.Log($"PotionItemSetup: {Defs.Length} vật phẩm ({added} mới), thêm {drops} dòng rơi đồ"
                      + (player ? ", cập nhật PlayerData (đồ khởi đầu / ô dùng nhanh)" : "") + ".", items);
        }

        // ---------------- dữ liệu ----------------

        // true khi vật phẩm mới thêm
        static bool Upsert(ItemDatabase items, Def d, int iconId)
        {
            ItemTemplate it = items.Find(d.name);
            bool isNew = it == null;
            if (isNew) it = items.Add(new ItemTemplate { name = d.name });
            it.displayName = d.display;
            it.description = d.description;
            it.kind = d.kind;
            it.rarity = d.rarity;
            it.value = d.value;
            it.maxStack = d.maxStack;
            it.iconId = iconId;
            it.options = new List<PlayerStatEffect>(d.options);
            return isNew;
        }

        internal static int Register(SpriteData sprites, string name, Sprite sprite)
        {
            sprites.ClearCache();
            int id = sprites.FindId(name);
            if (id > 0)
            {
                sprites.Get(id).sprite = sprite;
                sprites.Get(id).type = SpriteType.Icon;
                return id;
            }
            int max = 0;
            foreach (SpriteRecord r in sprites.sprites) max = Mathf.Max(max, r.id);
            sprites.sprites.Add(new SpriteRecord { id = max + 1, name = name, type = SpriteType.Icon, sprite = sprite });
            sprites.ClearCache();
            return max + 1;
        }

        static int FillDrops(ItemDatabase items)
        {
            DropDatabase db = DropTools.EnsureDatabase();
            int added = 0;
            if (db.Find(DropDatabase.MonsterDefaultTable) == null)
            {
                var t = new DropTable { name = DropDatabase.MonsterDefaultTable };
                foreach (var r in DropTools.MonsterDefault)
                    if (items.Find(r.item) != null) t.entries.Add(new DropEntry { item = r.item, chance = r.chance, min = r.min, max = r.max });
                db.tables.Add(t);
                added += t.entries.Count;
            }
            foreach (var r in ExtraDrops)
            {
                DropTable t = db.Find(r.table);
                if (t == null || t.entries.Exists(e => e != null && e.item == r.item)) continue;
                t.entries.Add(new DropEntry { item = r.item, chance = r.chance, min = r.min, max = r.max });
                added++;
            }
            foreach (string p in db.Validate(name => items.Find(name) != null)) Debug.LogWarning("DropDatabase: " + p, db);
            EditorUtility.SetDirty(db);
            CoreHostAsset.Bind("drops", db, onlyIfEmpty: true);
            return added;
        }

        static bool FillPlayerData(ItemDatabase items)
        {
            var data = AssetDatabase.LoadAssetAtPath<PlayerData>(PlayerDataPath);
            if (data == null) return false;
            bool changed = false;
            foreach (var (name, amount) in StartItems)
            {
                ItemTemplate it = items.Find(name);
                if (it == null || data.startItems.Exists(s => s != null && s.itemId == it.id)) continue;
                data.startItems.Add(new PlayerData.StartItem { itemId = it.id, amount = amount });
                changed = true;
            }
            if (data.quickItems == null || data.quickItems.TrueForAll(id => id <= 0))
            {
                data.quickItems = new List<int>();
                foreach (string name in DefaultQuickItems) data.quickItems.Add(items.Find(name)?.id ?? 0);
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(data);
            return changed;
        }

        // ---------------- vẽ icon ----------------

        internal const int N = 128;
        internal static readonly Color Outline = new Color(0.16f, 0.11f, 0.17f);
        static readonly Color Glass = new Color(0.8f, 0.9f, 0.98f, 0.82f);
        static readonly Color Cork = new Color(0.62f, 0.4f, 0.22f);
        static readonly Color CorkDark = new Color(0.45f, 0.27f, 0.14f);
        const float Stroke = 5f;

        static Sprite WriteIcon(Def d)
        {
            var px = new Color[N * N];
            if (d.shape == Shape.Round) DrawRoundFlask(px, d);
            else DrawSquareBottle(px, d);
            return SaveIcon(px, d.name);
        }

        /// <summary>Ghi ảnh N x N vào Data/Sprites/ItemIcons rồi nhập làm Sprite.</summary>
        internal static Sprite SaveIcon(Color[] px, string name)
        {
            MapPaths.EnsureFolder(IconFolder);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = IconFolder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;   // atlas icon (ItemIconAtlas) tự nén
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // bình tròn: thân tròn, cổ, miệng, nút bần; cỡ lớn thì thân to và cổ ngắn lại
        static void DrawRoundFlask(Color[] px, Def d)
        {
            float r = Mathf.Lerp(30f, 42f, d.size);
            var body = new Vector2(64f, 10f + r);
            float neckTop = Mathf.Min(104f, body.y + r + 18f);
            float neckW = Mathf.Lerp(9f, 11f, d.size);
            Func<float, float, float> glass = (x, y) => Mathf.Min(Circle(x, y, body, r),
                Mathf.Min(Box(x, y, 64f, (body.y + neckTop) * 0.5f, neckW, (neckTop - body.y) * 0.5f, 3f),
                          Box(x, y, 64f, neckTop, neckW + 4f, 4f, 3f)));
            float level = body.y + r * 0.35f;
            DrawBottle(px, glass, (x, y) => Mathf.Max(Circle(x, y, body, r - Stroke * 0.5f), y - level), d.liquid,
                       body.y - r, level);
            DrawCork(px, neckTop + 4f, neckW - 1f);
            // ánh sáng trên thân kính
            Paint(px, (x, y) => Ellipse(x, y, body.x - r * 0.45f, body.y + r * 0.3f, r * 0.14f, r * 0.3f), new Color(1f, 1f, 1f, 0.7f));
        }

        // chai vuông (thuốc): thân chữ nhật bo góc, vai, cổ, ký hiệu trên nhãn
        static void DrawSquareBottle(Color[] px, Def d)
        {
            const float cx = 64f, bottom = 10f, top = 84f, half = 27f;
            const float neckW = 10f, neckTop = 102f;
            Func<float, float, float> glass = (x, y) => Mathf.Min(Box(x, y, cx, (bottom + top) * 0.5f, half, (top - bottom) * 0.5f, 14f),
                Mathf.Min(Box(x, y, cx, (top + neckTop) * 0.5f - 4f, neckW, (neckTop - top) * 0.5f + 4f, 3f),
                          Box(x, y, cx, neckTop, neckW + 4f, 4f, 3f)));
            float level = top - 18f;
            DrawBottle(px, glass, (x, y) => Mathf.Max(Box(x, y, cx, (bottom + top) * 0.5f, half - Stroke * 0.5f, (top - bottom) * 0.5f - Stroke * 0.5f, 12f), y - level),
                       d.liquid, bottom, level);
            DrawCork(px, neckTop + 4f, neckW - 1f);
            Paint(px, (x, y) => Box(x, y, cx - half + 9f, 50f, 3.5f, 22f, 3f), new Color(1f, 1f, 1f, 0.6f));

            // nhãn tròn sáng + ký hiệu
            var label = new Vector2(cx + 3f, 40f);
            Paint(px, (x, y) => Circle(x, y, label, 16f) - Stroke * 0.6f, Outline);
            Paint(px, (x, y) => Circle(x, y, label, 16f), new Color(1f, 0.97f, 0.88f));
            Paint(px, SymbolSdf(d.symbol, label), Color.Lerp(d.liquid, Color.black, 0.35f));
        }

        // viền + kính + chất lỏng (dải màu sáng dần lên trên) + vạch sáng mặt chất lỏng
        static void DrawBottle(Color[] px, Func<float, float, float> glass, Func<float, float, float> liquid, Color color,
            float liquidBottom, float level)
        {
            Paint(px, (x, y) => glass(x, y) - Stroke, Outline);
            Paint(px, glass, Glass);
            Color deep = Color.Lerp(color, Color.black, 0.3f), bright = Color.Lerp(color, Color.white, 0.25f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float cov = Coverage(liquid(x + 0.5f, y + 0.5f));
                    if (cov <= 0f) continue;
                    float t = Mathf.InverseLerp(liquidBottom, level, y + 0.5f);
                    Blend(px, x, y, Color.Lerp(deep, bright, t), cov);
                }
            Paint(px, (x, y) => Mathf.Max(liquid(x, y), level - 4f - y), new Color(1f, 1f, 1f, 0.35f));
        }

        static void DrawCork(Color[] px, float centerY, float halfW)
        {
            Paint(px, (x, y) => Box(x, y, 64f, centerY + 4f, halfW, 8f, 3f) - Stroke * 0.8f, Outline);
            Paint(px, (x, y) => Box(x, y, 64f, centerY + 4f, halfW, 8f, 3f), Cork);
            Paint(px, (x, y) => Box(x, y, 64f + halfW * 0.5f, centerY + 4f, halfW * 0.3f, 7f, 2f), CorkDark);
        }

        static Func<float, float, float> SymbolSdf(Symbol s, Vector2 c)
        {
            switch (s)
            {
                case Symbol.Bolt:   // tia sét: tốc đánh
                    return (x, y) => Polygon(x, y, c, new[] { (3f, 12f), (-7f, -1f), (-1f, -1f), (-4f, -12f), (7f, 2f), (1f, 2f) });
                case Symbol.Wind:   // hai mũi tên kép: tốc chạy
                    return (x, y) => Mathf.Min(
                        Polygon(x, y, c, new[] { (-9f, 8f), (-4f, 8f), (3f, 0f), (-4f, -8f), (-9f, -8f), (-2f, 0f) }),
                        Polygon(x, y, c, new[] { (0f, 8f), (5f, 8f), (12f, 0f), (5f, -8f), (0f, -8f), (7f, 0f) }));
                case Symbol.Star:   // ngôi sao: sát thương
                {
                    var pts = new (float, float)[10];
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.PI / 2f + i * Mathf.PI / 5f, rad = i % 2 == 0 ? 12f : 5f;
                        pts[i] = (Mathf.Cos(a) * rad, Mathf.Sin(a) * rad);
                    }
                    return (x, y) => Polygon(x, y, c, pts);
                }
                default: return (x, y) => 1f;
            }
        }

        // ---------------- SDF (khoảng cách có dấu, âm = bên trong; toạ độ pixel, y hướng lên) ----------------

        static float Circle(float x, float y, Vector2 c, float r) => new Vector2(x - c.x, y - c.y).magnitude - r;

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float k = new Vector2((x - cx) / rx, (y - cy) / ry).magnitude;
            return (k - 1f) * Mathf.Min(rx, ry);
        }

        static float Box(float x, float y, float cx, float cy, float hx, float hy, float round)
        {
            float qx = Mathf.Abs(x - cx) - hx + round, qy = Mathf.Abs(y - cy) - hy + round;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
        }

        // đa giác (đỉnh tính từ tâm c): khoảng cách tới cạnh gần nhất, âm khi ở trong (even-odd)
        internal static float Polygon(float x, float y, Vector2 c, (float x, float y)[] pts)
        {
            var p = new Vector2(x - c.x, y - c.y);
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                var a = new Vector2(pts[i].x, pts[i].y);
                var b = new Vector2(pts[j].x, pts[j].y);
                Vector2 e = b - a, w = p - a;
                Vector2 proj = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, proj.magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }

        static float Coverage(float sdf) => Mathf.Clamp01(0.5f - sdf);

        internal static void Paint(Color[] px, Func<float, float, float> sdf, Color color)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float cov = Coverage(sdf(x + 0.5f, y + 0.5f));
                    if (cov > 0f) Blend(px, x, y, color, cov);
                }
        }

        // trộn "over" (alpha thẳng, không nhân trước)
        static void Blend(Color[] px, int x, int y, Color c, float coverage)
        {
            int i = y * N + x;
            Color dst = px[i];
            float a = c.a * coverage;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return;
            Color rgb = (c * a + dst * dst.a * (1f - a)) / outA;
            px[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
        }
    }
}
