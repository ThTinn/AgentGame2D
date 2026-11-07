using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Game.UI
{
    /// <summary>
    /// Định dạng chữ và số cho UI. Bỏ khỏi bản 3D: chuỗi theo độ hiếm/loại tiền/chức vụ bang (chưa có ở 2D) và
    /// ReapplyTMProShaders (chỉ để sửa shader mất khi tải từ AssetBundle).
    /// </summary>
    public static class Txt_Format
    {
        // Định dạng kiểu Việt Nam (1.234.567,5) tự dựng, không dùng CultureInfo("vi-VN") vì một số nền tảng (IL2CPP, WebGL)
        // không có dữ liệu culture và sẽ ném lỗi hoặc cho kết quả khác.
        static readonly NumberFormatInfo number = new NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = new[] { 3 },
            NegativeSign = "-",
        };

        static readonly string[] suffixes = { "", "M", "B" };
        static readonly string[] suffixesK = { "", "K", "M", "B" };

        static readonly Regex emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled);
        static readonly Regex multiColorRegex = new Regex(@"\|(\d+)\|([^|]+)", RegexOptions.Compiled);

        /// <summary>Dưới ngưỡng này hiện đủ chữ số, trên ngưỡng thì rút gọn (M, B).</summary>
        public const float M10 = 10000000;

        #region Rich text

        public static string GetFormattedLabelString(string label, string value) => $"<font-weight=700>{label}</font-weight>: {value}";
        public static string GetFormattedBoldString(string value) => $"<font-weight=700>{value}</font-weight>";
        public static string GetFormattedNormalString(string value) => $"<font-weight=400>{value}</font-weight>";

        #endregion

        #region Số

        // Bản 3D tính isNegative nhưng không dùng, nên số âm mất dấu trừ (Format(-5000) ra "5.000"). Đã sửa.
        public static string FormatK(long value) => Abbreviate(value, 1000, suffixesK);
        public static string FormatK(double value) => Abbreviate(value, 1000, suffixesK);
        public static string Format(long value) => Abbreviate(value, M10, suffixes);
        public static string Format(double value) => Abbreviate(value, M10, suffixes);

        static string Abbreviate(long value, double threshold, string[] suffix)
        {
            int index = 0;
            long temp = Math.Abs(value);
            while (temp > threshold && index < suffix.Length - 1)
            {
                temp /= 1000;
                index++;
            }
            return (value < 0 ? "-" : "") + FormatN2(temp) + suffix[index];
        }

        static string Abbreviate(double value, double threshold, string[] suffix)
        {
            int index = 0;
            double temp = Math.Abs(value);
            while (temp > threshold && index < suffix.Length - 1)
            {
                temp /= 1000;
                index++;
            }
            return (value < 0 ? "-" : "") + FormatN2(temp) + suffix[index];
        }

        /// <summary>Tối đa 2 số lẻ, bỏ số lẻ thừa (1.500 -> "1.500", 1,5 -> "1,5", 1,257 -> "1,25").</summary>
        public static string FormatN2(long value) => value.ToString(PickFormat((double)value), number);
        public static string FormatN2(double value) => value.ToString(PickFormat(value), number);

        static string PickFormat(double value)
        {
            double floored = Math.Floor(value * 100) / 100.0;
            if (floored % 1 == 0) return "N0";
            if ((floored * 10) % 1 == 0) return "N1";
            return "N2";
        }

        /// <summary>+5, -5, và 0 cho số không (bản 3D ra "-0").</summary>
        public static string FormatDelta(long value)
        {
            if (value == 0) return "0";
            return (value > 0 ? "+" : "-") + Format(Math.Abs(value));
        }

        public static string FormatDelta(double value)
        {
            if (value == 0) return "0";
            return (value > 0 ? "+" : "-") + Format(Math.Abs(value));
        }

        #endregion

        #region Thời gian

        /// <summary>elapsed tính theo đơn vị 0.01 giây trong bản 3D (short); trả "mm:ss".</summary>
        public static string FormatSeconds(short elapsed)
        {
            int d = (int)(elapsed * 100.0f);
            int minutes = d / 6000;
            int seconds = (d % 6000) / 100;
            return $"{minutes:00}:{seconds:00}";
        }

        /// <summary>Thời gian còn lại dạng "3 ngày", "5 giờ"... tô xanh; còn dưới 1 phút tô đỏ; hết hạn tô đỏ.</summary>
        public static string GetDateTimeFromSecond(int second)
        {
            if (second <= 0)
            {
                return Colored(Lang.Het_han, TxtColor.COLOR_RED);
            }

            TimeSpan span = TimeSpan.FromSeconds(second);

            if (span.Days > 0) return Colored($" {span.Days} {Lang.ngay}", TxtColor.COLOR_GREEN);
            if (span.Hours > 0) return Colored($" {span.Hours} {Lang.gio}", TxtColor.COLOR_GREEN);
            if (span.Minutes > 0) return Colored($" {span.Minutes} {Lang.phut}", TxtColor.COLOR_GREEN);

            return Colored($" {span.Seconds} {Lang.giay}", TxtColor.COLOR_RED);
        }

        /// <summary>Hạn sử dụng theo mốc Unix giây: "Hạn sử dụng: 3 ngày" hoặc "Hết hạn".</summary>
        public static string GetExpDaterFromSecond(long expireUnixSeconds)
        {
            long left = expireUnixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (left <= 0) return Colored(Lang.Het_han + " ", TxtColor.COLOR_RED);

            string text = Lang.Han_su_dung + ":";
            int days = (int)(left / 86400);
            int hours = (int)(left / 3600);
            int minutes = (int)(left / 60);

            if (days > 0) text += $" {days} {Lang.ngay}";
            else if (hours > 0) text += $" {hours} {Lang.gio}";
            else text += $" {Math.Max(1, minutes)} {Lang.phut}";

            return Colored(text, TxtColor.COLOR_GREEN);
        }

        #endregion

        #region Khác

        static string Colored(string text, sbyte color) => $"<color={TxtColor.GetHex(color)}>{text}</color>";

        public static bool IsValidEmail(string email) => !string.IsNullOrEmpty(email) && emailRegex.IsMatch(email);

        /// <summary>Chuyển "|3|chữ đỏ" (mã màu giữa hai dấu |) thành thẻ &lt;color&gt; của TMP. Mã màu không phải số hợp lệ thì giữ nguyên.</summary>
        public static string ParseMultiColorText(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return multiColorRegex.Replace(input, match =>
            {
                if (!sbyte.TryParse(match.Groups[1].Value, out sbyte code)) return match.Value;
                return $"<color={TxtColor.GetHex(code)}>{match.Groups[2].Value}</color>";
            });
        }

        #endregion
    }
}
