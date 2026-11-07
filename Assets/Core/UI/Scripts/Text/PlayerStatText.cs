using System;
using System.Collections.Generic;
using Game.Players;

namespace Game.UI
{
    /// <summary>
    /// Tên và cách ghi số của từng chỉ số (<see cref="PlayerStat"/>), đọc từ <see cref="OptionDatabase"/>: bảng cây nâng cấp,
    /// thẻ InfoPlayer và popup thông tin đồ mặc dùng chung nên một chỉ số ở đâu cũng cùng tên, cùng đơn vị, cùng trần.
    /// Chưa có bảng thì hiện tên khóa của enum và ghi dạng "+x%". Gọi lúc vẽ lại theo event (không mỗi frame) nên chuỗi
    /// tạo mới mỗi lần không đáng kể.
    /// </summary>
    // Game: bản GAME_2D để internal; InfoPlayerPanel của Game nằm ở Assembly-CSharp nên cần public
    public static class PlayerStatText
    {
        static readonly List<PlayerStat> order = new List<PlayerStat>();
        static OptionDatabase orderFrom;
        static int orderCount = -1;

        static string L(string s) => Lang.GetSplitText(s);

        static OptionTemplate Option(PlayerStat s) => OptionDatabase.Current != null ? OptionDatabase.Current.Get(s) : null;

        /// <summary>
        /// Thứ tự hiện trong các bảng tổng: theo <see cref="OptionTemplate.order"/> của bảng; chỉ số có trong enum mà bảng
        /// thiếu thì xếp cuối (để không bị ẩn mất).
        /// </summary>
        public static IReadOnlyList<PlayerStat> Order
        {
            get
            {
                OptionDatabase db = OptionDatabase.Current;
                int count = db != null ? db.options.Count : 0;
                if (order.Count > 0 && orderFrom == db && orderCount == count) return order;

                order.Clear();
                if (db != null)
                    foreach (OptionTemplate o in db.Sorted)
                        // IsDefined đòi đúng kiểu nền của enum (byte), đưa int vào là ném lỗi
                        if (o.id >= 0 && o.id <= byte.MaxValue && Enum.IsDefined(typeof(PlayerStat), (byte)o.id)
                            && o.Stat < PlayerStat.RestoreHp && !order.Contains(o.Stat)) order.Add(o.Stat);
                foreach (PlayerStat s in (PlayerStat[])Enum.GetValues(typeof(PlayerStat)))
                    if (s < PlayerStat.RestoreHp && !order.Contains(s)) order.Add(s);
                orderFrom = db;
                orderCount = count;
                return order;
            }
        }

        public static string Label(PlayerStat s)
        {
            OptionTemplate o = Option(s);
            return o != null && !string.IsNullOrEmpty(o.displayName) ? L(o.displayName) : s.ToString();
        }

        /// <summary>Mô tả của option (rỗng nếu bảng không ghi).</summary>
        public static string Description(PlayerStat s)
        {
            OptionTemplate o = Option(s);
            return o != null && !string.IsNullOrEmpty(o.description) ? L(o.description) : "";
        }

        static StatFormat FormatOf(PlayerStat s)
        {
            OptionTemplate o = Option(s);
            return o != null ? o.format : StatFormat.Percent;
        }

        /// <summary>Số kèm dấu và đơn vị theo <see cref="OptionTemplate.format"/>: "+5%", "+20", "+0.5/giây", "-6%".</summary>
        public static string Amount(PlayerStat s, float v)
        {
            switch (FormatOf(s))
            {
                case StatFormat.Flat: return Sign(v) + Math.Abs(v).ToString("0");
                case StatFormat.PerSecond: return Sign(v) + Math.Abs(v).ToString("0.##") + L("/giây|/s");
                case StatFormat.ReducePercent: return (v < 0f ? "+" : "-") + Math.Abs(v).ToString("0.#") + "%";
                case StatFormat.Cells: return Sign(v) + Math.Abs(v).ToString("0.##") + L(" ô| cells");
                case StatFormat.Seconds: return Sign(v) + Math.Abs(v).ToString("0.##") + "s";
                case StatFormat.Degrees: return Sign(v) + Math.Abs(v).ToString("0") + "°";
                default: return Sign(v) + Math.Abs(v).ToString("0.#") + "%";
            }
        }

        static string Sign(float v) => v < 0f ? "-" : "+";

        /// <summary>"Sát thương +5%": một option của nút / của đồ, hay một dòng tổng.</summary>
        public static string Effect(PlayerStat s, float v) => Label(s) + " " + Amount(s, v);

        /// <summary>Ghi chú trần của chỉ số (rỗng nếu không có trần).</summary>
        public static string CapNote(PlayerStat s)
        {
            OptionTemplate o = Option(s);
            if (o == null || !o.HasCap) return "";

            string cap = o.cap.ToString("0.#") + (o.format == StatFormat.Flat ? "" : o.format == StatFormat.PerSecond ? L("/giây|/s") : "%");
            switch (o.capKind)
            {
                case StatCap.Total: return L("tổng tối đa|total max") + " " + cap;
                case StatCap.SharedWithWeapon: return L("chung trần|shared cap") + " " + cap + " " + L("với vũ khí|with weapon");
                default: return L("tối đa|max") + " " + cap;
            }
        }

        /// <summary>Phần của đồ mặc trong một dòng tổng: "đồ +30" (rỗng khi đồ không cộng chỉ số này).</summary>
        public static string GearNote(PlayerStat s, float gear) => gear == 0f ? "" : L("đồ|gear") + " " + Amount(s, gear);

        /// <summary>Tổng thô của một chỉ số trong bảng gộp.</summary>
        public static float Value(in PlayerStats b, PlayerStat s) => b.Get(s);
    }
}
