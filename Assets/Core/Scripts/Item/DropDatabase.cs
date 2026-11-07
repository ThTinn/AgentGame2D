using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;

namespace Game.Items
{
    /// <summary>Một dòng của bảng rơi đồ: vật phẩm nào, tỉ lệ bao nhiêu, rơi mấy cái.</summary>
    [Serializable]
    public class DropEntry
    {
        [Tooltip("ItemTemplate.name, vd gem_red.")]
        public string item;
        [Tooltip("Tỉ lệ rơi (%), mỗi dòng tung riêng. 100 = chắc chắn rơi. Chỉ số Tìm đồ (itemFind) của người chơi nhân thêm.")]
        [Range(0f, 100f)] public float chance = 10f;
        [Tooltip("Rơi thì được ngẫu nhiên trong [min, max] cái.")]
        [Min(1)] public int min = 1;
        [Min(1)] public int max = 1;
    }

    /// <summary>
    /// Một bảng rơi đồ. Tên bảng là nguồn rơi: tên quái (MonsterTemplate.name, vd slime_green), quái 3D theo id mẫu
    /// (<see cref="DropDatabase.MonsterIdTable"/>, vd mob_1), tên rương (<see cref="DropDatabase.ChestTable"/>, vd chest_small)
    /// hoặc bảng chung của quái <see cref="DropDatabase.MonsterDefaultTable"/>.
    /// </summary>
    [Serializable]
    public class DropTable
    {
        public string name;
        public List<DropEntry> entries = new List<DropEntry>();
    }

    /// <summary>Một món vừa tung ra được: vật phẩm và số lượng.</summary>
    public struct DropRoll
    {
        public string item;
        public int count;
    }

    /// <summary>
    /// Bảng rơi đồ của quái và rương (Assets/Data/Items/DropDatabase.asset). GameController giữ tham chiếu và đăng ký lúc
    /// Awake, cả game tra qua <see cref="Current"/>. Game design sửa ở sheet "Rơi đồ" của Docs/Items_Data.xlsx rồi bấm
    /// Tools/Core/Items/Build Data From Excel; Tools/Core/Items/Build Drop Tables dựng bảng mẫu cho nguồn chưa có bảng.
    /// Luật tung (<see cref="Roll"/>) không đụng Unity để server C# dùng lại được.
    /// </summary>
    [CreateAssetMenu(fileName = "DropDatabase", menuName = "Core/Drop Database")]
    public class DropDatabase : ScriptableObject
    {
        public const int CurrentSchema = 1;
        /// <summary>Tên bảng của rương: ChestPrefix + tên ChestKind viết thường (chest_small, chest_iron...).</summary>
        public const string ChestPrefix = "chest_";
        /// <summary>
        /// Bảng chung của quái chưa có bảng riêng (hoặc chưa có trong MonsterDatabase, như quái 3D lúc chơi local):
        /// ngọc, bình máu / mana, thuốc tăng chỉ số.
        /// </summary>
        public const string MonsterDefaultTable = "monster";
        /// <summary>
        /// Bảng của quái 3D (không có trong MonsterDatabase): MonsterIdPrefix + id mẫu quái của Game (MonsterSOData idTemplate,
        /// vd mob_1 = Slime_Poly).
        /// </summary>
        public const string MonsterIdPrefix = "mob_";

        public int schemaVersion = CurrentSchema;
        public List<DropTable> tables = new List<DropTable>();

        static DropDatabase current;

        /// <summary>Bảng rơi đồ của game; null khi scene chưa có ai giữ bảng (quái rơi ngọc ngẫu nhiên như cũ).</summary>
        public static DropDatabase Current => current;

        /// <summary>Đăng ký bảng cho cả game (GameController.Awake).</summary>
        public static void Use(DropDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("DropDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public static string ChestTable(ChestKind kind) => ChestPrefix + kind.ToString().ToLowerInvariant();

        public DropTable Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (DropTable t in tables) if (t != null && t.name == name) return t;
            return null;
        }

        public static string MonsterIdTable(int templateId) => MonsterIdPrefix + templateId;

        /// <summary>Bảng của một loại quái: bảng riêng theo tên nếu có, không thì bảng chung <see cref="MonsterDefaultTable"/>.</summary>
        public DropTable ForMonster(string monsterName) => Find(monsterName) ?? Find(MonsterDefaultTable);

        /// <summary>
        /// Như <see cref="ForMonster(string)"/>, thêm một bước giữa: không có bảng theo tên thì tìm bảng theo id mẫu
        /// (<see cref="MonsterIdTable"/>) — quái 3D không có trong MonsterDatabase nên không có tên.
        /// </summary>
        public DropTable ForMonster(string monsterName, int templateId) =>
            Find(monsterName) ?? (templateId > 0 ? Find(MonsterIdTable(templateId)) : null) ?? Find(MonsterDefaultTable);

        /// <summary>
        /// Tung một bảng: mỗi dòng tung riêng với tỉ lệ chance x (1 + findBonus / 100), kẹp ở 100%. random01 trả số trong
        /// [0, 1) (client: UnityEngine.Random.value, server: RNG của nó). Kết quả thêm vào into (không xóa trước).
        /// </summary>
        public static void Roll(DropTable table, float findBonus, Func<float> random01, List<DropRoll> into)
        {
            if (table == null || table.entries == null || random01 == null) return;
            float scale = 1f + Math.Max(0f, findBonus) / 100f;
            foreach (DropEntry e in table.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.item) || e.chance <= 0f) continue;
                float chance = Math.Min(100f, e.chance * scale);
                if (random01() * 100f >= chance) continue;
                int lo = Math.Max(1, e.min), hi = Math.Max(lo, e.max);
                int count = lo + Math.Min(hi - lo, (int)(random01() * (hi - lo + 1)));
                into.Add(new DropRoll { item = e.item, count = count });
            }
        }

        // ---------------- kiểm tra ----------------

        /// <summary>
        /// Lỗi dữ liệu: tên bảng trống / trùng, vật phẩm không có (itemExists null = không kiểm), tỉ lệ ngoài (0, 100],
        /// min / max sai, vật phẩm lặp trong một bảng. Rỗng = ổn.
        /// </summary>
        public static List<string> Validate(List<DropTable> tables, Func<string, bool> itemExists)
        {
            var problems = new List<string>();
            var names = new HashSet<string>();
            foreach (DropTable t in tables)
            {
                if (t == null) continue;
                if (string.IsNullOrEmpty(t.name)) { problems.Add("Bảng rơi đồ không có tên."); continue; }
                if (!names.Add(t.name)) problems.Add(t.name + ": tên bảng trùng.");
                var items = new HashSet<string>();
                foreach (DropEntry e in t.entries)
                {
                    if (e == null) continue;
                    string where = t.name + "/" + e.item;
                    if (string.IsNullOrEmpty(e.item)) problems.Add(t.name + ": dòng thiếu vật phẩm.");
                    else if (itemExists != null && !itemExists(e.item)) problems.Add(where + ": không có trong ItemDatabase.");
                    if (!string.IsNullOrEmpty(e.item) && !items.Add(e.item)) problems.Add(where + ": vật phẩm lặp trong bảng, gộp thành một dòng.");
                    if (e.chance <= 0f || e.chance > 100f) problems.Add(where + ": tỉ lệ " + e.chance + " ngoài (0, 100].");
                    if (e.min < 1 || e.max < e.min || e.max > short.MaxValue) problems.Add(where + ": min / max phải 1 <= min <= max.");
                }
            }
            return problems;
        }

        public List<string> Validate(Func<string, bool> itemExists) => Validate(tables, itemExists);

        // ---------------- file cho server ----------------

        // Binary (MyWriter/MyReader, big-endian), file drops.bin gửi cho server:
        //   short schema, short count { UTF name, byte entryCount { UTF item, float chance (int x100), short min, short max } }
        public byte[] ToBytes() => ToBytes(tables, schemaVersion);

        public static byte[] ToBytes(List<DropTable> tables, int schemaVersion)
        {
            var w = new MyWriter(2 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)tables.Count));
            foreach (DropTable t in tables)
            {
                w.WriteUTF(t.name ?? "");
                int n = t.entries != null ? t.entries.Count : 0;
                w.WriteByte(checked((byte)n));
                for (int i = 0; i < n; i++)
                {
                    DropEntry e = t.entries[i];
                    w.WriteUTF(e.item ?? "");
                    w.WriteFloat(e.chance);
                    w.WriteShort(checked((short)e.min));
                    w.WriteShort(checked((short)e.max));
                }
            }
            return w.ToArray();
        }

        /// <summary>Đọc bảng từ <see cref="ToBytes"/> (server C# dùng lại được). False và lý do khi hỏng, schema mới hơn, tên trùng.</summary>
        public static bool Read(byte[] bytes, List<DropTable> into, out int schema, out string error)
        {
            schema = 0;
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty drop data"; return false; }
            try
            {
                var r = new MyReader(bytes);
                schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "drop schema too new (" + schema + ")"; return false; }
                int count = r.ReadShort();
                if (count < 0 || count > 30000) { error = "bad drop table count"; return false; }
                var names = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var t = new DropTable { name = r.ReadUTF() };
                    int n = r.ReadByte();
                    for (int k = 0; k < n; k++)
                        t.entries.Add(new DropEntry { item = r.ReadUTF(), chance = r.ReadFloat(), min = r.ReadShort(), max = r.ReadShort() });
                    if (string.IsNullOrEmpty(t.name) || !names.Add(t.name)) { error = "empty or duplicate drop table '" + t.name + "'"; return false; }
                    into.Add(t);
                }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad drop data: " + e.Message; return false; }
            return true;
        }
    }
}
