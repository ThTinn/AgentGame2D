using UnityEditor;
using UnityEngine;
using Game.Items;

namespace Game.EditorTools
{
    /// <summary>
    /// Menu TẠM để test hành trang / trang bị (Tools/Core/Items/Test). Chỉ chạy lúc Play (chế độ local): bỏ đồ thẳng vào
    /// túi của người chơi mình (CoreSystems.Me). Đồ khởi đầu lâu dài thì sửa PlayerData.startItems.
    /// </summary>
    public static class ItemCheats
    {
        const string Menu = "Tools/Core/Items/Test/";

        [MenuItem(Menu + "Thêm mỗi vật phẩm 1 cái")]
        static void GiveAll()
        {
            ItemDatabase items = ItemDatabase.Current;
            if (!Ready(items)) return;
            int given = 0;
            foreach (ItemTemplate it in items.items) if (CoreSystems.Me.Loot(it.id, 1) == 0) given++;
            Debug.Log($"[Test] Đã thêm {given}/{items.items.Count} vật phẩm vào túi.");
        }

        [MenuItem(Menu + "Thêm 10 ngọc mỗi màu")]
        static void GiveGems()
        {
            ItemDatabase items = ItemDatabase.Current;
            if (!Ready(items)) return;
            foreach (ItemTemplate it in items.OfKind(ItemKind.Gem)) CoreSystems.Me.Loot(it.id, 10);
            Debug.Log("[Test] Đã thêm 10 ngọc mỗi màu.");
        }

        [MenuItem(Menu + "+1000 vàng")]
        static void GiveGold()
        {
            if (!Ready(ItemDatabase.Current)) return;
            CoreSystems.Me.AddGold(1000);
            Debug.Log("[Test] +1000 vàng, giờ có " + CoreSystems.Me.Gold + ".");
        }

        [MenuItem(Menu + "Thêm đá cường hóa và 20000 vàng")]
        static void GiveStones()
        {
            ItemDatabase items = ItemDatabase.Current;
            if (!Ready(items)) return;
            foreach (ItemTemplate it in items.OfKind(ItemKind.Stone)) CoreSystems.Me.Loot(it.id, 20);
            foreach (ItemTemplate it in items.OfKind(ItemKind.Jewel)) CoreSystems.Me.Loot(it.id, 3);
            CoreSystems.Me.AddGold(20000);
            Debug.Log("[Test] Đã thêm 20 đá mỗi loại, 3 đá khảm mỗi loại và 20000 vàng.");
        }

        [MenuItem(Menu + "Thêm mỗi vật phẩm 1 cái", true)]
        [MenuItem(Menu + "Thêm 10 ngọc mỗi màu", true)]
        [MenuItem(Menu + "+1000 vàng", true)]
        [MenuItem(Menu + "Thêm đá cường hóa và 20000 vàng", true)]
        static bool Validate() => Application.isPlaying;

        static bool Ready(ItemDatabase items)
        {
            if (CoreSystems.Me == null)
            {
                Debug.LogWarning("[Test] Chưa có người chơi (CoreSystems.Me): vào Play ở chế độ local trước.");
                return false;
            }
            if (items == null)
            {
                Debug.LogWarning("[Test] Chưa có ItemDatabase (gán vào LocalPlaySettings.items).");
                return false;
            }
            return true;
        }
    }
}
