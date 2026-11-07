using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Cửa vào của UI trong game: nằm trên GameCanvas prefab, giữ các gốc UI (Hud, Popup), bảo đảm có EventSystem.
    /// Scene chỉ cần một instance của prefab GameCanvas.
    /// Việc bật/tắt và xếp lớp panel do UIManager (cùng object) lo; lớp này chỉ giữ tham chiếu cho tiện gọi.
    /// </summary>
    /// <remarks>
    /// Game: bản GAME_2D còn nối camera 2D (CameraDebugPanel / CameraController) và tra thẳng MapSelectPanel,
    /// InfoPlayerPanel. Ba panel đó gắn với map / nhân vật 2D nên chưa chép sang; mở panel khác qua <see cref="SetPanelVisible"/>.
    /// </remarks>
    [RequireComponent(typeof(Canvas))]
    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance { get; private set; }

        [SerializeField] Canvas canvas;
        [Tooltip("Gốc cho UI luôn hiện (máu, nút bấm, minimap...).")]
        [SerializeField] RectTransform hudRoot;
        [Tooltip("Gốc cho UI bật/tắt trên HUD (hộp thoại, menu, bảng).")]
        [SerializeField] RectTransform popupRoot;
        [Tooltip("UIManager trên cùng object: bật/tắt và xếp lớp panel.")]
        [SerializeField] UIManager panels;
        [SerializeField] bool persistAcrossScenes;

        public Canvas Canvas => canvas;
        public RectTransform HudRoot => hudRoot;
        public RectTransform PopupRoot => popupRoot;
        public UIManager Panels => panels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Scene có hai GameUIManager, bỏ cái thừa: " + name);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (!canvas) canvas = GetComponent<Canvas>();
            if (!panels) panels = GetComponent<UIManager>();
            UIFactory.EnsureEventSystem();
            if (persistAcrossScenes && transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Bật/tắt một panel qua UIManager để history và phím Esc vẫn đúng.</summary>
        public void SetPanelVisible(UIID id, bool visible)
        {
            if (panels != null) panels.SetVisible(id, visible);
            else Debug.LogWarning("GameUIManager: chưa có UIManager, không bật/tắt được panel " + id + ".");
        }
    }
}
