using System;

namespace NetKit
{
    /// <summary>
    /// Kiểu "client làm trước, server ghi đè" cho lệnh hồ sơ (mua bán, trang bị, nâng cấp...):
    /// <list type="number">
    /// <item>Hàm luật của client (ví dụ <c>DemoProfile.Buy</c>) kiểm điều kiện, sửa hồ sơ tại máy để UI đổi ngay, rồi gọi
    /// <c>NetSync&lt;IRemote&gt;.Send?.Buy(...)</c>. Send null khi chưa online, nên cùng hàm luật chạy được cả lúc offline / test.</item>
    /// <item>Server chạy lại đúng luật đó trên hồ sơ của nó, rồi luôn trả PROFILE SYNC, kể cả khi từ chối.</item>
    /// <item>Client áp bản của server trong <c>using (NetSync.ApplyingServer())</c>: hàm luật bị gọi trong lúc này (ví dụ
    /// handler đổi hồ sơ kích hoạt luật khác) không gửi gì lên server.</item>
    /// </list>
    /// Lệnh có kết quả ngẫu nhiên (quay tỉ lệ, rơi đồ) thì client không làm trước: chỉ gửi rồi chờ server.
    /// </summary>
    public static class NetSync
    {
        static int applying;

        /// <summary>Đang áp trạng thái server gửi về: không gửi lệnh lên server.</summary>
        public static bool Applying => applying > 0;

        /// <summary>Mở một đoạn áp trạng thái server. Dùng với using.</summary>
        public static Scope ApplyingServer()
        {
            applying++;
            return new Scope();
        }

        /// <summary>Đóng đoạn đã mở bằng <see cref="ApplyingServer"/>.</summary>
        public readonly struct Scope : IDisposable
        {
            public void Dispose() => applying--;
        }
    }

    /// <summary>
    /// Kênh gửi lệnh hồ sơ lên server, mỗi nhóm lệnh một interface (TRemote). Phía mạng gán <see cref="Remote"/> khi đăng
    /// nhập xong và gán null khi mất kết nối. Hàm luật gọi qua <see cref="Send"/>.
    /// </summary>
    public static class NetSync<TRemote> where TRemote : class
    {
        public static TRemote Remote { get; set; }

        /// <summary>Kênh gửi; null khi chưa online hoặc đang áp trạng thái server. Dùng: NetSync&lt;T&gt;.Send?.X(...).</summary>
        public static TRemote Send => NetSync.Applying ? null : Remote;
    }
}
