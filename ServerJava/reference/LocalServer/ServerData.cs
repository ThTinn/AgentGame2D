using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.ServerSim
{
    /// <summary>Một file đang gửi cho client, chia khối ở <see cref="ServerWorld.TickUploads"/>.</summary>
    sealed class AssetUpload
    {
        public sbyte type;
        public short id;
        public byte[] data;
        public int offset;
    }

    /// <summary>
    /// Kho dữ liệu của server giả lập: coi StreamingAssets/Data như file trên server thật. CLIENT_INFO trả phiên bản template và
    /// danh sách bundle của nền tảng client, UPDATE_DATA trả template, REQUEST_DATA trả bundle theo khối. File trong StreamingAssets
    /// đã mã hóa nên giải mã trước khi gửi (client tự mã hóa khi ghi vào máy). Phiên bản lấy theo giờ sửa file: đóng lại bundle thì
    /// client tự tải bản mới. Trên APK không liệt kê được StreamingAssets: danh sách rỗng, client đọc file có sẵn trong máy.
    /// </summary>
    public sealed partial class ServerWorld
    {
        const int ChunkSize = 128 * 1024;
        const int ChunksPerTick = 4;   // ~30 MB/s ở 60 FPS; ghi socket chạy trên main thread nên không gửi một lần cả file
        static readonly byte[] Templates = { GameData.Type_Anim_Template, GameData.Type_Map_Template, GameData.Type_Mod_Template };

        readonly Dictionary<ServerConnection, string> platforms = new Dictionary<ServerConnection, string>();
        readonly Dictionary<ServerConnection, Queue<AssetUpload>> uploads = new Dictionary<ServerConnection, Queue<AssetUpload>>();

        static string DataRoot => Path.Combine(Application.streamingAssetsPath, "Data");

        static string TemplatePath(byte type) => Path.Combine(DataRoot, GameData.txtGame_Data + type + ".bytes");

        static string PlatformOf(sbyte device) =>
            device == TCanvas.ANDROID ? "Android" : device == TCanvas.IOS ? "iOS" : "Windows";

        static int VersionOf(string path) => (int)new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds();

        void ClientInfo(ServerConnection conn, MyReader r)
        {
            string platform = PlatformOf(r.ReadSByte());
            platforms[conn] = platform;
            string root = Path.Combine(DataRoot, platform);
            if (!Directory.Exists(root))
            {
                Send(conn, Cmd_Message.CHECK_DATA_VERSION, w => w.writeByte(0));
                return;
            }
            var files = new List<(sbyte type, short id, string path)>();
            foreach (string folder in Directory.GetDirectories(root))
                foreach (string path in Directory.GetFiles(folder, "*.bytes"))
                {
                    string[] parts = Path.GetFileNameWithoutExtension(path).Split('_');
                    if (parts.Length == 2 && sbyte.TryParse(parts[0], out sbyte type) && short.TryParse(parts[1], out short id))
                        files.Add((type, id, path));
                }
            Send(conn, Cmd_Message.CHECK_DATA_VERSION, w =>
            {
                w.writeByte(Templates.Length);
                foreach (byte type in Templates)
                {
                    w.writeByte(type);
                    w.writeByte(1 + VersionOf(TemplatePath(type)) % 255);
                }
                w.writeShort(files.Count);
                foreach ((sbyte type, short id, string path) in files)
                {
                    w.writeByte(type);
                    w.writeShort(id);
                    w.writeInt(VersionOf(path));
                    w.writeInt((int)new FileInfo(path).Length);
                }
            });
        }

        void SendTemplate(ServerConnection conn, byte type)
        {
            string path = TemplatePath(type);
            if (!File.Exists(path))
            {
                Debug.LogWarning("LocalServer: không có template " + path);
                return;
            }
            byte[] bytes = FileController.ReadPlainFile(path);
            Send(conn, Cmd_Message.UPDATE_DATA, w =>
            {
                w.writeByte(type);
                w.writeByte(1 + VersionOf(path) % 255);
                w.WriteBytes(bytes);
            });
        }

        void RequestAsset(ServerConnection conn, MyReader r)
        {
            sbyte type = r.ReadSByte();
            short id = r.ReadShort();
            if (!uploads.TryGetValue(conn, out Queue<AssetUpload> queue)) uploads[conn] = queue = new Queue<AssetUpload>();
            foreach (AssetUpload u in queue)
                if (u.type == type && u.id == id) return;   // client hỏi lại lúc file đang gửi dở
            queue.Enqueue(new AssetUpload { type = type, id = id });
        }

        void DropUploads(ServerConnection conn)
        {
            uploads.Remove(conn);
            platforms.Remove(conn);
        }

        void TickUploads()
        {
            foreach (KeyValuePair<ServerConnection, Queue<AssetUpload>> pair in uploads)
            {
                ServerConnection conn = pair.Key;
                Queue<AssetUpload> queue = pair.Value;
                for (int i = 0; i < ChunksPerTick && queue.Count > 0; i++)
                {
                    AssetUpload u = queue.Peek();
                    if (u.data == null && !LoadUpload(conn, u))
                    {
                        queue.Dequeue();
                        continue;
                    }
                    int n = Mathf.Min(ChunkSize, u.data.Length - u.offset);
                    var chunk = new byte[n];
                    Buffer.BlockCopy(u.data, u.offset, chunk, 0, n);
                    int offset = u.offset;
                    Send(conn, Cmd_Message.REQUEST_DATA, w =>
                    {
                        w.writeByte(u.type);
                        w.writeShort(u.id);
                        w.writeInt(u.data.Length);
                        w.writeInt(offset);
                        w.WriteBytes(chunk);
                    });
                    u.offset += n;
                    if (u.offset >= u.data.Length) queue.Dequeue();
                }
            }
        }

        // đọc file khi tới lượt gửi; không có file thì báo client (total = -1)
        bool LoadUpload(ServerConnection conn, AssetUpload u)
        {
            platforms.TryGetValue(conn, out string platform);
            string path = FileController.LocalBundlePath(u.type, u.id, platform);
            if (path != null && File.Exists(path))
            {
                u.data = FileController.ReadPlainFile(path);
                return true;
            }
            Debug.LogWarning($"LocalServer: không có bundle {u.type},{u.id} cho {platform}.");
            Send(conn, Cmd_Message.REQUEST_DATA, w =>
            {
                w.writeByte(u.type);
                w.writeShort(u.id);
                w.writeInt(-1);
            });
            return false;
        }
    }
}
