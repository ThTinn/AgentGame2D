using System;

namespace Game.UI
{
    /// <summary>Tên âm thanh UI. Thêm giá trị mới trước None.</summary>
    public enum SFX_Sound
    {
        Click,
        None,
    }

    /// <summary>
    /// Cầu nối âm thanh cho UI: nút và toggle gọi UISound.Play, còn hệ thống audio (khi có) gán Handler.
    /// Nhờ vậy UI không phụ thuộc trực tiếp AudioManager (Assembly-CSharp), AudioManager tự gán Handler lúc khởi tạo.
    /// </summary>
    public static class UISound
    {
        public static Action<SFX_Sound> Handler;

        public static void Play(SFX_Sound sound)
        {
            if (sound == SFX_Sound.None) return;
            Handler?.Invoke(sound);
        }
    }
}
