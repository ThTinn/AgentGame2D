using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetKit
{
    /// <summary>
    /// Dữ liệu đã tải về máy (persistentDataPath/netkit trong Unity) và phiên bản của từng file. Template ở
    /// <c>template_{type}.bytes</c>, file theo nền tảng ở <c>{type}_{id}.bytes</c>, phiên bản ở <c>versions.txt</c>. Ghi file
    /// trước, ghi phiên bản sau: mất điện giữa chừng thì lần sau tải lại, không bao giờ coi file hỏng là bản mới.
    /// </summary>
    public sealed class DataStore
    {
        public readonly string Root;
        readonly Dictionary<string, int> versions = new Dictionary<string, int>();

        public DataStore(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "versions.txt");
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && int.TryParse(line.Substring(eq + 1), out int v)) versions[line.Substring(0, eq)] = v;
            }
        }

        static string TemplateKey(int type) => "template_" + type;
        static string FileKey(int type, int id) => type + "_" + id;
        string PathOf(string key) => Path.Combine(Root, key + ".bytes");

        /// <summary>Phiên bản template đã lưu, -1 khi chưa có hoặc file đã mất.</summary>
        public int TemplateVersion(int type) => VersionOf(TemplateKey(type));

        /// <summary>Phiên bản file đã lưu, -1 khi chưa có hoặc file đã mất.</summary>
        public int FileVersion(int type, int id) => VersionOf(FileKey(type, id));

        int VersionOf(string key) => versions.TryGetValue(key, out int v) && File.Exists(PathOf(key)) ? v : -1;

        public byte[] ReadTemplate(int type) => Read(TemplateKey(type));

        /// <summary>Nội dung file đã tải (ví dụ AssetBundle.LoadFromMemory), null khi chưa có.</summary>
        public byte[] ReadFile(int type, int id) => Read(FileKey(type, id));

        /// <summary>Đường dẫn file đã tải (ví dụ AssetBundle.LoadFromFile), null khi chưa có.</summary>
        public string FilePath(int type, int id)
        {
            string p = PathOf(FileKey(type, id));
            return File.Exists(p) ? p : null;
        }

        byte[] Read(string key)
        {
            string p = PathOf(key);
            return File.Exists(p) ? File.ReadAllBytes(p) : null;
        }

        public void WriteTemplate(int type, int version, byte[] data) => Write(TemplateKey(type), version, data);

        public void WriteFile(int type, int id, int version, byte[] data) => Write(FileKey(type, id), version, data);

        void Write(string key, int version, byte[] data)
        {
            WriteAtomic(PathOf(key), data);
            versions[key] = version;
            var sb = new StringBuilder();
            foreach (var kv in versions) sb.Append(kv.Key).Append('=').Append(kv.Value).Append('\n');
            WriteAtomic(Path.Combine(Root, "versions.txt"), Encoding.UTF8.GetBytes(sb.ToString()));
        }

        // ghi ra file tạm rồi đổi tên: không bao giờ để lại file ghi dở
        static void WriteAtomic(string path, byte[] data)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
    }
}
