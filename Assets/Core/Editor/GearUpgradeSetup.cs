using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Game.Items;
using Game.Map;
using Game.Map.Editor;
using Game.Npcs;
using Game.Players;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Dựng dữ liệu và UI cường hóa, khoét lỗ, khảm đá. Chạy lại bao nhiêu lần cũng được.
    /// <list type="bullet">
    /// <item>Tools/Core/Items/Build Gear Upgrade: bốn loại đá và 18 đá khảm (icon, ItemDatabase), bảng luật GearUpgradeTable (phần
    /// nào chưa có mới điền, số đã chỉnh được giữ), dòng rơi đồ, hàng bán ở Ông Hỏa và Thầy bói, gán vào LocalPlaySettings, xuất
    /// file cho server.</item>
    /// <item>Tools/Core/UI/Build Gear Upgrade UI: prefab GearUpgradePanel, GearSocketPanel, dòng trong Panel_Config, nút Cường hóa
    /// ở ItemInfoPopup.</item>
    /// </list>
    /// Sửa số của đá và bảng rơi đồ về sau thì qua Excel; bảng luật sửa trực tiếp trong Inspector.
    /// </summary>
    public static class GearUpgradeSetup
    {
        const string TablePath = MapPaths.Items + "/GearUpgradeTable.asset";
        const string NpcPath = MapPaths.Root + "/Npcs/NpcDatabase.asset";
        const string PanelPath = "Assets/_Project/Core/UI/Prefabs/Panel/GearUpgradePanel.prefab";
        const string SocketPanelPath = "Assets/_Project/Core/UI/Prefabs/Panel/GearSocketPanel.prefab";
        const string PopupPath = "Assets/_Project/Core/UI/Prefabs/Popup/ItemInfoPopup.prefab";
        const string PanelConfigPath = "Assets/_Project/Core/UI/Data/Panel_Config.asset";
        const string Blacksmith = "blacksmith";
        const string Fortune = "fortune";

        static readonly (string name, string display, string description, GearStone kind, ItemRarity rarity, Color color)[] Stones =
        {
            ("stone_upgrade", "Đá cường hóa", "Nguyên liệu cường hóa trang bị. Mỗi lần thử mất số viên theo cấp.", GearStone.Upgrade,
                ItemRarity.Uncommon, new Color(0.25f, 0.6f, 1f)),
            ("stone_lucky", "Đá may mắn", "Tăng tỉ lệ thành công của một lần cường hóa.", GearStone.Lucky,
                ItemRarity.Rare, new Color(0.3f, 0.85f, 0.45f)),
            ("stone_protect", "Đá bảo vệ", "Trượt ở cấp cao thì giữ nguyên cấp. Chỉ mất khi đã cứu món đồ.", GearStone.Protect,
                ItemRarity.Epic, new Color(0.7f, 0.4f, 1f)),
            ("stone_drill", "Đá khoét lỗ", "Khoét thêm lỗ trên vũ khí hoặc đồ mặc để khảm đá. Mỗi lần thử mất số viên theo lỗ.", GearStone.Drill,
                ItemRarity.Rare, new Color(0.95f, 0.6f, 0.2f)),
        };

        // đá khảm: mỗi loại ba bậc, tên jewel_<key>_<bậc>; bậc 1 bán ở Thầy bói, bậc 2 và 3 chỉ rơi.
        // Cân theo nhân vật Lv50 (705 máu, +145% sát thương, 34.5% chí mạng): 8 món khảm đủ bộ đánh (Hồng + Hoàng + Tử) bậc 3 được
        // +49% sát thương, bộ thủ (Lục + Hắc) bậc 3 được +54% sống sót. Giữ được mức này nhờ luật mỗi món một viên mỗi loại
        static readonly (string key, string display, PlayerStat stat, float[] values, Color color)[] Jewels =
        {
            ("ruby", "Hồng thạch", PlayerStat.Damage, new[] { 1f, 2f, 3f }, new Color(0.92f, 0.22f, 0.25f)),
            ("sapphire", "Lam thạch", PlayerStat.MaxMp, new[] { 5f, 10f, 20f }, new Color(0.22f, 0.45f, 0.95f)),
            ("emerald", "Lục thạch", PlayerStat.MaxHp, new[] { 8f, 15f, 25f }, new Color(0.2f, 0.78f, 0.42f)),
            ("topaz", "Hoàng thạch", PlayerStat.Crit, new[] { 0.5f, 1f, 2f }, new Color(0.98f, 0.8f, 0.22f)),
            ("amethyst", "Tử thạch", PlayerStat.CritDamage, new[] { 3f, 5f, 8f }, new Color(0.65f, 0.32f, 0.95f)),
            ("onyx", "Hắc thạch", PlayerStat.Defense, new[] { 0.5f, 1f, 1.5f }, new Color(0.38f, 0.4f, 0.48f)),
        };
        static readonly string[] Tiers = { "I", "II", "III" };
        static readonly ItemRarity[] TierRarity = { ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic };

        static string JewelName(string key, int tier) => "jewel_" + key + "_" + tier;

        // (tỉ lệ %, số đá, vàng, trượt thì tụt cấp, số đá bảo vệ): phần tử i là lần nâng từ +i lên +(i+1).
        // Cân theo thu nhập đo khi chơi thử (7 quái / phút, vàng = 10 + 6 x cấp quái): +5 ~15 phút ở Lv5, +8 ~30 phút ở Lv28,
        // +10 ~1 giờ cuối game khi dùng đủ đá. Đá bảo vệ tăng theo cấp để đường an toàn không rẻ hơn hẳn đường liều
        static readonly (float chance, int stones, long gold, bool drop, int protect)[] Steps =
        {
            (100f, 1, 100, false, 1), (95f, 1, 200, false, 1), (90f, 1, 300, false, 1), (80f, 2, 500, false, 1), (70f, 2, 800, false, 1),
            (60f, 3, 1200, true, 1), (50f, 3, 1800, true, 2), (40f, 4, 3000, true, 4), (30f, 5, 5000, true, 8), (20f, 6, 8000, true, 16),
        };

        // đá chen vào các bảng đã có: (bảng, vật phẩm, tỉ lệ %, min, max)
        static readonly (string table, string item, float chance, int min, int max)[] Drops =
        {
            (DropDatabase.MonsterDefaultTable, "stone_upgrade", 3f, 1, 1),
            (DropDatabase.MonsterDefaultTable, "stone_lucky", 0.5f, 1, 1),
            ("slime_green", "stone_upgrade", 4f, 1, 1),
            ("slime_fire", "stone_upgrade", 6f, 1, 1), ("slime_fire", "stone_lucky", 1f, 1, 1),
            (DropDatabase.MonsterIdTable(1), "stone_upgrade", 5f, 1, 1),
            (DropDatabase.MonsterIdTable(2), "stone_upgrade", 8f, 1, 1), (DropDatabase.MonsterIdTable(2), "stone_lucky", 1f, 1, 1),
            (DropDatabase.MonsterIdTable(5), "stone_upgrade", 8f, 1, 2), (DropDatabase.MonsterIdTable(5), "stone_lucky", 2f, 1, 1),
            (DropDatabase.MonsterIdTable(5), "stone_protect", 0.5f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Small), "stone_upgrade", 30f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Iron), "stone_upgrade", 50f, 1, 2), (DropDatabase.ChestTable(ChestKind.Iron), "stone_lucky", 10f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Gold), "stone_upgrade", 70f, 2, 3), (DropDatabase.ChestTable(ChestKind.Gold), "stone_lucky", 25f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Gold), "stone_protect", 10f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Large), "stone_upgrade", 100f, 3, 5), (DropDatabase.ChestTable(ChestKind.Large), "stone_lucky", 40f, 1, 2),
            (DropDatabase.ChestTable(ChestKind.Large), "stone_protect", 25f, 1, 1),
            (DropDatabase.MonsterDefaultTable, "stone_drill", 1.5f, 1, 1),
            ("slime_green", "stone_drill", 1.5f, 1, 1), ("slime_fire", "stone_drill", 2.5f, 1, 1),
            (DropDatabase.MonsterIdTable(1), "stone_drill", 2f, 1, 1),
            (DropDatabase.MonsterIdTable(2), "stone_drill", 3f, 1, 1), (DropDatabase.MonsterIdTable(5), "stone_drill", 4f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Small), "stone_drill", 10f, 1, 1), (DropDatabase.ChestTable(ChestKind.Iron), "stone_drill", 25f, 1, 1),
            (DropDatabase.ChestTable(ChestKind.Gold), "stone_drill", 40f, 1, 2), (DropDatabase.ChestTable(ChestKind.Large), "stone_drill", 60f, 2, 3),
        };

        // đá khảm rơi theo bậc: (bảng, tỉ lệ % mỗi bậc 1 / 2 / 3, các loại đá; loại trống = cả sáu loại). Quái càng khó càng có bậc cao
        static readonly (string table, float[] chance, string[] keys)[] JewelDrops =
        {
            (DropDatabase.MonsterDefaultTable, new[] { 0.4f, 0f, 0f }, new string[0]),
            ("slime_green", new[] { 1f, 0f, 0f }, new[] { "emerald", "ruby" }),
            ("slime_fire", new[] { 1.5f, 0.2f, 0f }, new[] { "ruby", "topaz" }),
            (DropDatabase.MonsterIdTable(1), new[] { 1.5f, 0f, 0f }, new[] { "emerald", "ruby" }),
            (DropDatabase.MonsterIdTable(2), new[] { 2f, 0.4f, 0f }, new[] { "onyx", "emerald" }),
            (DropDatabase.MonsterIdTable(5), new[] { 2f, 0.6f, 0.1f }, new[] { "amethyst", "sapphire", "topaz" }),
            (DropDatabase.ChestTable(ChestKind.Small), new[] { 8f, 0f, 0f }, new string[0]),
            (DropDatabase.ChestTable(ChestKind.Iron), new[] { 12f, 3f, 0f }, new string[0]),
            (DropDatabase.ChestTable(ChestKind.Gold), new[] { 0f, 10f, 2f }, new string[0]),
            (DropDatabase.ChestTable(ChestKind.Large), new[] { 0f, 15f, 5f }, new string[0]),
        };

        static readonly (string option, string label, string item, int price)[] Shop =
        {
            ("buy_stone_upgrade", "Mua đá cường hóa", "stone_upgrade", 150),
            ("buy_stone_lucky", "Mua đá may mắn", "stone_lucky", 500),
            ("buy_stone_protect", "Mua đá bảo vệ", "stone_protect", 1000),
            ("buy_stone_drill", "Mua đá khoét lỗ", "stone_drill", 400),
        };

        const int JewelPrice = 300;   // đá khảm bậc 1 ở Thầy bói

        // lần khoét lỗ thứ i+1: tỉ lệ, số đá khoét lỗ, vàng
        static readonly (float chance, int stones, long gold)[] SocketSteps = { (100f, 1, 500), (70f, 2, 1500), (40f, 3, 4000) };

        [MenuItem("Tools/Core/Items/Build Gear Upgrade")]
        public static void Run()
        {
            ItemDatabase items = ChestTools.EnsureItems();
            SpriteData sprites = items.sprites != null ? items.sprites : MapExport.LoadOrCreateAssets().sprites;
            if (items.sprites == null) items.sprites = sprites;

            foreach (var s in Stones)
            {
                int iconId = PotionItemSetup.Register(sprites, "icon_" + s.name, DrawStone(s.name, s.color));
                ItemTemplate it = items.Find(s.name) ?? items.Add(new ItemTemplate { name = s.name });
                it.displayName = s.display;
                it.description = s.description;
                it.kind = ItemKind.Stone;
                it.rarity = s.rarity;
                it.value = (int)s.kind;
                it.maxStack = 99;
                it.iconId = iconId;
            }
            foreach (var j in Jewels)
                for (int tier = 1; tier <= Tiers.Length; tier++)
                {
                    string name = JewelName(j.key, tier);
                    int iconId = PotionItemSetup.Register(sprites, "icon_" + name, DrawJewel(name, j.color, tier));
                    ItemTemplate it = items.Find(name) ?? items.Add(new ItemTemplate { name = name });
                    it.displayName = j.display + " " + Tiers[tier - 1];
                    it.description = "Khảm vào lỗ trên vũ khí hoặc đồ mặc, mỗi món một viên mỗi loại. Gỡ ra được, đá trả về túi.";
                    it.kind = ItemKind.Jewel;
                    it.rarity = TierRarity[tier - 1];
                    it.value = tier;
                    it.maxStack = 99;
                    it.iconId = iconId;
                    it.options = new List<PlayerStatEffect> { new PlayerStatEffect(j.stat, j.values[tier - 1]) };
                }
            items.ClearCache();
            EditorUtility.SetDirty(items);
            EditorUtility.SetDirty(sprites);

            GearUpgradeTable table = EnsureTable();
            int drops = FillDrops(items);
            int shop = FillShop();
            CoreHostAsset.Bind("gearUpgrades", table, onlyIfEmpty: true);
            AssetDatabase.SaveAssets();
            ItemIconAtlas.Build();   // icon mới vào atlas chung
            ItemsExcelImport.ExportServerData();

            Debug.Log($"GearUpgradeSetup: {Stones.Length} loại đá, {Jewels.Length * Tiers.Length} đá khảm, {table.steps.Count} cấp, "
                      + $"{table.sockets.Count} lỗ, thêm {drops} dòng rơi đồ, {shop} hàng bán.", table);
        }

        static GearUpgradeTable EnsureTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<GearUpgradeTable>(TablePath);
            bool isNew = table == null;
            if (isNew) table = ScriptableObject.CreateInstance<GearUpgradeTable>();

            if (table.steps.Count == 0)
                foreach (var s in Steps)
                    table.steps.Add(new GearUpgradeStep { chance = s.chance, stones = s.stones, gold = s.gold, dropOnFail = s.drop, protectStones = s.protect });
            if (table.sockets.Count == 0)
                foreach (var s in SocketSteps)
                    table.sockets.Add(new GearSocketStep { chance = s.chance, stones = s.stones, gold = s.gold });

            if (isNew) AssetDatabase.CreateAsset(table, TablePath);
            else EditorUtility.SetDirty(table);
            return table;
        }

        static int FillDrops(ItemDatabase items)
        {
            DropDatabase db = DropTools.EnsureDatabase();
            int added = 0;
            foreach (var r in Drops) added += AddDrop(db, r.table, r.item, r.chance, r.min, r.max);
            foreach (var r in JewelDrops)
                foreach (var j in Jewels)
                {
                    if (r.keys.Length > 0 && System.Array.IndexOf(r.keys, j.key) < 0) continue;
                    for (int tier = 1; tier <= Tiers.Length; tier++)
                        if (r.chance[tier - 1] > 0f) added += AddDrop(db, r.table, JewelName(j.key, tier), r.chance[tier - 1], 1, 1);
                }
            foreach (string p in db.Validate(name => items.Find(name) != null)) Debug.LogWarning("DropDatabase: " + p, db);
            EditorUtility.SetDirty(db);
            return added;
        }

        static int AddDrop(DropDatabase db, string table, string item, float chance, int min, int max)
        {
            DropTable t = db.Find(table);
            if (t == null || t.entries.Exists(e => e != null && e.item == item)) return 0;
            t.entries.Add(new DropEntry { item = item, chance = chance, min = min, max = max });
            return 1;
        }

        static int FillShop()
        {
            var npcs = AssetDatabase.LoadAssetAtPath<NpcDatabase>(NpcPath);
            if (npcs == null)
            {
                Debug.LogWarning("GearUpgradeSetup: không có " + NpcPath + ", đá chưa có chỗ bán.");
                return 0;
            }
            int added = 0;
            foreach (var s in Shop) added += AddShopItem(npcs, Blacksmith, s.option, s.label, s.item, s.price);
            foreach (var j in Jewels)
                added += AddShopItem(npcs, Fortune, "buy_" + JewelName(j.key, 1), "Mua " + j.display + " " + Tiers[0], JewelName(j.key, 1), JewelPrice);
            EditorUtility.SetDirty(npcs);
            return added;
        }

        static int AddShopItem(NpcDatabase npcs, string npc, string option, string label, string item, long price)
        {
            NpcTemplate t = npcs.Find(npc);
            if (t == null)
            {
                Debug.LogWarning("GearUpgradeSetup: không có NPC " + npc + " trong " + NpcPath + ".");
                return 0;
            }
            NpcOption old = t.options.Find(o => o != null && o.name == option);
            if (old != null)
            {
                old.price = price;
                return 0;
            }
            t.options.Add(new NpcOption { name = option, label = label, action = NpcAction.BuyItem, price = price, item = item });
            return 1;
        }

        // viên đá lục giác: viền, mặt trên sáng, mặt phải tối, vệt sáng
        static Sprite DrawStone(string name, Color color)
        {
            int n = PotionItemSetup.N;
            var px = new Color[n * n];
            var c = new Vector2(n * 0.5f, n * 0.5f);
            (float, float)[] body = { (0f, 54f), (38f, 26f), (38f, -24f), (0f, -54f), (-38f, -24f), (-38f, 26f) };
            (float, float)[] top = { (0f, 54f), (38f, 26f), (0f, 6f), (-38f, 26f) };
            (float, float)[] right = { (0f, 6f), (38f, 26f), (38f, -24f), (0f, -54f) };
            (float, float)[] glint = { (-24f, 22f), (-8f, 34f), (-4f, 28f), (-20f, 16f) };

            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, body) - 5f, PotionItemSetup.Outline);
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, body), color);
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, top), Color.Lerp(color, Color.white, 0.4f));
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, right), Color.Lerp(color, Color.black, 0.3f));
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, glint), new Color(1f, 1f, 1f, 0.85f));
            return PotionItemSetup.SaveIcon(px, name);
        }

        // đá khảm: giác cắt tám cạnh, bậc càng cao viên càng to và có thêm ánh sao
        static Sprite DrawJewel(string name, Color color, int tier)
        {
            int n = PotionItemSetup.N;
            var px = new Color[n * n];
            var c = new Vector2(n * 0.5f, n * 0.5f);
            float r = 34f + tier * 6f;
            (float, float)[] body = Ring(r, 8);
            (float, float)[] table = Ring(r * 0.55f, 8);
            (float, float)[] lower = { (-r * 0.92f, -r * 0.38f), (r * 0.92f, -r * 0.38f), (r * 0.38f, -r * 0.92f), (-r * 0.38f, -r * 0.92f) };

            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, body) - 5f, PotionItemSetup.Outline);
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, body), color);
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, lower), Color.Lerp(color, Color.black, 0.3f));
            PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, c, table), Color.Lerp(color, Color.white, 0.45f));
            for (int i = 1; i < tier; i++)
            {
                var star = new Vector2(c.x + r * 0.62f - (i - 1) * r * 1.2f, c.y + r * 0.72f);
                (float, float)[] spark = { (0f, 11f), (2.5f, 2.5f), (11f, 0f), (2.5f, -2.5f), (0f, -11f), (-2.5f, -2.5f), (-11f, 0f), (-2.5f, 2.5f) };
                PotionItemSetup.Paint(px, (x, y) => PotionItemSetup.Polygon(x, y, star, spark), Color.white);
            }
            return PotionItemSetup.SaveIcon(px, name);
        }

        static (float, float)[] Ring(float r, int sides)
        {
            var pts = new (float, float)[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / sides;
                pts[i] = (Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }

        // ================================================================ UI

        static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
        static readonly Color Soft = new Color(0.82f, 0.85f, 0.92f);

        [MenuItem("Tools/Core/UI/Build Gear Upgrade UI")]
        public static void BuildUI()
        {
            if (!QuestUIBuilder.Prepare()) return;

            RegisterPanel(UIID.GearUpgrade, QuestUIBuilder.Save(BuildPanel(), PanelPath).GetComponent<GearUpgradePanel>());
            RegisterPanel(UIID.GearSocket, QuestUIBuilder.Save(BuildSocketPanel(), SocketPanelPath).GetComponent<GearSocketPanel>());
            AddPopupButton();
            UIKitSetup.RestyleCore(PanelPath);
            UIKitSetup.RestyleCore(SocketPanelPath);
            UIKitSetup.RestyleCore(PopupPath);
            AssetDatabase.SaveAssets();
            Debug.Log("GearUpgradeSetup: đã dựng " + PanelPath + " và " + SocketPanelPath + ".");
        }

        static GameObject BuildPanel()
        {
            RectTransform root = QuestUIBuilder.Rect("GearUpgradePanel", null);
            QuestUIBuilder.Stretch(root);
            QuestUIBuilder.Img(root, new Color(0f, 0f, 0f, 0.6f), raycast: true, sliced: false);
            var panel = root.gameObject.AddComponent<GearUpgradePanel>();
            panel.ID = UIID.GearUpgrade;
            panel.BlockPlayerMove = true;

            var center = new Vector2(0.5f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var bottomRight = new Vector2(1f, 0f);

            RectTransform box = QuestUIBuilder.Rect("Box", root);
            QuestUIBuilder.Place(box, center, center, center, Vector2.zero, new Vector2(900f, 560f));
            QuestUIBuilder.Img(box, new Color(0.09f, 0.11f, 0.17f, 1f), raycast: true);

            TMP_Text title = QuestUIBuilder.Txt("Title", box, "Cường hóa trang bị", 32f, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            QuestUIBuilder.Place(title.rectTransform, topLeft, topLeft, topLeft, new Vector2(28f, -12f), new Vector2(600f, 52f));

            TextBtn close = QuestUIBuilder.Btn("Btn_Close", box, "X", new Vector2(52f, 48f), 26f);
            QuestUIBuilder.Place((RectTransform)close.transform, topRight, topRight, topRight, new Vector2(-16f, -14f), new Vector2(52f, 48f));
            QuestLogPanel.Paint(close, new Color(0.55f, 0.22f, 0.22f));

            TextBtn socketTab = QuestUIBuilder.Btn("Btn_SocketTab", box, "Khảm đá", new Vector2(150f, 48f), 21f);
            QuestUIBuilder.Place((RectTransform)socketTab.transform, topRight, topRight, topRight, new Vector2(-80f, -14f), new Vector2(150f, 48f));

            // ---- cột trái: icon + tên
            RectTransform frame = QuestUIBuilder.Rect("IconFrame", box);
            QuestUIBuilder.Place(frame, topLeft, topLeft, topLeft, new Vector2(70f, -110f), new Vector2(180f, 180f));
            Image border = QuestUIBuilder.Img(frame, Color.gray);
            RectTransform inner = QuestUIBuilder.Rect("Inner", frame);
            QuestUIBuilder.Offsets(inner, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            QuestUIBuilder.Img(inner, new Color(0.05f, 0.06f, 0.1f, 1f));
            RectTransform iconRect = QuestUIBuilder.Rect("Icon", frame);
            QuestUIBuilder.Offsets(iconRect, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -24f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text name = QuestUIBuilder.Txt("Name", box, "Tên món", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            QuestUIBuilder.Place(name.rectTransform, topLeft, topLeft, topLeft, new Vector2(20f, -304f), new Vector2(280f, 76f));
            name.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text result = QuestUIBuilder.Txt("Result", box, "", 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            QuestUIBuilder.Place(result.rectTransform, topLeft, topLeft, topLeft, new Vector2(20f, -390f), new Vector2(280f, 120f));
            result.textWrappingMode = TextWrappingModes.Normal;

            // ---- cột phải: chỉ số, chi phí, đá, nút
            TMP_Text stats = QuestUIBuilder.Txt("Stats", box, "", 22f, Soft, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            QuestUIBuilder.Place(stats.rectTransform, topRight, topRight, topRight, new Vector2(-28f, -84f), new Vector2(540f, 150f));
            stats.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text cost = QuestUIBuilder.Txt("Cost", box, "", 22f, Soft, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            QuestUIBuilder.Place(cost.rectTransform, topRight, topRight, topRight, new Vector2(-28f, -242f), new Vector2(540f, 130f));
            cost.textWrappingMode = TextWrappingModes.Normal;

            TextBtn lucky = QuestUIBuilder.Btn("Btn_Lucky", box, "Đá may mắn", new Vector2(264f, 56f), 19f);
            QuestUIBuilder.Place((RectTransform)lucky.transform, bottomRight, bottomRight, bottomRight, new Vector2(-304f, 96f), new Vector2(264f, 56f));
            TextBtn protect = QuestUIBuilder.Btn("Btn_Protect", box, "Đá bảo vệ", new Vector2(264f, 56f), 19f);
            QuestUIBuilder.Place((RectTransform)protect.transform, bottomRight, bottomRight, bottomRight, new Vector2(-28f, 96f), new Vector2(264f, 56f));
            TextBtn upgrade = QuestUIBuilder.Btn("Btn_Upgrade", box, "Cường hóa", new Vector2(540f, 64f), 26f);
            QuestUIBuilder.Place((RectTransform)upgrade.transform, bottomRight, bottomRight, bottomRight, new Vector2(-28f, 20f), new Vector2(540f, 64f));

            var so = new SerializedObject(panel);
            QuestUIBuilder.Ref(so, "titleText", title);
            QuestUIBuilder.Ref(so, "closeButton", close);
            QuestUIBuilder.Ref(so, "border", border);
            QuestUIBuilder.Ref(so, "iconImage", icon);
            QuestUIBuilder.Ref(so, "nameText", name);
            QuestUIBuilder.Ref(so, "statsText", stats);
            QuestUIBuilder.Ref(so, "costText", cost);
            QuestUIBuilder.Ref(so, "luckyButton", lucky);
            QuestUIBuilder.Ref(so, "protectButton", protect);
            QuestUIBuilder.Ref(so, "upgradeButton", upgrade);
            QuestUIBuilder.Ref(so, "resultText", result);
            QuestUIBuilder.Ref(so, "socketTabButton", socketTab);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.gameObject;
        }

        static GameObject BuildSocketPanel()
        {
            RectTransform root = QuestUIBuilder.Rect("GearSocketPanel", null);
            QuestUIBuilder.Stretch(root);
            QuestUIBuilder.Img(root, new Color(0f, 0f, 0f, 0.6f), raycast: true, sliced: false);
            var panel = root.gameObject.AddComponent<GearSocketPanel>();
            panel.ID = UIID.GearSocket;
            panel.BlockPlayerMove = true;

            var center = new Vector2(0.5f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            var rightMid = new Vector2(1f, 0.5f);
            var leftMid = new Vector2(0f, 0.5f);

            RectTransform box = QuestUIBuilder.Rect("Box", root);
            QuestUIBuilder.Place(box, center, center, center, Vector2.zero, new Vector2(900f, 560f));
            QuestUIBuilder.Img(box, new Color(0.09f, 0.11f, 0.17f, 1f), raycast: true);

            TMP_Text title = QuestUIBuilder.Txt("Title", box, "Khoét lỗ, khảm đá", 32f, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            QuestUIBuilder.Place(title.rectTransform, topLeft, topLeft, topLeft, new Vector2(28f, -12f), new Vector2(600f, 52f));

            TextBtn close = QuestUIBuilder.Btn("Btn_Close", box, "X", new Vector2(52f, 48f), 26f);
            QuestUIBuilder.Place((RectTransform)close.transform, topRight, topRight, topRight, new Vector2(-16f, -14f), new Vector2(52f, 48f));
            QuestLogPanel.Paint(close, new Color(0.55f, 0.22f, 0.22f));

            TextBtn upgradeTab = QuestUIBuilder.Btn("Btn_UpgradeTab", box, "Cường hóa", new Vector2(150f, 48f), 21f);
            QuestUIBuilder.Place((RectTransform)upgradeTab.transform, topRight, topRight, topRight, new Vector2(-80f, -14f), new Vector2(150f, 48f));

            RectTransform frame = QuestUIBuilder.Rect("IconFrame", box);
            QuestUIBuilder.Place(frame, topLeft, topLeft, topLeft, new Vector2(70f, -110f), new Vector2(180f, 180f));
            Image border = QuestUIBuilder.Img(frame, Color.gray);
            RectTransform inner = QuestUIBuilder.Rect("Inner", frame);
            QuestUIBuilder.Offsets(inner, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            QuestUIBuilder.Img(inner, new Color(0.05f, 0.06f, 0.1f, 1f));
            RectTransform iconRect = QuestUIBuilder.Rect("Icon", frame);
            QuestUIBuilder.Offsets(iconRect, Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -24f));
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text name = QuestUIBuilder.Txt("Name", box, "Tên món", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            QuestUIBuilder.Place(name.rectTransform, topLeft, topLeft, topLeft, new Vector2(20f, -304f), new Vector2(280f, 76f));
            name.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text result = QuestUIBuilder.Txt("Result", box, "", 22f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            QuestUIBuilder.Place(result.rectTransform, topLeft, topLeft, topLeft, new Vector2(20f, -390f), new Vector2(280f, 120f));
            result.textWrappingMode = TextWrappingModes.Normal;

            var so = new SerializedObject(panel);
            SerializedProperty icons = so.FindProperty("gemIcons");
            SerializedProperty texts = so.FindProperty("gemTexts");
            SerializedProperty buttons = so.FindProperty("gemButtons");
            icons.arraySize = texts.arraySize = buttons.arraySize = ItemStack.MaxSockets;
            for (int i = 0; i < ItemStack.MaxSockets; i++)
            {
                RectTransform row = QuestUIBuilder.Rect("Socket_" + i, box);
                QuestUIBuilder.Place(row, topRight, topRight, topRight, new Vector2(-28f, -84f - i * 150f), new Vector2(540f, 138f));
                QuestUIBuilder.Img(row, new Color(0f, 0f, 0f, 0.25f));

                RectTransform slot = QuestUIBuilder.Rect("Slot", row);
                QuestUIBuilder.Place(slot, leftMid, leftMid, leftMid, new Vector2(14f, 0f), new Vector2(92f, 92f));
                QuestUIBuilder.Img(slot, new Color(0.05f, 0.06f, 0.1f, 1f));
                RectTransform gemRect = QuestUIBuilder.Rect("Gem", slot);
                QuestUIBuilder.Offsets(gemRect, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
                Image gem = gemRect.gameObject.AddComponent<Image>();
                gem.preserveAspect = true;
                gem.raycastTarget = false;

                TMP_Text text = QuestUIBuilder.Txt("Text", row, "", 19f, Soft, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
                QuestUIBuilder.Place(text.rectTransform, leftMid, leftMid, leftMid, new Vector2(118f, 0f), new Vector2(262f, 126f));
                text.textWrappingMode = TextWrappingModes.Normal;

                TextBtn button = QuestUIBuilder.Btn("Btn_Action", row, "Khảm", new Vector2(136f, 56f), 19f);
                QuestUIBuilder.Place((RectTransform)button.transform, rightMid, rightMid, rightMid, new Vector2(-10f, 0f), new Vector2(136f, 56f));

                icons.GetArrayElementAtIndex(i).objectReferenceValue = gem;
                texts.GetArrayElementAtIndex(i).objectReferenceValue = text;
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
            // danh sách chọn đá: phủ lên cả bảng, cuộn được vì túi có thể có đủ 18 loại
            RectTransform picker = QuestUIBuilder.Rect("Picker", box);
            QuestUIBuilder.Stretch(picker);
            QuestUIBuilder.Img(picker, new Color(0f, 0f, 0f, 0.65f), raycast: true, sliced: false);
            RectTransform pickerBox = QuestUIBuilder.Rect("PickerBox", picker);
            QuestUIBuilder.Place(pickerBox, center, center, center, Vector2.zero, new Vector2(660f, 480f));
            QuestUIBuilder.Img(pickerBox, new Color(0.12f, 0.15f, 0.22f, 1f), raycast: true);
            TMP_Text pickerTitle = QuestUIBuilder.Txt("PickerTitle", pickerBox, "", 24f, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            QuestUIBuilder.Place(pickerTitle.rectTransform, topLeft, topLeft, topLeft, new Vector2(20f, -10f), new Vector2(540f, 50f));
            TextBtn pickerCancel = QuestUIBuilder.Btn("Btn_PickerCancel", pickerBox, "X", new Vector2(52f, 48f), 26f);
            QuestUIBuilder.Place((RectTransform)pickerCancel.transform, topRight, topRight, topRight, new Vector2(-12f, -10f), new Vector2(52f, 48f));
            QuestLogPanel.Paint(pickerCancel, new Color(0.55f, 0.22f, 0.22f));
            RectTransform pickerList = QuestUIBuilder.Rect("List", pickerBox);
            QuestUIBuilder.Offsets(pickerList, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -68f));
            QuestUIBuilder.Img(pickerList, new Color(0f, 0f, 0f, 0.25f));
            ScrollRect pickerScroll = QuestUIBuilder.Scroll(pickerList, out RectTransform pickerContent, new RectOffset(8, 8, 8, 8), 8f);
            TextBtn pickerRow = QuestUIBuilder.Btn("Row", pickerContent, "Đá khảm", new Vector2(600f, 58f), 20f);
            QuestUIBuilder.Layout(pickerRow.gameObject, preferredHeight: 58f);
            pickerRow.gameObject.SetActive(false);
            picker.gameObject.SetActive(false);

            QuestUIBuilder.Ref(so, "pickerRoot", picker.gameObject);
            QuestUIBuilder.Ref(so, "pickerTitle", pickerTitle);
            QuestUIBuilder.Ref(so, "pickerCancel", pickerCancel);
            QuestUIBuilder.Ref(so, "pickerScroll", pickerScroll);
            QuestUIBuilder.Ref(so, "pickerRow", pickerRow);
            QuestUIBuilder.Ref(so, "titleText", title);
            QuestUIBuilder.Ref(so, "closeButton", close);
            QuestUIBuilder.Ref(so, "upgradeTabButton", upgradeTab);
            QuestUIBuilder.Ref(so, "border", border);
            QuestUIBuilder.Ref(so, "iconImage", icon);
            QuestUIBuilder.Ref(so, "nameText", name);
            QuestUIBuilder.Ref(so, "resultText", result);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root.gameObject;
        }

        static void RegisterPanel(UIID id, PanelBase panel)
        {
            var config = AssetDatabase.LoadAssetAtPath<PanelConfig>(PanelConfigPath);
            if (config == null)
            {
                Debug.LogWarning("GearUpgradeSetup: không có " + PanelConfigPath + ", nhớ khai panel vào Panel_Config.");
                return;
            }
            var entries = new List<PanelEntry>(config.entries);
            PanelEntry entry = entries.Find(e => e.id == id);
            if (entry == null) entries.Add(entry = new PanelEntry { id = id });
            entry.prefab = panel;
            config.entries = entries.ToArray();
            EditorUtility.SetDirty(config);
        }

        // nút Cường hóa nằm ngay sau nút Nâng cấp, cùng cỡ; có rồi thì thôi. Hàng nút co lại khi đủ 5 nút (xem vũ khí trong túi)
        static void AddPopupButton()
        {
            using (var scope = new PrefabUtility.EditPrefabContentsScope(PopupPath))
            {
                var so = new SerializedObject(scope.prefabContentsRoot.GetComponent<ItemInfoPopup>());
                var upgrade = (TextBtn)so.FindProperty("upgradeButton").objectReferenceValue;
                if (so.FindProperty("gearButton").objectReferenceValue == null)
                {
                    TMP_Text label = upgrade.GetComponentInChildren<TMP_Text>(true);
                    TextBtn gear = QuestUIBuilder.Btn("Btn_Gear", upgrade.transform.parent, "Cường hóa",
                        ((RectTransform)upgrade.transform).sizeDelta, label != null ? label.fontSize : 22f);
                    gear.transform.SetSiblingIndex(upgrade.transform.GetSiblingIndex() + 1);
                    QuestUIBuilder.Ref(so, "gearButton", gear);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                Transform row = upgrade.transform.parent;
                float width = ((RectTransform)upgrade.transform).sizeDelta.x;
                row.GetComponent<HorizontalLayoutGroup>().childControlWidth = true;
                foreach (Transform button in row)
                {
                    LayoutElement e = button.GetComponent<LayoutElement>();
                    if (e == null) e = button.gameObject.AddComponent<LayoutElement>();
                    e.minWidth = 100f;
                    e.preferredWidth = width;
                }
            }
        }
    }
}
