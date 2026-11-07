using System;
using System.IO;
using System.Text;

namespace NetKit
{
    /// <summary>
    /// Đọc thân một gói. Số nguyên big-endian (như DataInputStream của Java), chuỗi là short độ dài + UTF-8. Đọc quá cuối gói
    /// thì ném <see cref="EndOfStreamException"/>: handler lỗi chỉ bỏ gói đó, không làm rớt kết nối.
    /// </summary>
    public sealed class PacketReader
    {
        readonly byte[] data;
        int pos;

        public PacketReader(byte[] data)
        {
            this.data = data ?? Array.Empty<byte>();
        }

        /// <summary>Số byte chưa đọc.</summary>
        public int Available => data.Length - pos;

        void Need(int n)
        {
            if (pos + n > data.Length) throw new EndOfStreamException("Goi het du lieu: can " + n + ", con " + Available);
        }

        public byte ReadByte()
        {
            Need(1);
            return data[pos++];
        }

        public sbyte ReadSByte() => unchecked((sbyte)ReadByte());

        public bool ReadBool() => ReadByte() != 0;

        public short ReadShort()
        {
            Need(2);
            int v = data[pos] << 8 | data[pos + 1];
            pos += 2;
            return (short)v;
        }

        public int ReadInt()
        {
            Need(4);
            int v = data[pos] << 24 | data[pos + 1] << 16 | data[pos + 2] << 8 | data[pos + 3];
            pos += 4;
            return v;
        }

        public long ReadLong()
        {
            long hi = (uint)ReadInt();
            long lo = (uint)ReadInt();
            return hi << 32 | lo;
        }

        /// <summary>Chuỗi: short độ dài (0 hoặc âm là rỗng) rồi UTF-8.</summary>
        public string ReadUTF()
        {
            int len = ReadShort();
            if (len <= 0) return string.Empty;
            Need(len);
            string s = Encoding.UTF8.GetString(data, pos, len);
            pos += len;
            return s;
        }

        /// <summary>n byte tiếp theo.</summary>
        public byte[] ReadBytes(int n)
        {
            Need(n);
            var b = new byte[n];
            Buffer.BlockCopy(data, pos, b, 0, n);
            pos += n;
            return b;
        }

        /// <summary>Mọi byte còn lại (dữ liệu file, JSON ở cuối gói).</summary>
        public byte[] ReadRest() => ReadBytes(Available);
    }
}
