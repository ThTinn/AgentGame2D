using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace NetKit
{
    /// <summary>Một gói đã nhận: mã lệnh và thân gói.</summary>
    public readonly struct InPacket
    {
        public readonly int Cmd;
        public readonly byte[] Body;

        public InPacket(int cmd, byte[] body)
        {
            Cmd = cmd;
            Body = body;
        }
    }

    /// <summary>
    /// Kết nối TCP ở mức khung gói, không biết gì về luật game. Khung gói: int big-endian, 9 bit cao là mã lệnh (có dấu,
    /// -256..255), 23 bit thấp là độ dài thân (tối đa 8 MB), rồi tới thân gói.
    /// <list type="bullet">
    /// <item>Mỗi lần <see cref="Connect"/> chạy một thread ghi (kết nối rồi gom gói gửi) và một thread đọc. Gói nhận được xếp vào
    /// hàng đợi, main thread lấy ra bằng <see cref="TryReceive"/>: handler luôn chạy ở main thread, gọi API Unity được.</item>
    /// <item>Trạng thái kết nối (<see cref="CONNECT_OK"/>, <see cref="DISCONNECT"/>, <see cref="CONNECT_FAIL"/>) cũng chuyển về
    /// main thread qua <see cref="TakeState"/>.</item>
    /// <item>Mỗi lần kết nối tăng thế hệ: thread của kết nối cũ thấy khác thế hệ thì tự thoát, không báo mất kết nối. Không dùng
    /// Thread.Abort (IL2CPP không có).</item>
    /// <item>PING được trả lời ngay trên thread đọc để đo độ trễ chính xác, không xếp hàng.</item>
    /// </list>
    /// Chưa mã hóa: dùng tài khoản thật trên Internet thì phải bọc TLS (SslStream) quanh stream ở <see cref="DoConnect"/>.
    /// </summary>
    public sealed class Session
    {
        public const int CONNECT_OK = 1, DISCONNECT = 2, CONNECT_FAIL = 3;
        public const int MaxBody = (1 << 23) - 1;
        const int ConnectTimeoutMs = 10000;

        // 0: chưa kết nối, 1: đang kết nối, 2: đã kết nối. Ghi từ thread mạng, đọc ở main thread
        volatile int connected;
        int state;              // đọc / ghi qua Interlocked, Volatile
        volatile int generation;
        readonly object gate = new object();

        TcpClient client;
        NetworkStream stream;
        string host;
        int port;

        readonly ConcurrentQueue<InPacket> sending = new ConcurrentQueue<InPacket>();
        readonly ConcurrentQueue<InPacket> received = new ConcurrentQueue<InPacket>();
        readonly AutoResetEvent sendSignal = new AutoResetEvent(false);

        static readonly Stopwatch clock = Stopwatch.StartNew();
        long pingSentAt = -1;

        /// <summary>Độ trễ khứ hồi của PING gần nhất (ms), -1 khi chưa đo.</summary>
        public volatile int PingMs = -1;

        long serverOffset, totalRead;

        /// <summary>Giờ server (ms) gửi trong PING gần nhất trừ giờ máy lúc nhận. 0 khi chưa có.</summary>
        public long ServerOffsetMs => Interlocked.Read(ref serverOffset);

        /// <summary>Tổng byte thân gói đã nhận, để hiện tốc độ tải.</summary>
        public long TotalRead => Interlocked.Read(ref totalRead);

        public bool IsConnected => connected == 2;
        public bool IsConnecting => connected == 1;

        /// <summary>Mốc thời gian đơn điệu (ms) dùng cho PING.</summary>
        public static long NowMs => clock.ElapsedMilliseconds;

        /// <summary>Mở kết nối ở thread nền. Đang kết nối hoặc đã kết nối thì bỏ qua (gọi Close trước).</summary>
        public void Connect(string host, int port)
        {
            if (connected != 0) return;
            this.host = host;
            this.port = port;
            connected = 1;
            int gen = ++generation;
            new Thread(() => Run(gen)) { IsBackground = true, Name = "NetKit write" }.Start();
        }

        /// <summary>Đóng kết nối, bỏ mọi gói chưa gửi / chưa xử lý. Không báo DISCONNECT (mình chủ động đóng).</summary>
        public void Close()
        {
            lock (gate)
            {
                generation++;
                CloseSocket();
            }
            while (sending.TryDequeue(out _)) { }
            while (received.TryDequeue(out _)) { }
            Volatile.Write(ref state, 0);
            sendSignal.Set();
        }

        /// <summary>Xếp một gói vào hàng gửi. Gọi từ thread nào cũng được.</summary>
        public void Send(int cmd, byte[] body)
        {
            if (cmd < -256 || cmd > 255) throw new ArgumentOutOfRangeException(nameof(cmd), "Ma lenh 9 bit: -256..255");
            body = body ?? Array.Empty<byte>();
            if (body.Length > MaxBody) throw new ArgumentException("Goi qua lon: " + body.Length + " byte");
            if (cmd == Cmd.PING) Interlocked.Exchange(ref pingSentAt, NowMs);
            sending.Enqueue(new InPacket(cmd, body));
            sendSignal.Set();
        }

        /// <summary>Lấy một gói đã nhận. Gọi ở main thread.</summary>
        public bool TryReceive(out InPacket p) => received.TryDequeue(out p);

        /// <summary>Trạng thái kết nối vừa đổi (CONNECT_OK / DISCONNECT / CONNECT_FAIL), 0 khi không có gì mới.</summary>
        public int TakeState() => Interlocked.Exchange(ref state, 0);

        // ---------------- thread mạng ----------------

        bool IsCurrent(int gen) => gen == generation;

        void Run(int gen)
        {
            try
            {
                DoConnect(gen);
                new Thread(() => ReadLoop(gen)) { IsBackground = true, Name = "NetKit read" }.Start();
                WriteLoop(gen);
            }
            catch (Exception e)
            {
                if (IsCurrent(gen))
                {
                    NetLog.Error("[NetKit] " + e.Message);
                    Lost(gen);
                }
            }
        }

        void DoConnect(int gen)
        {
            var c = new TcpClient();
            try
            {
                if (!c.ConnectAsync(host, port).Wait(ConnectTimeoutMs)) throw new TimeoutException("Ket noi qua thoi gian");
            }
            catch
            {
                c.Close();
                throw;
            }
            c.NoDelay = true;
            c.ReceiveBufferSize = 128 * 1024;
            c.SendBufferSize = 64 * 1024;
            lock (gate)
            {
                if (!IsCurrent(gen)) // Close() trong lúc chờ kết nối
                {
                    c.Close();
                    throw new OperationCanceledException();
                }
                client = c;
                stream = c.GetStream();
                connected = 2;
                Volatile.Write(ref state, CONNECT_OK);
            }
        }

        // gom nhiều gói vào một lần ghi; gói lớn hơn bộ đệm vẫn gửi một mình
        void WriteLoop(int gen)
        {
            var buffer = new MemoryStream();
            var header = new byte[4];
            while (IsCurrent(gen))
            {
                if (sending.IsEmpty)
                {
                    sendSignal.WaitOne(1000);
                    continue;
                }
                NetworkStream s = stream;
                if (s == null) return;
                buffer.SetLength(0);
                while (sending.TryPeek(out InPacket p))
                {
                    if (buffer.Length > 0 && buffer.Length + p.Body.Length + 4 > 64 * 1024) break;
                    sending.TryDequeue(out _);
                    int h = (p.Cmd & 0x1FF) << 23 | p.Body.Length;
                    header[0] = (byte)(h >> 24);
                    header[1] = (byte)(h >> 16);
                    header[2] = (byte)(h >> 8);
                    header[3] = (byte)h;
                    buffer.Write(header, 0, 4);
                    buffer.Write(p.Body, 0, p.Body.Length);
                }
                s.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
        }

        void ReadLoop(int gen)
        {
            try
            {
                var header = new byte[4];
                while (IsCurrent(gen))
                {
                    NetworkStream s = stream;
                    if (s == null) return;
                    ReadExactly(s, header, 4);
                    int h = header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3];
                    int cmd = h >> 23;          // dịch có dấu: lấy lại mã lệnh âm
                    int size = h & 0x7FFFFF;
                    var body = new byte[size];
                    ReadExactly(s, body, size);
                    Interlocked.Add(ref totalRead, size);
                    if (cmd == Cmd.PING)
                        OnPong(body);
                    else
                        received.Enqueue(new InPacket(cmd, body));
                }
            }
            catch (Exception e)
            {
                if (IsCurrent(gen))
                {
                    NetLog.Error("[NetKit] Mat ket noi: " + e.Message);
                    Lost(gen);
                }
            }
        }

        void OnPong(byte[] body)
        {
            long sent = Interlocked.Read(ref pingSentAt);
            if (sent >= 0) PingMs = (int)(NowMs - sent);
            if (body.Length >= 8)
            {
                long server = new PacketReader(body).ReadLong();
                Interlocked.Exchange(ref serverOffset, server - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        static void ReadExactly(Stream s, byte[] buf, int size)
        {
            int n = 0;
            while (n < size)
            {
                int k = s.Read(buf, n, size - n);
                if (k <= 0) throw new EndOfStreamException("Server dong ket noi"); // không thoát thì quay vòng 100% CPU
                n += k;
            }
        }

        // socket hỏng: dọn socket, báo main thread qua state
        void Lost(int gen)
        {
            lock (gate)
            {
                if (!IsCurrent(gen)) return;
                bool was = connected == 2;
                generation++;
                CloseSocket();
                Volatile.Write(ref state, was ? DISCONNECT : CONNECT_FAIL);
            }
            while (sending.TryDequeue(out _)) { }   // gói của phiên vừa mất không còn nghĩa ở lần kết nối sau
            sendSignal.Set();
        }

        void CloseSocket()
        {
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            stream = null;
            client = null;
            connected = 0;
            PingMs = -1;
        }
    }

    /// <summary>Nơi ghi log của NetKit. NetClient gán sang Debug.Log của Unity; ngoài Unity thì ra Console.</summary>
    public static class NetLog
    {
        public static Action<string> Info = Console.WriteLine;
        public static Action<string> Error = Console.Error.WriteLine;
    }
}
