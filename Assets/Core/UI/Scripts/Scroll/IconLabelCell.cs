using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Ô có ảnh + chữ trên một nút, dùng chung cho các danh sách chọn (tư thế, skill, map). Bố cục (ảnh trên / chữ dưới, hay
    /// ảnh trái / chữ phải) do tool Editor dựng sẵn trong ô mẫu; lúc chạy chỉ gắn chữ, ảnh, màu.
    /// </summary>
    public class IconLabelCell : MonoBehaviour
    {
        [SerializeField] TextBtn button;
        [SerializeField] Image icon;

        public TextBtn Button => button;

        public void Set(string label, Sprite sprite)
        {
            if (icon)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (button) button.SetText(label);
        }

        public void SetColor(Color color)
        {
            if (button) button.SetColorText(color);
        }
    }
}
