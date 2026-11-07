using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Bảng màu chữ theo mã sbyte (server gửi kèm chuỗi). Bản 3D tên ColorUtility, trùng với UnityEngine.ColorUtility
    /// nên đổi thành TxtColor. Bỏ phần màu theo độ hiếm (RARITY) vì 2D chưa có vật phẩm.
    /// </summary>
    public static class TxtColor
    {
        public const sbyte COLOR_BLACK = 1;
        public const sbyte COLOR_WHITE = 2;
        public const sbyte COLOR_RED = 3;
        public const sbyte COLOR_GREEN = 4;
        public const sbyte COLOR_BLUE = 5;
        public const sbyte COLOR_YELLOW = 6;
        public const sbyte COLOR_PURPLE = 7;
        public const sbyte COLOR_CYAN = 8;
        public const sbyte COLOR_BROWN = 9;
        public const sbyte COLOR_ORANGE = 10;
        public const sbyte COLOR_DARK_WHITE = 11;

        public const sbyte COLOR_DARK_RED = 12;
        public const sbyte COLOR_LIGHT_RED = 13;
        public const sbyte COLOR_DARK_GREEN = 14;
        public const sbyte COLOR_LIGHT_GREEN = 15;
        public const sbyte COLOR_DARK_BLUE = 16;
        public const sbyte COLOR_LIGHT_BLUE = 17;
        public const sbyte COLOR_DARK_YELLOW = 18;
        public const sbyte COLOR_LIGHT_YELLOW = 19;
        public const sbyte COLOR_DARK_PURPLE = 20;
        public const sbyte COLOR_LIGHT_PURPLE = 21;
        public const sbyte COLOR_DARK_CYAN = 22;
        public const sbyte COLOR_LIGHT_CYAN = 23;
        public const sbyte COLOR_DARK_BROWN = 24;
        public const sbyte COLOR_LIGHT_BROWN = 25;
        public const sbyte COLOR_DARK_ORANGE = 26;
        public const sbyte COLOR_LIGHT_ORANGE = 27;

        static readonly Dictionary<sbyte, string> hex = new Dictionary<sbyte, string>
        {
            { COLOR_BLACK, "#000000" },
            { COLOR_WHITE, "#FAFCF3" },
            { COLOR_RED, "#dc3926" },
            { COLOR_GREEN, "#317a06" },
            { COLOR_BLUE, "#61a0cf" },
            { COLOR_YELLOW, "#e9ac28" },
            { COLOR_PURPLE, "#41337f" },
            { COLOR_CYAN, "#00d4d4" },
            { COLOR_BROWN, "#683a04" },
            { COLOR_DARK_WHITE, "#686662" },
            { COLOR_ORANGE, "#e68e1f" },

            { COLOR_DARK_RED, "#990000" },
            { COLOR_LIGHT_RED, "#ff6666" },
            { COLOR_DARK_GREEN, "#009900" },
            { COLOR_LIGHT_GREEN, "#66ff66" },
            { COLOR_DARK_BLUE, "#000099" },
            { COLOR_LIGHT_BLUE, "#6666ff" },
            { COLOR_DARK_YELLOW, "#999900" },
            { COLOR_LIGHT_YELLOW, "#ffff66" },
            { COLOR_DARK_PURPLE, "#6b0ca7" },
            { COLOR_LIGHT_PURPLE, "#c270f5" },
            { COLOR_DARK_CYAN, "#009999" },
            { COLOR_LIGHT_CYAN, "#66ffff" },
            { COLOR_DARK_BROWN, "#4d2600" },
            { COLOR_LIGHT_BROWN, "#cc6600" },
            { COLOR_DARK_ORANGE, "#b35900" },
            { COLOR_LIGHT_ORANGE, "#ffa64d" },
        };

        static readonly Dictionary<sbyte, Color> colors = BuildColors();

        static Dictionary<sbyte, Color> BuildColors()
        {
            var map = new Dictionary<sbyte, Color>(hex.Count);
            foreach (KeyValuePair<sbyte, string> pair in hex) map[pair.Key] = HexToColor(pair.Value);
            return map;
        }

        /// <summary>Mã màu dạng "#rrggbb" để chèn vào thẻ &lt;color=...&gt; của TMP. Mã lạ trả về đen.</summary>
        public static string GetHex(sbyte index)
        {
            return hex.TryGetValue(index, out string value) ? value : hex[COLOR_BLACK];
        }

        /// <summary>Mã lạ trả về trắng.</summary>
        public static Color Get(sbyte index)
        {
            return colors.TryGetValue(index, out Color value) ? value : Color.white;
        }

        /// <summary>Nhận "rrggbb", "#rrggbb", "0xrrggbb" hoặc có thêm 2 số alpha ở cuối. Sai định dạng trả về trắng.</summary>
        public static Color HexToColor(string hexString)
        {
            if (string.IsNullOrEmpty(hexString)) return Color.white;

            if (hexString.StartsWith("0x")) hexString = hexString.Substring(2);
            if (!hexString.StartsWith("#")) hexString = "#" + hexString;

            return UnityEngine.ColorUtility.TryParseHtmlString(hexString, out Color color) ? color : Color.white;
        }
    }
}
