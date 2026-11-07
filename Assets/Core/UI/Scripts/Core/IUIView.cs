using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Hợp đồng chung của mọi màn UI do <see cref="UIStack{TId,TView}"/> quản lý: popup (PopupBase) và panel (PanelBase).
    /// Nhờ nó mà phần xếp lớp, hàng đợi và định tuyến phím chỉ viết một lần cho cả hai hệ.
    /// <typeparamref name="TId"/> phải là enum có giá trị 0 là "None" (UIStack dùng default(TId) làm mã rỗng).
    /// </summary>
    public interface IUIView<TId> where TId : struct, System.Enum
    {
        /// <summary>Mã của màn này. UIStack gán khi tạo từ config; panel có sẵn trong scene tự khai trong Inspector.</summary>
        TId ID { get; set; }

        /// <summary>Đang mở thì người chơi không điều khiển được nhân vật.</summary>
        bool BlockPlayerMove { get; }

        /// <summary>Không vào history: không nhận phím, không chặn input (toast của popup, thẻ HUD của panel).</summary>
        bool SkipHistory { get; }

        /// <summary>Gọi Show khi đang mở thì xếp hàng chờ thay vì đè nội dung đang hiện.</summary>
        bool UseQueue { get; }

        /// <summary>Đóng là hủy object. Mặc định false: giữ lại để mở lần sau không phải Instantiate.</summary>
        bool DestroyOnClose { get; }

        /// <summary>Gọi một lần ngay sau khi được tạo (hoặc được đăng ký nếu có sẵn trong scene).</summary>
        void OnInit();

        void OnShow();
        void OnClose();

        /// <summary>Phím bấm khi màn này đang ở trên cùng.</summary>
        void OnKey(Key key);
    }
}
