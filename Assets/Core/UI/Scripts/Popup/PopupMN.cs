using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// Quản lý popup của game. Phần xếp lớp / hàng đợi / định tuyến phím nằm ở <see cref="UIStack{TId,TView}"/>;
    /// lớp này chỉ lo nạp Popup_Config và mấy lối tắt static hay dùng (Alert, Confirm, Toast...).
    /// Đặt component này lên object "Popup" trong GameCanvas (Tools/Core/UI/Build Popup Prefabs dựng sẵn giúp).
    /// </summary>
    public class PopupMN : UIStack<PopupID, PopupBase>, IInputBlocker
    {
        public static PopupMN Instance { get; private set; }

        [Tooltip("Bảng mã popup > prefab. Thiếu bảng này thì không mở được popup nào.")]
        [SerializeField] PopupConfig config;
        [SerializeField] bool persistAcrossScenes;

        protected override string LogTag => "[PopupMN]";

        /// <summary>True khi popup trên cùng chặn điều khiển nhân vật.</summary>
        public static bool IsBlockingInput => Instance != null && Instance.IsBlocking;

        /// <summary>Cho <see cref="InputGate"/>: lớp gameplay hỏi qua cổng đó chứ không gọi thẳng vào đây.</summary>
        public bool BlocksPlayerInput => IsBlocking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Scene có hai PopupMN, bỏ cái thừa: " + name);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadConfig();
            InputGate.Register(this);
            Notify.Toasted += OnNotifyToast;
            if (persistAcrossScenes && transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            InputGate.Unregister(this);
            Notify.Toasted -= OnNotifyToast;
            Instance = null;
        }

        // Lớp gameplay gửi thông báo qua Game.Core.Notify (nó không được phép biết PopupMN); đây là đầu nhận.
        static void OnNotifyToast(string content, float seconds) => Toast(content, seconds);

        void LoadConfig()
        {
            if (config == null)
            {
                Debug.LogError("PopupMN: chưa gán Popup Config, không mở được popup nào.");
                return;
            }

            PopupEntry[] entries = config.entries;
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                PopupEntry entry = entries[i];
                if (entry != null) AddPrefab(entry.id, entry.prefab);
            }
        }

        /// <summary>
        /// Đọc phím khi scene không có UIManager. Có UIManager thì nó đọc một lần rồi gọi RouteKey,
        /// tránh hai vòng Update cùng bắt một phím.
        /// </summary>
        void Update()
        {
            if (UIManager.Instance != null || Current == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) RouteKey(Key.Escape);
            else if (keyboard.enterKey.wasPressedThisFrame) RouteKey(Key.Enter);
            else if (keyboard.spaceKey.wasPressedThisFrame) RouteKey(Key.Space);
        }

        // ---------------- Lối tắt static ----------------
        // Chỉ là lớp mỏng: PopupMN không giữ logic riêng của popup nào.
        // Popup mới không cần thêm hàm ở đây, gọi thẳng PopupMN.Open<T>(id, p => p.Init(...)).

        /// <summary>Mở popup bất kỳ từ code. Trả null khi chưa có PopupMN trong scene hoặc yêu cầu bị xếp hàng.</summary>
        public static T Open<T>(PopupID id, Action<T> init = null) where T : PopupBase
        {
            if (Instance == null)
            {
                Debug.LogWarning("PopupMN: chưa có trong scene, bỏ qua yêu cầu mở " + id + ".");
                return null;
            }
            return Instance.Show(id, init);
        }

        /// <summary>
        /// Mở popup dựng bằng code (không có prefab trong Popup_Config): lần đầu gọi build(gốc chứa popup) rồi đăng ký,
        /// các lần sau dùng lại như mọi popup. build trả về popup còn tắt.
        /// </summary>
        public static T OpenBuilt<T>(PopupID id, Func<Transform, T> build, Action<T> init = null) where T : PopupBase
        {
            if (Instance == null)
            {
                Debug.LogWarning("PopupMN: chưa có trong scene, bỏ qua yêu cầu mở " + id + ".");
                return null;
            }
            if (!Instance.Has(id))
            {
                T view = build(Instance.ViewRoot);
                if (view == null) return null;
                view.ID = id;
                view.gameObject.SetActive(false);
                Instance.Register(view);
            }
            return Instance.Show(id, init);
        }

        /// <summary>Thông báo một nút OK.</summary>
        public static SelectionPopup Alert(string content, UnityAction onOk = null) =>
            Open<SelectionPopup>(PopupID.Selection, p => p.ShowOk(content, onOk));

        /// <summary>Hỏi Xác nhận / Hủy bỏ.</summary>
        public static SelectionPopup Confirm(string content, UnityAction onYes, UnityAction onNo = null) =>
            Open<SelectionPopup>(PopupID.Selection, p => p.ShowYesNo(content, onYes, onNo));

        /// <summary>Danh sách lựa chọn tự đặt (mỗi BtnData là một nút).</summary>
        public static SelectionPopup Choices(string content, List<BtnData> buttons, string title = null) =>
            Open<SelectionPopup>(PopupID.Selection, p => p.ShowChoices(content, buttons, title));

        /// <summary>Hội thoại nhiều trang, bấm nút tiếp hoặc Space để sang trang.</summary>
        public static SelectionPopup Pages(string[] contents, UnityAction onDone = null, string title = null) =>
            Open<SelectionPopup>(PopupID.Selection, p => p.ShowPages(contents, onDone, title));

        /// <summary>Thông báo trôi ở giữa màn hình, tự tắt. Không chặn thao tác.</summary>
        public static PopupNotice Toast(string content, float seconds = 2f) =>
            Open<PopupNotice>(PopupID.Notice, p => p.Show(content, seconds));

        /// <summary>Nhập số (vd số lượng vứt bỏ, giá bán).</summary>
        public static PopupInput InputNumber(string title, int defaultValue, Action<int> onAccept, int min = 0, int max = int.MaxValue) =>
            Open<PopupInput>(PopupID.Input, p => p.Open(title, defaultValue, onAccept, min, max));

        public static PopupLoading ShowLoading(string content = null) =>
            Open<PopupLoading>(PopupID.Loading, p => p.Show(content));

        public static void HideLoading()
        {
            if (Instance != null) Instance.Close(PopupID.Loading);
        }
    }
}
