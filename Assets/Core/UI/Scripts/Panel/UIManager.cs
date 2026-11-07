using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// Quản lý panel của game và là nơi duy nhất đọc bàn phím cho UI. Phần xếp lớp / hàng đợi nằm ở
    /// <see cref="UIStack{TId,TView}"/>; lớp này lo nạp Panel_Config, đăng ký panel có sẵn trong GameCanvas
    /// (InfoPlayer, WorldMap...) và định tuyến phím: popup trước, rồi đến panel.
    /// Đặt component này lên gốc GameCanvas (Tools/Core/UI/Build GameCanvas Prefab dựng sẵn giúp).
    /// </summary>
    public class UIManager : UIStack<UIID, PanelBase>, IInputBlocker
    {
        public static UIManager Instance { get; private set; }

        /// <summary>Đang gõ chữ (ô chat, ô nhập số): phím tắt gameplay (Z / X, Tab, Q...) là chữ, không phải lệnh.</summary>
        public static bool IsTyping()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && (selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<InputField>() != null);
        }

        [Tooltip("Panel đặt sẵn trong GameCanvas. Mỗi panel phải tự khai ID trong Inspector của nó.")]
        [SerializeField] List<PanelBase> scenePanels = new List<PanelBase>();
        [Tooltip("Bảng mã panel > prefab cho panel không đặt sẵn trong scene. Để trống cũng được.")]
        [SerializeField] PanelConfig config;
        [Tooltip("Nơi đặt panel tạo từ config. Để trống thì đặt vào chính object này.")]
        [SerializeField] RectTransform oneShotHolder;

        /// <summary>Mã các panel HUD khai trong config, Start() mở sẵn (xem <see cref="LoadConfig"/>).</summary>
        readonly List<UIID> hudPanels = new List<UIID>();

        protected override string LogTag => "[UIManager]";
        protected override Transform ViewRoot => oneShotHolder != null ? oneShotHolder : transform;

        /// <summary>True khi panel trên cùng chặn điều khiển nhân vật.</summary>
        public static bool IsBlockingInput => Instance != null && Instance.IsBlocking;

        /// <summary>Cho <see cref="InputGate"/>: lớp gameplay hỏi qua cổng đó chứ không gọi thẳng vào đây.</summary>
        public bool BlocksPlayerInput => IsBlocking;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Scene có hai UIManager, bỏ cái thừa: " + name);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            LoadConfig();
            InputGate.Register(this);
            for (int i = 0; i < scenePanels.Count; i++) Register(scenePanels[i]);
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            InputGate.Unregister(this);
            Instance = null;
        }

        void LoadConfig()
        {
            if (config == null) return;   // không bắt buộc: có thể chỉ dùng panel đặt sẵn trong scene

            PanelEntry[] entries = config.entries;
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                PanelEntry entry = entries[i];
                if (entry == null) continue;

                AddPrefab(entry.id, entry.prefab);
                // Panel dạng HUD phải hiện ngay từ đầu. Panel đặt sẵn trong GameCanvas đã bật sẵn rồi,
                // còn panel tạo từ config thì lúc này chưa tồn tại nên Start() phải mở giúp.
                if (entry.prefab != null && entry.prefab.IsHud) hudPanels.Add(entry.id);
            }
        }

        void Start()
        {
            if (Instance != this) return;
            for (int i = 0; i < hudPanels.Count; i++) Show(hudPanels[i]);
        }

        /// <summary>Đăng ký thêm một panel dựng lúc chạy (không nằm trong scenePanels hay config).</summary>
        public void Add(PanelBase panel) => Register(panel);

        /// <summary>
        /// Đọc phím một lần cho cả UI: popup trên cùng được ưu tiên, không có thì tới panel trên cùng.
        /// Không có gì mở thì thoát ngay, không đụng tới Input System.
        /// </summary>
        void Update()
        {
            PopupMN popups = PopupMN.Instance;
            bool hasPopup = popups != null && popups.Current != null;
            if (!hasPopup && Current == null) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            Key key;
            if (keyboard.escapeKey.wasPressedThisFrame) key = Key.Escape;
            else if (keyboard.enterKey.wasPressedThisFrame) key = Key.Enter;
            else if (keyboard.spaceKey.wasPressedThisFrame) key = Key.Space;
            else return;

            if (hasPopup && popups.RouteKey(key)) return;
            RouteKey(key);
        }

        /// <summary>Đóng sạch popup lẫn panel. Dùng khi mất kết nối hoặc về menu.</summary>
        public static void CloseEverything()
        {
            if (PopupMN.Instance != null) PopupMN.Instance.CloseAll();
            if (Instance != null) Instance.CloseAll();
        }
    }
}
