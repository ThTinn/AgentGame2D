using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;

namespace Game.Players
{
    /// <summary>Cách ghi số của một chỉ số. Giá trị cố định (ghi xuống asset).</summary>
    public enum StatFormat : byte
    {
        Percent = 0,        // "+5%"
        Flat = 1,           // "+20"
        PerSecond = 2,      // "+0.5/giây"
        ReducePercent = 3,  // "-6%" (hồi chiêu giảm)
        Cells = 4,          // "+0.4 ô"
        Seconds = 5,        // "+0.25s"
        Degrees = 6,        // "+20°"
    }

    /// <summary>Trần của một chỉ số áp lên cái gì. Giá trị cố định (ghi xuống asset).</summary>
    public enum StatCap : byte
    {
        None = 0,              // không có trần
        Bonus = 1,             // phần cộng thêm (cây + đồ) không vượt quá cap
        Total = 2,             // tổng gốc + cộng thêm không vượt quá cap (chí mạng: gốc của GameController + cộng thêm)
        SharedWithWeapon = 3,  // cộng chung trần với nâng cấp của vũ khí (tốc đánh)
    }

    /// <summary>
    /// Một option = một chỉ số của nhân vật. Cây nâng cấp nhân vật (nút, Chuyên tinh) và đồ mặc (ItemTemplate.options) đều
    /// cộng vào các chỉ số này; UI đọc tên, cách ghi số và trần ở đây. <see cref="id"/> chính là <see cref="PlayerStat"/>:
    /// enum giữ phần gameplay (chỉ số đó làm gì), bảng giữ phần dữ liệu (tên, mô tả, trần) để chỉnh không phải sửa code.
    /// </summary>
    [Serializable]
    public class OptionTemplate
    {
        [Tooltip("= (int)PlayerStat. Cố định, không dùng lại.")]
        public int id;
        [Tooltip("Tên khóa, không đổi (vd max_hp).")]
        public string name;
        [Tooltip("Tên hiển thị, dạng 'Tiếng Việt|English'.")]
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Thứ tự hiện trong các bảng tổng (nhỏ trước).")]
        public int order;
        public StatFormat format;
        public StatCap capKind;
        [Tooltip("Trần, cùng đơn vị với chỉ số (vd 50 = 50%). Bỏ qua khi capKind = None.")]
        [Min(0f)] public float cap;

        public PlayerStat Stat => (PlayerStat)id;
        public bool HasCap => capKind != StatCap.None && cap > 0f;
    }

    /// <summary>
    /// Bảng option (chỉ số) của game. Một asset ở Assets/Data/Player/OptionDatabase.asset, dựng bằng
    /// Tools/Core/Player/Build Option Database. Cùng khuôn với các bảng khác: GameController đăng ký lúc Awake,
    /// ai cần thì đọc <see cref="Current"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "OptionDatabase", menuName = "Core/Option Database")]
    public class OptionDatabase : ScriptableObject
    {
        public const int CurrentSchema = 1;

        public int schemaVersion = CurrentSchema;
        public List<OptionTemplate> options = new List<OptionTemplate>();

        [NonSerialized] Dictionary<int, OptionTemplate> byId;
        [NonSerialized] List<OptionTemplate> sorted;

        static OptionDatabase current;

        /// <summary>Bảng option của game; null khi scene chưa có ai giữ bảng.</summary>
        public static OptionDatabase Current => current;

        /// <summary>Đăng ký bảng cho cả game (GameController.Awake).</summary>
        public static void Use(OptionDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("OptionDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        void OnValidate() => ClearCache();

        public void ClearCache()
        {
            byId = null;
            sorted = null;
        }

        void Index()
        {
            if (byId != null && byId.Count == options.Count) return;
            byId = new Dictionary<int, OptionTemplate>(options.Count);
            foreach (OptionTemplate o in options) byId[o.id] = o;
            sorted = null;
        }

        public OptionTemplate Get(int id)
        {
            Index();
            return byId.TryGetValue(id, out OptionTemplate o) ? o : null;
        }

        public OptionTemplate Get(PlayerStat stat) => Get((int)stat);

        public OptionTemplate Find(string optionName)
        {
            foreach (OptionTemplate o in options) if (o.name == optionName) return o;
            return null;
        }

        /// <summary>Mọi option theo <see cref="OptionTemplate.order"/> (bằng nhau thì theo id). Danh sách dùng chung, đừng sửa.</summary>
        public IReadOnlyList<OptionTemplate> Sorted
        {
            get
            {
                Index();
                if (sorted != null) return sorted;
                sorted = new List<OptionTemplate>(options);
                sorted.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.id.CompareTo(b.id));
                return sorted;
            }
        }

        /// <summary>
        /// Trần của một chỉ số. Chưa có bảng thì dùng <paramref name="fallback"/> (giá trị gốc trong code, để game vẫn cân
        /// bằng khi scene quên gán bảng); có bảng mà option không đặt trần thì không giới hạn.
        /// </summary>
        public static float CapOf(PlayerStat stat, float fallback)
        {
            if (current == null) return fallback;
            OptionTemplate o = current.Get(stat);
            if (o == null) return fallback;
            return o.HasCap ? o.cap : float.MaxValue;
        }

        // Binary (MyWriter/MyReader, big-endian), file options.bin gửi cho server:
        //   short schema, short count { short id, UTF name, UTF displayName, UTF description, short order,
        //                               byte format, byte capKind, float cap }
        public byte[] ToBytes()
        {
            var w = new MyWriter(2 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)options.Count));
            foreach (OptionTemplate o in options)
            {
                w.WriteShort(checked((short)o.id));
                w.WriteUTF(o.name ?? "");
                w.WriteUTF(o.displayName ?? "");
                w.WriteUTF(o.description ?? "");
                w.WriteShort(checked((short)o.order));
                w.WriteByte((byte)o.format);
                w.WriteByte((byte)o.capKind);
                w.WriteFloat(o.cap);
            }
            return w.ToArray();
        }

        /// <summary>Đọc bảng từ <see cref="ToBytes"/> vào danh sách. Trả false và lý do khi hỏng hay id trùng.</summary>
        public static bool Read(byte[] bytes, List<OptionTemplate> into, out int schema, out string error)
        {
            schema = 0;
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty option data"; return false; }
            try
            {
                var r = new MyReader(bytes);
                schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "option schema too new (" + schema + ")"; return false; }
                int count = r.ReadShort();
                if (count < 0 || count > 1000) { error = "bad option count"; return false; }
                var ids = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    var o = new OptionTemplate
                    {
                        id = r.ReadShort(), name = r.ReadUTF(), displayName = r.ReadUTF(), description = r.ReadUTF(),
                        order = r.ReadShort(), format = (StatFormat)r.ReadByte(), capKind = (StatCap)r.ReadByte(), cap = r.ReadFloat(),
                    };
                    if (!ids.Add(o.id)) { error = "duplicate option id " + o.id; return false; }
                    into.Add(o);
                }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad option data: " + e.Message; return false; }
            return true;
        }

        /// <summary>Bảng mới (CreateInstance) từ bytes; null và lý do khi hỏng.</summary>
        public static OptionDatabase FromBytes(byte[] bytes, out string error)
        {
            var list = new List<OptionTemplate>();
            if (!Read(bytes, list, out int schema, out error)) return null;
            var db = CreateInstance<OptionDatabase>();
            db.schemaVersion = schema;
            db.options = list;
            return db;
        }
    }
}
