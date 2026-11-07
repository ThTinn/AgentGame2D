using System;
using System.Text;

namespace NetKit
{
    /// <summary>Ghi thân một gói, cùng định dạng <see cref="PacketReader"/> đọc. Mỗi gói một writer, không dùng lại sau khi gửi.</summary>
    public sealed class PacketWriter
    {
        byte[] buf = new byte[64];
        int len;

        /// <summary>Số byte đã ghi.</summary>
        public int Length => len;

        void Grow(int n)
        {
            if (len + n <= buf.Length) return;
            Array.Resize(ref buf, Math.Max(buf.Length * 2, len + n));
        }

        public void WriteByte(int v)
        {
            Grow(1);
            buf[len++] = (byte)v;
        }

        public void WriteBool(bool v) => WriteByte(v ? 1 : 0);

        public void WriteShort(int v)
        {
            Grow(2);
            buf[len++] = (byte)(v >> 8);
            buf[len++] = (byte)v;
        }

        public void WriteInt(int v)
        {
            Grow(4);
            buf[len++] = (byte)(v >> 24);
            buf[len++] = (byte)(v >> 16);
            buf[len++] = (byte)(v >> 8);
            buf[len++] = (byte)v;
        }

        public void WriteLong(long v)
        {
            WriteInt((int)(v >> 32));
            WriteInt((int)v);
        }

        /// <summary>Chuỗi: short độ dài rồi UTF-8. null ghi thành chuỗi rỗng.</summary>
        public void WriteUTF(string s)
        {
            byte[] raw = Encoding.UTF8.GetBytes(s ?? string.Empty);
            if (raw.Length > short.MaxValue) throw new ArgumentException("Chuoi qua dai: " + raw.Length + " byte");
            WriteShort(raw.Length);
            WriteBytes(raw);
        }

        public void WriteBytes(byte[] b)
        {
            if (b == null || b.Length == 0) return;
            Grow(b.Length);
            Buffer.BlockCopy(b, 0, buf, len, b.Length);
            len += b.Length;
        }

        /// <summary>Bản sao các byte đã ghi.</summary>
        public byte[] ToArray()
        {
            var b = new byte[len];
            Buffer.BlockCopy(buf, 0, b, 0, len);
            return b;
        }
    }
}
