using System;
using System.Collections.Generic;

namespace Game.Items
{
    /// <summary>
    /// Đồ đang mặc/cầm trên người: mỗi <see cref="EquipSlot"/> giữ một món (id, cấp cường hóa, lỗ và đá khảm; id 0 = trống).
    /// Đồ đang mặc KHÔNG nằm trong hành trang: mặc là lấy ra khỏi túi, tháo là bỏ lại vào túi (<see cref="Game.Players.Player.Equip"/> /
    /// <see cref="Game.Players.Player.Unequip"/> lo phần chuyển qua lại, đổi vũ khí và đổi skin). Player giữ một cái.
    /// </summary>
    public class Equipment
    {
        /// <summary>Số ô: EquipSlot 1..9 (8 ô đồ + ô Skin).</summary>
        public const int SlotCount = 9;

        // chỉ số = (int)EquipSlot, ô 0 (None) bỏ trống
        readonly ItemStack[] gear = new ItemStack[SlotCount + 1];

        /// <summary>Một ô vừa đổi (mặc, tháo, cường hóa, khảm đá hoặc nạp lúc vào game).</summary>
        public event Action<EquipSlot> Changed;

        public static bool IsValid(EquipSlot slot) => slot > EquipSlot.None && (int)slot <= SlotCount;

        /// <summary>Id vật phẩm ở ô, 0 = trống.</summary>
        public int this[EquipSlot slot] => IsValid(slot) ? gear[(int)slot].itemId : 0;

        /// <summary>Cấp cường hóa của món ở ô, 0 = chưa cường hóa.</summary>
        public int Level(EquipSlot slot) => IsValid(slot) ? gear[(int)slot].level : 0;

        /// <summary>Món ở ô, đủ cấp, lỗ và đá khảm; ô trống trả về stack rỗng.</summary>
        public ItemStack Stack(EquipSlot slot) => IsValid(slot) ? gear[(int)slot] : default;

        /// <summary>Đặt thẳng một ô, không đụng tới hành trang. Dùng cho Player (sau khi đã chuyển đồ) và lúc nạp.</summary>
        public void Set(EquipSlot slot, ItemStack stack)
        {
            if (!IsValid(slot)) return;
            if (stack.itemId <= 0) stack = default;
            else stack.count = 1;
            if (gear[(int)slot].SameGear(stack)) return;
            gear[(int)slot] = stack;
            Changed?.Invoke(slot);
        }

        /// <summary>Đặt món chưa cường hóa vào ô.</summary>
        public void Set(EquipSlot slot, int itemId) => Set(slot, new ItemStack(itemId, 1));

        /// <summary>Chép id các món đang mặc (trừ vũ khí: PlayerData lưu vũ khí riêng ở weaponId) để lưu.</summary>
        public void SaveArmor(List<int> into)
        {
            into.Clear();
            for (int i = 1; i <= SlotCount; i++)
                if (gear[i].itemId > 0 && i != (int)EquipSlot.Weapon) into.Add(gear[i].itemId);
        }

        /// <summary>Chép đủ các ô (chỉ số = (int)EquipSlot, kể cả vũ khí) để lưu cấp, lỗ và đá khảm.</summary>
        public void SaveGear(List<ItemStack> into)
        {
            into.Clear();
            into.AddRange(gear);
        }
    }
}
