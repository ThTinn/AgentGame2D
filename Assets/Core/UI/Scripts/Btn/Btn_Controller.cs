using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Nút có chữ và icon. Bản 3D tải icon từ server theo id; ở đây nhận thẳng Sprite.</summary>
    public class Btn_Controller : TextBtn
    {
        public Image icon;

        public void SetIcon(Sprite sprite)
        {
            if (!icon) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        public void SetActive_Icon(bool active)
        {
            if (icon) icon.gameObject.SetActive(active);
        }
    }
}
