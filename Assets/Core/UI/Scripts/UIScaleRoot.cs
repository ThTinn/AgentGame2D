using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// Khung phủ kín cha nhưng phóng to <see cref="scale"/> lần: UI con thiết kế theo độ phân giải nhỏ hơn của canvas gốc vẫn
    /// hiện đúng cỡ. GameCanvas để 1920x1080 (như UI GameScene); khung Core để 1.5 cho Hud / Popup / panel Core thiết kế theo
    /// 1280x720. Chỉ CanvasScaler của canvas gốc có tác dụng, nên không dùng được scaler thứ hai ở canvas con.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class UIScaleRoot : UIBehaviour
    {
        [Tooltip("Độ phân giải của canvas gốc chia cho độ phân giải UI con được thiết kế (1920 / 1280 = 1.5).")]
        [SerializeField, Min(0.01f)] float scale = 1.5f;

        bool applying;

        public float Scale => scale;

        protected override void OnEnable()
        {
            base.OnEnable();
            Apply();
        }

        // cha đổi cỡ thì khung (neo kín cha) cũng đổi theo: tính lại
        protected override void OnRectTransformDimensionsChange() => Apply();

        protected override void OnTransformParentChanged() => Apply();

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Apply();
        }
#endif

        void Apply()
        {
            if (applying) return;
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            applying = true;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            Vector2 size = parent.rect.size;
            // neo kín cha: rect = cỡ cha + sizeDelta; muốn rect = cỡ cha / scale
            Vector2 delta = size / scale - size;
            if ((rt.sizeDelta - delta).sqrMagnitude > 0.0001f) rt.sizeDelta = delta;
            Vector3 s = new Vector3(scale, scale, 1f);
            if (rt.localScale != s) rt.localScale = s;
            applying = false;
        }
    }
}
