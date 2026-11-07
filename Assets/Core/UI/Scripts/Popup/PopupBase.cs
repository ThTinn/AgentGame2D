using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Mã của từng popup. Giá trị được ghi cố định để thêm popup mới không làm lệch dữ liệu đã lưu trong Popup_Config.asset.
    /// None phải là 0 (UIStack dùng nó làm mã rỗng).
    /// Thêm popup: thêm một mã ở đây, tạo prefab, khai báo cặp (mã, prefab) trong Popup_Config.
    /// </summary>
    public enum PopupID
    {
        None = 0,
        Selection = 1,
        Notice = 2,
        Input = 3,
        Loading = 4,
        /// <summary>Thông tin một ô hành trang: icon, tên, phẩm chất, mô tả, nút Trang bị / Bỏ.</summary>
        ItemInfo = 5,
        /// <summary>Hội thoại NPC: khung dưới màn hình, ảnh + tên NPC, chữ hiện dần, lựa chọn xếp dọc.</summary>
        NpcDialog = 6,
        /// <summary>Thông tin một người chơi khác (mở từ thẻ mục tiêu): cấp, HP / MP, vũ khí, trang bị. Dựng bằng code, không prefab.</summary>
        PlayerInfo = 7,
        /// <summary>Tổ đội & bang (chơi online): thành viên, rời tổ đội, lập / vào / rời bang. Dựng bằng code, không prefab.</summary>
        Social = 8,
        /// <summary>Chuyển khu của map (chơi online): số người từng khu, bấm để sang khu đó. Dựng bằng code, không prefab.</summary>
        Zone = 9,
    }

    /// <summary>
    /// Lớp cơ sở của mọi popup. PopupMN tạo, bật/tắt và xếp lớp popup; popup con chỉ lo nội dung của mình.
    /// Vòng đời: OnInit (một lần, lúc tạo) > OnShow (mỗi lần mở) > OnClose (mỗi lần đóng).
    /// Đừng tự gọi SetActive từ ngoài, dùng PopupMN.Show/Close để history và phím Esc chạy đúng.
    /// </summary>
    public abstract class PopupBase : MonoBehaviour, IUIView<PopupID>
    {
        [Tooltip("Popup đang mở thì người chơi không điều khiển được nhân vật (PlayerInputHandler bỏ qua input).")]
        [SerializeField] bool blockPlayerMove = true;

        public PopupID ID { get; set; }

        public bool BlockPlayerMove
        {
            get => blockPlayerMove;
            set => blockPlayerMove = value;
        }

        /// <summary>Đóng là hủy luôn GameObject. Mặc định false: popup được giữ lại để mở lần sau không phải Instantiate.</summary>
        public virtual bool DestroyOnClose => false;

        /// <summary>Toast (thông báo trôi): không vào history, không nhận phím, không chặn input.</summary>
        public virtual bool IsToast => false;

        /// <summary>Gọi Show khi popup đang mở thì xếp hàng chờ thay vì ghi đè nội dung đang hiện.</summary>
        public virtual bool UseQueue => false;

        bool IUIView<PopupID>.SkipHistory => IsToast;

        /// <summary>Gọi một lần ngay sau khi PopupMN Instantiate prefab.</summary>
        public virtual void OnInit() { }

        public virtual void OnShow()
        {
            gameObject.SetActive(true);
            // chỉ đảo thứ tự khi chưa nằm trên cùng, tránh dirty layout mỗi lần mở lại
            Transform parent = transform.parent;
            if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1) transform.SetAsLastSibling();
        }

        public virtual void OnClose()
        {
            gameObject.SetActive(false);
            if (DestroyOnClose) Destroy(gameObject);
        }

        /// <summary>Đóng popup này. Gán thẳng vào nút Close trong Inspector cũng được.</summary>
        public virtual void Close()
        {
            if (PopupMN.Instance != null) PopupMN.Instance.Close(this);
            else OnClose();
        }

        /// <summary>Phím bấm khi popup này đang ở trên cùng. Mặc định Esc là đóng.</summary>
        public virtual void OnKey(Key key)
        {
            if (key == Key.Escape) Close();
        }
    }
}
