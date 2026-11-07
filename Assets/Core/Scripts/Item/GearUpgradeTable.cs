using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;
using Game.Players;

namespace Game.Items
{
    /// <summary>Loại đá dùng cho trang bị (ItemTemplate.value của ItemKind.Stone). Giá trị cố định vì được ghi xuống file.</summary>
    public enum GearStone : byte
    {
        None = 0,
        Upgrade = 1,  // đá nâng cấp: bắt buộc, số viên theo từng cấp
        Lucky = 2,    // đá may mắn: mỗi lần dùng một viên, cộng tỉ lệ
        Protect = 3,  // đá bảo vệ: chỉ mất khi cứu món khỏi tụt cấp
        Drill = 4,    // đá khoét lỗ: bắt buộc khi khoét, số viên theo từng lỗ
    }

    /// <summary>Lý do chưa cường hóa / khoét lỗ / khảm được (UI hiện cho người chơi); None là được.</summary>
    public enum GearBlock : byte
    {
        None, NoTable, NoItem, NotGear, Maxed, NeedStone, NeedLucky, NeedProtect, NeedGold,
        NoSocket,     // lỗ chưa khoét
        SocketFull,   // lỗ đã có đá
        SocketEmpty,  // lỗ chưa có đá để gỡ
        NotJewel,     // vật phẩm không phải đá khảm
        NeedJewel,    // hết đá khảm này
        BagFull,      // gỡ đá nhưng túi không còn chỗ
        SameJewel,    // món đã khảm loại đá này
    }

    /// <summary>Kết quả một lần cường hóa / khoét lỗ. Giá trị cố định vì được gửi trong gói UPGRADE_RESULT, SOCKET_RESULT.</summary>
    public enum GearOutcome : byte
    {
        Refused = 0,    // không làm được, không mất gì
        Pending = 1,    // client online: đã gửi lên server, chờ UPGRADE_RESULT
        Success = 2,    // lên một cấp
        Failed = 3,     // trượt, giữ cấp
        Dropped = 4,    // trượt, tụt một cấp
        Protected = 5,  // trượt, đá bảo vệ giữ cấp
    }

    /// <summary>Một lần nâng từ cấp +i lên +(i+1).</summary>
    [Serializable]
    public class GearUpgradeStep
    {
        [Tooltip("Tỉ lệ thành công, % (0-100).")]
        [Range(0f, 100f)] public float chance = 100f;
        [Tooltip("Số đá nâng cấp mất mỗi lần thử.")]
        [Min(1)] public int stones = 1;
        [Tooltip("Vàng mất mỗi lần thử.")]
        [Min(0)] public long gold;
        [Tooltip("Trượt thì tụt một cấp (đá bảo vệ giữ lại được).")]
        public bool dropOnFail;
        [Tooltip("Số đá bảo vệ mất khi cứu món khỏi tụt cấp. Tăng theo cấp: cấp càng cao, làm lại càng đắt.")]
        [Min(1)] public int protectStones = 1;
    }

    /// <summary>Một lần khoét lỗ thứ i+1 trên món. Trượt thì mất đá và vàng, món giữ nguyên.</summary>
    [Serializable]
    public class GearSocketStep
    {
        [Tooltip("Tỉ lệ thành công, % (0-100).")]
        [Range(0f, 100f)] public float chance = 100f;
        [Tooltip("Số đá khoét lỗ mất mỗi lần thử.")]
        [Min(1)] public int stones = 1;
        [Tooltip("Vàng mất mỗi lần thử.")]
        [Min(0)] public long gold;
    }

    /// <summary>
    /// Bảng luật cường hóa (+1, +2...), khoét lỗ và gỡ đá khảm. Một asset ở Core/Data/Items/GearUpgradeTable.asset, dựng bằng
    /// Tools/Core/Items/Build Gear Upgrade. Cùng khuôn với các bảng khác: đăng ký qua CoreSystems.Use, đọc ở <see cref="Current"/>.
    /// Luật chạy ở PlayerProfile (UpgradeGear, OpenSocket, InlayGem, RemoveGem); online thì chỉ server quay tỉ lệ.
    /// </summary>
    [CreateAssetMenu(fileName = "GearUpgradeTable", menuName = "Core/Gear Upgrade Table")]
    public class GearUpgradeTable : ScriptableObject
    {
        [Tooltip("Phần tử i là lần nâng từ +i lên +(i+1). Số phần tử = cấp tối đa.")]
        public List<GearUpgradeStep> steps = new List<GearUpgradeStep>();
        [Tooltip("Mỗi cấp cộng thêm chừng này % vào mọi option sẵn có của món.")]
        [Min(0f)] public float optionPerLevel = 8f;
        [Tooltip("Vũ khí: mỗi cấp cộng thêm chừng này % sát thương đòn đánh thường (vũ khí không có option sẵn).")]
        [Min(0f)] public float weaponPowerPerLevel = 3f;
        [Tooltip("Đá may mắn cộng chừng này % vào tỉ lệ thành công.")]
        [Min(0f)] public float luckyBonus = 10f;

        [Header("Khoét lỗ, khảm đá")]
        [Tooltip("Phần tử i là lần khoét lỗ thứ i+1. Số phần tử = số lỗ tối đa, không quá ItemStack.MaxSockets.")]
        public List<GearSocketStep> sockets = new List<GearSocketStep>();
        [Tooltip("Vàng mất khi gỡ đá khảm ra khỏi món. Đá trả về túi.")]
        [Min(0)] public long removeGemGold = 200;

        static GearUpgradeTable current;

        /// <summary>Bảng của game; null khi chưa ai đăng ký (không cường hóa được, cấp đã có coi như không cộng gì).</summary>
        public static GearUpgradeTable Current => current;

        public static void Use(GearUpgradeTable table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("GearUpgradeTable: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public const int CurrentSchema = 1;

        public int MaxLevel => steps.Count;

        /// <summary>Lần nâng từ cấp <paramref name="level"/>; null khi đã tối đa.</summary>
        public GearUpgradeStep Step(int level) => level >= 0 && level < steps.Count ? steps[level] : null;

        /// <summary>Số lỗ tối đa trên một món.</summary>
        public int MaxSockets => Mathf.Min(sockets.Count, ItemStack.MaxSockets);

        /// <summary>Lần khoét khi món đang có <paramref name="opened"/> lỗ; null khi đã đủ lỗ.</summary>
        public GearSocketStep SocketStep(int opened) => opened >= 0 && opened < MaxSockets ? sockets[opened] : null;

        /// <summary>Tỉ lệ thành công thật (đã cộng đá may mắn), % trong 0..100.</summary>
        public float ChanceOf(GearUpgradeStep step, bool lucky) =>
            Mathf.Clamp(step.chance + (lucky ? luckyBonus : 0f), 0f, 100f);

        /// <summary>
        /// Chỉ số của món ở cấp <paramref name="level"/>: option sẵn có nhân theo cấp, vũ khí thêm % đòn đánh thường.
        /// Chép vào <paramref name="into"/>. Trang bị và UI (xem trước cấp sau) dùng chung. Option gốc của đòn đánh thường
        /// không vào đây: cường hóa vũ khí tăng qua WeaponPower, còn gốc đọc bằng SkillTemplate.Stats.
        /// </summary>
        public static void EffectsOf(ItemTemplate item, int level, List<PlayerStatEffect> into)
        {
            into.Clear();
            if (item == null) return;
            GearUpgradeTable table = current;
            float scale = table != null && level > 0 ? 1f + table.optionPerLevel * level / 100f : 1f;
            if (item.options != null)
                foreach (PlayerStatEffect e in item.options)
                    if (e.stat < PlayerStat.AttackDamage) into.Add(new PlayerStatEffect(e.stat, e.value * scale));
            if (table != null && level > 0 && item.kind == ItemKind.Weapon)
                into.Add(new PlayerStatEffect(PlayerStat.WeaponPower, table.weaponPowerPerLevel * level));
        }

        /// <summary>
        /// Thêm option của các đá đã khảm trên <paramref name="gear"/> vào <paramref name="into"/> (không xóa danh sách).
        /// Cấp cường hóa không nhân lên đá khảm.
        /// </summary>
        public static void AddGemEffects(in ItemStack gear, List<PlayerStatEffect> into)
        {
            ItemDatabase items = ItemDatabase.Current;
            if (items == null) return;
            for (int i = 0; i < gear.sockets; i++)
            {
                ItemTemplate gem = gear.Gem(i) > 0 ? items.Get(gear.Gem(i)) : null;
                if (gem?.options != null) into.AddRange(gem.options);
            }
        }

        // ---------------- file cho server ----------------

        // Binary (MyWriter/MyReader, big-endian), file gear.bin gửi cho server:
        //   short schema, float optionPerLevel, float weaponPowerPerLevel, float luckyBonus, long removeGemGold,
        //   byte stepCount { float chance, short stones, long gold, bool dropOnFail, short protectStones },
        //   byte socketCount { float chance, short stones, long gold }
        public byte[] ToBytes()
        {
            var w = new MyWriter(256);
            w.WriteShort(CurrentSchema);
            w.WriteFloat(optionPerLevel);
            w.WriteFloat(weaponPowerPerLevel);
            w.WriteFloat(luckyBonus);
            w.WriteLong(removeGemGold);
            w.WriteByte(checked((byte)steps.Count));
            foreach (GearUpgradeStep s in steps)
            {
                w.WriteFloat(s.chance);
                w.WriteShort(checked((short)s.stones));
                w.WriteLong(s.gold);
                w.WriteBool(s.dropOnFail);
                w.WriteShort(checked((short)s.protectStones));
            }
            w.WriteByte(checked((byte)sockets.Count));
            foreach (GearSocketStep s in sockets)
            {
                w.WriteFloat(s.chance);
                w.WriteShort(checked((short)s.stones));
                w.WriteLong(s.gold);
            }
            return w.ToArray();
        }

        /// <summary>Đọc bảng từ <see cref="ToBytes"/> vào <paramref name="into"/>. False và lý do khi dữ liệu hỏng hay schema mới hơn.</summary>
        public static bool Read(byte[] bytes, GearUpgradeTable into, out string error)
        {
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty gear data"; return false; }
            try
            {
                var r = new MyReader(bytes);
                int schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "gear schema too new (" + schema + ")"; return false; }
                into.optionPerLevel = r.ReadFloat();
                into.weaponPowerPerLevel = r.ReadFloat();
                into.luckyBonus = r.ReadFloat();
                into.removeGemGold = r.ReadLong();
                into.steps.Clear();
                int count = r.ReadByte();
                for (int i = 0; i < count; i++)
                    into.steps.Add(new GearUpgradeStep
                    {
                        chance = r.ReadFloat(), stones = r.ReadShort(), gold = r.ReadLong(),
                        dropOnFail = r.ReadBool(), protectStones = r.ReadShort(),
                    });
                into.sockets.Clear();
                count = r.ReadByte();
                for (int i = 0; i < count; i++)
                    into.sockets.Add(new GearSocketStep { chance = r.ReadFloat(), stones = r.ReadShort(), gold = r.ReadLong() });
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad gear data: " + e.Message; return false; }
            return true;
        }
    }
}
