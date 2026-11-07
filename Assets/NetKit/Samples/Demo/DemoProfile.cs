using System;
using System.Collections.Generic;

namespace NetKit.Samples
{
    /// <summary>Lệnh hồ sơ của cửa hàng gửi lên server. Phía mạng (DemoGame) cài đặt bằng gói SHOP.</summary>
    public interface IShopRemote
    {
        void Buy(string item, int count);
    }

    [Serializable]
    public class BagSlot
    {
        public string item;
        public int count;
    }

    /// <summary>
    /// Hồ sơ người chơi, cùng định dạng JSON với players.profile của server (core/start_profile.json). Đọc bằng JsonUtility nên
    /// chỉ dùng trường public, mảng thay cho Dictionary.
    /// </summary>
    [Serializable]
    public class DemoProfile
    {
        public const int MaxCount = 99;

        public long gold;
        public List<BagSlot> bag = new List<BagSlot>();

        public int CountOf(string item)
        {
            foreach (BagSlot s in bag) if (s.item == item) return s.count;
            return 0;
        }

        /// <summary>
        /// Mua count món item. Cùng luật với Shop.buy của server: kiểm, sửa hồ sơ tại máy để UI đổi ngay, rồi gửi lệnh. Trả lý
        /// do từ chối, null khi mua được. Server luôn trả PROFILE SYNC: nếu server từ chối, bản của nó ghi đè phần vừa làm.
        /// </summary>
        public string Buy(ShopTable shop, string item, int count)
        {
            long price = shop.PriceOf(item);
            if (price <= 0) return "Không bán " + item + ".";
            if (count < 1 || count > MaxCount) return "Số lượng 1-" + MaxCount + ".";
            long cost = price * count;
            if (gold < cost) return "Không đủ vàng.";
            gold -= cost;
            BagSlot slot = bag.Find(s => s.item == item);
            if (slot == null) bag.Add(slot = new BagSlot { item = item });
            slot.count += count;
            NetSync<IShopRemote>.Send?.Buy(item, count);
            return null;
        }
    }

    [Serializable]
    public class ShopItem
    {
        public string id;
        public long price;
    }

    /// <summary>Bảng giá, template 1 (templates/shop.json) tải từ server. Server đọc cùng file này để kiểm lệnh mua.</summary>
    [Serializable]
    public class ShopTable
    {
        public List<ShopItem> items = new List<ShopItem>();

        public long PriceOf(string id)
        {
            foreach (ShopItem i in items) if (i.id == id) return i.price;
            return 0;
        }
    }
}
