using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Game.Anim;
using Game.Enemies;
using Game.Items;
using Game.Map;
using Game.Map.Editor;
using Game.Players;
using Game.Skills;

namespace Game.EditorTools
{
    /// <summary>
    /// Quy trình dữ liệu cho game design: điền Docs/Items_Data.xlsx, bấm Tools/Core/Items/Build Data From Excel.
    /// <list type="number">
    /// <item>Đọc bốn sheet Vật phẩm, Option (chỉ số), Option đồ mặc, Rơi đồ (cột tìm theo tên tiêu đề, xem <see cref="ItemsExcelSchema"/>).</item>
    /// <item>Kiểm tra toàn bộ, báo từng lỗi kèm sheet / dòng / cột. Có lỗi thì dừng, không ghi gì.</item>
    /// <item>Ghi vào ItemDatabase / OptionDatabase / DropDatabase (client dùng).</item>
    /// <item>Xuất items.bin, options.bin, weapons.bin, drops.bin vào Assets/StreamingAssets/Data (gửi cho server), đọc ngược lại để chắc khớp.</item>
    /// <item>Có vật phẩm mới được cấp id thì xuất lại Excel để file có id thật.</item>
    /// </list>
    /// Excel là nguồn của các cột nó có: option đồ mặc và bảng rơi đồ trong Excel thay hẳn bản trong asset. Vật phẩm / option có trong asset
    /// mà Excel thiếu thì giữ nguyên (không xóa: id có thể đang nằm trong dữ liệu lưu của người chơi) và cảnh báo.
    /// </summary>
    public static class ItemsExcelImport
    {
        public const string ServerDataFolder = "Assets/StreamingAssets/Core";
        public const string ItemsFile = "items.bin", OptionsFile = "options.bin", WeaponsFile = "weapons.bin", DropsFile = "drops.bin";
        const string WeaponPath = "Assets/_Project/Core/Data/Skills/WeaponDatabase.asset";
        const string AnimDataPath = "Assets/_Project/Core/Data/Anim/AnimData.asset";

        // ================================================================================================
        // Kết quả đọc
        // ================================================================================================

        public class Issue
        {
            public bool Error;
            public string Where, Message;
            public override string ToString() => (Error ? "✗ " : "! ") + Where + ": " + Message;
        }

        public class ItemRow
        {
            public int Row;
            public int? Id;
            public string Name, Display, Description, Icon;
            public ItemKind Kind;
            public ItemRarity Rarity;
            public int Value, MaxStack;
        }

        public class OptionRow
        {
            public int Row;
            public PlayerStat Stat;
            public string Name, DisplayName, Description;
            public int Order;
            public StatFormat Format;
            public StatCap CapKind;
            public float Cap;
        }

        public class Parsed
        {
            public readonly List<ItemRow> Items = new List<ItemRow>();
            public readonly List<OptionRow> Options = new List<OptionRow>();
            /// <summary>Option đồ mặc theo name vật phẩm (thứ tự như trong sheet).</summary>
            public readonly Dictionary<string, List<PlayerStatEffect>> Gear = new Dictionary<string, List<PlayerStatEffect>>();
            /// <summary>Bảng rơi đồ theo thứ tự gặp trong sheet Rơi đồ (mỗi bảng các dòng theo thứ tự).</summary>
            public readonly List<DropTable> Drops = new List<DropTable>();
            /// <summary>Dòng Excel đầu tiên của mỗi bảng rơi đồ, để báo lỗi lúc ghi.</summary>
            public readonly Dictionary<string, int> DropRows = new Dictionary<string, int>();
            public readonly List<Issue> Issues = new List<Issue>();
            public bool HasOptionSheet, HasGearSheet, HasDropSheet;

            public bool HasErrors
            {
                get
                {
                    foreach (Issue i in Issues) if (i.Error) return true;
                    return false;
                }
            }

            public void Error(string where, string message) => Issues.Add(new Issue { Error = true, Where = where, Message = message });
            public void Warn(string where, string message) => Issues.Add(new Issue { Error = false, Where = where, Message = message });
        }

        // ================================================================================================
        // Đọc + kiểm tra (không đụng asset)
        // ================================================================================================

        /// <summary>Bảng của một sheet: dòng tiêu đề (dòng đầu có ô trùng <paramref name="key"/>) và các dòng tới dòng trống đầu tiên.</summary>
        class Table
        {
            public string Sheet;
            public Dictionary<string, int> Cols = new Dictionary<string, int>();
            public List<XlsxReader.Row> Rows = new List<XlsxReader.Row>();

            public int Col(string header) => Cols.TryGetValue(header, out int c) ? c : -1;
            public string Where(XlsxReader.Row row, string header) =>
                Sheet + " dòng " + row.Number + (header != null && Col(header) >= 0 ? " cột " + XlsxWriter.Col(Col(header)) + " (" + header + ")" : "");
        }

        static Table FindTable(XlsxReader.Sheet sheet, string key)
        {
            int start = -1;
            for (int i = 0; i < sheet.Rows.Count && start < 0; i++)
                foreach (string v in sheet.Rows[i].Cells.Values)
                    if (v.Trim() == key) { start = i; break; }
            if (start < 0) return null;

            var t = new Table { Sheet = sheet.Name };
            foreach (var kv in sheet.Rows[start].Cells)
            {
                string h = kv.Value.Trim();
                if (h.Length > 0 && !t.Cols.ContainsKey(h)) t.Cols[h] = kv.Key;
            }
            // bảng dừng ở dòng trống đầu tiên: phần tổng / ghi chú bên dưới không phải dữ liệu
            int expect = sheet.Rows[start].Number + 1;
            for (int i = start + 1; i < sheet.Rows.Count; i++)
            {
                XlsxReader.Row row = sheet.Rows[i];
                if (row.Number != expect || row.IsEmpty) break;   // dòng bị bỏ qua trong XML = dòng trống
                t.Rows.Add(row);
                expect++;
            }
            return t;
        }

        static bool Require(Table t, Parsed p, params string[] headers)
        {
            bool ok = true;
            foreach (string h in headers)
            {
                if (t.Col(h) >= 0) continue;
                p.Error(t.Sheet, "thiếu cột \"" + h + "\" (đừng đổi tên tiêu đề cột).");
                ok = false;
            }
            return ok;
        }

        static string Cell(Table t, XlsxReader.Row row, string header) => t.Col(header) >= 0 ? row[t.Col(header)].Trim() : "";

        static bool TryNumber(string s, out double v) =>
            double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        static bool TryInt(string s, out int v)
        {
            v = 0;
            if (!TryNumber(s, out double d) || Math.Abs(d - Math.Round(d)) > 1e-6 || d < int.MinValue || d > int.MaxValue) return false;
            v = (int)Math.Round(d);
            return true;
        }

        static bool TryEnum<T>(string s, out T v) where T : struct, Enum =>
            Enum.TryParse(s.Trim(), true, out v) && Enum.IsDefined(typeof(T), v);

        /// <summary>"2 Quý", "2", "Quý", "Rare" đều là Rare.</summary>
        static bool TryRarity(string s, out ItemRarity r)
        {
            r = ItemRarity.Common;
            s = s.Trim();
            if (s.Length == 0) return false;
            int space = s.IndexOf(' ');
            string head = space > 0 ? s.Substring(0, space) : s;
            if (TryInt(head, out int n)) { r = (ItemRarity)n; return Enum.IsDefined(typeof(ItemRarity), r); }
            foreach (ItemRarity v in (ItemRarity[])Enum.GetValues(typeof(ItemRarity)))
                if (string.Equals(s, v.ToString(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, ItemTemplate.RarityName(v), StringComparison.OrdinalIgnoreCase)) { r = v; return true; }
            return false;
        }

        static string Join(string vn, string en) => string.IsNullOrEmpty(en) ? vn : vn + "|" + en;

        public static Parsed Parse(XlsxReader book)
        {
            var p = new Parsed();
            ParseItems(book, p);
            ParseOptions(book, p);
            ParseGear(book, p);
            ParseDrops(book, p);
            return p;
        }

        static void ParseItems(XlsxReader book, Parsed p)
        {
            XlsxReader.Sheet sheet = book.Get(ItemsExcelSchema.ItemSheet);
            Table t = sheet != null ? FindTable(sheet, ItemsExcelSchema.ItemName) : null;
            if (t == null) { p.Error(ItemsExcelSchema.ItemSheet, "không thấy sheet hoặc dòng tiêu đề (cột \"" + ItemsExcelSchema.ItemName + "\")."); return; }
            if (!Require(t, p, ItemsExcelSchema.ItemName, ItemsExcelSchema.ItemKind, ItemsExcelSchema.ItemRarity,
                         ItemsExcelSchema.ItemValue, ItemsExcelSchema.ItemMaxStack)) return;

            var ids = new Dictionary<int, int>();
            var names = new Dictionary<string, int>();
            foreach (XlsxReader.Row row in t.Rows)
            {
                var it = new ItemRow { Row = row.Number };
                it.Name = Cell(t, row, ItemsExcelSchema.ItemName);
                if (it.Name.Length == 0) { p.Error(t.Where(row, ItemsExcelSchema.ItemName), "thiếu name."); continue; }
                if (names.TryGetValue(it.Name, out int firstName))
                    p.Error(t.Where(row, ItemsExcelSchema.ItemName), "name \"" + it.Name + "\" trùng với dòng " + firstName + ".");
                else names[it.Name] = row.Number;
                foreach (char ch in it.Name)
                    if (!(ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '_'))
                    {
                        p.Warn(t.Where(row, ItemsExcelSchema.ItemName), "name nên chỉ gồm a-z, 0-9, _ (\"" + it.Name + "\").");
                        break;
                    }

                string idText = Cell(t, row, ItemsExcelSchema.ItemId);
                if (idText.Length > 0)
                {
                    if (!TryInt(idText, out int id) || id <= 0 || id > short.MaxValue)
                        p.Error(t.Where(row, ItemsExcelSchema.ItemId), "id phải là số nguyên 1.." + short.MaxValue + " (để trống = cấp id mới).");
                    else if (ids.TryGetValue(id, out int firstId))
                        p.Error(t.Where(row, ItemsExcelSchema.ItemId), "id " + id + " trùng với dòng " + firstId + ".");
                    else { ids[id] = row.Number; it.Id = id; }
                }

                bool kindOk = TryEnum(Cell(t, row, ItemsExcelSchema.ItemKind), out it.Kind);
                if (!kindOk)
                    p.Error(t.Where(row, ItemsExcelSchema.ItemKind), "loại \"" + Cell(t, row, ItemsExcelSchema.ItemKind) + "\" không có. Dùng: "
                            + string.Join(", ", Enum.GetNames(typeof(ItemKind))) + ".");
                if (!TryRarity(Cell(t, row, ItemsExcelSchema.ItemRarity), out it.Rarity))
                    p.Error(t.Where(row, ItemsExcelSchema.ItemRarity), "phẩm chất \"" + Cell(t, row, ItemsExcelSchema.ItemRarity)
                            + "\" không đọc được. Dùng 0-4 hoặc Thường / Hiếm / Quý / Sử thi / Huyền thoại.");
                if (!TryInt(Cell(t, row, ItemsExcelSchema.ItemValue), out it.Value))
                    p.Error(t.Where(row, ItemsExcelSchema.ItemValue), "value phải là số nguyên.");
                if (!TryInt(Cell(t, row, ItemsExcelSchema.ItemMaxStack), out it.MaxStack) || it.MaxStack < 0 || it.MaxStack > short.MaxValue)
                    p.Error(t.Where(row, ItemsExcelSchema.ItemMaxStack), "maxStack phải là số nguyên >= 0 (0 = không giới hạn, 1 = không xếp chồng).");

                // cột không có trong file thì null: giữ nguyên giá trị trong asset thay vì ghi đè bằng chuỗi rỗng
                it.Display = t.Col(ItemsExcelSchema.ItemDisplay) >= 0 ? Cell(t, row, ItemsExcelSchema.ItemDisplay) : null;
                it.Description = t.Col(ItemsExcelSchema.ItemDescription) >= 0 ? row[t.Col(ItemsExcelSchema.ItemDescription)] : null;
                it.Icon = t.Col(ItemsExcelSchema.ItemIcon) >= 0 ? Cell(t, row, ItemsExcelSchema.ItemIcon) : null;
                if (it.Display != null && it.Display.Length == 0)
                    p.Warn(t.Where(row, ItemsExcelSchema.ItemDisplay), "chưa có tên hiển thị, game sẽ hiện name.");

                // value theo loại (loại sai thì đã báo ở trên, không báo dây chuyền)
                string whereValue = t.Where(row, ItemsExcelSchema.ItemValue);
                if (kindOk)
                switch (it.Kind)
                {
                    case ItemKind.Gem:
                        if (it.Value <= 0 || !Enum.IsDefined(typeof(ItemColor), (byte)Math.Clamp(it.Value, 0, 255)))
                            p.Error(whereValue, "ngọc: value là ItemColor 1-6 (lam, lục, cam, tím, đỏ, vàng).");
                        break;
                    case ItemKind.Key:
                        if (it.Value <= 0 || !Enum.IsDefined(typeof(ChestKind), (byte)Math.Clamp(it.Value, 0, 255)))
                            p.Error(whereValue, "chìa: value là ChestKind 1-4 (nhỏ, sắt, vàng, lớn).");
                        break;
                    case ItemKind.Armor:
                        if (new ItemTemplate { kind = ItemKind.Armor, value = it.Value }.Slot == EquipSlot.None)
                            p.Error(whereValue, "đồ mặc: value là ô trang bị 1 nón, 2 áo, 3 quần, 4 giày, 6 găng, 7 dây chuyền, 8 nhẫn.");
                        break;
                    case ItemKind.Weapon:
                        if (it.Value <= 0) p.Error(whereValue, "vũ khí: value là id trong WeaponDatabase.");
                        break;
                    case ItemKind.Skin:
                        if (it.Value <= 0) p.Error(whereValue, "skin: value là id bộ anim (1-4 = Variant B-E), 0 là bộ mặc định không phải vật phẩm.");
                        break;
                    case ItemKind.Buff:
                        if (it.Value <= 0) p.Error(whereValue, "thuốc tăng chỉ số: value là số giây tác dụng, phải > 0.");
                        break;
                    case ItemKind.Stone:
                        if (it.Value < (int)GearStone.Upgrade || it.Value > (int)GearStone.Drill)
                            p.Error(whereValue, "đá cường hóa: value là GearStone (1 nâng cấp, 2 may mắn, 3 bảo vệ, 4 khoét lỗ).");
                        break;
                    case ItemKind.Jewel:
                        if (it.Value < 1 || it.Value > 3) p.Error(whereValue, "đá khảm: value là bậc đá, từ 1 đến 3.");
                        break;
                }
                if (kindOk && (it.Kind == ItemKind.Weapon || it.Kind == ItemKind.Armor || it.Kind == ItemKind.Skin) && it.MaxStack != 1)
                    p.Error(t.Where(row, ItemsExcelSchema.ItemMaxStack), it.Kind + " mặc vào ô trang bị nên maxStack phải là 1.");

                p.Items.Add(it);
            }
            if (p.Items.Count == 0) p.Error(ItemsExcelSchema.ItemSheet, "không có dòng vật phẩm nào.");
        }

        static void ParseOptions(XlsxReader book, Parsed p)
        {
            XlsxReader.Sheet sheet = book.Get(ItemsExcelSchema.OptionSheet);
            Table t = sheet != null ? FindTable(sheet, ItemsExcelSchema.OptStat) : null;
            if (t == null) { p.Warn(ItemsExcelSchema.OptionSheet, "không có sheet / bảng: giữ nguyên OptionDatabase."); return; }
            if (!Require(t, p, ItemsExcelSchema.OptStat, ItemsExcelSchema.OptName, ItemsExcelSchema.OptVn, ItemsExcelSchema.OptOrder,
                         ItemsExcelSchema.OptFormat, ItemsExcelSchema.OptCapKind, ItemsExcelSchema.OptCap)) return;
            p.HasOptionSheet = true;

            var seen = new Dictionary<PlayerStat, int>();
            foreach (XlsxReader.Row row in t.Rows)
            {
                var o = new OptionRow { Row = row.Number };
                string stat = Cell(t, row, ItemsExcelSchema.OptStat);
                if (!TryEnum(stat, out o.Stat))
                {
                    p.Error(t.Where(row, ItemsExcelSchema.OptStat), "\"" + stat + "\" không phải PlayerStat. Chỉ số mới phải thêm trong code trước "
                            + "(enum PlayerStat, PlayerStats, chỗ gameplay đọc).");
                    continue;
                }
                if (seen.TryGetValue(o.Stat, out int first)) p.Error(t.Where(row, ItemsExcelSchema.OptStat), stat + " trùng với dòng " + first + ".");
                else seen[o.Stat] = row.Number;

                string idText = Cell(t, row, ItemsExcelSchema.OptId);
                if (idText.Length > 0 && (!TryInt(idText, out int id) || id != (int)o.Stat))
                    p.Error(t.Where(row, ItemsExcelSchema.OptId), "id của " + o.Stat + " là " + (int)o.Stat + " (cố định theo enum), không sửa được.");

                o.Name = Cell(t, row, ItemsExcelSchema.OptName);
                if (o.Name.Length == 0) p.Error(t.Where(row, ItemsExcelSchema.OptName), "thiếu name.");
                string vn = Cell(t, row, ItemsExcelSchema.OptVn), en = Cell(t, row, ItemsExcelSchema.OptEn);
                if (vn.Length == 0) p.Error(t.Where(row, ItemsExcelSchema.OptVn), "thiếu tên tiếng Việt.");
                if (vn.Contains("|") || en.Contains("|")) p.Error(t.Where(row, ItemsExcelSchema.OptVn), "tên không được chứa dấu |.");
                o.DisplayName = Join(vn, en);
                // không có cột mô tả nào (file cũ) thì null: giữ mô tả trong asset
                bool hasDesc = t.Col(ItemsExcelSchema.OptDescVn) >= 0 || t.Col(ItemsExcelSchema.OptDescEn) >= 0;
                o.Description = hasDesc
                    ? Join(t.Col(ItemsExcelSchema.OptDescVn) >= 0 ? row[t.Col(ItemsExcelSchema.OptDescVn)].Trim() : "",
                           t.Col(ItemsExcelSchema.OptDescEn) >= 0 ? row[t.Col(ItemsExcelSchema.OptDescEn)].Trim() : "")
                    : null;

                if (!TryInt(Cell(t, row, ItemsExcelSchema.OptOrder), out o.Order))
                    p.Error(t.Where(row, ItemsExcelSchema.OptOrder), "order phải là số nguyên.");
                if (!TryEnum(Cell(t, row, ItemsExcelSchema.OptFormat), out o.Format))
                    p.Error(t.Where(row, ItemsExcelSchema.OptFormat), "format phải là " + string.Join(", ", Enum.GetNames(typeof(StatFormat))) + ".");
                if (!TryEnum(Cell(t, row, ItemsExcelSchema.OptCapKind), out o.CapKind))
                    p.Error(t.Where(row, ItemsExcelSchema.OptCapKind), "capKind phải là " + string.Join(", ", Enum.GetNames(typeof(StatCap))) + ".");
                string capText = Cell(t, row, ItemsExcelSchema.OptCap);
                if (capText.Length == 0) o.Cap = 0f;
                else if (!TryNumber(capText, out double cap) || cap < 0) p.Error(t.Where(row, ItemsExcelSchema.OptCap), "cap phải là số >= 0.");
                else o.Cap = (float)cap;
                if (o.CapKind != StatCap.None && o.Cap <= 0f)
                    p.Error(t.Where(row, ItemsExcelSchema.OptCap), "capKind " + o.CapKind + " thì cap phải > 0 (hoặc đặt capKind None).");

                p.Options.Add(o);
            }
            var missing = new List<string>();
            foreach (PlayerStat s in (PlayerStat[])Enum.GetValues(typeof(PlayerStat)))
                if (!seen.ContainsKey(s)) missing.Add(s.ToString());
            if (missing.Count > 0)
                p.Warn(ItemsExcelSchema.OptionSheet, "không có trong sheet, giữ nguyên trong asset: " + string.Join(", ", missing) + ".");
        }

        static void ParseGear(XlsxReader book, Parsed p)
        {
            XlsxReader.Sheet sheet = book.Get(ItemsExcelSchema.GearSheet);
            Table t = sheet != null ? FindTable(sheet, ItemsExcelSchema.GearItem) : null;
            if (t == null) { p.Warn(ItemsExcelSchema.GearSheet, "không có sheet / bảng: giữ nguyên option đồ mặc trong asset."); return; }
            if (!Require(t, p, ItemsExcelSchema.GearItem, ItemsExcelSchema.GearStat, ItemsExcelSchema.GearValue)) return;
            p.HasGearSheet = true;

            var names = new Dictionary<string, ItemRow>();
            foreach (ItemRow it in p.Items) names[it.Name] = it;

            foreach (XlsxReader.Row row in t.Rows)
            {
                string item = Cell(t, row, ItemsExcelSchema.GearItem);
                if (item.Length == 0) continue;   // dòng chỉ có bảng tổng bên phải
                string stat = Cell(t, row, ItemsExcelSchema.GearStat);
                string value = Cell(t, row, ItemsExcelSchema.GearValue);

                if (!names.TryGetValue(item, out ItemRow owner))
                {
                    p.Error(t.Where(row, ItemsExcelSchema.GearItem), "vật phẩm \"" + item + "\" không có trong sheet " + ItemsExcelSchema.ItemSheet + ".");
                    continue;
                }
                if (!TryEnum(stat, out PlayerStat s))
                {
                    p.Error(t.Where(row, ItemsExcelSchema.GearStat), "\"" + stat + "\" không phải PlayerStat.");
                    continue;
                }
                if (p.HasOptionSheet && !p.Options.Exists(o => o.Stat == s))
                {
                    p.Error(t.Where(row, ItemsExcelSchema.GearStat), s + " chưa có ở sheet " + ItemsExcelSchema.OptionSheet + ": khai option trước rồi mới gắn vào vật phẩm.");
                    continue;
                }
                if (!TryNumber(value, out double v) || v == 0)
                {
                    p.Error(t.Where(row, ItemsExcelSchema.GearValue), "giá trị phải là số khác 0.");
                    continue;
                }
                if (Math.Abs(v * 100 - Math.Round(v * 100)) > 1e-6)
                    p.Warn(t.Where(row, ItemsExcelSchema.GearValue), "file server giữ 2 chữ số thập phân, " + v + " sẽ thành " + Math.Round(v, 2) + ".");

                if (!p.Gear.TryGetValue(item, out List<PlayerStatEffect> list)) p.Gear[item] = list = new List<PlayerStatEffect>();
                foreach (PlayerStatEffect e in list)
                    if (e.stat == s) p.Error(t.Where(row, ItemsExcelSchema.GearStat), item + " đã có " + s + " ở dòng trên: gộp thành một dòng.");
                list.Add(new PlayerStatEffect(s, (float)v));

                ItemTemplate probe = new ItemTemplate { kind = owner.Kind, value = owner.Value };
                if (!probe.Equippable && !probe.Usable && probe.kind != ItemKind.Jewel)
                    p.Warn(t.Where(row, ItemsExcelSchema.GearItem), item + " không mặc được (cũng không phải thuốc, đá khảm) nên option không bao giờ được tính.");
            }
            foreach (ItemRow it in p.Items)
            {
                p.Gear.TryGetValue(it.Name, out List<PlayerStatEffect> list);
                bool Has(PlayerStat need) => list != null && list.Exists(e => e.stat == need && e.value > 0f);

                if (it.Kind == ItemKind.Weapon)
                {
                    foreach (PlayerStat need in new[] { PlayerStat.AttackDamage, PlayerStat.AttackCooldown, PlayerStat.AttackRange })
                        if (!Has(need)) p.Error(ItemsExcelSchema.GearSheet, it.Name + ": vũ khí phải có một dòng " + need + " lớn hơn 0.");
                }
                else if (it.Kind == ItemKind.HpPotion || it.Kind == ItemKind.MpPotion)
                {
                    PlayerStat need = it.Kind == ItemKind.HpPotion ? PlayerStat.RestoreHp : PlayerStat.RestoreMp;
                    if (!Has(need)) p.Error(ItemsExcelSchema.GearSheet, it.Name + ": bình phải có một dòng " + need + " lớn hơn 0.");
                }
            }
        }

        static void ParseDrops(XlsxReader book, Parsed p)
        {
            XlsxReader.Sheet sheet = book.Get(ItemsExcelSchema.DropSheet);
            Table t = sheet != null ? FindTable(sheet, ItemsExcelSchema.DropTable) : null;
            if (t == null) { p.Warn(ItemsExcelSchema.DropSheet, "không có sheet / bảng: giữ nguyên DropDatabase."); return; }
            if (!Require(t, p, ItemsExcelSchema.DropTable, ItemsExcelSchema.DropItem, ItemsExcelSchema.DropChance,
                         ItemsExcelSchema.DropMin, ItemsExcelSchema.DropMax)) return;
            p.HasDropSheet = true;

            var items = new HashSet<string>();
            foreach (ItemRow it in p.Items) items.Add(it.Name);
            var byName = new Dictionary<string, DropTable>();
            var seen = new Dictionary<string, int>();   // "bảng/vật phẩm" -> dòng

            foreach (XlsxReader.Row row in t.Rows)
            {
                string table = Cell(t, row, ItemsExcelSchema.DropTable);
                string item = Cell(t, row, ItemsExcelSchema.DropItem);
                if (table.Length == 0 && item.Length == 0) continue;
                if (table.Length == 0) { p.Error(t.Where(row, ItemsExcelSchema.DropTable), "thiếu tên bảng (tên quái hoặc chest_small...)."); continue; }
                if (item.Length == 0) { p.Error(t.Where(row, ItemsExcelSchema.DropItem), "thiếu vật phẩm."); continue; }
                bool ok = true;
                if (!items.Contains(item))
                {
                    p.Error(t.Where(row, ItemsExcelSchema.DropItem), "vật phẩm \"" + item + "\" không có trong sheet " + ItemsExcelSchema.ItemSheet + ".");
                    ok = false;
                }
                if (seen.TryGetValue(table + "/" + item, out int first))
                {
                    p.Error(t.Where(row, ItemsExcelSchema.DropItem), item + " đã có trong bảng " + table + " ở dòng " + first + ": gộp thành một dòng.");
                    ok = false;
                }
                else seen[table + "/" + item] = row.Number;

                var e = new DropEntry { item = item };
                if (!TryNumber(Cell(t, row, ItemsExcelSchema.DropChance), out double chance) || chance <= 0 || chance > 100)
                {
                    p.Error(t.Where(row, ItemsExcelSchema.DropChance), "tỉ lệ phải là số trong (0, 100].");
                    ok = false;
                }
                else
                {
                    e.chance = (float)chance;
                    if (Math.Abs(chance * 100 - Math.Round(chance * 100)) > 1e-6)
                        p.Warn(t.Where(row, ItemsExcelSchema.DropChance), "file server giữ 2 chữ số thập phân, " + chance + " sẽ thành " + Math.Round(chance, 2) + ".");
                }
                string minText = Cell(t, row, ItemsExcelSchema.DropMin), maxText = Cell(t, row, ItemsExcelSchema.DropMax);
                if (minText.Length == 0) e.min = 1;
                else if (!TryInt(minText, out e.min) || e.min < 1 || e.min > short.MaxValue)
                {
                    p.Error(t.Where(row, ItemsExcelSchema.DropMin), "Min phải là số nguyên >= 1 (trống = 1).");
                    ok = false;
                }
                if (maxText.Length == 0) e.max = e.min;
                else if (!TryInt(maxText, out e.max) || e.max < e.min || e.max > short.MaxValue)
                {
                    p.Error(t.Where(row, ItemsExcelSchema.DropMax), "Max phải là số nguyên >= Min (trống = Min).");
                    ok = false;
                }
                if (!ok) continue;

                if (!byName.TryGetValue(table, out DropTable target))
                {
                    byName[table] = target = new DropTable { name = table };
                    p.Drops.Add(target);
                    p.DropRows[table] = row.Number;
                }
                target.entries.Add(e);
            }
        }

        // ================================================================================================
        // Ghi vào asset
        // ================================================================================================

        /// <summary>Tra / đăng ký icon: (đường dẫn trong ô Icon, name vật phẩm, commit) -> id SpriteData; -1 = không có ảnh.</summary>
        public delegate int IconResolver(string path, string itemName, bool commit);

        public class Report
        {
            public int Added, Updated, OptionsUpdated, DropTables;
            public readonly List<string> NewIds = new List<string>();
        }

        /// <summary>
        /// Kiểm tra với dữ liệu đang có (vũ khí, bộ anim, icon, đổi name) rồi ghi. <paramref name="commit"/> = false: chỉ kiểm tra.
        /// </summary>
        public static Report Apply(Parsed p, ItemDatabase items, OptionDatabase options, IconResolver icon,
                                   Func<int, bool> weaponExists, Func<int, bool> animSetExists, bool commit)
        {
            var report = new Report();
            items.ClearCache();
            var byId = new Dictionary<int, ItemTemplate>();
            var byName = new Dictionary<string, ItemTemplate>();
            int maxId = 0;
            foreach (ItemTemplate it in items.items)
            {
                byId[it.id] = it;
                if (it.name != null) byName[it.name] = it;
                maxId = Math.Max(maxId, it.id);
            }
            foreach (ItemRow r in p.Items) if (r.Id.HasValue) maxId = Math.Max(maxId, r.Id.Value);

            string Where(ItemRow r, string header) => ItemsExcelSchema.ItemSheet + " dòng " + r.Row + " (" + header + ")";

            var seenIds = new HashSet<int>();
            foreach (ItemRow r in p.Items)
            {
                // id: có thì là danh tính; trống thì theo name; mới thì cấp id kế tiếp
                ItemTemplate target = null;
                int id;
                if (r.Id.HasValue)
                {
                    id = r.Id.Value;
                    if (byId.TryGetValue(id, out target) && target.name != r.Name)
                    {
                        if (byName.TryGetValue(r.Name, out ItemTemplate other) && other != target)
                            p.Error(Where(r, ItemsExcelSchema.ItemName), "name \"" + r.Name + "\" đang là của id " + other.id + " trong asset.");
                        else
                            p.Warn(Where(r, ItemsExcelSchema.ItemName), "đổi name của id " + id + " từ \"" + target.name + "\" thành \"" + r.Name
                                   + "\": code tra theo name (vd FindWeapon, công cụ Editor) có thể không thấy nữa.");
                    }
                    else if (target == null && byName.TryGetValue(r.Name, out ItemTemplate named))
                        p.Error(Where(r, ItemsExcelSchema.ItemId), "name \"" + r.Name + "\" đã có id " + named.id + " trong asset, không đổi id được.");
                }
                else if (byName.TryGetValue(r.Name, out target)) id = target.id;
                else
                {
                    id = ++maxId;
                    report.NewIds.Add(r.Name + " = " + id);
                }
                seenIds.Add(id);

                if (r.Kind == ItemKind.Weapon && weaponExists != null && !weaponExists(r.Value))
                    p.Error(Where(r, ItemsExcelSchema.ItemValue), "WeaponDatabase không có vũ khí id " + r.Value + ".");
                if (r.Kind == ItemKind.Skin && animSetExists != null && !animSetExists(r.Value))
                    p.Warn(Where(r, ItemsExcelSchema.ItemValue), "AnimData chưa có bộ anim " + r.Value + " (chạy Tools/Core/Anim/Rebuild Anim Data + Controller).");

                int iconId = 0;
                if (!string.IsNullOrEmpty(r.Icon))
                {
                    iconId = icon != null ? icon(r.Icon, r.Name, commit) : 0;
                    if (iconId < 0) p.Error(Where(r, ItemsExcelSchema.ItemIcon), "không thấy ảnh \"" + r.Icon + "\" (đường dẫn tính từ Assets/, vd Data/Sprites/ItemIcons/x.png).");
                }
                else if (r.Icon != null) p.Warn(Where(r, ItemsExcelSchema.ItemIcon), "chưa có icon.");

                if (!commit || p.HasErrors) continue;

                bool isNew = target == null;
                if (isNew)
                {
                    target = new ItemTemplate { id = id };
                    items.items.Add(target);
                    byId[id] = target;
                    report.Added++;
                }
                else report.Updated++;
                target.name = r.Name;
                if (r.Display != null) target.displayName = r.Display;
                if (r.Description != null) target.description = r.Description;
                target.kind = r.Kind;
                target.rarity = r.Rarity;
                target.value = r.Value;
                target.maxStack = r.MaxStack;
                if (iconId > 0) target.iconId = iconId;
                if (p.HasGearSheet)
                    target.options = p.Gear.TryGetValue(r.Name, out List<PlayerStatEffect> list)
                        ? new List<PlayerStatEffect>(list) : new List<PlayerStatEffect>();
            }

            foreach (ItemTemplate it in items.items)
                if (!seenIds.Contains(it.id))
                    p.Warn(ItemsExcelSchema.ItemSheet, "id " + it.id + " (" + it.name + ") có trong asset mà không có trong Excel: giữ nguyên, không xóa.");

            if (options != null && p.HasOptionSheet)
            {
                options.ClearCache();
                foreach (OptionRow o in p.Options)
                {
                    if (!commit || p.HasErrors) continue;
                    OptionTemplate target = options.Get(o.Stat);
                    if (target == null)
                    {
                        target = new OptionTemplate { id = (int)o.Stat };
                        options.options.Add(target);
                    }
                    target.name = o.Name;
                    target.displayName = o.DisplayName;
                    if (o.Description != null) target.description = o.Description;
                    target.order = o.Order;
                    target.format = o.Format;
                    target.capKind = o.CapKind;
                    target.cap = o.Cap;
                    report.OptionsUpdated++;
                }
                options.ClearCache();
            }
            else if (options == null && p.HasOptionSheet)
                p.Warn(ItemsExcelSchema.OptionSheet, "chưa có OptionDatabase.asset (Tools/Core/Player/Build Option Database): bỏ qua sheet này.");

            if (commit && !p.HasErrors)
            {
                items.items.Sort((a, b) => a.id.CompareTo(b.id));
                items.schemaVersion = ItemDatabase.CurrentSchema;
                items.ClearCache();
            }
            return report;
        }

        /// <summary>
        /// Ghi bảng rơi đồ (sheet Rơi đồ thay hẳn bảng trong asset). sourceKnown: tên bảng có phải một loại quái / kiểu rương
        /// không (null = không kiểm). <paramref name="commit"/> = false: chỉ kiểm tra.
        /// </summary>
        public static void ApplyDrops(Parsed p, DropDatabase drops, Func<string, bool> sourceKnown, Report report, bool commit)
        {
            if (!p.HasDropSheet || drops == null) return;
            var inSheet = new HashSet<string>();
            foreach (DropTable t in p.Drops)
            {
                inSheet.Add(t.name);
                if (sourceKnown != null && !sourceKnown(t.name))
                    p.Warn(ItemsExcelSchema.DropSheet + " dòng " + p.DropRows[t.name] + " (" + ItemsExcelSchema.DropTable + ")",
                           "\"" + t.name + "\" không phải tên quái (MonsterDatabase) hay chest_small / chest_iron / chest_gold / chest_large: bảng sẽ không bao giờ được tung.");
            }
            foreach (DropTable t in drops.tables)
                if (t != null && !inSheet.Contains(t.name))
                    p.Warn(ItemsExcelSchema.DropSheet, "bảng \"" + t.name + "\" có trong asset mà không có trong Excel: bị bỏ (nguồn đó rơi đồ mặc định).");

            if (!commit || p.HasErrors) return;
            drops.tables = new List<DropTable>(p.Drops);
            drops.schemaVersion = DropDatabase.CurrentSchema;
            report.DropTables = p.Drops.Count;
        }

        /// <summary>Tên bảng rơi đồ có nguồn thật: một kiểu rương (chest_*), bảng chung của quái hoặc một loại quái trong MonsterDatabase.</summary>
        static bool DropSourceKnown(MonsterDatabase monsters, string table)
        {
            if (table == DropDatabase.MonsterDefaultTable) return true;
            foreach (ChestKind k in (ChestKind[])Enum.GetValues(typeof(ChestKind)))
                if (k != ChestKind.None && DropDatabase.ChestTable(k) == table) return true;
            return monsters == null || monsters.Find(table) != null;
        }

        // ================================================================================================
        // Menu
        // ================================================================================================

        [MenuItem("Tools/Core/Items/Build Data From Excel")]
        public static void BuildFromExcel()
        {
            string path = ItemsExcelExport.FilePath;
            if (!File.Exists(path))
            {
                Debug.LogError("ItemsExcelImport: không thấy " + path + ". Chạy Tools/Core/Items/Export Items Excel để tạo file mẫu.");
                return;
            }

            XlsxReader book;
            try { book = XlsxReader.Load(path); }
            catch (Exception e)
            {
                Debug.LogError("ItemsExcelImport: không đọc được " + path + ": " + e.Message);
                return;
            }

            Parsed parsed = Parse(book);
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ChestTools.ItemDatabasePath);
            if (items == null) items = ChestTools.EnsureItems();
            var options = AssetDatabase.LoadAssetAtPath<OptionDatabase>(OptionSetup.DataPath);
            var weapons = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(WeaponPath);
            var anim = AssetDatabase.LoadAssetAtPath<AnimData>(AnimDataPath);
            SpriteData sprites = items.sprites != null ? items.sprites : MapExport.LoadOrCreateAssets().sprites;
            DropDatabase drops = parsed.HasDropSheet ? DropTools.EnsureDatabase() : AssetDatabase.LoadAssetAtPath<DropDatabase>(DropTools.DatabasePath);
            var monsters = AssetDatabase.LoadAssetAtPath<MonsterDatabase>(MonsterTools.DatabasePath);
            Func<string, bool> dropSource = name => DropSourceKnown(monsters, name);
            if (weapons != null) weapons.ClearCache();
            if (anim != null) anim.ClearCache();

            Func<int, bool> weaponExists = weapons != null ? (Func<int, bool>)(id => weapons.Get(id) != null) : null;
            Func<int, bool> setExists = anim != null ? (Func<int, bool>)(id => anim.GetSet(id) != null) : null;
            IconResolver icon = (p, name, commit) => ResolveIcon(sprites, p, name, commit);

            // chạy thử: gom hết lỗi trước khi đụng vào asset
            Report dry = Apply(parsed, items, options, icon, weaponExists, setExists, false);
            ApplyDrops(parsed, drops, dropSource, dry, false);
            if (parsed.HasErrors)
            {
                Debug.LogError(Summary("ItemsExcelImport: Excel có lỗi, chưa ghi gì.", parsed.Issues));
                return;
            }

            // lần chạy thử đã thêm cảnh báo, chạy thật thì bỏ để không in hai lần
            parsed.Issues.RemoveAll(i => !i.Error);
            Report report = Apply(parsed, items, options, icon, weaponExists, setExists, true);
            ApplyDrops(parsed, drops, dropSource, report, true);
            if (drops != null) EditorUtility.SetDirty(drops);
            EditorUtility.SetDirty(items);
            EditorUtility.SetDirty(sprites);
            if (options != null) EditorUtility.SetDirty(options);
            AssetDatabase.SaveAssets();

            string head = "ItemsExcelImport: " + report.Added + " vật phẩm mới, " + report.Updated + " cập nhật, "
                          + report.OptionsUpdated + " option, " + report.DropTables + " bảng rơi đồ." + (report.NewIds.Count > 0 ? " Id mới: " + string.Join(", ", report.NewIds) + "." : "");
            if (parsed.Issues.Count > 0) Debug.LogWarning(Summary(head, parsed.Issues));
            else Debug.Log(head);

            OptionSetup.Validate();
            ExportServerData();

            if (report.NewIds.Count > 0)
            {
                // ghi id vừa cấp vào Excel, để lần sửa sau designer thấy id thật
                try { ItemsExcelExport.Export(); }
                catch (IOException e)
                {
                    Debug.LogWarning("ItemsExcelImport: không ghi lại được Excel (đang mở trong Excel?): " + e.Message
                                     + ". Đóng file rồi chạy Tools/Core/Items/Export Items Excel để có id mới.");
                }
            }
        }

        /// <summary>
        /// Xuất các bảng ra file nhị phân cho server (items.bin, options.bin, weapons.bin, drops.bin) vào StreamingAssets/Data,
        /// rồi đọc ngược lại để chắc file đọc được và khớp asset.
        /// </summary>
        [MenuItem("Tools/Core/Items/Export Server Data")]
        public static void ExportServerData()
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ChestTools.ItemDatabasePath);
            var options = AssetDatabase.LoadAssetAtPath<OptionDatabase>(OptionSetup.DataPath);
            var weapons = AssetDatabase.LoadAssetAtPath<WeaponDatabase>(WeaponPath);
            var drops = AssetDatabase.LoadAssetAtPath<DropDatabase>(DropTools.DatabasePath);
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), ServerDataFolder);
            Directory.CreateDirectory(dir);

            var log = new StringBuilder("Export Server Data -> " + ServerDataFolder + ":\n");
            bool ok = true;

            if (items != null)
            {
                byte[] bytes = items.ToBytes();
                File.WriteAllBytes(Path.Combine(dir, ItemsFile), bytes);
                var back = new List<ItemTemplate>();
                bool same = ItemDatabase.Read(bytes, back, out _, out string error) && back.Count == items.items.Count;
                for (int i = 0; same && i < back.Count; i++)
                    same = back[i].id == items.items[i].id && back[i].name == items.items[i].name
                           && (back[i].options?.Count ?? 0) == (items.items[i].options?.Count ?? 0);
                ok &= same;
                log.Append("  ").Append(ItemsFile).Append(": ").Append(items.items.Count).Append(" vật phẩm, ").Append(bytes.Length).Append(" byte")
                   .Append(same ? ", đọc lại khớp\n" : ", ĐỌC LẠI KHÔNG KHỚP " + error + "\n");
            }
            if (options != null)
            {
                byte[] bytes = options.ToBytes();
                File.WriteAllBytes(Path.Combine(dir, OptionsFile), bytes);
                var back = new List<OptionTemplate>();
                bool same = OptionDatabase.Read(bytes, back, out _, out string error) && back.Count == options.options.Count;
                ok &= same;
                log.Append("  ").Append(OptionsFile).Append(": ").Append(options.options.Count).Append(" option, ").Append(bytes.Length).Append(" byte")
                   .Append(same ? ", đọc lại khớp\n" : ", ĐỌC LẠI KHÔNG KHỚP " + error + "\n");
            }
            if (weapons != null)
            {
                byte[] bytes = weapons.ToBytes();
                File.WriteAllBytes(Path.Combine(dir, WeaponsFile), bytes);
                log.Append("  ").Append(WeaponsFile).Append(": ").Append(weapons.weapons.Count).Append(" vũ khí, ").Append(bytes.Length).Append(" byte\n");
            }
            if (drops != null)
            {
                byte[] bytes = drops.ToBytes();
                File.WriteAllBytes(Path.Combine(dir, DropsFile), bytes);
                var back = new List<DropTable>();
                bool same = DropDatabase.Read(bytes, back, out _, out string error) && back.Count == drops.tables.Count;
                for (int i = 0; same && i < back.Count; i++)
                    same = back[i].name == drops.tables[i].name && back[i].entries.Count == drops.tables[i].entries.Count;
                ok &= same;
                log.Append("  ").Append(DropsFile).Append(": ").Append(drops.tables.Count).Append(" bảng rơi đồ, ").Append(bytes.Length).Append(" byte")
                   .Append(same ? ", đọc lại khớp\n" : ", ĐỌC LẠI KHÔNG KHỚP " + error + "\n");
            }

            AssetDatabase.Refresh();
            if (ok) Debug.Log(log.ToString());
            else Debug.LogError(log.ToString());
        }

        /// <summary>Ảnh ở đường dẫn (tính từ Assets/) -> id trong SpriteData; ảnh chưa có trong bảng thì thêm khi commit.</summary>
        static int ResolveIcon(SpriteData sprites, string path, string itemName, bool commit)
        {
            string full = path.Replace('\\', '/');
            if (!full.StartsWith("Assets/")) full = "Assets/" + full.TrimStart('/');
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(full);
            // Excel ghi trước khi thư mục đổi thành Assets/_Project/Core (đường dẫn cũ dạng "Game2D/Data/Sprites/..."): thử chỗ mới
            if (sprite == null && !full.StartsWith("Assets/_Project/"))
            {
                string rest = full.Substring("Assets/".Length);
                const string OldRoot = "Game" + "2D/";   // ghép chuỗi để công cụ đổi tên hàng loạt không sửa mất
                if (rest.StartsWith(OldRoot)) rest = "Core/" + rest.Substring(OldRoot.Length);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/" + rest);
            }
            if (sprite == null) return -1;
            if (sprites == null) return 0;
            foreach (SpriteRecord rec in sprites.sprites)
                if (rec.sprite == sprite) return rec.id;
            if (!commit) return 0;
            // bản ghi riêng của vật phẩm: đổi ảnh trong Excel thì bản ghi này nhận ảnh mới, id giữ nguyên
            return sprites.Set("icon_" + itemName, sprite, SpriteType.Icon);
        }

        static string Summary(string head, List<Issue> issues)
        {
            var sb = new StringBuilder(head);
            int errors = 0, warnings = 0;
            foreach (Issue i in issues) if (i.Error) errors++; else warnings++;
            sb.Append("  (").Append(errors).Append(" lỗi, ").Append(warnings).Append(" cảnh báo)\n");
            foreach (Issue i in issues) if (i.Error) sb.Append("  ").Append(i).Append('\n');
            foreach (Issue i in issues) if (!i.Error) sb.Append("  ").Append(i).Append('\n');
            return sb.ToString();
        }
    }
}
