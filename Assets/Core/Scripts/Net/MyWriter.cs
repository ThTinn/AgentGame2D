using System;
using System.Text;

namespace Game.Net
{
    /// <summary>Ghi dữ liệu big-endian vào buffer tự giãn. Không phụ thuộc UnityEngine.</summary>
    public sealed class MyWriter
    {
        byte[] buf;
        int len;

        public MyWriter(int capacity = 64)
        {
            buf = new byte[Math.Max(16, capacity)];
        }

        public int Length => len;

        /// <summary>Buffer nội bộ, chỉ hợp lệ trong [0, Length). Đừng giữ tham chiếu lâu.</summary>
        public byte[] Buffer => buf;

        public void Reset() => len = 0;

        public byte[] ToArray()
        {
            var copy = new byte[len];
            Array.Copy(buf, copy, len);
            return copy;
        }

        void Ensure(int extra)
        {
            int need = len + extra;
            if (need <= buf.Length) return;

            int size = buf.Length * 2;
            while (size < need) size *= 2;
            Array.Resize(ref buf, size);
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            buf[len++] = value;
        }

        public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteShort(short value)
        {
            Ensure(2);
            buf[len++] = (byte)(value >> 8);
            buf[len++] = (byte)value;
        }

        public void WriteInt(int value)
        {
            Ensure(4);
            buf[len++] = (byte)(value >> 24);
            buf[len++] = (byte)(value >> 16);
            buf[len++] = (byte)(value >> 8);
            buf[len++] = (byte)value;
        }

        public void WriteLong(long value)
        {
            Ensure(8);
            for (int shift = 56; shift >= 0; shift -= 8) buf[len++] = (byte)(value >> shift);
        }

        /// <summary>
        /// float lưu dạng int(x * 100): độ chính xác 0.01, đủ cho toạ độ map.
        /// Giá trị hỏng (NaN/Infinity) hoặc vượt khoảng int thì ghi giá trị thay thế và cảnh báo, không ném exception:
        /// chỗ gọi là gameplay (Service.SendMove) nên làm văng ở đây là hỏng cả luồng chơi chỉ vì một gói.
        /// </summary>
        public void WriteFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                NetLog.Warn("MyWriter.WriteFloat: giá trị không hợp lệ (" + value + "), ghi 0");
                WriteInt(0);
                return;
            }

            double scaled = Math.Round(value * 100.0);
            if (scaled > int.MaxValue || scaled < int.MinValue)
            {
                NetLog.Warn("MyWriter.WriteFloat: giá trị vượt khoảng (" + value + "), cắt bớt");
                scaled = scaled > 0 ? int.MaxValue : int.MinValue;
            }
            WriteInt((int)scaled);
        }

        public void WriteUTF(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > short.MaxValue)
                throw new ArgumentException("Chuỗi quá dài (" + bytes.Length + " byte)");

            WriteShort((short)bytes.Length);
            WriteBytes(bytes, 0, bytes.Length);
        }

        public void WriteBytes(byte[] src) => WriteBytes(src, 0, src.Length);

        public void WriteBytes(byte[] src, int offset, int count)
        {
            Ensure(count);
            Array.Copy(src, offset, buf, len, count);
            len += count;
        }
    }
}
