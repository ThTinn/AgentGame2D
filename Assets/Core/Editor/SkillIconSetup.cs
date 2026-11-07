using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Map;
using Game.Map.Editor;
using Game.Skills;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Core/Skills/Build Skill Icons: vẽ icon 128x128 cho từng skill trong SkillDatabase vào Data/Sprites/SkillIcons
    /// (huy hiệu tròn theo màu skill; skill ô 1-4 vẽ bằng SDF theo hiệu ứng 3D của Game: pha lê, vệt chém, thiên thạch, laser...;
    /// đánh thường là ảnh vũ khí của ItemIcons),
    /// nhập làm Sprite, thêm vào SpriteData (loại Icon) rồi gán <see cref="SkillTemplate.idIcon"/>.
    /// </summary>
    /// <remarks>
    /// Chạy lại bao nhiêu lần cũng được (giữ id ảnh theo tên). Có ảnh vẽ tay thì ghi đè file png cùng tên là xong, khỏi chạy
    /// lại menu. Skill mới chưa có mẫu vẽ trong <see cref="Draw"/> thì bị bỏ qua (báo trong Console); icon của skill đã bỏ khỏi
    /// bảng thì bị xóa (bản ghi SpriteData + file ảnh).
    /// </remarks>
    public static class SkillIconSetup
    {
        const string SkillPath = MapPaths.Root + "/Skills/SkillDatabase.asset";
        const string IconFolder = MapPaths.Sprites + "/SkillIcons";
        const string ItemIconFolder = MapPaths.Sprites + "/ItemIcons";

        [MenuItem("Tools/Core/Skills/Build Skill Icons")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillPath);
            if (db == null) { Debug.LogError("SkillIconSetup: không thấy " + SkillPath); return; }
            SpriteData sprites = db.sprites != null ? db.sprites : AssetDatabase.LoadAssetAtPath<SpriteData>(MapPaths.SpriteData);
            if (sprites == null) { Debug.LogError("SkillIconSetup: SkillDatabase chưa gán SpriteData."); return; }
            if (db.sprites == null) db.sprites = sprites;
            MapPaths.EnsureFolder(IconFolder);

            int done = 0;
            var skipped = new List<string>();
            foreach (SkillTemplate t in db.skills)
            {
                var px = new Color[N * N];
                if (!Draw(t.name, px)) { skipped.Add(t.name); continue; }
                t.idIcon = Register(sprites, "skill_" + t.name, WriteIcon(t.name, px));
                done++;
            }
            sourceCache.Clear();
            int pruned = Prune(db, sprites);
            db.ClearCache();
            EditorUtility.SetDirty(db);
            EditorUtility.SetDirty(sprites);
            AssetDatabase.SaveAssets();
            ItemIconAtlas.Build();   // icon skill vào chung atlas icon

            Debug.Log($"SkillIconSetup: vẽ {done} icon skill vào {IconFolder}"
                      + (pruned > 0 ? $", xóa {pruned} icon của skill không còn trong bảng" : "")
                      + (skipped.Count > 0 ? ", chưa có mẫu vẽ: " + string.Join(", ", skipped) : "") + ".", db);
        }

        // icon skill_<tên> (trong IconFolder) của skill đã bỏ khỏi bảng: xóa bản ghi SpriteData và file ảnh
        static int Prune(SkillDatabase db, SpriteData sprites)
        {
            var keep = new HashSet<string>();
            foreach (SkillTemplate t in db.skills) keep.Add("skill_" + t.name);
            int pruned = 0;
            for (int i = sprites.sprites.Count - 1; i >= 0; i--)
            {
                SpriteRecord r = sprites.sprites[i];
                if (r == null || r.type != SpriteType.Icon || r.name == null || !r.name.StartsWith("skill_") || keep.Contains(r.name)) continue;
                string path = r.sprite != null ? AssetDatabase.GetAssetPath(r.sprite) : "";
                if (!path.StartsWith(IconFolder + "/")) continue;   // ảnh không do công cụ này vẽ: để yên
                sprites.sprites.RemoveAt(i);
                AssetDatabase.DeleteAsset(path);
                pruned++;
            }
            sprites.ClearCache();
            return pruned;
        }

        static int Register(SpriteData sprites, string name, Sprite sprite)
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

        static Sprite WriteIcon(string name, Color[] px)
        {
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

        // ---------------- mẫu vẽ từng skill ----------------

        static readonly Color Blue = new Color(0.24f, 0.5f, 0.9f);
        static readonly Color Orange = new Color(0.95f, 0.5f, 0.16f);
        static readonly Color Gold = new Color(0.86f, 0.62f, 0.2f);
        static readonly Color Purple = new Color(0.5f, 0.3f, 0.8f);
        static readonly Color Teal = new Color(0.16f, 0.66f, 0.6f);
        static readonly Color Green = new Color(0.32f, 0.66f, 0.3f);
        static readonly Color Steel = new Color(0.4f, 0.45f, 0.54f);
        static readonly Color Crimson = new Color(0.78f, 0.22f, 0.24f);
        static readonly Color Electric = new Color(0.3f, 0.32f, 0.82f);
        static readonly Color Ice = new Color(0.36f, 0.7f, 0.9f);
        static readonly Color Stone = new Color(0.56f, 0.46f, 0.36f);
        static readonly Color Sky = new Color(0.45f, 0.62f, 0.86f);
        static readonly Color Magenta = new Color(0.74f, 0.26f, 0.66f);
        static readonly Color Frost = new Color(0.2f, 0.52f, 0.8f);
        static readonly Color Ember = new Color(0.86f, 0.36f, 0.14f);
        static readonly Color Earth = new Color(0.5f, 0.55f, 0.34f);
        static readonly Color Wine = new Color(0.58f, 0.15f, 0.22f);
        static readonly Color Navy = new Color(0.18f, 0.3f, 0.66f);

        static readonly Color Outline = new Color(0.16f, 0.11f, 0.17f);
        static readonly Color Spark = new Color(1f, 0.86f, 0.25f);
        static readonly Color White = Color.white;
        static readonly Color IceWhite = new Color(0.85f, 0.96f, 1f);
        static readonly Color Rock = new Color(0.45f, 0.4f, 0.36f);

        // skill ô 1-4: theo hiệu ứng 3D của Game (SkillTemplate.model3D); đánh thường: ảnh vũ khí trên nền thép
        static bool Draw(string name, Color[] px)
        {
            switch (name)
            {
                case "energy_bolt": Medallion(px, Blue, EnergyBolt); return true;
                case "green_crystal": Medallion(px, Green, p => Crystals(p, new Color(0.55f, 1f, 0.6f), new Color(0.12f, 0.55f, 0.3f), false)); return true;
                case "red_crystal": Medallion(px, Crimson, p => Crystals(p, new Color(1f, 0.6f, 0.55f), new Color(0.6f, 0.08f, 0.12f), true)); return true;
                case "electro_slash": Medallion(px, Electric, ElectroSlash); return true;
                case "frost_slash": Medallion(px, Ice, FrostSlash); return true;
                case "stone_slash": Medallion(px, Stone, StoneSlash); return true;
                case "snowstorm": Medallion(px, Sky, Snowstorm); return true;
                case "meteor_shower": Medallion(px, Orange, MeteorShower); return true;
                case "laser": Medallion(px, Magenta, Laser); return true;
                case "freeze": Medallion(px, Frost, p => Snowflake(p, Center, 40f, IceWhite)); return true;
                case "holy_light": Medallion(px, Gold, HolyLight); return true;
                case "energy_blast": Medallion(px, Purple, EnergyBlast); return true;
                case "charge_slash": Medallion(px, Teal, ChargeSlash); return true;
                case "blast_zone": Medallion(px, Ember, BlastZone); return true;
                case "stone_throw": Medallion(px, Earth, StoneThrow); return true;
                case "bullet_hell": Medallion(px, Wine, BulletHell); return true;
                case "frost_nova": Medallion(px, Navy, FrostNova); return true;
                case "basic_sword": Medallion(px, Steel, p => Item(p, "weapon_sword", new Vector2(64f, 64f), 0.86f, 0f)); return true;
                case "basic_bow": Medallion(px, Steel, p => Item(p, "weapon_bow", new Vector2(64f, 64f), 0.86f, 0f)); return true;
                case "basic_hammer": Medallion(px, Steel, p => Item(p, "weapon_hammer", new Vector2(64f, 64f), 0.86f, 0f)); return true;
                case "basic_scythe": Medallion(px, Steel, p => Item(p, "weapon_scythe", new Vector2(64f, 64f), 0.86f, 0f)); return true;
                case "basic_gun": Medallion(px, Steel, p => Item(p, "weapon_gun", new Vector2(64f, 64f), 0.86f, 0f)); return true;
                case "basic_rifle": Medallion(px, Steel, p => Item(p, "weapon_rifle", new Vector2(64f, 64f), 0.86f, 0f)); return true;
                default: return false;
            }
        }

        // Đạn năng lượng: quả cầu sáng bay chéo lên, vệt đuôi phía sau
        static void EnergyBolt(Color[] px)
        {
            var orb = new Vector2(78f, 76f);
            var tails = new[] { (22f, 30f, 6f), (30f, 54f, 4f), (48f, 26f, 4f) };
            foreach (var (x0, y0, r) in tails)
            {
                var a = new Vector2(x0, y0);
                float w = r;
                Paint(px, (x, y) => Segment(x, y, a, orb, w), new Color(0.75f, 0.95f, 1f, 0.55f));
            }
            Paint(px, (x, y) => Circle(x, y, orb, 30f), new Color(0.7f, 0.95f, 1f, 0.35f));
            Paint(px, (x, y) => Circle(x, y, orb, 21f) - 3.5f, Outline);
            PaintRadial(px, (x, y) => Circle(x, y, orb, 21f), orb + new Vector2(-5f, 5f), 24f, White, new Color(0.3f, 0.75f, 1f));
            Sparkle(px, new Vector2(102f, 102f), 8f);
            Sparkle(px, new Vector2(36f, 92f), 6f);
        }

        // Pha lê: mảnh pha lê dài (mặt sáng / tối); cluster = cụm ba mảnh đứng
        static void Crystals(Color[] px, Color light, Color dark, bool cluster)
        {
            if (cluster)
            {
                Shard(px, new Vector2(40f, 54f), 0.62f, 28f, light, dark);
                Shard(px, new Vector2(88f, 54f), 0.62f, -28f, light, dark);
                Shard(px, new Vector2(64f, 62f), 1f, 0f, light, dark);
            }
            else
            {
                Paint(px, (x, y) => Segment(x, y, new Vector2(18f, 40f), new Vector2(44f, 66f), 2.5f), new Color(1f, 1f, 1f, 0.7f));
                Paint(px, (x, y) => Segment(x, y, new Vector2(40f, 18f), new Vector2(66f, 44f), 2.5f), new Color(1f, 1f, 1f, 0.7f));
                Shard(px, new Vector2(68f, 68f), 1f, -45f, light, dark);
            }
            Sparkle(px, new Vector2(98f, 100f), 8f);
        }

        static void Shard(Color[] px, Vector2 c, float s, float angle, Color light, Color dark)
        {
            var whole = Rot(new[] { (0f, 46f * s), (13f * s, 14f * s), (9f * s, -32f * s), (0f, -40f * s), (-9f * s, -32f * s), (-13f * s, 14f * s) }, angle);
            var half = Rot(new[] { (0f, 46f * s), (13f * s, 14f * s), (9f * s, -32f * s), (0f, -40f * s) }, angle);
            var glint = Rot(new[] { (-3f * s, 30f * s), (-8f * s, 12f * s), (-5f * s, -14f * s) }, angle);
            Paint(px, (x, y) => Polygon(x, y, c, whole) - 3.5f, Outline);
            Paint(px, (x, y) => Polygon(x, y, c, whole), Color.Lerp(light, dark, 0.35f));
            Paint(px, (x, y) => Polygon(x, y, c, half), dark);
            Paint(px, (x, y) => Polygon(x, y, c, glint), new Color(1f, 1f, 1f, 0.65f));
        }

        // Chém điện: vệt chém trăng khuyết + tia sét vàng
        static void ElectroSlash(Color[] px)
        {
            Crescent(px, White, new Color(0.65f, 0.75f, 1f));
            var bolt = new[] { (4f, 30f), (-10f, 2f), (-2f, 2f), (-8f, -28f), (10f, 4f), (2f, 4f), (12f, 30f) };
            var c = new Vector2(70f, 62f);
            Paint(px, (x, y) => Polygon(x, y, c, bolt) - 3.5f, Outline);
            Paint(px, (x, y) => Polygon(x, y, c, bolt), Spark);
            Sparkle(px, new Vector2(30f, 96f), 7f);
        }

        // Chém băng: vệt chém trắng xanh + bông tuyết nhỏ
        static void FrostSlash(Color[] px)
        {
            Crescent(px, White, new Color(0.55f, 0.88f, 1f));
            Snowflake(px, new Vector2(96f, 96f), 13f, IceWhite);
            Snowflake(px, new Vector2(30f, 40f), 9f, IceWhite);
        }

        // Chém đá: vệt chém màu cát + mấy hòn đá văng ra
        static void StoneSlash(Color[] px)
        {
            Crescent(px, new Color(0.98f, 0.9f, 0.72f), new Color(0.7f, 0.52f, 0.32f));
            Pebble(px, new Vector2(96f, 38f), 12f, 15f);
            Pebble(px, new Vector2(78f, 24f), 8f, -20f);
            Pebble(px, new Vector2(104f, 62f), 6f, 40f);
        }

        // Bão tuyết: đám mây trên, tuyết rơi + gió xoáy dưới
        static void Snowstorm(Color[] px)
        {
            Func<float, float, float> cloud = (x, y) => Mathf.Min(Mathf.Min(Circle(x, y, new Vector2(42f, 80f), 17f), Circle(x, y, new Vector2(64f, 90f), 22f)),
                Mathf.Min(Circle(x, y, new Vector2(87f, 81f), 17f), Box(x, y, 64f, 72f, 40f, 9f, 8f)));
            Paint(px, (x, y) => cloud(x, y) - 3.5f, Outline);
            PaintGradient(px, cloud, new Vector2(64f, 108f), White, new Vector2(64f, 64f), new Color(0.72f, 0.8f, 0.9f));
            Paint(px, (x, y) => Mathf.Abs(Circle(x, y, new Vector2(64f, 40f), 22f)) - 2f, new Color(1f, 1f, 1f, 0.45f));
            Snowflake(px, new Vector2(40f, 44f), 9f, IceWhite);
            Snowflake(px, new Vector2(66f, 30f), 10f, IceWhite);
            Snowflake(px, new Vector2(90f, 46f), 8f, IceWhite);
        }

        // Mưa thiên thạch: ba hòn lửa rơi chéo xuống trái, đuôi lửa phía sau
        static void MeteorShower(Color[] px)
        {
            Meteor(px, new Vector2(54f, 50f), 15f);
            Meteor(px, new Vector2(92f, 78f), 10f);
            Meteor(px, new Vector2(38f, 92f), 8f);
        }

        static void Meteor(Color[] px, Vector2 head, float r)
        {
            var dir = new Vector2(1f, 1f).normalized;   // đuôi kéo về phía trên phải
            var n = new Vector2(-dir.y, dir.x);
            Vector2 tip = dir * r * 3.4f, a = n * r, b = -n * r;
            var tail = new[] { (a.x, a.y), (tip.x, tip.y), (b.x, b.y) };
            var core = Scale(tail, 0.6f);
            Paint(px, (x, y) => Polygon(x, y, head, tail), new Color(1f, 0.55f, 0.15f, 0.85f));
            Paint(px, (x, y) => Polygon(x, y, head, core), new Color(1f, 0.9f, 0.4f, 0.9f));
            Paint(px, (x, y) => Circle(x, y, head, r) - 3f, Outline);
            PaintRadial(px, (x, y) => Circle(x, y, head, r), head + (n + dir) * r * 0.3f, r * 1.3f, new Color(1f, 0.75f, 0.35f), new Color(0.55f, 0.18f, 0.08f));
        }

        // Tia laser: cột sáng dọc chiếu xuống, vòng sáng chỗ chạm đất
        static void Laser(Color[] px)
        {
            var ground = new Vector2(64f, 34f);
            Paint(px, (x, y) => Mathf.Abs(Ellipse(x, y, ground.x, ground.y, 36f, 10f)) - 2.5f, new Color(1f, 0.8f, 0.95f, 0.9f));
            Paint(px, (x, y) => Ellipse(x, y, ground.x, ground.y, 22f, 6f), new Color(1f, 0.85f, 1f, 0.7f));
            Func<float, float, float> beam = (x, y) => Box(x, y, 64f, 82f, 13f, 48f, 6f);
            Paint(px, (x, y) => beam(x, y) - 3.5f, Outline);
            Paint(px, beam, new Color(1f, 0.35f, 0.75f));
            Paint(px, (x, y) => Box(x, y, 64f, 82f, 5f, 48f, 4f), White);
            Sparkle(px, new Vector2(34f, 52f), 7f);
            Sparkle(px, new Vector2(96f, 56f), 8f);
        }

        // Vầng sáng: mặt trời nhiều tia + quầng sáng
        static void HolyLight(Color[] px)
        {
            var c = Center;
            var rays = Star(12, 46f, 28f, 0f);
            Paint(px, (x, y) => Polygon(x, y, c, rays) - 3f, Outline);
            PaintRadial(px, (x, y) => Polygon(x, y, c, rays), c, 46f, new Color(1f, 0.97f, 0.75f), Spark);
            Paint(px, (x, y) => Circle(x, y, c, 20f) - 3f, Outline);
            PaintRadial(px, (x, y) => Circle(x, y, c, 20f), c + new Vector2(-4f, 4f), 22f, White, new Color(1f, 0.9f, 0.55f));
        }

        // Nổ năng lượng: vụ nổ hình sao tím, lõi trắng, mảnh văng
        static void EnergyBlast(Color[] px)
        {
            var c = new Vector2(64f, 62f);
            var burst = Star(10, 44f, 22f, 8f);
            var core = Star(8, 20f, 10f, 0f);
            Paint(px, (x, y) => Polygon(x, y, c, burst) - 3.5f, Outline);
            PaintRadial(px, (x, y) => Polygon(x, y, c, burst), c, 44f, new Color(1f, 0.85f, 1f), new Color(0.75f, 0.35f, 1f));
            Paint(px, (x, y) => Polygon(x, y, c, core), White);
            foreach (var (x0, y0) in new[] { (24f, 30f), (104f, 36f), (100f, 98f) })
            {
                var d = new Vector2(x0, y0);
                Paint(px, (x, y) => Circle(x, y, d, 4f), new Color(1f, 0.85f, 1f, 0.85f));
            }
        }

        // Lướt chém: thanh kiếm lao về trước, vệt gió phía sau
        static void ChargeSlash(Color[] px)
        {
            var lines = new[] { (12f, 46f, 70f), (16f, 40f, 52f), (20f, 48f, 88f) };
            foreach (var (x0, x1, yl) in lines)
            {
                var a = new Vector2(x0, yl);
                var b = new Vector2(x1, yl);
                Paint(px, (x, y) => Segment(x, y, a, b, 3f) - 2.5f, Outline);
                Paint(px, (x, y) => Segment(x, y, a, b, 3f), White);
            }
            Item(px, "weapon_sword", new Vector2(72f, 66f), 0.8f, -12f);
            Sparkle(px, new Vector2(104f, 100f), 8f);
        }

        // Vùng nổ: vòng cảnh báo trên đất, vụ nổ bốc lên giữa vòng
        static void BlastZone(Color[] px)
        {
            var g = new Vector2(64f, 38f);
            Paint(px, (x, y) => Ellipse(x, y, g.x, g.y, 46f, 15f), new Color(1f, 0.55f, 0.2f, 0.45f));
            Paint(px, (x, y) => Mathf.Abs(Ellipse(x, y, g.x, g.y, 46f, 15f)) - 2.5f, new Color(1f, 0.9f, 0.6f, 0.95f));
            var c = new Vector2(64f, 62f);
            var burst = Star(9, 34f, 16f, 0f);
            Paint(px, (x, y) => Polygon(x, y, c, burst) - 3.5f, Outline);
            PaintRadial(px, (x, y) => Polygon(x, y, c, burst), c, 34f, new Color(1f, 0.95f, 0.7f), Orange);
            Paint(px, (x, y) => Mathf.Min(Circle(x, y, new Vector2(40f, 92f), 9f), Circle(x, y, new Vector2(88f, 96f), 7f)), new Color(1f, 1f, 1f, 0.45f));
        }

        // Ném đá: tảng đá lớn có vết nứt, chấm vệt bay phía sau
        static void StoneThrow(Color[] px)
        {
            var dots = new[] { (20f, 28f, 3f), (28f, 40f, 4f), (38f, 50f, 5f) };
            foreach (var (x0, y0, r) in dots)
            {
                var d = new Vector2(x0, y0);
                float rr = r;
                Paint(px, (x, y) => Circle(x, y, d, rr), new Color(1f, 1f, 1f, 0.8f));
            }
            Pebble(px, new Vector2(74f, 70f), 28f, 10f);
            Paint(px, (x, y) => Mathf.Min(Segment(x, y, new Vector2(70f, 84f), new Vector2(78f, 70f), 1.6f),
                Segment(x, y, new Vector2(78f, 70f), new Vector2(72f, 58f), 1.6f)), new Color(0.2f, 0.16f, 0.14f, 0.8f));
        }

        // Mưa đạn: tám viên đạn tỏa ra quanh người
        static void BulletHell(Color[] px)
        {
            var c = Center;
            Paint(px, (x, y) => Mathf.Abs(Circle(x, y, c, 20f)) - 2f, new Color(1f, 0.85f, 0.6f, 0.6f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 p0 = c + d * 28f, p1 = c + d * 44f;
                Paint(px, (x, y) => Segment(x, y, p0, p1, 6f) - 3f, Outline);
                PaintGradient(px, (x, y) => Segment(x, y, p0, p1, 6f), p0, new Color(1f, 0.6f, 0.2f), p1, Spark);
            }
            Paint(px, (x, y) => Circle(x, y, c, 11f) - 3f, Outline);
            Paint(px, (x, y) => Circle(x, y, c, 11f), Spark);
        }

        // Băng giá: vòng gai băng chĩa ra ngoài, bông tuyết nhỏ ở giữa
        static void FrostNova(Color[] px)
        {
            var c = Center;
            for (int i = 0; i < 8; i++)
            {
                var spike = Rot(new[] { (0f, 50f), (8f, 26f), (-8f, 26f) }, i * 45f);
                Paint(px, (x, y) => Polygon(x, y, c, spike) - 3f, Outline);
                Paint(px, (x, y) => Polygon(x, y, c, spike), new Color(0.75f, 0.92f, 1f));
            }
            Paint(px, (x, y) => Mathf.Abs(Circle(x, y, c, 24f)) - 2.5f, IceWhite);
            Snowflake(px, c, 15f, IceWhite);
        }

        // ---------------- mảnh vẽ dùng chung ----------------

        // vệt chém trăng khuyết lớn (sáng ở ngoài, nhạt dần vào trong)
        static void Crescent(Color[] px, Color edge, Color inner)
        {
            var outer = new Vector2(62f, 56f);
            var cut = new Vector2(46f, 72f);
            Func<float, float, float> crescent = (x, y) => Mathf.Max(Circle(x, y, outer, 46f), -Circle(x, y, cut, 44f));
            Paint(px, (x, y) => crescent(x, y) - 3.5f, Outline);
            PaintGradient(px, crescent, new Vector2(96f, 40f), edge, new Vector2(30f, 20f), inner);
        }

        // bông tuyết 6 cánh, mỗi cánh có hai nhánh
        static void Snowflake(Color[] px, Vector2 c, float r, Color color)
        {
            float w = Mathf.Max(1.5f, r * 0.09f);
            var arms = new List<(Vector2 a, Vector2 b, float w)>();
            for (int i = 0; i < 6; i++)
            {
                float ang = i * Mathf.PI / 3f + Mathf.PI / 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var n = new Vector2(-dir.y, dir.x);
                Vector2 mid = c + dir * r * 0.55f;
                arms.Add((c, c + dir * r, w));
                arms.Add((mid, mid + (dir + n).normalized * r * 0.32f, w * 0.8f));
                arms.Add((mid, mid + (dir - n).normalized * r * 0.32f, w * 0.8f));
            }
            Func<float, float, float> flake = (x, y) =>
            {
                float d = 99f;
                foreach (var s in arms) d = Mathf.Min(d, Segment(x, y, s.a, s.b, s.w));
                return d;
            };
            Paint(px, (x, y) => flake(x, y) - 2.5f, Outline);
            Paint(px, flake, color);
        }

        // hòn đá bảy cạnh méo, sáng phía trên trái
        static void Pebble(Color[] px, Vector2 c, float r, float angle)
        {
            var pts = Rot(new[] { (r * 0.9f, r * 0.3f), (r * 0.5f, r * 0.95f), (-r * 0.35f, r * 0.85f), (-r * 0.95f, r * 0.25f),
                                  (-r * 0.8f, -r * 0.55f), (-r * 0.1f, -r * 0.95f), (r * 0.7f, -r * 0.7f) }, angle);
            Paint(px, (x, y) => Polygon(x, y, c, pts) - 3f, Outline);
            PaintRadial(px, (x, y) => Polygon(x, y, c, pts), c + new Vector2(-r * 0.35f, r * 0.35f), r * 1.6f, new Color(0.78f, 0.72f, 0.64f), Rock);
        }

        static (float x, float y)[] Rot((float x, float y)[] pts, float angle)
        {
            float a = angle * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            var o = new (float, float)[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = (pts[i].x * cos - pts[i].y * sin, pts[i].x * sin + pts[i].y * cos);
            return o;
        }

        static (float x, float y)[] Scale((float x, float y)[] pts, float s)
        {
            var o = new (float, float)[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = (pts[i].x * s, pts[i].y * s);
            return o;
        }

        // ---------------- huy hiệu ----------------

        const int N = 128;
        const float Radius = 58f;
        static readonly Vector2 Center = new Vector2(64f, 64f);

        // vẽ trong huy hiệu bị cắt theo vòng trong; viền vẽ sau cùng nên mép hiệu ứng luôn gọn
        static Func<float, float, float> clip;

        static void Medallion(Color[] px, Color theme, Action<Color[]> content)
        {
            Color light = Color.Lerp(theme, Color.white, 0.35f), dark = Color.Lerp(theme, Color.black, 0.45f);
            var glow = new Vector2(48f, 82f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cov = Coverage(Circle(fx, fy, Center, Radius));
                    if (cov <= 0f) continue;
                    float t = Mathf.Clamp01(new Vector2(fx - glow.x, fy - glow.y).magnitude / 92f);
                    Blend(px, x, y, Color.Lerp(light, dark, t), cov);
                }

            clip = (x, y) => Circle(x, y, Center, Radius - 3f);
            content(px);
            clip = null;

            Paint(px, (x, y) => Mathf.Abs(Circle(x, y, Center, Radius - 6.5f)) - 1f, new Color(1f, 1f, 1f, 0.3f));
            Paint(px, (x, y) => Mathf.Abs(Circle(x, y, Center, Radius - 1.5f)) - 3f, Outline);
        }

        // ảnh vật phẩm (ItemIcons, 128x128) đặt tâm tại center, thu phóng + xoay (độ, ngược chiều kim đồng hồ)
        static void Item(Color[] px, string iconName, Vector2 center, float scale, float angle)
        {
            Color[] src = Source(iconName);
            if (src == null) return;
            float rad = -angle * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f - center.x) / scale, dy = (y + 0.5f - center.y) / scale;
                    float sx = dx * cos - dy * sin + N * 0.5f, sy = dx * sin + dy * cos + N * 0.5f;
                    Color c = Sample(src, sx, sy);
                    float cov = c.a * ClipCoverage(x + 0.5f, y + 0.5f);
                    if (cov > 0f) Blend(px, x, y, new Color(c.r, c.g, c.b, 1f), cov);
                }
        }

        static readonly Dictionary<string, Color[]> sourceCache = new Dictionary<string, Color[]>();

        static Color[] Source(string iconName)
        {
            if (sourceCache.TryGetValue(iconName, out Color[] cached)) return cached;
            string path = ItemIconFolder + "/" + iconName + ".png";
            if (!File.Exists(path)) { Debug.LogWarning("SkillIconSetup: không thấy " + path); return null; }
            // đọc thẳng file png để khỏi phải bật Read/Write cho texture
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            Color[] px = tex.width == N && tex.height == N ? tex.GetPixels() : null;
            UnityEngine.Object.DestroyImmediate(tex);
            if (px == null) Debug.LogWarning("SkillIconSetup: " + path + " không phải 128x128");
            sourceCache[iconName] = px;
            return px;
        }

        // lấy mẫu song tuyến (alpha nhân trước để mép trong suốt không ám màu)
        static Color Sample(Color[] src, float x, float y)
        {
            x -= 0.5f; y -= 0.5f;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            Color a = Premul(src, x0, y0), b = Premul(src, x0 + 1, y0), c = Premul(src, x0, y0 + 1), d = Premul(src, x0 + 1, y0 + 1);
            Color p = Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
            return p.a > 0f ? new Color(p.r / p.a, p.g / p.a, p.b / p.a, p.a) : Color.clear;
        }

        static Color Premul(Color[] src, int x, int y)
        {
            if (x < 0 || y < 0 || x >= N || y >= N) return Color.clear;
            Color c = src[y * N + x];
            return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
        }

        // ngôi sao 4 cánh nhỏ (tia sáng)
        static void Sparkle(Color[] px, Vector2 c, float r)
        {
            var pts = Star(4, r, r * 0.3f, 0f);
            Paint(px, (x, y) => Polygon(x, y, c, pts), White);
        }

        // ---------------- hình ----------------

        // sao n cánh (đỉnh tính từ tâm), cánh đầu chỉ lên + xoay rot độ
        static (float x, float y)[] Star(int n, float outer, float inner, float rot)
        {
            var pts = new (float, float)[n * 2];
            for (int i = 0; i < n * 2; i++)
            {
                float a = Mathf.PI / 2f + (rot * Mathf.Deg2Rad) + i * Mathf.PI / n, r = i % 2 == 0 ? outer : inner;
                pts[i] = (Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }

        // hình chữ nhật nửa cạnh hx, hy, xoay angle độ (đỉnh tính từ tâm)
        static (float x, float y)[] Rect(float hx, float hy, float angle)
        {
            float a = angle * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            var corners = new[] { (-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy) };
            var pts = new (float, float)[4];
            for (int i = 0; i < 4; i++)
                pts[i] = (corners[i].Item1 * cos - corners[i].Item2 * sin, corners[i].Item1 * sin + corners[i].Item2 * cos);
            return pts;
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

        static float Segment(float x, float y, Vector2 a, Vector2 b, float r)
        {
            Vector2 p = new Vector2(x, y) - a, e = b - a;
            return (p - e * Mathf.Clamp01(Vector2.Dot(p, e) / e.sqrMagnitude)).magnitude - r;
        }

        // đa giác (đỉnh tính từ tâm c): khoảng cách tới cạnh gần nhất, âm khi ở trong (even-odd)
        static float Polygon(float x, float y, Vector2 c, (float x, float y)[] pts)
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

        static float ClipCoverage(float x, float y) => clip != null ? Coverage(clip(x, y)) : 1f;

        static void Paint(Color[] px, Func<float, float, float> sdf, Color color)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float cov = Coverage(sdf(x + 0.5f, y + 0.5f)) * ClipCoverage(x + 0.5f, y + 0.5f);
                    if (cov > 0f) Blend(px, x, y, color, cov);
                }
        }

        // tô theo dải màu tuyến tính từ điểm a (màu ca) tới điểm b (màu cb)
        static void PaintGradient(Color[] px, Func<float, float, float> sdf, Vector2 a, Color ca, Vector2 b, Color cb)
        {
            Vector2 e = b - a;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cov = Coverage(sdf(fx, fy)) * ClipCoverage(fx, fy);
                    if (cov <= 0f) continue;
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(fx, fy) - a, e) / e.sqrMagnitude);
                    Blend(px, x, y, Color.Lerp(ca, cb, t), cov);
                }
        }

        // tô theo dải màu tròn: màu cin ở tâm c, ra tới bán kính r thì thành cout
        static void PaintRadial(Color[] px, Func<float, float, float> sdf, Vector2 c, float r, Color cin, Color cout)
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float fx = x + 0.5f, fy = y + 0.5f;
                    float cov = Coverage(sdf(fx, fy)) * ClipCoverage(fx, fy);
                    if (cov <= 0f) continue;
                    float t = Mathf.Clamp01(new Vector2(fx - c.x, fy - c.y).magnitude / r);
                    Blend(px, x, y, Color.Lerp(cin, cout, t), cov);
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
