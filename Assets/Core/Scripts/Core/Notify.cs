using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Kênh thông báo ngắn gửi tới người chơi (toast). Lớp gameplay — Player, ChestSystem... — bắn vào đây thay vì
    /// gọi thẳng <c>PopupMN.Toast</c>, nhờ vậy assembly Game.Gameplay không phải tham chiếu lên assembly Game.UI.
    /// Phía UI (<c>PopupMN</c>) đăng ký nhận trong Awake và hiện popup.
    /// </summary>
    /// <remarks>
    /// Chưa có ai đăng ký (scene không có PopupMN, hoặc test không dựng UI) thì thông báo bị bỏ qua, không lỗi.
    /// </remarks>
    public static class Notify
    {
        /// <summary>(nội dung, số giây hiện). UI đăng ký vào đây.</summary>
        public static event Action<string, float> Toasted;

        /// <summary>Gửi một thông báo trôi. Gọi được từ bất kỳ lớp nào, kể cả khi scene chưa có UI.</summary>
        public static void Toast(string content, float seconds = 2f)
        {
            // server giả lập trong Editor chạy chung process với client: thông báo của server không hiện thẳng lên UI
            if (NetSync.Capture(content)) return;
            Toasted?.Invoke(content, seconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Toasted = null;
    }
}
