using UnityEngine;

namespace NetKit
{
    /// <summary>Ghi / đọc kiểu Unity trong gói. vec = 3 int, đơn vị mét x100 (cm), như Packet.vec của server.</summary>
    public static class PacketExtensions
    {
        public static Vector3 ReadVec3(this PacketReader r) => new Vector3(r.ReadInt() / 100f, r.ReadInt() / 100f, r.ReadInt() / 100f);

        public static void WriteVec3(this PacketWriter w, Vector3 v)
        {
            w.WriteInt(Mathf.RoundToInt(v.x * 100));
            w.WriteInt(Mathf.RoundToInt(v.y * 100));
            w.WriteInt(Mathf.RoundToInt(v.z * 100));
        }
    }
}
