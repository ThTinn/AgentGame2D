
namespace Game.Core
{
    /// <summary>Đổi số nguyên sang chuỗi có cache: số lặp lại rất nhiều (sát thương, đếm ngược, huy hiệu) nên không cấp phát chuỗi mới mỗi lần.</summary>
    public static class NumberCache
    {
        static readonly string[] cache = new string[10000];

        public static string Get(long value)
        {
            if (value < 0 || value >= cache.Length) return value.ToString();
            return cache[value] ?? (cache[value] = value.ToString());
        }
    }
}
