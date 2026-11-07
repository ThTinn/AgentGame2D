using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Game.EditorTools
{
    /// <summary>
    /// Đọc giá trị ô của file .xlsx (không cần Excel): chữ ở bảng dùng chung (Excel lưu lại luôn dùng) hay ghi thẳng trong ô
    /// (<see cref="XlsxWriter"/>), số, bool, và giá trị đã tính của ô công thức. Không đọc định dạng, không tính công thức.
    /// File đang mở trong Excel vẫn đọc được (mở chia sẻ đọc / ghi).
    /// </summary>
    public class XlsxReader
    {
        /// <summary>Một sheet: các dòng có dữ liệu, mỗi dòng là ô theo chỉ số cột 0-based (A = 0).</summary>
        public class Sheet
        {
            public string Name;
            public readonly List<Row> Rows = new List<Row>();
        }

        public class Row
        {
            /// <summary>Số dòng trong Excel (1-based), để báo lỗi đúng chỗ.</summary>
            public int Number;
            public readonly Dictionary<int, string> Cells = new Dictionary<int, string>();

            public string this[int col] => col >= 0 && Cells.TryGetValue(col, out string v) ? v : "";

            public bool IsEmpty
            {
                get
                {
                    foreach (string v in Cells.Values) if (!string.IsNullOrWhiteSpace(v)) return false;
                    return true;
                }
            }
        }

        const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

        public readonly List<Sheet> Sheets = new List<Sheet>();

        public Sheet Get(string name)
        {
            foreach (Sheet s in Sheets) if (s.Name == name) return s;
            return null;
        }

        public static XlsxReader Load(string path)
        {
            var book = new XlsxReader();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                List<string> shared = SharedStrings(zip);

                XmlDocument wb = Xml(zip, "xl/workbook.xml");
                XmlDocument rels = Xml(zip, "xl/_rels/workbook.xml.rels");
                if (wb == null || rels == null) throw new InvalidDataException("không phải file .xlsx hợp lệ (thiếu workbook).");

                var targets = new Dictionary<string, string>();
                var relNs = new XmlNamespaceManager(rels.NameTable);
                relNs.AddNamespace("p", PkgRel);
                foreach (XmlElement rel in rels.SelectNodes("//p:Relationship", relNs)) targets[rel.GetAttribute("Id")] = rel.GetAttribute("Target");

                var ns = new XmlNamespaceManager(wb.NameTable);
                ns.AddNamespace("m", Main);
                foreach (XmlElement sh in wb.SelectNodes("//m:sheets/m:sheet", ns))
                {
                    string id = sh.GetAttribute("id", RelNs);
                    if (!targets.TryGetValue(id, out string target)) continue;
                    // Target tương đối với xl/ ("worksheets/sheet1.xml") hoặc tuyệt đối ("/xl/worksheets/sheet1.xml")
                    string part = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
                    XmlDocument doc = Xml(zip, part);
                    if (doc != null) book.Sheets.Add(ReadSheet(sh.GetAttribute("name"), doc, shared));
                }
            }
            return book;
        }

        static XmlDocument Xml(ZipArchive zip, string part)
        {
            ZipArchiveEntry e = zip.GetEntry(part);
            if (e == null) return null;
            var doc = new XmlDocument();
            using (Stream s = e.Open()) doc.Load(s);
            return doc;
        }

        static List<string> SharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            XmlDocument doc = Xml(zip, "xl/sharedStrings.xml");
            if (doc == null) return list;
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", Main);
            foreach (XmlElement si in doc.SelectNodes("//m:si", ns)) list.Add(Text(si, ns));
            return list;
        }

        /// <summary>Chữ của một &lt;si&gt; / &lt;is&gt;: một &lt;t&gt;, hoặc nhiều đoạn &lt;r&gt;&lt;t&gt; (chữ có định dạng), bỏ phần phiên âm.</summary>
        static string Text(XmlElement node, XmlNamespaceManager ns)
        {
            var sb = new StringBuilder();
            foreach (XmlElement t in node.SelectNodes("m:t | m:r/m:t", ns)) sb.Append(t.InnerText);
            return sb.ToString();
        }

        static Sheet ReadSheet(string name, XmlDocument doc, List<string> shared)
        {
            var sheet = new Sheet { Name = name };
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("m", Main);
            int rowNumber = 0;
            foreach (XmlElement rowEl in doc.SelectNodes("//m:sheetData/m:row", ns))
            {
                rowNumber = int.TryParse(rowEl.GetAttribute("r"), out int rn) ? rn : rowNumber + 1;
                var row = new Row { Number = rowNumber };
                int col = -1;
                foreach (XmlElement c in rowEl.SelectNodes("m:c", ns))
                {
                    string at = c.GetAttribute("r");
                    col = string.IsNullOrEmpty(at) ? col + 1 : ColumnIndex(at);
                    string type = c.GetAttribute("t");
                    string value;
                    if (type == "inlineStr")
                    {
                        var isEl = c.SelectSingleNode("m:is", ns) as XmlElement;
                        value = isEl != null ? Text(isEl, ns) : "";
                    }
                    else
                    {
                        string v = c.SelectSingleNode("m:v", ns)?.InnerText ?? "";
                        if (type == "s") value = int.TryParse(v, out int k) && k >= 0 && k < shared.Count ? shared[k] : "";
                        else if (type == "b") value = v == "1" ? "TRUE" : "FALSE";
                        else value = v;   // số, "str" (kết quả công thức dạng chữ), "e" (lỗi #N/A...)
                    }
                    row.Cells[col] = value;
                }
                sheet.Rows.Add(row);
            }
            return sheet;
        }

        /// <summary>"B12" -> 1, "AA3" -> 26.</summary>
        public static int ColumnIndex(string cellRef)
        {
            int n = 0;
            foreach (char ch in cellRef)
            {
                if (ch < 'A' || ch > 'Z') break;
                n = n * 26 + (ch - 'A' + 1);
            }
            return n - 1;
        }
    }
}
