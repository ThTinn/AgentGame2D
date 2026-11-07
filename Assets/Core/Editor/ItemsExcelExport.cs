using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.Anim.Editor;
using Game.Enemies;
using Game.Items;
using Game.Map;
using Game.Map.Editor;
using Game.Players;
using Game.Skills;
using static Game.EditorTools.XlsxWriter;

namespace Game.EditorTools
{
    /// <summary>
    /// Xuất Docs/Items_Data.xlsx từ chính các asset (ItemDatabase, OptionDatabase, DropDatabase, WeaponDatabase, SkillDatabase) và
    /// <see cref="AnimSetup.Skins"/>: id là id thật, số liệu luôn khớp Unity. Chạy lại sau mỗi lần sửa dữ liệu vật phẩm.
    /// Game design sửa file rồi bấm Build Data From Excel (<see cref="ItemsExcelImport"/>) để ghi ngược vào asset và xuất file server.
    /// </summary>
    public static class ItemsExcelExport
    {
        const string OutPath = "Docs/Core/Items_Data.xlsx";   // tính từ thư mục project (cạnh Assets/)
        const string WeaponPath = "Assets/_Project/Core/Data/Skills/WeaponDatabase.asset";
        const string SkillPath = "Assets/_Project/Core/Data/Skills/SkillDatabase.asset";

        const string ItemSheet = ItemsExcelSchema.ItemSheet, OptionSheet = ItemsExcelSchema.OptionSheet, GearSheet = ItemsExcelSchema.GearSheet,
                     DropSheet = ItemsExcelSchema.DropSheet;
        static string Ref(string sheet) => "'" + sheet + "'";

        /// <summary>Đường dẫn file trên đĩa (Docs/ cạnh Assets/).</summary>
        public static string FilePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), OutPath);

        [MenuItem("Tools/Core/Items/Export Items Excel")]
        public static void Export()
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ChestTools.ItemDatabasePath);
            if (items == null)
            {
                Debug.LogError("ItemsExcelExport: chưa có " + ChestTools.ItemDatabasePath + ".");
                return;
            }
            var options = AssetDatabase.LoadAssetAtPath<OptionDatabase>(OptionSetup.DataPath);
            var weapons = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(WeaponPath);
            var skills = AssetDatabase.LoadAssetAtPath<SkillDatabase>(SkillPath);
            items.ClearCache();

            var x = new XlsxWriter { DocumentTitle = "Core - Dữ liệu vật phẩm" };
            ReadMe(x);
            Items(x, items, weapons);
            Options(x, options);
            GearOptions(x, items, options);
            Drops(x, AssetDatabase.LoadAssetAtPath<DropDatabase>(DropTools.DatabasePath),
                  AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterTools.DatabasePath));
            Slots(x);
            Weapons(x, items, weapons, skills);
            Skins(x, items);
            Enums(x);

            string path = FilePath;
            x.Save(path);
            Debug.Log("ItemsExcelExport: đã ghi " + path + " (" + items.items.Count + " vật phẩm).");
        }

        // ---------------- chữ ----------------

        /// <summary>Phần tiếng Việt của chuỗi song ngữ "VN|EN".</summary>
        static string Vn(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int bar = s.IndexOf('|');
            return bar >= 0 ? s.Substring(0, bar) : s;
        }

        static string En(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int bar = s.IndexOf('|');
            return bar >= 0 ? s.Substring(bar + 1) : "";
        }

        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string RarityText(ItemRarity r) => (int)r + " " + Cap(ItemTemplate.RarityName(r));

        /// <summary>Tên ô trang bị viết hoa (cùng chữ ở sheet Vật phẩm và Ô trang bị để COUNTIF khớp).</summary>
        static string SlotText(EquipSlot s) => s == EquipSlot.None ? "—" : Cap(ItemTemplate.SlotName(s));

        static string AssetPathOf(ItemDatabase items, int iconId)
        {
            Sprite sprite = items.sprites != null ? items.sprites.GetSprite(iconId) : null;
            string p = sprite != null ? AssetDatabase.GetAssetPath(sprite) : "";
            return p.StartsWith("Assets/") ? p.Substring("Assets/".Length) : p;
        }

        static string VariantOf(int setId)
        {
            if (setId == 0) return "A";
            foreach (var s in AnimSetup.Skins) if (s.id == setId) return s.variant;
            return "?";
        }

        static string Meaning(ItemTemplate it, WeaponDatabase weapons)
        {
            switch (it.kind)
            {
                case ItemKind.Gem: return "ItemColor." + (ItemColor)it.value;
                case ItemKind.Key: return "ChestKind." + (ChestKind)it.value;
                case ItemKind.Weapon:
                    WeaponTemplate w = weapons != null ? weapons.Get(it.value) : null;
                    return "WeaponDatabase id " + it.value + " (" + (w != null ? w.name : "không có trong bảng") + ")";
                case ItemKind.Armor: return "EquipSlot." + (EquipSlot)it.value;
                case ItemKind.Skin: return "AnimSet id " + it.value + " (Variant " + VariantOf(it.value) + ")";
                case ItemKind.HpPotion: return "hồi " + it.RestoreHp + " máu (chỉ số ở sheet Option đồ mặc)";
                case ItemKind.MpPotion: return "hồi " + it.RestoreMp + " mana (chỉ số ở sheet Option đồ mặc)";
                case ItemKind.Buff: return "tác dụng " + it.value + " giây (chỉ số ở sheet Option đồ mặc)";
                case ItemKind.Stone: return "GearStone." + (GearStone)it.value;
                case ItemKind.Jewel: return "đá khảm bậc " + it.value + " (chỉ số ở sheet Option đồ mặc)";
                default: return it.value.ToString();
            }
        }

        /// <summary>Hướng dẫn cho game design ở sheet Đọc trước (mục, nội dung).</summary>
        public static readonly (string, string)[] DesignerGuide =
        {
            ("Quy trình", "1) Sửa file này (Excel, Google Sheets tải về .xlsx đều được). 2) Trong Unity bấm Tools/Core/Items/Build Data From Excel. "
                + "3) Tool kiểm tra, ghi vào ItemDatabase / OptionDatabase và xuất Assets/StreamingAssets/Data/items.bin, options.bin, weapons.bin "
                + "để gửi server. Có lỗi thì Console liệt kê sheet / dòng / cột và không ghi gì."),
            ("Sửa được (chữ xanh dương)", "Vật phẩm: id, name, Tên hiển thị, Loại, Phẩm chất, value, maxStack, Icon, Mô tả. "
                + "Option (chỉ số): name, Tên (VN/EN), order, format, capKind, cap, Mô tả (VN/EN). Option đồ mặc: Vật phẩm, Chỉ số, Giá trị. "
                + "Rơi đồ: Bảng, Vật phẩm, Tỉ lệ %, Min, Max."),
            ("Không cần sửa", "Chữ đen / xanh lá là công thức hoặc tham khảo, tool bỏ qua. Sheet Ô trang bị, Vũ khí, Skin, Enum chỉ để tra "
                + "(vũ khí và skin dựng bằng công cụ riêng)."),
            ("Thêm vật phẩm", "Thêm một dòng ngay dưới bảng Vật phẩm (không để dòng trống giữa bảng: bảng dừng ở dòng trống đầu tiên), "
                + "để trống id: tool cấp id mới và ghi lại vào file. Option cho đồ mặc: thêm dòng ở sheet Option đồ mặc."),
            ("Không được", "Đổi tên tiêu đề cột, đổi id của món đã có (id nằm trong dữ liệu lưu của người chơi), dùng lại id cũ. "
                + "Xóa dòng không xóa vật phẩm trong game (tool chỉ cảnh báo). Chỉ số mới (PlayerStat) phải thêm trong code trước."),
            ("Giá trị", "Loại: Gem, Key, Weapon, Armor, Skin. Phẩm chất: 0-4 hoặc Thường / Hiếm / Quý / Sử thi / Huyền thoại. "
                + "value theo loại: Gem = màu 1-6, Key = rương 1-4, Weapon = id vũ khí, Armor = ô 1-4, 6-8, Skin = id bộ anim. "
                + "maxStack: 0 không giới hạn, 1 không xếp chồng (vũ khí, đồ mặc, skin bắt buộc 1). Icon: đường dẫn ảnh tính từ Assets/. "
                + "Số lẻ: file server giữ 2 chữ số thập phân."),
            ("Rơi đồ", "Mỗi dòng một vật phẩm của một bảng; mỗi dòng tung riêng (Tỉ lệ % 0-100, 100 = chắc chắn), trúng thì rơi Min-Max cái "
                + "xuống đất, người chơi bấm F nhặt. Bảng = tên quái (cột name của MonsterDatabase, vd slime_green), quái 3D theo id "
                + "mẫu (mob_<id> theo MonsterSOData: mob_1 Slime_Poly, mob_2 TurtleShell_Poly, mob_5 Beholder_Poly) hoặc rương "
                + "chest_small / chest_iron / chest_gold / chest_large. Chỉ số Tìm đồ của người chơi nhân thêm vào tỉ lệ. "
                + "Sheet Rơi đồ là toàn bộ bảng: bảng / dòng không có trong sheet sẽ bị bỏ khỏi game. Quái không có bảng thì rơi ngọc ngẫu nhiên, "
                + "rương không có bảng thì đổ ngọc / chìa vào túi như cũ."),
            ("Màu chữ", "Xanh dương = dữ liệu (sửa được). Đen = công thức tính trong sheet. Xanh lá = công thức lấy từ sheet khác. "
                + "Nền cột Phẩm chất theo ItemTemplate.RarityTint."),
        };

        // ---------------- sheet ----------------

        static void ReadMe(XlsxWriter x)
        {
            Sheet s = x.AddSheet("Đọc trước", 26, 110);
            s.FreezeRows = 0;
            s.Add(Title("Dữ liệu vật phẩm Core"));
            s.Add(Note("Xuất ngày " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " bằng Tools/Core/Items/Export Items Excel, đọc thẳng từ asset. "
                       + "Tài liệu kèm: Docs/07-Items.md, Docs/03-Stats.md mục 9."));
            s.Add();
            s.Add(H("Mục"), H("Nội dung"));
            void Row(string a, string b) => s.Add(B(a), W(b));
            foreach (var (a, b) in DesignerGuide) Row(a, b);
            Row(ItemSheet, "Mọi vật phẩm trong ItemDatabase. Cột Xếp chồng và Số option là công thức.");
            Row(OptionSheet, "OptionDatabase: chỉ số của nhân vật, dùng chung cho cây nâng cấp và đồ mặc (tên, cách ghi số, trần, thứ tự).");
            Row(GearSheet, "Option của từng vật phẩm (ItemTemplate.options), mỗi dòng một option. Bảng bên phải: tổng nếu mặc mọi món có option, so với trần.");
            Row(DropSheet, "DropDatabase: quái / rương rơi gì, tỉ lệ bao nhiêu. Cột Kỳ vọng / lần là công thức (số cái trung bình mỗi lần hạ / mở).");
            Row("Ô trang bị", "Các ô EquipSlot: vị trí trên bảng hành trang và số vật phẩm mặc được vào ô đó.");
            Row("Vũ khí", "WeaponDatabase và chỉ số gốc của đòn đánh thường (option của item vũ khí). Cột ST % / giây là công thức để so sức mạnh.");
            Row("Skin", "Các màu nhân vật: Variant A mặc định, còn lại là vật phẩm skin, mỗi cái một AnimSet.");
            Row("Enum", "Giá trị cố định của các enum được ghi xuống asset.");
        }

        static void Items(XlsxWriter x, ItemDatabase items, WeaponDatabase weapons)
        {
            Sheet s = x.AddSheet(ItemSheet, 5, 16, 22, 10, 15, 7, 30, 10, 20, 12, 10, 44, 50);
            s.Add(H(ItemsExcelSchema.ItemId), H(ItemsExcelSchema.ItemName), H(ItemsExcelSchema.ItemDisplay), H(ItemsExcelSchema.ItemKind),
                  H(ItemsExcelSchema.ItemRarity), H(ItemsExcelSchema.ItemValue), H("value nghĩa là"), H(ItemsExcelSchema.ItemMaxStack),
                  H("Xếp chồng"), H("Ô trang bị"), H("Số option"), H(ItemsExcelSchema.ItemIcon), H(ItemsExcelSchema.ItemDescription));
            var sorted = new List<ItemTemplate>(items.items);
            sorted.Sort((a, b) => a.id.CompareTo(b.id));
            foreach (ItemTemplate it in sorted)
            {
                int r = s.NextRow;
                s.Add(N(it.id), I(it.name), I(it.Label), I(it.kind.ToString()), Rarity((int)it.rarity, RarityText(it.rarity)),
                      N(it.value), T(Meaning(it, weapons)), N(it.maxStack),
                      F($"IF(H{r}=1,\"Không, mỗi cái 1 ô\",IF(H{r}=0,\"Không giới hạn\",\"Tối đa \"&H{r}&\" / ô\"))"),
                      I(SlotText(it.Slot)),
                      LN($"COUNTIF({Ref(GearSheet)}!$A:$A,B{r})"),
                      T(AssetPathOf(items, it.iconId)), W(it.description));
            }
            int last = s.Rows.Count;
            s.Filter = "A1:M" + last;
            s.Add();
            s.Add(Empty(), B("Tổng số món"), FN($"COUNTA(B2:B{last})"));
            foreach (ItemKind kind in (ItemKind[])Enum.GetValues(typeof(ItemKind)))
            {
                int r = s.NextRow;
                s.Add(Empty(), T(kind.ToString()), FN($"COUNTIF($D$2:$D${last},B{r})"));
            }
        }

        static void Options(XlsxWriter x, OptionDatabase options)
        {
            Sheet s = x.AddSheet(OptionSheet, 5, 14, 15, 20, 20, 7, 14, 17, 7, 24, 52, 52, 12);
            s.Add(H(ItemsExcelSchema.OptId), H(ItemsExcelSchema.OptStat), H(ItemsExcelSchema.OptName), H(ItemsExcelSchema.OptVn),
                  H(ItemsExcelSchema.OptEn), H(ItemsExcelSchema.OptOrder), H(ItemsExcelSchema.OptFormat), H(ItemsExcelSchema.OptCapKind),
                  H(ItemsExcelSchema.OptCap), H("Ghi trần"), H(ItemsExcelSchema.OptDescVn), H(ItemsExcelSchema.OptDescEn), H("Số option dùng"));
            if (options == null)
            {
                s.Add(Note("Chưa có OptionDatabase: chạy Tools/Core/Player/Build Option Database."));
                return;
            }
            options.ClearCache();
            foreach (OptionTemplate o in options.Sorted)
            {
                int r = s.NextRow;
                s.Add(N(o.id), I(o.Stat.ToString()), I(o.name), I(Vn(o.displayName)), I(En(o.displayName)), N(o.order),
                      I(o.format.ToString()), I(o.capKind.ToString()), N(o.HasCap ? o.cap : 0),
                      F($"IF(H{r}=\"None\",\"—\",IF(H{r}=\"Total\",\"tổng tối đa \",IF(H{r}=\"SharedWithWeapon\",\"chung trần với vũ khí \",\"tối đa \"))&I{r}&IF(G{r}=\"Flat\",\"\",\"%\"))"),
                      W(Vn(o.description)), W(En(o.description)),
                      LN($"COUNTIF({Ref(GearSheet)}!$D:$D,B{r})"));
            }
            s.Filter = "A1:M" + s.Rows.Count;
            s.Add();
            s.Add(Empty(), Note("cap = 0 khi không có trần. Trần áp lên tổng cây nâng cấp + đồ mặc; Total còn tính cả tỉ lệ gốc."));
        }

        static void GearOptions(XlsxWriter x, ItemDatabase items, OptionDatabase options)
        {
            Sheet s = x.AddSheet(GearSheet, 14, 22, 12, 14, 20, 8, 14, 3, 14, 20, 9, 9, 11);
            string opt = Ref(OptionSheet), itm = Ref(ItemSheet);
            s.Add(H(ItemsExcelSchema.GearItem), H("Tên hiển thị"), H("Ô"), H(ItemsExcelSchema.GearStat), H("Tên chỉ số"),
                  H(ItemsExcelSchema.GearValue), H("Hiển thị"),
                  Empty(), H("Tổng mọi món"), H("Tên chỉ số"), H("Tổng"), H("Trần"), H("Sau trần"));

            var rows = new List<(string item, PlayerStat stat, float value)>();
            foreach (ItemTemplate it in items.items)
                if (it.options != null)
                    foreach (PlayerStatEffect e in it.options) rows.Add((it.name, e.stat, e.value));

            // chỉ số có mặt, theo thứ tự của bảng option (thiếu bảng thì theo thứ tự gặp)
            var stats = new List<PlayerStat>();
            if (options != null)
                foreach (OptionTemplate o in options.Sorted)
                    foreach (var row in rows)
                        if (row.stat == o.Stat && !stats.Contains(o.Stat)) stats.Add(o.Stat);
            foreach (var row in rows) if (!stats.Contains(row.stat)) stats.Add(row.stat);

            int dataLast = 1 + rows.Count;
            int count = Math.Max(rows.Count, stats.Count);
            for (int i = 0; i < count; i++)
            {
                int r = s.NextRow;
                var line = new List<Cell>();
                if (i < rows.Count)
                {
                    var g = rows[i];
                    string format = $"INDEX({opt}!$G:$G,MATCH(D{r},{opt}!$B:$B,0))";
                    line.Add(I(g.item));
                    line.Add(L($"INDEX({itm}!$C:$C,MATCH(A{r},{itm}!$B:$B,0))"));
                    line.Add(L($"INDEX({itm}!$J:$J,MATCH(A{r},{itm}!$B:$B,0))"));
                    line.Add(I(g.stat.ToString()));
                    line.Add(L($"INDEX({opt}!$D:$D,MATCH(D{r},{opt}!$B:$B,0))"));
                    line.Add(N(g.value));
                    line.Add(F($"IF({format}=\"Flat\",\"+\"&F{r},IF({format}=\"PerSecond\",\"+\"&F{r}&\"/giây\",IF({format}=\"ReducePercent\",\"-\"&F{r}&\"%\",\"+\"&F{r}&\"%\")))"));
                }
                else for (int k = 0; k < 7; k++) line.Add(null);

                if (i < stats.Count)
                {
                    line.Add(null);
                    line.Add(I(stats[i].ToString()));
                    line.Add(L($"INDEX({opt}!$D:$D,MATCH(I{r},{opt}!$B:$B,0))"));
                    line.Add(FN($"SUMIF($D$2:$D${dataLast},I{r},$F$2:$F${dataLast})"));
                    line.Add(LN($"INDEX({opt}!$I:$I,MATCH(I{r},{opt}!$B:$B,0))"));
                    line.Add(FN($"IF(L{r}>0,MIN(K{r},L{r}),K{r})"));
                }
                s.Rows.Add(line);
            }
            if (rows.Count > 0) s.Filter = "A1:G" + dataLast;
            s.Add();
            s.Add(Note("Tổng mọi món = mỗi ô mặc đúng một món, hiện mỗi ô có một món cơ bản nên đây là khi mặc đủ bộ. Trần 0 = không trần; "
                       + "trần áp lên cây nâng cấp + đồ mặc cộng lại."));
        }

        static void Drops(XlsxWriter x, DropDatabase drops, MonsterDatabase monsters)
        {
            Sheet s = x.AddSheet(DropSheet, 16, 22, 16, 22, 15, 9, 6, 6, 12);
            string itm = Ref(ItemSheet);
            s.Add(H(ItemsExcelSchema.DropTable), H("Nguồn"), H(ItemsExcelSchema.DropItem), H("Tên hiển thị"), H("Phẩm chất"),
                  H(ItemsExcelSchema.DropChance), H(ItemsExcelSchema.DropMin), H(ItemsExcelSchema.DropMax), H("Kỳ vọng / lần"));
            if (drops == null)
            {
                s.Add(Note("Chưa có " + DropTools.DatabasePath + ": chạy Tools/Core/Items/Build Drop Tables, hoặc điền bảng rồi Build Data From Excel."));
                return;
            }

            string SourceOf(string table)
            {
                if (table == DropDatabase.MonsterDefaultTable) return "Quái chưa có bảng riêng";
                if (table.StartsWith(DropDatabase.MonsterIdPrefix) && int.TryParse(table.Substring(DropDatabase.MonsterIdPrefix.Length), out int mobId))
                {
                    string label = DropTools.Monster3DLabel(mobId);
                    return "Quái 3D: id mẫu " + mobId + (label != null ? " (" + label + ")" : "");
                }
                foreach (ChestKind k in (ChestKind[])Enum.GetValues(typeof(ChestKind)))
                    if (k != ChestKind.None && DropDatabase.ChestTable(k) == table) return "Rương: " + ItemTemplate.ChestName(k);
                MonsterTemplate m = monsters != null ? monsters.Find(table) : null;
                return m != null ? "Quái: " + (string.IsNullOrEmpty(m.displayName) ? m.name : Vn(m.displayName)) : "(chưa có quái / rương tên này)";
            }

            foreach (DropTable t in drops.tables)
            {
                if (t == null || t.entries == null) continue;
                foreach (DropEntry e in t.entries)
                {
                    if (e == null) continue;
                    int r = s.NextRow;
                    s.Add(I(t.name), T(SourceOf(t.name)), I(e.item),
                          L($"INDEX({itm}!$C:$C,MATCH(C{r},{itm}!$B:$B,0))"),
                          L($"INDEX({itm}!$E:$E,MATCH(C{r},{itm}!$B:$B,0))"),
                          N(e.chance), N(e.min), N(e.max),
                          FN($"F{r}/100*(G{r}+H{r})/2"));
                }
            }
            if (s.Rows.Count > 1) s.Filter = "A1:I" + s.Rows.Count;
            s.Add();
            s.Add(Note("Thêm dòng ngay dưới bảng (không để dòng trống giữa bảng). Một vật phẩm chỉ một dòng trong mỗi bảng. "
                       + "Tỉ lệ thật = Tỉ lệ % x (1 + Tìm đồ % / 100), tối đa 100. Đồ xếp chồng được rơi thành một đống, đồ mặc / vũ khí mỗi cái một đống."));
        }

        static void Slots(XlsxWriter x)
        {
            Sheet s = x.AddSheet("Ô trang bị", 5, 12, 13, 30, 12);
            s.Add(H("id"), H("EquipSlot"), H("Tên"), H("Vị trí trên bảng hành trang"), H("Số món"));
            foreach (EquipSlot slot in (EquipSlot[])Enum.GetValues(typeof(EquipSlot)))
            {
                if (slot == EquipSlot.None) continue;
                int r = s.NextRow;
                s.Add(N((int)slot), I(slot.ToString()), I(SlotText(slot)), T(PositionOf(slot)),
                      LN($"COUNTIF({Ref(ItemSheet)}!$J:$J,C{r})"));
            }
            s.Filter = "A1:E" + s.Rows.Count;
            s.Add();
            s.Add(Empty(), Note("Đồ đang mặc rời khỏi túi; tháo ra thì về túi. Ô trống hiện hình mờ của loại đồ (ô Skin: phi hành gia xanh)."));
        }

        static string PositionOf(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "Cột trái, hàng 1";
                case EquipSlot.Body: return "Cột trái, hàng 2";
                case EquipSlot.Legs: return "Cột trái, hàng 3";
                case EquipSlot.Feet: return "Cột trái, hàng 4";
                case EquipSlot.Weapon: return "Cột phải, hàng 1";
                case EquipSlot.Hands: return "Cột phải, hàng 2";
                case EquipSlot.Neck: return "Cột phải, hàng 3";
                case EquipSlot.Ring: return "Cột phải, hàng 4";
                case EquipSlot.Skin: return "Giữa đáy, dưới tên nhân vật";
                default: return "";
            }
        }

        static void Weapons(XlsxWriter x, ItemDatabase items, WeaponDatabase weapons, SkillDatabase skills)
        {
            Sheet s = x.AddSheet("Vũ khí", 5, 9, 16, 13, 15, 14, 12, 10, 8, 8, 10, 12, 36, 52);
            s.Add(H("id (WeaponDatabase)"), H("name"), H("Vật phẩm"), H("Tên"), H("Phẩm chất"), H("Skill đánh thường"), H("Kiểu đòn"),
                  H("Sát thương %"), H("Hồi (giây)"), H("Tầm (ô)"), H("Mục tiêu"), H("ST % / giây"), H("Icon"), H("Mô tả"));
            if (weapons == null)
            {
                s.Add(Note("Chưa có " + WeaponPath + "."));
                return;
            }
            weapons.ClearCache();
            foreach (WeaponTemplate w in weapons.weapons)
            {
                int r = s.NextRow;
                ItemTemplate item = items.FindWeapon(w.id);
                SkillTemplate skill = skills != null ? skills.Get(w.basicSkill) : null;
                float Base(PlayerStat stat) => item != null ? item.OptionValue(stat) : 0f;
                s.Add(N(w.id), I(w.name), T(item != null ? item.name : "(chưa có vật phẩm)"), I(w.Label),
                      item != null ? Rarity((int)item.rarity, RarityText(item.rarity)) : T("—"),
                      I(skill != null ? skill.name : "?"), I(skill != null ? skill.type.ToString() : "?"),
                      N(Base(PlayerStat.AttackDamage)), N(Base(PlayerStat.AttackCooldown)), N(Base(PlayerStat.AttackRange)),
                      N(Base(PlayerStat.AttackTargets)),
                      FN($"IF(I{r}>0,H{r}/I{r},0)"),
                      T(item != null ? AssetPathOf(items, item.iconId) : ""), W(w.description));
            }
            s.Filter = "A1:N" + s.Rows.Count;
            s.Add();
            s.Add(Empty(), Note("Chỉ xem: chỉ số gốc sửa ở sheet " + GearSheet + ". Mục tiêu 0 = mọi kẻ trong tầm. "
                                + "ST % / giây = sát thương % chia hồi chiêu, chưa tính số mục tiêu, nâng cấp vũ khí và cây."));
        }

        static void Skins(XlsxWriter x, ItemDatabase items)
        {
            Sheet s = x.AddSheet("Skin", 8, 10, 12, 20, 15, 34, 30, 22);
            s.Add(H("Variant"), H("AnimSet id"), H("Vật phẩm"), H("Tên"), H("Phẩm chất"), H("Icon"), H("Atlas"), H("Clip ghép vũ khí"));
            var sets = new List<(int id, string variant, string name)> { (0, "A", "Astronaut (default)") };
            sets.AddRange(AnimSetup.Skins);
            foreach (var set in sets)
            {
                ItemTemplate item = set.id > 0 ? items.Find(ItemKind.Skin, set.id) : null;
                string low = set.variant.ToLowerInvariant();
                s.Add(I(set.variant), N(set.id),
                      T(set.id == 0 ? "— (mặc định, không phải vật phẩm)" : item != null ? item.name : "(chưa có vật phẩm)"),
                      I(item != null ? item.Label : set.name),
                      item != null ? Rarity((int)item.rarity, RarityText(item.rarity)) : T("—"),
                      T("Data/Sprites/ItemIcons/skin_" + low + ".png"), T("Atlas_Character_Astronaut_" + set.variant),
                      T(set.id == 0 ? "Data/Anim/Weapon/" : "Data/Anim/Weapon/" + set.variant + "/"));
            }
            s.Filter = "A1:H" + s.Rows.Count;
            s.Add();
            s.Add(Note("Mọi bộ dựng cùng id tư thế với bộ mặc định, nên đổi skin giữ nguyên tư thế và anim skill. Skin không cộng chỉ số."));
        }

        static void Enums(XlsxWriter x)
        {
            Sheet s = x.AddSheet("Enum", 14, 8, 18, 50);
            s.Add(H("Enum"), H("Giá trị"), H("Tên"), H("Nghĩa"));
            void E<T>(Func<T, string> meaning) where T : Enum
            {
                foreach (T v in (T[])Enum.GetValues(typeof(T)))
                    s.Add(XlsxWriter.T(typeof(T).Name), N(Convert.ToInt32(v)), I(v.ToString()), W(meaning(v)));
            }
            E<ItemKind>(k => Cap(ItemTemplate.KindName(k)));
            foreach (ItemRarity r in (ItemRarity[])Enum.GetValues(typeof(ItemRarity)))
                s.Add(XlsxWriter.T("ItemRarity"), N((int)r), Rarity((int)r, r.ToString()),
                      W(Cap(ItemTemplate.RarityName(r)) + ", màu #" + ColorUtility.ToHtmlStringRGB(ItemTemplate.RarityTint(r))));
            E<ItemColor>(c => c == ItemColor.None ? "không màu" : ItemTemplate.ColorName(c));
            E<ChestKind>(c => c == ChestKind.None ? "không" : ItemTemplate.ChestName(c));
            E<EquipSlot>(e => e == EquipSlot.None ? "không" : ItemTemplate.SlotName(e));
            E<StatFormat>(f => f == StatFormat.Percent ? "\"+5%\"" : f == StatFormat.Flat ? "\"+20\""
                             : f == StatFormat.PerSecond ? "\"+0.5/giây\"" : f == StatFormat.ReducePercent ? "\"-6%\" (hồi chiêu giảm)"
                             : f == StatFormat.Cells ? "\"+0.4 ô\"" : f == StatFormat.Seconds ? "\"+0.25s\"" : "\"+20°\"");
            E<StatCap>(c => c == StatCap.None ? "không trần" : c == StatCap.Bonus ? "phần cộng thêm (cây + đồ) không vượt cap"
                          : c == StatCap.Total ? "gốc + cộng thêm không vượt cap (chí mạng)" : "cộng chung trần với nâng cấp vũ khí (tốc đánh)");
            s.Filter = "A1:D" + s.Rows.Count;
            s.Add();
            s.Add(Note("Giá trị enum được ghi xuống asset: không đổi số của giá trị đã có, chỉ thêm giá trị mới."));
        }
    }
}
