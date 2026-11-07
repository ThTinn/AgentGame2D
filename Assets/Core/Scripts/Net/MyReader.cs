using System;
using System.IO;
using System.Text;

namespace Game.Net
{
    /// <summary>Đọc dữ liệu big-endian từ mảng byte. Đọc quá cuối buffer thì ném EndOfStreamException.</summary>
    public sealed class MyReader
    {
        byte[] data;
        int len;   // buffer thuê từ ArrayPool thường dài hơn dữ liệu, nên không dùng data.Length
        int pos;

        public MyReader(byte[] data) : this(data, data != null ? data.Length : 0) { }

        public MyReader(byte[] data, int length)
        {
            Reset(data, length);
        }

        /// <summary>Gắn lại buffer khác để dùng lại object này (Message trong pool).</summary>
        public void Reset(byte[] data, int length)
        {
            this.data = data ?? Array.Empty<byte>();
            len = Math.Min(length, this.data.Length);
            pos = 0;
        }

        public int Available => len - pos;

        void Need(int count)
        {
            if (count < 0 || pos + count > len)
                throw new EndOfStreamException("Đọc quá cuối gói tin (cần " + count + ", còn " + Available + ")");
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
            short v = (short)((data[pos] << 8) | data[pos + 1]);
            pos += 2;
            return v;
        }

        public int ReadInt()
        {
            Need(4);
            int v = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
            return v;
        }

        public long ReadLong()
        {
            Need(8);
            long v = 0;
            for (int i = 0; i < 8; i++) v = (v << 8) | data[pos + i];
            pos += 8;
            return v;
        }

        public float ReadFloat() => ReadInt() / 100f;

        public string ReadUTF()
        {
            short len = ReadShort();
            if (len <= 0) return string.Empty;

            Need(len);
            string s = Encoding.UTF8.GetString(data, pos, len);
            pos += len;
            return s;
        }

        public byte[] ReadBytes(int count)
        {
            if (count <= 0) return Array.Empty<byte>();

            Need(count);
            var result = new byte[count];
            Array.Copy(data, pos, result, 0, count);
            pos += count;
            return result;
        }
    }
}
