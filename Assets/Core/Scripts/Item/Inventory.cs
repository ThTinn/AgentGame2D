using System;
using System.Collections.Generic;

namespace Game.Items
{
    /// <summary>
    /// Một ô hành trang: vật phẩm nào, bao nhiêu cái. itemId 0 = ô trống. Trang bị mang thêm cấp cường hóa (+N), số lỗ đã
    /// khoét và đá khảm từng lỗ; các trường này đi theo món khi mặc / tháo / sắp xếp. Giữ trường rời (không dùng mảng) để
    /// struct chép theo giá trị, không có hai món dùng chung một mảng.
    /// </summary>
    [Serializable]
    public struct ItemStack
    {
        /// <summary>Số lỗ tối đa trên một món.</summary>
        public const int MaxSockets = 3;

        public int itemId;
        public int count;
        public int level;
        public int sockets;
        public int gem0, gem1, gem2;

        public ItemStack(int itemId, int count, int level = 0)
        {
            this.itemId = itemId;
            this.count = count;
            this.level = level;
            sockets = gem0 = gem1 = gem2 = 0;
        }

        public bool IsEmpty => itemId <= 0 || count <= 0;

        /// <summary>Món chưa cường hóa, chưa khoét lỗ: gộp chồng và sắp xếp như đồ thường được.</summary>
        public bool IsPlain => level <= 0 && sockets <= 0;

        /// <summary>Id đá khảm ở lỗ <paramref name="index"/>, 0 = lỗ trống.</summary>
        public int Gem(int index)
        {
            switch (index)
            {
                case 0: return gem0;
                case 1: return gem1;
                case 2: return gem2;
                default: return 0;
            }
        }

        public void SetGem(int index, int itemId)
        {
            switch (index)
            {
                case 0: gem0 = itemId; break;
                case 1: gem1 = itemId; break;
                case 2: gem2 = itemId; break;
            }
        }

        /// <summary>Cùng món, cùng cấp, cùng lỗ và đá.</summary>
        public bool SameGear(in ItemStack o) =>
            itemId == o.itemId && level == o.level && sockets == o.sockets && gem0 == o.gem0 && gem1 == o.gem1 && gem2 == o.gem2;
    }

    /// <summary>
    /// Hành trang của một người chơi: một dãy ô cố định (<see cref="Capacity"/>), mỗi ô giữ một <see cref="ItemStack"/>.
    /// Vật phẩm xếp chồng được (ngọc, chìa khóa) dồn vào ô đã có tới <see cref="ItemTemplate.StackLimit"/> rồi mới sang ô mới;
    /// vật phẩm không xếp chồng (vũ khí, maxStack = 1) mỗi cái một ô. Ô giữ nguyên chỗ khi ô khác trống ra,
    /// gọi <see cref="Sort"/> để dồn lại. Chưa lưu xuống đâu cả. Player giữ một cái.
    /// </summary>
    public class Inventory
    {
        public const int DefaultCapacity = 48;

        readonly ItemStack[] slots;
        // tổng số của từng id, để Count/Has không phải quét cả dãy ô
        readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        readonly List<ItemStack> sortBuffer = new List<ItemStack>();

        /// <summary>Bắn mỗi khi số lượng hay thứ tự ô đổi. UI nghe cái này để vẽ lại.</summary>
        public event Action Changed;

        public Inventory(int capacity = DefaultCapacity)
        {
            slots = new ItemStack[Math.Max(1, capacity)];
        }

        public int Capacity => slots.Length;

        /// <summary>Các ô theo thứ tự, kể cả ô trống.</summary>
        public IReadOnlyList<ItemStack> Slots => slots;

        /// <summary>Tổng số theo id vật phẩm (gộp mọi ô).</summary>
        public IReadOnlyDictionary<int, int> All => counts;

        /// <summary>Số ô đang có đồ.</summary>
        public int UsedSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Length; i++) if (!slots[i].IsEmpty) n++;
                return n;
            }
        }

        public ItemStack this[int slot] => slot >= 0 && slot < slots.Length ? slots[slot] : default;

        public int Count(int itemId) => counts.TryGetValue(itemId, out int n) ? n : 0;

        public bool Has(int itemId, int amount = 1) => amount <= 0 || Count(itemId) >= amount;

        /// <summary>
        /// Bỏ vật phẩm vào: lấp các ô cùng loại còn chỗ trước, rồi mới lấy ô trống.
        /// Trả về số cái KHÔNG bỏ vào được vì hết ô (0 = vào hết).
        /// </summary>
        public int Add(int itemId, int amount = 1)
        {
            if (itemId <= 0 || amount <= 0) return 0;

            int limit = StackLimit(itemId);
            int left = amount;

            if (limit > 1)
            {
                for (int i = 0; i < slots.Length && left > 0; i++)
                {
                    if (slots[i].itemId != itemId || slots[i].count >= limit) continue;
                    int put = Math.Min(left, limit - slots[i].count);
                    slots[i].count += put;
                    left -= put;
                }
            }

            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                if (!slots[i].IsEmpty) continue;
                int put = Math.Min(left, limit);
                slots[i] = new ItemStack(itemId, put);
                left -= put;
            }

            int added = amount - left;
            if (added > 0)
            {
                counts[itemId] = Count(itemId) + added;
                Changed?.Invoke();
            }
            return left;
        }

        /// <summary>Bỏ một món trang bị (giữ cấp, lỗ, đá khảm) vào ô trống đầu tiên. false khi hết ô.</summary>
        public bool AddGear(ItemStack gear)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty) continue;
                gear.count = 1;
                slots[i] = gear;
                counts[gear.itemId] = Count(gear.itemId) + 1;
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Trừ số lượng, lấy từ các ô phía sau trước, món đã cường hóa lấy sau cùng. Trả false (và không trừ gì) khi không đủ.
        /// </summary>
        public bool Remove(int itemId, int amount = 1)
        {
            if (amount <= 0) return true;
            if (!Has(itemId, amount)) return false;

            int left = amount;
            for (int pass = 0; pass < 2 && left > 0; pass++)
            {
                for (int i = slots.Length - 1; i >= 0 && left > 0; i--)
                {
                    if (slots[i].itemId != itemId || (pass == 0 && !slots[i].IsPlain)) continue;
                    int take = Math.Min(left, slots[i].count);
                    Take(i, take);
                    left -= take;
                }
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Trừ số lượng ở đúng một ô. Trả false (và không trừ gì) khi ô không đủ.</summary>
        public bool RemoveAt(int slot, int amount = 1)
        {
            if (slot < 0 || slot >= slots.Length || amount <= 0) return false;
            if (slots[slot].IsEmpty || slots[slot].count < amount) return false;

            Take(slot, amount);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Đổi món ở một ô (đang có đúng 1 cái) lấy một cái khác, giữ nguyên chỗ. Dùng khi mặc đồ: món vừa tháo ra nằm vào
        /// đúng ô của món vừa mặc lên, thay vì chạy sang ô trống đầu tiên. Trả false (không đổi gì) khi ô không có đúng 1 cái.
        /// </summary>
        public bool ReplaceAt(int slot, ItemStack gear)
        {
            if (slot < 0 || slot >= slots.Length || gear.itemId <= 0) return false;
            if (slots[slot].IsEmpty || slots[slot].count != 1) return false;

            Take(slot, 1);
            gear.count = 1;
            slots[slot] = gear;
            counts[gear.itemId] = Count(gear.itemId) + 1;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Đổi cấp / lỗ / đá khảm của món ở một ô, giữ nguyên id và số lượng. Trả false khi ô trống.</summary>
        public bool SetGear(int slot, ItemStack gear)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot].IsEmpty) return false;
            gear.itemId = slots[slot].itemId;
            gear.count = slots[slot].count;
            slots[slot] = gear;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Dồn đồ lên đầu, gộp các ô cùng loại chưa đầy và xếp theo loại > phẩm chất (cao trước) > id > cấp cường hóa.
        /// Món đã cường hóa hoặc khoét lỗ giữ nguyên cấp, lỗ và đá khảm.
        /// </summary>
        public void Sort()
        {
            sortBuffer.Clear();
            foreach (KeyValuePair<int, int> pair in counts)
            {
                int left = pair.Value;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].itemId != pair.Key || slots[i].IsPlain) continue;
                    sortBuffer.Add(slots[i]);
                    left -= slots[i].count;
                }
                int limit = StackLimit(pair.Key);
                for (; left > 0; left -= limit)
                    sortBuffer.Add(new ItemStack(pair.Key, Math.Min(left, limit)));
            }
            // chỉ xảy ra khi maxStack trong bảng bị giảm sau khi đã nhặt đồ: không đủ ô để xếp lại thì giữ nguyên
            if (sortBuffer.Count > slots.Length) return;
            sortBuffer.Sort(Compare);

            for (int i = 0; i < slots.Length; i++) slots[i] = i < sortBuffer.Count ? sortBuffer[i] : default;
            Changed?.Invoke();
        }

        /// <summary>Nạp lại đúng các ô đã lưu (giữ vị trí). Ô thừa so với sức chứa bị bỏ.</summary>
        public void Load(IReadOnlyList<ItemStack> saved)
        {
            Array.Clear(slots, 0, slots.Length);
            counts.Clear();
            if (saved != null)
            {
                for (int i = 0; i < saved.Count && i < slots.Length; i++)
                {
                    ItemStack s = saved[i];
                    if (s.IsEmpty) continue;
                    slots[i] = s;
                    counts[s.itemId] = Count(s.itemId) + s.count;
                }
            }
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (counts.Count == 0) return;
            Array.Clear(slots, 0, slots.Length);
            counts.Clear();
            Changed?.Invoke();
        }

        void Take(int slot, int amount)
        {
            int id = slots[slot].itemId;
            slots[slot].count -= amount;
            if (slots[slot].count <= 0) slots[slot] = default;

            int total = Count(id) - amount;
            if (total > 0) counts[id] = total;
            else counts.Remove(id);
        }

        // chưa có bảng vật phẩm (hoặc id lạ) thì coi như xếp chồng không giới hạn, giống hành trang cũ chỉ đếm số
        static int StackLimit(int itemId)
        {
            ItemTemplate info = ItemDatabase.Current != null ? ItemDatabase.Current.Get(itemId) : null;
            return info != null ? info.StackLimit : int.MaxValue;
        }

        static int Compare(ItemStack a, ItemStack b)
        {
            ItemDatabase table = ItemDatabase.Current;
            ItemTemplate x = table != null ? table.Get(a.itemId) : null;
            ItemTemplate y = table != null ? table.Get(b.itemId) : null;
            if (x != null && y != null)
            {
                if (x.kind != y.kind) return ((int)y.kind).CompareTo((int)x.kind);   // đồ mặc > vũ khí > chìa > ngọc
                if (x.rarity != y.rarity) return ((int)y.rarity).CompareTo((int)x.rarity);
            }
            if (a.itemId != b.itemId) return a.itemId.CompareTo(b.itemId);
            if (a.level != b.level) return b.level.CompareTo(a.level);
            if (a.sockets != b.sockets) return b.sockets.CompareTo(a.sockets);
            return b.count.CompareTo(a.count);
        }
    }
}
