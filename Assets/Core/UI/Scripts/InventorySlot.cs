using Game.Core;
using Game.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Một ô vật phẩm: viền theo màu phẩm chất, icon, số lượng ở góc dưới phải (ẩn khi chỉ có 1), đồ đã cường hóa thì hiện +N.
    /// Dùng cho cả lưới hành trang (InventoryPanel nhân bản từ ô mẫu) lẫn 8 ô trang bị quanh hình nhân vật; ô trang bị có
    /// <see cref="placeholder"/> là hình mờ của loại đồ nên ô trống vẫn nhìn ra là ô nón, ô áo...
    /// </summary>
    public class InventorySlot : MonoBehaviour
    {
        [SerializeField] BaseBtn button;
        [Tooltip("Viền: tô màu phẩm chất của vật phẩm, xám mờ khi ô trống.")]
        [SerializeField] Image border;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text countText;
        [Tooltip("Hình mờ hiện khi ô trống (ô trang bị). Để trống với ô hành trang.")]
        [SerializeField] Sprite placeholder;

        static readonly Color EmptyBorder = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color PlaceholderTint = new Color(0.6f, 0.68f, 0.85f, 0.22f);

        public BaseBtn Button => button;

        /// <summary>Chỉ số ô mà ô này đang vẽ: ô trong Inventory.Slots, hoặc (int)EquipSlot với ô trang bị.</summary>
        public int SlotIndex { get; set; } = -1;

        public void Bind(int slotIndex, ItemTemplate info, Sprite sprite, int count, int level = 0)
        {
            SlotIndex = slotIndex;
            if (border) border.color = info != null ? ItemTemplate.RarityTint(info.rarity) : Color.gray;
            if (icon)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
                icon.enabled = sprite != null;
            }
            if (countText)
            {
                bool show = count > 1 || level > 0;
                countText.gameObject.SetActive(show);
                if (show) countText.text = level > 0 ? "+" + NumberCache.Get(level) : NumberCache.Get(count);
            }
            if (button) button.SetInteractable(true);
        }

        public void BindEmpty(int slotIndex)
        {
            SlotIndex = slotIndex;
            if (border) border.color = EmptyBorder;
            if (icon)
            {
                icon.sprite = placeholder;
                icon.color = PlaceholderTint;
                icon.enabled = placeholder != null;
            }
            if (countText) countText.gameObject.SetActive(false);
            if (button) button.SetInteractable(false);
        }
    }
}
