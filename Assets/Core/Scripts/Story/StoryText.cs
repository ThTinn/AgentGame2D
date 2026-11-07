using System;

namespace Game.Story
{
    /// <summary>
    /// Dịch chữ cho phần cốt truyện ở lớp gameplay. Game.Gameplay không được gọi lên Game.UI (nơi có Lang), nên UI gán
    /// <see cref="Localize"/> = Lang.GetSplitText lúc khởi động (NpcDialog.Init). Chưa gán (test, server) thì lấy phần
    /// tiếng Việt (trước dấu '|').
    /// <list type="bullet">
    /// <item><see cref="T"/>: chữ trong dữ liệu (tên NPC, tên nhiệm vụ, lời thoại...) — tra bản dịch trong Lang_Data.xlsx.</item>
    /// <item><see cref="L"/>: chữ cố định trong code, viết song ngữ "Tiếng Việt|English", có thể có chỗ chèn {0}.
    /// Tên hàm là L để Tools/Core/Lang (LangExcel) tự gom các chuỗi này vào Excel.</item>
    /// </list>
    /// </summary>
    public static class StoryText
    {
        public static Func<string, string> Localize;

        public static string T(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return Localize != null ? Localize(s) : First(s);
        }

        public static string L(string bilingual, params object[] args)
        {
            string text = T(bilingual);
            if (args == null || args.Length == 0) return text;
            try
            {
                return string.Format(text, args);
            }
            catch (FormatException)
            {
                // bản dịch ghi sai chỗ chèn {0}: dùng chữ gốc
                return string.Format(First(bilingual), args);
            }
        }

        static string First(string s)
        {
            int i = s.IndexOf('|');
            return i < 0 ? s : s.Substring(0, i);
        }
    }
}
