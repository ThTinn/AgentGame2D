using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Nút trên HUD để mở/đóng một panel: gắn cạnh BaseBtn (hoặc TextBtn) và chọn mã panel trong Inspector.
    /// Nhờ nó mà panel không phải tự giữ nút mở của mình, HUD và panel tách rời nhau.
    /// </summary>
    public class PanelOpenBtn : MonoBehaviour
    {
        [SerializeField] UIID id = UIID.None;
        [Tooltip("Bật: bấm lần nữa thì đóng panel. Tắt: chỉ mở.")]
        [SerializeField] bool toggle = true;

        BaseBtn button;

        void Awake()
        {
            // không dùng RequireComponent để Unity khỏi tự thêm một BaseBtn thứ hai cạnh TextBtn có sẵn
            button = GetComponent<BaseBtn>();
            if (button == null)
            {
                Debug.LogError("PanelOpenBtn " + name + ": object này chưa có BaseBtn/TextBtn.");
                return;
            }
            button.AddListener(Click);
        }

        void OnDestroy()
        {
            if (button) button.onClick.RemoveListener(Click);
        }

        public void Click()
        {
            UIManager manager = UIManager.Instance;
            if (manager == null || id == UIID.None) return;

            if (toggle) manager.Toggle(id);
            else manager.Show(id);
        }
    }
}
