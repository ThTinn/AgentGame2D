using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Nút Menu trên HUD gom các nút mở panel (bản đồ, hành trang, kỹ năng...) vào một khung bung ra dưới nó, cho màn chơi
    /// đỡ rối. Bấm Menu: mở / đóng khung. Bấm một mục: mở panel đó (bấm lại khi panel đang mở thì đóng) và thu khung.
    /// Bấm ra ngoài khung (lớp chặn trong suốt phủ màn): thu khung. Chữ nút lấy theo ngôn ngữ, đổi ngay khi đổi ngôn ngữ.
    /// </summary>
    public class HudMenu : MonoBehaviour
    {
        [Serializable]
        public class Item
        {
            public TextBtn button;
            public UIID panel = UIID.None;
        }

        [SerializeField] TextBtn menuButton;
        [Tooltip("Khung chứa các mục, ẩn sẵn.")]
        [SerializeField] GameObject drawer;
        [Tooltip("Lớp trong suốt phủ màn nằm dưới khung: bấm ra ngoài thì thu khung. Ẩn cùng khung.")]
        [SerializeField] BaseBtn blocker;
        [SerializeField] Item[] items = Array.Empty<Item>();

        public bool IsOpen => drawer != null && drawer.activeSelf;

        // ---------------- mục thêm từ code ngoài (không phải panel UIID) ----------------

        /// <summary>Mục do lớp khác gắn vào lúc chạy (vd OnlineCore thêm "Tổ đội" khi chơi online). label: chữ "vi|en".</summary>
        sealed class Extra
        {
            public string key, label;
            public Action<TextBtn> onClick;
        }

        static readonly List<Extra> extras = new List<Extra>();
        static event Action ExtrasChanged;
        readonly List<TextBtn> extraButtons = new List<TextBtn>();

        /// <summary>Thêm (hoặc thay) một mục của menu; onClick nhận nút vừa bấm (lấy font của HUD...). Gọi trước hay sau khi HUD dựng đều được.</summary>
        public static void AddExtra(string key, string label, Action<TextBtn> onClick)
        {
            extras.RemoveAll(e => e.key == key);
            extras.Add(new Extra { key = key, label = label, onClick = onClick });
            ExtrasChanged?.Invoke();
        }

        public static void RemoveExtra(string key)
        {
            if (extras.RemoveAll(e => e.key == key) > 0) ExtrasChanged?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            extras.Clear();
            ExtrasChanged = null;
        }

        // nhân bản nút cuối cùng của khung cho mỗi mục thêm; khung không có layout thì xếp tiếp theo khoảng cách hai nút cuối
        void BuildExtras()
        {
            foreach (TextBtn b in extraButtons) if (b) Destroy(b.gameObject);
            extraButtons.Clear();
            TextBtn template = null, before = null;
            foreach (Item item in items)
                if (item.button)
                {
                    before = template;
                    template = item.button;
                }
            if (template == null) return;
            var last = (RectTransform)template.transform;
            Vector2 step = before != null ? last.anchoredPosition - ((RectTransform)before.transform).anchoredPosition
                                          : new Vector2(0f, -last.rect.height);
            bool hasLayout = last.parent != null && last.parent.GetComponent<LayoutGroup>() != null;
            for (int i = 0; i < extras.Count; i++)
            {
                Extra extra = extras[i];
                TextBtn button = Instantiate(template, last.parent);
                button.name = "Extra_" + extra.key;
                button.RemoveAllListeners();
                button.AddListener(() =>
                {
                    Collapse();
                    extra.onClick?.Invoke(button);
                });
                button.SetText(Lang.GetSplitText(extra.label));
                if (!hasLayout) ((RectTransform)button.transform).anchoredPosition = last.anchoredPosition + step * (i + 1);
                extraButtons.Add(button);
            }
        }

        void Awake()
        {
            if (menuButton) menuButton.AddListener(Toggle);
            if (blocker) blocker.AddListener(Collapse);
            foreach (Item item in items)
            {
                if (item.button == null) continue;
                UIID id = item.panel;
                item.button.AddListener(() => Open(id));
            }
            ExtrasChanged += BuildExtras;
            BuildExtras();
            Collapse();
        }

        // Start chứ không Awake: UIManager nạp Panel_Config ở Awake của nó, thứ tự Awake giữa hai object không chắc chắn
        void Start()
        {
            // game không khai panel trong Panel_Config (Agen bỏ các panel cần code game 3D): ẩn nút mở nó
            UIManager manager = UIManager.Instance;
            if (manager == null) return;
            foreach (Item item in items)
                if (item.button && item.panel != UIID.None && !manager.Has(item.panel))
                    item.button.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            ExtrasChanged -= BuildExtras;
            if (menuButton) menuButton.RemoveAllListeners();
            if (blocker) blocker.RemoveAllListeners();
            foreach (Item item in items) if (item.button) item.button.RemoveAllListeners();
        }

        void OnEnable()
        {
            Lang.OnChanged += LoadLang;
            LoadLang();
        }

        void OnDisable()
        {
            Lang.OnChanged -= LoadLang;
            Collapse();
        }

        void LoadLang()
        {
            if (menuButton) menuButton.SetText(Lang.GetSplitText("Menu|Menu"));
            foreach (Item item in items)
                if (item.button) item.button.SetText(Label(item.panel));
            for (int i = 0; i < extraButtons.Count && i < extras.Count; i++)
                if (extraButtons[i]) extraButtons[i].SetText(Lang.GetSplitText(extras[i].label));
        }

        /// <summary>Chữ nút mở panel. Viết trong code (không để trong prefab) để Docs/Lang_Data.xlsx quét được.</summary>
        public static string Label(UIID id)
        {
            switch (id)
            {
                case UIID.WorldMap: return Lang.GetSplitText("Bản đồ|Map");
                case UIID.Inventory: return Lang.GetSplitText("Hành trang|Inventory");
                case UIID.SkillEquip: return Lang.GetSplitText("Kỹ năng|Skills");
                case UIID.PlayerUpgrade: return Lang.GetSplitText("Nâng cấp|Upgrade");
                case UIID.AnimEquip: return Lang.GetSplitText("Tư thế|Poses");
                case UIID.Settings: return Lang.GetSplitText("Cài đặt|Settings");
                case UIID.QuestLog: return Lang.GetSplitText("Nhiệm vụ|Quests");
                default: return id.ToString();
            }
        }

        public void Toggle()
        {
            if (IsOpen) Collapse();
            else Expand();
        }

        public void Expand() => SetOpen(true);

        public void Collapse() => SetOpen(false);

        void SetOpen(bool open)
        {
            if (drawer) drawer.SetActive(open);
            if (blocker) blocker.gameObject.SetActive(open);
            if (open)
            {
                // panel HUD (khung nhiệm vụ, minimap...) nằm ở Popup, vẽ sau Hud: khung đang mở phải đè lên chúng
                if (blocker) DrawOnTop(blocker.gameObject, 1);
                if (drawer) DrawOnTop(drawer, 2);
            }
            if (menuButton) menuButton.SetColorText(open ? UIColors.TextSelected : UIColors.TextNormal);
        }

        // Canvas con vẽ trên canvas cha (thứ tự canvas cha đổi lúc chạy: LocalPlaySettings.uiSortingOrder). Đặt khi object đang
        // bật: Canvas đang tắt (hay trong prefab) không nhận overrideSorting.
        static void DrawOnTop(GameObject go, int above)
        {
            Canvas parent = go.transform.parent != null ? go.transform.parent.GetComponentInParent<Canvas>() : null;
            if (parent == null) return;
            if (!go.TryGetComponent(out Canvas canvas))
            {
                canvas = go.AddComponent<Canvas>();
                go.AddComponent<GraphicRaycaster>();
            }
            canvas.overrideSorting = true;
            canvas.sortingLayerID = parent.sortingLayerID;
            canvas.sortingOrder = parent.sortingOrder + above;
        }

        void Open(UIID id)
        {
            Collapse();
            if (UIManager.Instance != null && id != UIID.None) UIManager.Instance.Toggle(id);
        }
    }
}
