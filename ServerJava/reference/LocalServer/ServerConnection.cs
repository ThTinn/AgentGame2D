using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Game.ServerSim
{
    /// <summary>Một gói từ client (hoặc báo client rớt mạng: <see cref="disconnect"/>), chờ main thread xử lý.</summary>
    public struct Incoming
    {
        public ServerConnection conn;
        public short cmd;
        public byte[] data;
        public bool disconnect;
    }

    /// <summary>
    /// Một client nối vào server giả lập. Khung gói giống hệt Session của client: int đầu (big-endian) = mã lệnh 9 bit bù hai
    /// (Session.Pack / UnpackNineBit) + độ dài 23 bit, rồi tới thân gói; lớp XOR của client dùng khóa {0} nên không đổi byte.
    /// Thread đọc đẩy gói vào hàng đợi chung, main thread (LocalServer.Update) xử lý; gửi chạy thẳng trên main thread.
    /// </summary>
    public sealed class ServerConnection
    {
        public readonly int id;
        public ServerPlayer player;
        public volatile bool closed;

        readonly TcpClient client;
        readonly NetworkStream stream;
        readonly ConcurrentQueue<Incoming> inbox;
        readonly object writeLock = new object();

        public ServerConnection(int id, TcpClient client, ConcurrentQueue<Incoming> inbox)
        {
            this.id = id;
            this.client = client;
            this.inbox = inbox;
            client.NoDelay = true;
            stream = client.GetStream();
        }

        public string Remote => client.Client?.RemoteEndPoint?.ToString() ?? "?";

        public void Start()
        {
            var thread = new Thread(ReadLoop) { IsBackground = true, Name = "LocalServer Read " + id };
            thread.Start();
        }

        void ReadLoop()
        {
            var header = new byte[4];
            try
            {
                while (!closed)
                {
                    ReadExactly(header, 4);
                    int pack = header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3];
                    short cmd = (short)Session.UnpackNineBit(pack);
                    int size = Session.UnpackTwentyThreeBit(pack);
                    var data = new byte[size];
                    ReadExactly(data, size);
                    inbox.Enqueue(new Incoming { conn = this, cmd = cmd, data = data });
                }
            }
            catch (Exception)
            {
                // client đóng / lỗi mạng: báo main thread dọn người chơi
            }
            if (!closed) inbox.Enqueue(new Incoming { conn = this, disconnect = true });
            closed = true;
        }

        void ReadExactly(byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0) throw new EndOfStreamException("client closed");
                read += n;
            }
        }

        /// <summary>Gửi một gói (mã lệnh + thân). Lỗi ghi thì đóng kết nối.</summary>
        public void Send(short cmd, byte[] data)
        {
            if (closed) return;
            int len = data != null ? data.Length : 0;
            int pack = Session.Pack(cmd, len);
            var frame = new byte[4 + len];
            frame[0] = (byte)(pack >> 24);
            frame[1] = (byte)(pack >> 16);
            frame[2] = (byte)(pack >> 8);
            frame[3] = (byte)pack;
            if (len > 0) Buffer.BlockCopy(data, 0, frame, 4, len);
            try
            {
                lock (writeLock) stream.Write(frame, 0, frame.Length);
            }
            catch (Exception)
            {
                Close();
                inbox.Enqueue(new Incoming { conn = this, disconnect = true });
            }
        }

        public void Send(Message m)
        {
            Send(m.command, m.getData());
            m.Close();
        }

        public void Close()
        {
            closed = true;
            try { client.Close(); } catch { }
        }
    }
}
