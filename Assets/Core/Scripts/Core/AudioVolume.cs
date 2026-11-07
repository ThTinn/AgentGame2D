using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Âm lượng nhạc nền và hiệu ứng (0..1) người chơi chỉnh ở bảng Cài đặt, lưu PlayerPrefs. Nằm ở Core.Data để bảng
    /// Cài đặt (Core.UI) và AudioManager (Assembly-CSharp) cùng đọc / ghi mà không phải tham chiếu nhau.
    /// </summary>
    /// <remarks>
    /// Kéo thanh trượt gọi setter liên tục: chỉ ghi PlayerPrefs trong bộ nhớ, ra đĩa khi gọi <see cref="Save"/>
    /// (đóng bảng Cài đặt, app vào nền / thoát).
    /// </remarks>
    public static class AudioVolume
    {
        const string MusicKey = "Audio.MusicVolume", SfxKey = "Audio.SfxVolume";
        public const float Default = 0.8f;

        static float? music, sfx;

        /// <summary>Báo khi đổi âm lượng (AudioManager nghe để áp ngay).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            music = null;
            sfx = null;
            Changed = null;
        }

        public static float Music
        {
            get => music ??= Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, Default));
            set => Set(ref music, MusicKey, value);
        }

        public static float Sfx
        {
            get => sfx ??= Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, Default));
            set => Set(ref sfx, SfxKey, value);
        }

        static void Set(ref float? field, string key, float value)
        {
            value = Mathf.Clamp01(value);
            if (field.HasValue && Mathf.Approximately(field.Value, value)) return;
            field = value;
            PlayerPrefs.SetFloat(key, value);
            Changed?.Invoke();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
