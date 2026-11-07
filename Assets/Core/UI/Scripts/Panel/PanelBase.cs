using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Mã của từng panel (màn UI lớn: HUD, bảng chọn map, hành trang, cửa hàng...).
    /// Giá trị ghi cố định để thêm panel mới không làm lệch dữ liệu đã lưu; None phải là 0.
    /// Thêm panel: thêm mã ở đây, cho script kế thừa PanelBase, rồi hoặc đặt sẵn trong GameCanvas
    /// (kéo vào danh sách Scene Panels của UIManager), hoặc làm prefab và khai trong Panel_Config.
    /// </summary>
    public enum UIID
    {
        None = 0,
        /// <summary>Thẻ thông tin người chơi ở góc HUD (IsHud: luôn hiện, không nhận phím).</summary>
        InfoPlayer = 1,
        /// <summary>Bảng chọn map / dịch chuyển.</summary>
        WorldMap = 2,
        /// <summary>Bảng trang bị anim: chọn tư thế cho từng hành động của nhân vật (trừ tấn công và skill).</summary>
        AnimEquip = 3,
        /// <summary>Minimap ở góc trên phải HUD (IsHud).</summary>
        MiniMap = 4,
        // 5: bảng trang bị vũ khí cũ, đã bỏ (vũ khí giờ trang bị trong bảng hành trang). Đừng dùng lại số 5.
        /// <summary>Cây nâng cấp của một vũ khí, mở từ popup thông tin vũ khí trong hành trang.</summary>
        WeaponUpgrade = 6,
        /// <summary>Thẻ mục tiêu đang focus ở giữa trên HUD: tên + máu (IsHud).</summary>
        FocusTarget = 7,
        /// <summary>Bảng trang bị skill: gắn skill vào bốn ô, anim skill đi theo skill.</summary>
        SkillEquip = 8,
        /// <summary>Cây nâng cấp nhân vật: tiêu điểm tiềm năng, cây toả ra từ một nút gốc, cuộn hai chiều.</summary>
        PlayerUpgrade = 9,
        /// <summary>Hành trang: lưới ô vật phẩm, lọc theo loại, bấm ô để xem thông tin.</summary>
        Inventory = 10,
        /// <summary>Cài đặt: chọn ngôn ngữ.</summary>
        Settings = 11,
        /// <summary>Cửa sổ nhiệm vụ: tab Cốt truyện / Việc phụ / Hằng ngày / Quan hệ, chi tiết + nút Chỉ đường (QuestLogPanel).</summary>
        QuestLog = 12,
        /// <summary>Khung theo dõi nhiệm vụ ở góc phải HUD, dưới minimap (IsHud). Chạm dòng để chỉ đường, nút Mở để mở QuestLog.</summary>
        QuestTracker = 13,
        /// <summary>Cường hóa trang bị (+N): mở từ popup thông tin vật phẩm.</summary>
        GearUpgrade = 14,
        /// <summary>Khoét lỗ và khảm đá: mở từ bảng cường hóa.</summary>
        GearSocket = 15,
    }

    /// <summary>
    /// Lớp cơ sở của mọi panel. UIManager tạo, bật/tắt và xếp lớp panel; panel con chỉ lo nội dung của mình.
    /// Vòng đời: OnInit (một lần) > OnShow (mỗi lần mở) > OnClose (mỗi lần đóng).
    /// Khác popup ở chỗ panel là màn chính của UI (hành trang, bản đồ, cửa hàng), popup là hộp thoại chồng lên trên.
    /// </summary>
    public abstract class PanelBase : MonoBehaviour, IUIView<UIID>
    {
        [Header("Panel")]
        [SerializeField] UIID id = UIID.None;
        [Tooltip("Panel đang mở thì người chơi không điều khiển được nhân vật. Thẻ HUD nên tắt cờ này.")]
        [SerializeField] bool blockPlayerMove = true;

        public UIID ID
        {
            get => id;
            set => id = value;
        }

        public bool BlockPlayerMove
        {
            get => blockPlayerMove;
            set => blockPlayerMove = value;
        }

        /// <summary>Thẻ HUD: luôn nằm dưới, không vào history, không nhận phím, không chặn input.</summary>
        public virtual bool IsHud => false;

        /// <summary>Đóng là hủy luôn GameObject. Mặc định false: giữ lại để mở lần sau không phải Instantiate.</summary>
        public virtual bool DestroyOnClose => false;

        /// <summary>Gọi Show khi panel đang mở thì xếp hàng chờ thay vì ghi đè. Panel hiếm khi cần.</summary>
        public virtual bool UseQueue => false;

        bool IUIView<UIID>.SkipHistory => IsHud;

        public bool IsOpen => gameObject.activeSelf;

        public virtual void OnInit() { }

        public virtual void OnShow()
        {
            gameObject.SetActive(true);
            // HUD giữ nguyên thứ tự trong layout; panel thường thì đẩy lên trên cùng
            if (IsHud) return;

            Transform parent = transform.parent;
            if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1) transform.SetAsLastSibling();
        }

        public virtual void OnClose()
        {
            gameObject.SetActive(false);
            if (DestroyOnClose) Destroy(gameObject);
        }

        /// <summary>Đóng panel này. Gán thẳng vào nút Close trong Inspector cũng được.</summary>
        public virtual void Close()
        {
            if (UIManager.Instance != null) UIManager.Instance.Close(this);
            else OnClose();
        }

        /// <summary>Phím bấm khi panel này đang ở trên cùng và không có popup nào chặn. Mặc định Esc là đóng.</summary>
        public virtual void OnKey(Key key)
        {
            if (key == Key.Escape) Close();
        }

        protected virtual void OnEnable()
        {
            Lang.OnChanged += OnLoadLang;
            OnLoadLang();
        }

        protected virtual void OnDisable()
        {
            Lang.OnChanged -= OnLoadLang;
        }

        /// <summary>Gán lại chữ theo ngôn ngữ hiện tại. Gọi lúc bật panel và mỗi khi người chơi đổi ngôn ngữ.</summary>
        public virtual void OnLoadLang() { }
    }
}
