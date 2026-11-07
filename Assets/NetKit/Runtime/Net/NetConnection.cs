using System;
using System.Collections.Generic;

namespace NetKit
{
    /// <summary>
    /// Kết nối dùng ở main thread: gửi gói, chia gói nhận được về handler theo mã lệnh, báo sự kiện kết nối, ping định kỳ.
    /// Không phụ thuộc Unity: <see cref="NetClient"/> gọi <see cref="Pump"/> mỗi frame; chạy ngoài Unity (test, bot) thì tự
    /// gọi trong vòng lặp của mình.
    /// </summary>
    public sealed class NetConnection
    {
        public readonly Session Session = new Session();
        readonly Dictionary<int, Action<PacketReader>> handlers = new Dictionary<int, Action<PacketReader>>();
        float pingTimer;

        /// <summary>Giây giữa hai lần PING khi đã kết nối. Server ngắt kết nối im lặng quá 60 giây (chưa đăng nhập).</summary>
        public float PingInterval = 1f;

        /// <summary>
        /// Tạm dừng xử lý gói (gói vẫn nằm trong hàng đợi), ví dụ trong lúc nạp scene map mới: gói MOVE / ADD_PLAYER tới sớm
        /// không bị xử lý khi map chưa có.
        /// </summary>
        public bool Paused;

        public event Action Connected;
        /// <summary>Đang chơi thì mất kết nối.</summary>
        public event Action Disconnected;
        /// <summary>Không kết nối được (sai địa chỉ, server tắt, quá thời gian).</summary>
        public event Action ConnectFailed;
        /// <summary>Gói có mã lệnh chưa đăng ký handler.</summary>
        public event Action<int> Unhandled;
        /// <summary>Sau mỗi lần Pump, kèm số giây từ lần trước. Bộ tải dữ liệu dùng để kiểm đường truyền còn chạy.</summary>
        public event Action<float> Ticked;

        public bool IsConnected => Session.IsConnected;
        public int PingMs => Session.PingMs;

        /// <summary>Đăng ký handler cho một mã lệnh, thay handler cũ nếu có. Handler chạy ở thread gọi Pump.</summary>
        public void On(int cmd, Action<PacketReader> handler) => handlers[cmd] = handler;

        /// <summary>Bỏ handler của mã lệnh.</summary>
        public void Off(int cmd) => handlers.Remove(cmd);

        public void Connect(string host, int port)
        {
            pingTimer = 0;
            Session.Connect(host, port);
        }

        public void Close() => Session.Close();

        /// <summary>Gửi gói cmd, thân ghi bằng write (null = thân rỗng).</summary>
        public void Send(int cmd, Action<PacketWriter> write = null)
        {
            var w = new PacketWriter();
            write?.Invoke(w);
            Session.Send(cmd, w.ToArray());
        }

        /// <summary>Gửi gói cmd có sub-command ở byte đầu thân gói.</summary>
        public void Send(int cmd, byte sub, Action<PacketWriter> write = null)
        {
            var w = new PacketWriter();
            w.WriteByte(sub);
            write?.Invoke(w);
            Session.Send(cmd, w.ToArray());
        }

        /// <summary>
        /// Gọi mỗi frame ở main thread: báo sự kiện kết nối, gửi PING tới hạn, rồi chạy handler cho mọi gói đang chờ (trừ khi
        /// <see cref="Paused"/>). Handler ném lỗi thì chỉ bỏ gói đó.
        /// </summary>
        public void Pump(float deltaTime)
        {
            switch (Session.TakeState())
            {
                case Session.CONNECT_OK: Connected?.Invoke(); break;
                case Session.DISCONNECT: Disconnected?.Invoke(); break;
                case Session.CONNECT_FAIL: ConnectFailed?.Invoke(); break;
            }

            if (Session.IsConnected)
            {
                pingTimer -= deltaTime;
                if (pingTimer <= 0)
                {
                    pingTimer = PingInterval;
                    Send(Cmd.PING, w => w.WriteLong(Session.NowMs));
                }
            }

            while (!Paused && Session.TryReceive(out InPacket p))
            {
                if (!handlers.TryGetValue(p.Cmd, out Action<PacketReader> h))
                {
                    Unhandled?.Invoke(p.Cmd);
                    continue;
                }
                try
                {
                    h(new PacketReader(p.Body));
                }
                catch (Exception e)
                {
                    NetLog.Error("[NetKit] cmd " + p.Cmd + ": " + e);
                }
            }
            Ticked?.Invoke(deltaTime);
        }
    }
}
