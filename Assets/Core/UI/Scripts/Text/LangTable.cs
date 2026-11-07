using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Bảng chữ đa ngôn ngữ, dựng từ Docs/Lang_Data.xlsx (Tools/Core/Lang/Build Lang From Excel), nằm ở
    /// Resources/<see cref="ResourcePath"/> để <see cref="Lang"/> nạp lúc vào game. Mỗi dòng một key, mỗi ngôn ngữ một giá trị
    /// theo thứ tự <see cref="languages"/> (0 = Tiếng Việt, 1 = English, sau đó là ngôn ngữ thêm trong Excel).
    /// <list type="bullet">
    /// <item><see cref="fields"/>: key là tên field của Lang (vd "Close"), đè lên chữ trong T_Vn / T_Eng.</item>
    /// <item><see cref="texts"/>: key là nguyên chuỗi song ngữ trong code / dữ liệu (vd "Hành trang|Inventory"),
    /// <see cref="Lang.GetSplitText"/> tra theo key này.</item>
    /// </list>
    /// Ô trống = chưa dịch: Lang lấy chữ gốc trong code (ngôn ngữ thêm thì lấy tiếng Anh).
    /// </summary>
    public class LangTable : ScriptableObject
    {
        public const string ResourcePath = "Lang/LangTable";

        [Serializable]
        public class Language
        {
            [Tooltip("Mã ngắn, vd vi, en, zh.")]
            public string code;
            [Tooltip("Tên hiện trong danh sách chọn ngôn ngữ, viết bằng chính ngôn ngữ đó.")]
            public string name;
        }

        [Serializable]
        public class Row
        {
            public string key;
            [Tooltip("Một giá trị mỗi ngôn ngữ, cùng thứ tự với languages. Trống = chưa dịch.")]
            public string[] values;
        }

        public List<Language> languages = new List<Language>();
        public List<Row> fields = new List<Row>();
        public List<Row> texts = new List<Row>();

        Dictionary<string, Row> textMap;

        /// <summary>Chữ của <paramref name="key"/> (chuỗi song ngữ gốc) ở ngôn ngữ <paramref name="lang"/>; false khi chưa dịch.</summary>
        public bool TryText(string key, int lang, out string value)
        {
            if (textMap == null)
            {
                textMap = new Dictionary<string, Row>(texts.Count);
                foreach (Row r in texts)
                    if (r != null && !string.IsNullOrEmpty(r.key)) textMap[r.key] = r;
            }
            value = textMap.TryGetValue(key, out Row row) ? ValueOf(row, lang) : null;
            return !string.IsNullOrEmpty(value);
        }

        public static string ValueOf(Row row, int lang) =>
            row != null && row.values != null && lang >= 0 && lang < row.values.Length ? row.values[lang] : null;

        /// <summary>Bỏ chỉ mục tra cứu: lần tra sau dựng lại. Gọi sau khi ghi lại <see cref="texts"/>.</summary>
        public void ClearCache() => textMap = null;

        void OnValidate() => ClearCache();
    }
}
