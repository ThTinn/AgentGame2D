using System;
using System.Collections.Generic;

namespace NetKit
{
    /// <summary>
    /// Tải dữ liệu game từ server ngay sau khi kết nối, trước khi đăng nhập:
    /// <list type="number">
    /// <item><see cref="Begin"/> gửi CLIENT_INFO (thiết bị, phiên bản client).</item>
    /// <item>Server trả CHECK_DATA_VERSION: phiên bản từng template và danh sách file của nền tảng.</item>
    /// <item>Template cùng phiên bản với bản đã lưu thì đọc từ máy, khác thì xin UPDATE_DATA. File khác phiên bản hoặc chưa có thì
    /// xin REQUEST_DATA, server gửi theo khối 128 KB, đủ khối thì ghi vào <see cref="DataStore"/>.</item>
    /// <item>Đủ hết thì bắn <see cref="Done"/>. Game dùng <see cref="TemplateReady"/> để nạp bảng, và
    /// <see cref="DataStore.ReadFile"/> / <see cref="DataStore.FilePath"/> để nạp AssetBundle khi cần.</item>
    /// </list>
    /// Đường truyền im quá <see cref="StallSeconds"/> giây thì xin lại các file đang tải dở.
    /// </summary>
    public sealed class DataDownloader
    {
        /// <summary>Một file theo nền tảng trong danh sách của server.</summary>
        public struct FileInfo
        {
            public int Type, Id, Version, Size;
        }

        sealed class Download
        {
            public FileInfo Info;
            public byte[] Data;
            public int Next;
        }

        readonly NetConnection net;
        public readonly DataStore Store;

        /// <summary>Số file xin cùng lúc. Server gửi lần lượt, xin trước vài file để đường truyền không rảnh giữa hai file.</summary>
        public int Parallel = 2;
        public float StallSeconds = 15f;

        readonly HashSet<int> waitingTemplates = new HashSet<int>();
        readonly Queue<FileInfo> queue = new Queue<FileInfo>();
        readonly Dictionary<string, Download> active = new Dictionary<string, Download>();
        float sinceChunk;
        bool running;

        /// <summary>Danh sách file server có cho nền tảng này (sau CHECK_DATA_VERSION).</summary>
        public readonly List<FileInfo> ServerFiles = new List<FileInfo>();

        /// <summary>Template đã sẵn sàng (vừa tải hoặc đọc từ máy): type, nội dung.</summary>
        public event Action<int, byte[]> TemplateReady;
        /// <summary>File vừa tải xong và đã ghi vào máy: type, id.</summary>
        public event Action<int, int> FileReady;
        /// <summary>Server báo không có file: type, id.</summary>
        public event Action<int, int> FileMissing;
        /// <summary>Đã có đủ mọi template và file.</summary>
        public event Action Done;

        public bool IsDone { get; private set; }
        public long TotalBytes { get; private set; }
        public long ReceivedBytes { get; private set; }
        public float Progress => TotalBytes <= 0 ? (IsDone ? 1f : 0f) : (float)ReceivedBytes / TotalBytes;

        public DataDownloader(NetConnection net, DataStore store)
        {
            this.net = net;
            Store = store;
            net.On(Cmd.CHECK_DATA_VERSION, OnCheckVersion);
            net.On(Cmd.UPDATE_DATA, OnTemplate);
            net.On(Cmd.REQUEST_DATA, OnChunk);
            net.Ticked += OnTick;
        }

        /// <summary>Bắt đầu kiểm và tải, gọi khi vừa kết nối. device: 1 Android, 2 iOS, 3 PC (NetClient.DeviceType).</summary>
        public void Begin(int device, int clientVersion)
        {
            Reset();
            running = true;
            net.Send(Cmd.CLIENT_INFO, w =>
            {
                w.WriteByte(device);
                w.WriteShort(clientVersion);
            });
        }

        /// <summary>Dừng và quên lần tải đang chạy (mất kết nối). Gọi Begin lại khi kết nối lại.</summary>
        public void Reset()
        {
            running = false;
            IsDone = false;
            waitingTemplates.Clear();
            queue.Clear();
            active.Clear();
            ServerFiles.Clear();
            TotalBytes = ReceivedBytes = 0;
            sinceChunk = 0;
        }

        // CHECK_DATA_VERSION: byte n { byte type, byte ver }, short m { byte type, short id, int ver, int size }
        void OnCheckVersion(PacketReader r)
        {
            if (!running) return;
            var local = new List<int>();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int type = r.ReadByte(), version = r.ReadByte();
                if (Store.TemplateVersion(type) == version)
                {
                    local.Add(type);
                }
                else
                {
                    waitingTemplates.Add(type);
                    net.Send(Cmd.UPDATE_DATA, w => w.WriteByte(type));
                }
            }
            int m = r.Available > 0 ? r.ReadShort() : 0;
            for (int i = 0; i < m; i++)
            {
                var f = new FileInfo { Type = r.ReadByte(), Id = r.ReadShort(), Version = r.ReadInt(), Size = r.ReadInt() };
                ServerFiles.Add(f);
                if (Store.FileVersion(f.Type, f.Id) == f.Version) continue;
                queue.Enqueue(f);
                TotalBytes += f.Size;
            }
            foreach (int type in local) TemplateReady?.Invoke(type, Store.ReadTemplate(type));
            RequestMore();
            CheckDone();
        }

        // UPDATE_DATA: byte type, byte ver, dữ liệu
        void OnTemplate(PacketReader r)
        {
            int type = r.ReadByte(), version = r.ReadByte();
            byte[] data = r.ReadRest();
            Store.WriteTemplate(type, version, data);
            waitingTemplates.Remove(type);
            TemplateReady?.Invoke(type, data);
            CheckDone();
        }

        // REQUEST_DATA: byte type, short id, int tổng (-1: không có), int vị trí, dữ liệu khối. Khối tới đúng thứ tự, khối đầu vị trí 0
        void OnChunk(PacketReader r)
        {
            int type = r.ReadByte(), id = r.ReadShort(), total = r.ReadInt();
            string key = type + "_" + id;
            if (!active.TryGetValue(key, out Download d)) return;   // khối lạc của lần xin trước
            sinceChunk = 0;
            if (total < 0)
            {
                active.Remove(key);
                ReceivedBytes += d.Info.Size;
                FileMissing?.Invoke(type, id);
                RequestMore();
                CheckDone();
                return;
            }
            int offset = r.ReadInt();
            byte[] chunk = r.ReadRest();
            if (offset == 0)
            {
                ReceivedBytes -= d.Next;
                d.Data = new byte[total];
                d.Next = 0;
            }
            if (d.Data == null || offset != d.Next || offset + chunk.Length > d.Data.Length) return;
            Buffer.BlockCopy(chunk, 0, d.Data, offset, chunk.Length);
            d.Next += chunk.Length;
            ReceivedBytes += chunk.Length;
            if (d.Next < d.Data.Length) return;

            active.Remove(key);
            Store.WriteFile(type, id, d.Info.Version, d.Data);
            FileReady?.Invoke(type, id);
            RequestMore();
            CheckDone();
        }

        void RequestMore()
        {
            while (active.Count < Parallel && queue.Count > 0)
            {
                FileInfo f = queue.Dequeue();
                active[f.Type + "_" + f.Id] = new Download { Info = f };
                Request(f);
            }
        }

        void Request(FileInfo f)
        {
            net.Send(Cmd.REQUEST_DATA, w =>
            {
                w.WriteByte(f.Type);
                w.WriteShort(f.Id);
            });
        }

        // đường truyền im quá lâu: xin lại từ đầu các file đang tải dở
        void OnTick(float dt)
        {
            if (!running || IsDone || active.Count == 0 || !net.IsConnected) return;
            sinceChunk += dt;
            if (sinceChunk < StallSeconds) return;
            sinceChunk = 0;
            foreach (Download d in active.Values)
            {
                NetLog.Info("[NetKit] Xin lai file " + d.Info.Type + "_" + d.Info.Id);
                Request(d.Info);
            }
        }

        void CheckDone()
        {
            if (IsDone || waitingTemplates.Count > 0 || queue.Count > 0 || active.Count > 0) return;
            IsDone = true;
            running = false;
            Done?.Invoke();
        }
    }
}
