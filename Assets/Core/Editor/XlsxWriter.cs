using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Game.EditorTools
{
    /// <summary>
    /// Ghi file .xlsx (OOXML thuần, không cần Excel) cho các công cụ xuất bảng dữ liệu ra Docs/. Quy ước theo
    /// Docs/PlayerUpgradeTree_Nodes.xlsx: font Arial, tiêu đề chữ trắng trên nền 1F3864, cố định dòng tiêu đề, có bộ lọc;
    /// chữ xanh dương = số nhập tay (dữ liệu từ asset), đen = công thức, xanh lá = công thức lấy từ sheet khác.
    /// Công thức ghi không kèm giá trị tính sẵn; workbook đặt fullCalcOnLoad nên Excel tính lại lúc mở.
    /// </summary>
    public class XlsxWriter
    {
        public enum Style
        {
            None = 0, Header = 1, Text = 2, Input = 3, Formula = 4, Link = 5, Note = 6, Title = 7, Wrap = 8,
            Rarity0 = 9, Rarity1 = 10, Rarity2 = 11, Rarity3 = 12, Rarity4 = 13,
            InputNum = 14, Bold = 15, FormulaNum = 16, LinkNum = 17,
        }

        public class Cell
        {
            public string Text;
            public double? Num;
            public string Formula;
            public Style Style;
        }

        public class Sheet
        {
            public string Name;
            public readonly List<List<Cell>> Rows = new List<List<Cell>>();
            public double[] Widths;
            /// <summary>Số dòng cố định ở đầu (0 = không).</summary>
            public int FreezeRows = 1;
            /// <summary>Vùng bộ lọc, vd "A1:N28"; null = không lọc.</summary>
            public string Filter;

            public Sheet(string name, params double[] widths)
            {
                Name = name;
                Widths = widths;
            }

            /// <summary>Số của dòng sắp thêm (1-based), để viết công thức trỏ vào chính dòng đó.</summary>
            public int NextRow => Rows.Count + 1;

            public void Add(params Cell[] cells) => Rows.Add(new List<Cell>(cells));
        }

        // ---------------- tạo ô ----------------

        public static Cell H(string t) => new Cell { Text = t, Style = Style.Header };
        public static Cell T(string t) => new Cell { Text = t ?? "", Style = Style.Text };
        public static Cell W(string t) => new Cell { Text = t ?? "", Style = Style.Wrap };
        public static Cell I(string t) => new Cell { Text = t ?? "", Style = Style.Input };
        public static Cell N(double v) => new Cell { Num = v, Style = Style.InputNum };
        public static Cell F(string f) => new Cell { Formula = f, Style = Style.Formula };
        public static Cell FN(string f) => new Cell { Formula = f, Style = Style.FormulaNum };
        public static Cell L(string f) => new Cell { Formula = f, Style = Style.Link };
        public static Cell LN(string f) => new Cell { Formula = f, Style = Style.LinkNum };
        public static Cell B(string t) => new Cell { Text = t, Style = Style.Bold };
        public static Cell Note(string t) => new Cell { Text = t, Style = Style.Note };
        public static Cell Title(string t) => new Cell { Text = t, Style = Style.Title };
        public static Cell Empty() => null;
        /// <summary>Ô tô nền theo phẩm chất 0..4 (ItemTemplate.RarityTint, nhạt đi cho dễ đọc).</summary>
        public static Cell Rarity(int rarity, string text) =>
            new Cell { Text = text, Style = Style.Rarity0 + UnityEngine.Mathf.Clamp(rarity, 0, 4) };

        // ---------------- ghi ----------------

        public readonly List<Sheet> Sheets = new List<Sheet>();
        public string DocumentTitle = "";

        public Sheet AddSheet(string name, params double[] widths)
        {
            var s = new Sheet(name, widths);
            Sheets.Add(s);
            return s;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            if (File.Exists(path)) File.Delete(path);
            using (var fs = new FileStream(path, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                void Put(string name, string content)
                {
                    ZipArchiveEntry e = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(content);
                }

                Put("[Content_Types].xml", ContentTypes());
                Put("_rels/.rels", Xml +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>" +
                    "<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>" +
                    "</Relationships>");
                Put("docProps/core.xml", Xml +
                    "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
                    "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
                    "<dc:title>" + Esc(DocumentTitle) + "</dc:title><dc:creator>Core</dc:creator>" +
                    "<dcterms:created xsi:type=\"dcterms:W3CDTF\">" + System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "</dcterms:created></cp:coreProperties>");
                Put("docProps/app.xml", Xml +
                    "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"><Application>Microsoft Excel</Application></Properties>");
                Put("xl/workbook.xml", Workbook());
                Put("xl/_rels/workbook.xml.rels", WorkbookRels());
                Put("xl/styles.xml", StylesXml);
                for (int i = 0; i < Sheets.Count; i++) Put("xl/worksheets/sheet" + (i + 1) + ".xml", SheetXml(Sheets[i]));
            }
        }

        const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

        static string Esc(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        /// <summary>Tên cột Excel của chỉ số 0-based: 0 = A, 26 = AA.</summary>
        public static string Col(int i)
        {
            string s = "";
            for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }

        string ContentTypes()
        {
            var sb = new StringBuilder(Xml);
            sb.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            sb.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            sb.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            sb.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            sb.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            sb.Append("<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>");
            sb.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            for (int i = 0; i < Sheets.Count; i++)
                sb.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i + 1)
                  .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            sb.Append("</Types>");
            return sb.ToString();
        }

        string Workbook()
        {
            var sb = new StringBuilder(Xml);
            sb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<bookViews><workbookView xWindow=\"0\" yWindow=\"0\" windowWidth=\"28200\" windowHeight=\"14520\"/></bookViews><sheets>");
            for (int i = 0; i < Sheets.Count; i++)
                sb.Append("<sheet name=\"").Append(Esc(Sheets[i].Name)).Append("\" sheetId=\"").Append(i + 1).Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
            sb.Append("</sheets><definedNames>");
            for (int i = 0; i < Sheets.Count; i++)
            {
                if (Sheets[i].Filter == null) continue;
                // A1:N28 -> $A$1:$N$28
                string abs = Regex.Replace(Sheets[i].Filter, "([A-Z]+)(\\d+)", "$$$1$$$2");
                sb.Append("<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"").Append(i).Append("\" hidden=\"1\">'")
                  .Append(Esc(Sheets[i].Name)).Append("'!").Append(abs).Append("</definedName>");
            }
            sb.Append("</definedNames><calcPr calcId=\"191029\" fullCalcOnLoad=\"1\"/></workbook>");
            return sb.ToString();
        }

        string WorkbookRels()
        {
            var sb = new StringBuilder(Xml);
            sb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (int i = 0; i < Sheets.Count; i++)
                sb.Append("<Relationship Id=\"rId").Append(i + 1)
                  .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                  .Append(i + 1).Append(".xml\"/>");
            sb.Append("<Relationship Id=\"rId").Append(Sheets.Count + 1)
              .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
            sb.Append("</Relationships>");
            return sb.ToString();
        }

        static string SheetXml(Sheet s)
        {
            var sb = new StringBuilder(Xml);
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
            if (s.FreezeRows > 0)
                sb.Append("<pane ySplit=\"").Append(s.FreezeRows).Append("\" topLeftCell=\"A").Append(s.FreezeRows + 1)
                  .Append("\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/>");
            sb.Append("</sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"15\"/>");
            if (s.Widths != null && s.Widths.Length > 0)
            {
                sb.Append("<cols>");
                for (int i = 0; i < s.Widths.Length; i++)
                    sb.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1).Append("\" width=\"")
                      .Append(s.Widths[i].ToString(CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < s.Rows.Count; r++)
            {
                List<Cell> row = s.Rows[r];
                bool header = row.Count > 0 && row[0] != null && row[0].Style == Style.Header;
                sb.Append("<row r=\"").Append(r + 1).Append('"').Append(header ? " ht=\"32\" customHeight=\"1\"" : "").Append('>');
                for (int c = 0; c < row.Count; c++)
                {
                    Cell cell = row[c];
                    if (cell == null) continue;
                    string at = Col(c) + (r + 1);
                    int st = (int)cell.Style;
                    if (cell.Formula != null)
                        sb.Append("<c r=\"").Append(at).Append("\" s=\"").Append(st).Append("\"><f>").Append(Esc(cell.Formula)).Append("</f></c>");
                    else if (cell.Num != null)
                        sb.Append("<c r=\"").Append(at).Append("\" s=\"").Append(st).Append("\"><v>")
                          .Append(cell.Num.Value.ToString(CultureInfo.InvariantCulture)).Append("</v></c>");
                    else
                        sb.Append("<c r=\"").Append(at).Append("\" s=\"").Append(st).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                          .Append(Esc(cell.Text)).Append("</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");
            if (s.Filter != null) sb.Append("<autoFilter ref=\"").Append(s.Filter).Append("\"/>");
            sb.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>");
            return sb.ToString();
        }

        // font: 0 thường, 1 tiêu đề trắng, 2 xanh dương (nhập tay), 3 xanh lá (sheet khác), 4 ghi chú nghiêng, 5 tiêu đề trang,
        // 6 đậm, 7 đen (công thức). Fill: 2 nền tiêu đề, 3..7 năm phẩm chất. numFmt 164 "0.##", 165 "0.##" và số 0 ghi "—".
        // Thứ tự cellXfs phải khớp enum Style.
        const string StylesXml = Xml +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"2\"><numFmt numFmtId=\"164\" formatCode=\"0.##\"/><numFmt numFmtId=\"165\" formatCode=\"0.##;-0.##;&quot;—&quot;\"/></numFmts>" +
            "<fonts count=\"8\">" +
            "<font><sz val=\"11\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><sz val=\"11\"/><color rgb=\"FF0000FF\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><sz val=\"11\"/><color rgb=\"FF008000\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><i/><sz val=\"9\"/><color rgb=\"FF595959\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><b/><sz val=\"14\"/><color rgb=\"FF1F3864\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "<font><sz val=\"11\"/><color rgb=\"FF000000\"/><name val=\"Arial\"/><family val=\"2\"/></font>" +
            "</fonts>" +
            "<fills count=\"8\">" +
            "<fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1F3864\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEDEDED\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9F2D9\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD6E4FF\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8D9FF\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFE0C2\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left/><right/><top/><bottom style=\"thin\"><color rgb=\"FFD9D9D9\"/></bottom><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"18\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"7\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"3\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"4\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "<xf numFmtId=\"0\" fontId=\"5\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"5\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"6\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"7\" borderId=\"1\" xfId=\"0\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"164\" fontId=\"2\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"0\" fontId=\"6\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"165\" fontId=\"7\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "<xf numFmtId=\"165\" fontId=\"3\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\"/></xf>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
    }
}
