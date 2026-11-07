using System.Collections.Generic;
using Game.Net;

namespace Game.Story
{
    /// <summary>
    /// Ghi / đọc các khối logic dùng chung của cốt truyện (điều kiện, hậu quả, lựa chọn) cho file gửi server
    /// (npcs.bin, quests.bin; MyWriter / MyReader, big-endian). Chỉ ghi phần server cần để kiểm tra và tính toán: mã, điều
    /// kiện, số liệu, hậu quả. Chữ hiển thị (label, lời thoại, tên kết cục) KHÔNG ghi: server không cần, file nhỏ hơn, và sửa
    /// câu chữ / dịch thì không phải cập nhật server. Client vẫn đọc chữ từ asset (và bản dịch trong Lang).
    /// </summary>
    /// <remarks>
    /// Chuỗi null ghi thành "" (đọc ra ""; mọi chỗ kiểm tra đều dùng IsNullOrEmpty). Số đếm danh sách là byte (tối đa 255).
    /// </remarks>
    public static class StoryBinary
    {
        //   condition: UTF requiresFlag, UTF forbidsFlag, UTF tierNpc, sbyte minTier, byte minAllies, sbyte allyTier,
        //              UTF item, short itemCount
        public static void Write(MyWriter w, StoryCondition c)
        {
            c ??= new StoryCondition();
            w.WriteUTF(c.requiresFlag ?? "");
            w.WriteUTF(c.forbidsFlag ?? "");
            w.WriteUTF(c.tierNpc ?? "");
            w.WriteSByte((sbyte)c.minTier);
            w.WriteByte(checked((byte)c.minAllies));
            w.WriteSByte((sbyte)c.allyTier);
            w.WriteUTF(c.item ?? "");
            w.WriteShort(checked((short)c.itemCount));
        }

        public static StoryCondition ReadCondition(MyReader r) => new StoryCondition
        {
            requiresFlag = r.ReadUTF(), forbidsFlag = r.ReadUTF(), tierNpc = r.ReadUTF(),
            minTier = (AffinityTier)r.ReadSByte(), minAllies = r.ReadByte(), allyTier = (AffinityTier)r.ReadSByte(),
            item = r.ReadUTF(), itemCount = r.ReadShort(),
        };

        //   effects: byte count { byte kind, UTF target, int amount }
        public static void Write(MyWriter w, List<StoryEffect> effects)
        {
            int n = effects != null ? effects.Count : 0;
            w.WriteByte(checked((byte)n));
            for (int i = 0; i < n; i++)
            {
                StoryEffect e = effects[i] ?? new StoryEffect();
                w.WriteByte((byte)e.kind);
                w.WriteUTF(e.target ?? "");
                w.WriteInt(e.amount);
            }
        }

        public static List<StoryEffect> ReadEffects(MyReader r)
        {
            int n = r.ReadByte();
            var list = new List<StoryEffect>(n);
            for (int i = 0; i < n; i++)
                list.Add(new StoryEffect { kind = (EffectKind)r.ReadByte(), target = r.ReadUTF(), amount = r.ReadInt() });
            return list;
        }

        //   choices: byte count { UTF name, condition, effects, UTF flag }
        public static void Write(MyWriter w, List<StoryChoice> choices)
        {
            int n = choices != null ? choices.Count : 0;
            w.WriteByte(checked((byte)n));
            for (int i = 0; i < n; i++)
            {
                StoryChoice c = choices[i] ?? new StoryChoice();
                w.WriteUTF(c.name ?? "");
                Write(w, c.condition);
                Write(w, c.effects);
                w.WriteUTF(c.flag ?? "");
            }
        }

        /// <summary>Đọc danh sách lựa chọn; false khi có mã trống hoặc trùng (gói tin chọn theo mã nên mã phải duy nhất).</summary>
        public static bool ReadChoices(MyReader r, List<StoryChoice> into, out string error)
        {
            error = null;
            int n = r.ReadByte();
            var names = new HashSet<string>();
            for (int i = 0; i < n; i++)
            {
                var c = new StoryChoice { name = r.ReadUTF(), condition = ReadCondition(r), effects = ReadEffects(r), flag = r.ReadUTF() };
                if (string.IsNullOrEmpty(c.name) || !names.Add(c.name)) { error = "choice name empty or duplicate: '" + c.name + "'"; return false; }
                into.Add(c);
            }
            return true;
        }

        /// <summary>Mã trống hoặc trùng trong một danh sách (dùng cho kiểm tra lúc dựng dữ liệu). null = ổn.</summary>
        public static string FindBadName(IEnumerable<string> names)
        {
            var seen = new HashSet<string>();
            foreach (string n in names)
            {
                if (string.IsNullOrEmpty(n)) return "(trống)";
                if (!seen.Add(n)) return n;
            }
            return null;
        }
    }
}
