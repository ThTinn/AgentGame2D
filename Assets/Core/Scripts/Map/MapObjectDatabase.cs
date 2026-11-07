using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;

namespace Game.Map
{
    /// <summary>Object đặt trên map dùng để làm gì. Giá trị cố định vì được ghi xuống file và gói mạng.</summary>
    public enum MapObjectKind : byte
    {
        Decor = 0,   // chỉ trang trí (bàn, chậu cây...): có va chạm, không tương tác
        Seat = 1,    // ngồi được (ghế, ghế dài): prefab có component Seat với các chỗ ngồi
        Portal = 2,  // cổng dịch chuyển: chỗ đặt mang map đích + uid cổng đích (MapObjectPlacement.targetMapId / targetUid)
        Zone = 3,    // mốc chuyển khu: đứng gần bấm F mở bảng khu (component ZoneBoard)
    }

    /// <summary>
    /// Một loại object của map (ghế gỗ, ghế dài, bàn...). Id cố định, không dùng lại. Hình / va chạm / chỗ ngồi nằm trong
    /// prefab; bảng chỉ giữ nội dung mà server cũng cần biết (id, tên, loại).
    /// </summary>
    [Serializable]
    public class MapObjectTemplate
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd chair_wood).")]
        public string name;
        public string displayName;
        public MapObjectKind kind;
        [Tooltip("Prefab 3D. Seat: gắn component Seat, mỗi chỗ ngồi là một Transform con (vị trí chân + hướng mặt khi ngồi).")]
        public GameObject prefab;
        [Tooltip("Số chỗ ngồi (Seat). Server dùng để kiểm chỗ còn trống; phải khớp số chỗ trong prefab.")]
        [Min(0)] public int seats;

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    /// <summary>
    /// Một object đặt trên một map. uid duy nhất trong map: client và server gọi nhau theo uid này (ngồi chỗ nào, ai ngồi).
    /// Độ cao không lưu: lúc dựng dò theo mặt map.
    /// </summary>
    [Serializable]
    public class MapObjectPlacement
    {
        [Tooltip("Duy nhất trong map, không dùng lại khi xóa object.")]
        public int uid;
        public short mapId;
        [Tooltip("Id trong MapObjectDatabase.objects.")]
        public int objectId;
        [Tooltip("Vị trí (x, z) trên map.")]
        public Vector2 position;
        [Tooltip("Góc xoay quanh trục đứng (độ). 0 = mặt trước của prefab nhìn về +z.")]
        public float rotationY;
        [Header("Cổng dịch chuyển (Portal)")]
        [Tooltip("Map đích. -1 = không phải cổng / chưa nối.")]
        public short targetMapId = -1;
        [Tooltip("uid của cổng ở map đích: đi qua thì xuất hiện ngay trước cổng đó. 0 = xuất hiện ở chỗ xuất hiện mặc định của map đích.")]
        public int targetUid;

        public bool IsLinked => targetMapId >= 0;

        public MapObjectPlacement() { }

        public MapObjectPlacement(int uid, short mapId, int objectId, Vector2 position, float rotationY,
            short targetMapId = -1, int targetUid = 0)
        {
            this.uid = uid;
            this.mapId = mapId;
            this.objectId = objectId;
            this.position = position;
            this.rotationY = rotationY;
            this.targetMapId = targetMapId;
            this.targetUid = targetUid;
        }

        /// <summary>Hướng mặt trước (mặt phẳng ngang) theo rotationY: (0, 1) khi góc 0, tức +z.</summary>
        public Vector2 Forward
        {
            get
            {
                float rad = rotationY * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            }
        }
    }

    /// <summary>
    /// Bảng object của map (bàn ghế...) và chỗ đặt chúng trên từng map. Một asset ở Assets/_Project/Core/Data/MapObjects/
    /// MapObjectDatabase.asset, dựng bằng Tools/Core/Map Objects/Build Sample Objects + Portals. Chỗ đặt xuất sang gói server;
    /// client nhận danh sách chỗ đặt của map qua gói MAP_OBJECT (<see cref="ReadPlacements"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "MapObjectDatabase", menuName = "Core/Map Object Database")]
    public class MapObjectDatabase : ScriptableObject
    {
        /// <summary>2: chỗ đặt có thêm map đích + uid cổng đích (cổng dịch chuyển).</summary>
        public const int CurrentSchema = 2;

        public int schemaVersion = CurrentSchema;
        public List<MapObjectTemplate> objects = new List<MapObjectTemplate>();
        [Tooltip("Chỗ đặt object trên các map: xuất sang gói server, server gửi cho client.")]
        public List<MapObjectPlacement> placements = new List<MapObjectPlacement>();

        [NonSerialized] Dictionary<int, MapObjectTemplate> byId;

        static MapObjectDatabase current;

        /// <summary>Bảng của game; null khi scene chưa có ai đăng ký (OnlineCore đăng ký lúc kết nối).</summary>
        public static MapObjectDatabase Current => current;

        public static void Use(MapObjectDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("MapObjectDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public void ClearCache() => byId = null;

        public MapObjectTemplate Get(int id)
        {
            if (byId == null || byId.Count != objects.Count)
            {
                byId = new Dictionary<int, MapObjectTemplate>(objects.Count);
                foreach (MapObjectTemplate t in objects) if (t != null) byId[t.id] = t;
            }
            return byId.TryGetValue(id, out MapObjectTemplate found) ? found : null;
        }

        public MapObjectTemplate Find(string name)
        {
            foreach (MapObjectTemplate t in objects) if (t != null && t.name == name) return t;
            return null;
        }

        /// <summary>Chỗ đặt của một map, thêm vào <paramref name="into"/> (không xóa trước).</summary>
        public void PlacementsOf(short mapId, List<MapObjectPlacement> into)
        {
            foreach (MapObjectPlacement p in placements) if (p != null && p.mapId == mapId) into.Add(p);
        }

        /// <summary>Chỗ đặt uid trong map mapId; null khi không có.</summary>
        public MapObjectPlacement FindPlacement(short mapId, int uid)
        {
            foreach (MapObjectPlacement p in placements) if (p != null && p.mapId == mapId && p.uid == uid) return p;
            return null;
        }

        // ---------------- file / gói mạng cho server ----------------
        // Binary (MyWriter/MyReader, big-endian), dùng chung cho file mapobjects.bin và gói MAP_OBJECT:
        //   bảng:     short schema, short count { short id, UTF name, UTF displayName, byte kind, byte seats }
        //   chỗ đặt:  short count { int uid, short mapId, short objectId, float x, float z, short rotationY,
        //                           short targetMapId, int targetUid }
        // float ghi dạng int x100 (MyWriter.WriteFloat), góc làm tròn độ. targetMapId -1 = không phải cổng.

        public byte[] ToBytes()
        {
            var w = new MyWriter(2 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)objects.Count));
            foreach (MapObjectTemplate t in objects)
            {
                w.WriteShort(checked((short)t.id));
                w.WriteUTF(t.name ?? "");
                w.WriteUTF(t.displayName ?? "");
                w.WriteByte((byte)t.kind);
                w.WriteByte(checked((byte)t.seats));
            }
            WritePlacements(w, placements);
            return w.ToArray();
        }

        public static void WritePlacements(MyWriter w, IList<MapObjectPlacement> list)
        {
            w.WriteShort(checked((short)list.Count));
            foreach (MapObjectPlacement p in list)
            {
                w.WriteInt(p.uid);
                w.WriteShort(p.mapId);
                w.WriteShort(checked((short)p.objectId));
                w.WriteFloat(p.position.x);
                w.WriteFloat(p.position.y);
                w.WriteShort((short)Mathf.RoundToInt(Mathf.Repeat(p.rotationY, 360f)));
                w.WriteShort(p.targetMapId);
                w.WriteInt(p.targetUid);
            }
        }

        /// <summary>Đọc danh sách chỗ đặt (phần sau bảng trong file). Hỏng thì ném lỗi.</summary>
        public static void ReadPlacements(MyReader r, List<MapObjectPlacement> into) =>
            ReadPlacements(r.ReadInt, r.ReadShort, r.ReadFloat, into);

        /// <summary>
        /// Như trên nhưng nhận hàm đọc thay cho reader: gói mạng của Game đọc bằng MyReader global (Assembly-CSharp), khác lớp
        /// với Game.Net.MyReader nhưng cùng cách ghi (big-endian, float = int / 100), nên dùng chung được một hàm đọc.
        /// </summary>
        public static void ReadPlacements(Func<int> readInt, Func<short> readShort, Func<float> readFloat, List<MapObjectPlacement> into)
        {
            int count = readShort();
            if (count < 0 || count > 30000) throw new FormatException("bad map object count " + count);
            for (int i = 0; i < count; i++)
            {
                // tách từng lần đọc ra biến: thứ tự đọc phải đúng thứ tự ghi
                int uid = readInt();
                short mapId = readShort();
                short objectId = readShort();
                float x = readFloat(), z = readFloat();
                short rotationY = readShort();
                short targetMapId = readShort();
                int targetUid = readInt();
                into.Add(new MapObjectPlacement(uid, mapId, objectId, new Vector2(x, z), rotationY, targetMapId, targetUid));
            }
        }

        /// <summary>
        /// Đọc file từ <see cref="ToBytes"/> (server C# dùng lại được, không tạo ScriptableObject). false và lý do khi hỏng,
        /// schema mới hơn, id / uid trùng.
        /// </summary>
        public static bool Read(byte[] bytes, List<MapObjectTemplate> objects, List<MapObjectPlacement> placements, out string error)
        {
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty map object data"; return false; }
            try
            {
                var r = new MyReader(bytes);
                int schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "map object schema too new (" + schema + ")"; return false; }
                int count = r.ReadShort();
                if (count < 0 || count > 30000) { error = "bad map object template count"; return false; }
                var ids = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    var t = new MapObjectTemplate
                    {
                        id = r.ReadShort(), name = r.ReadUTF(), displayName = r.ReadUTF(),
                        kind = (MapObjectKind)r.ReadByte(), seats = r.ReadByte(),
                    };
                    if (t.id <= 0 || !ids.Add(t.id)) { error = "bad or duplicate map object id " + t.id; return false; }
                    objects.Add(t);
                }
                ReadPlacements(r, placements);
                var uids = new HashSet<(short, int)>();
                foreach (MapObjectPlacement p in placements)
                    if (!uids.Add((p.mapId, p.uid))) { error = "duplicate uid " + p.uid + " in map " + p.mapId; return false; }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad map object data: " + e.Message; return false; }
            return true;
        }
    }
}
