using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// ScrollRect kéo được cả hai chiều và zoom được: lăn chuột / pinch hai ngón phóng to thu nhỏ Content quanh điểm con
    /// trỏ (điểm dưới con trỏ đứng yên), thay cho cuộn dọc mặc định của ScrollRect. Kéo một ngón / chuột vẫn là kéo khung.
    /// Lúc đang pinch thì bỏ qua kéo để hai thao tác không giành nhau. Dùng cho cây nâng cấp nhân vật.
    /// </summary>
    /// <remarks>Inspector của ScrollRect không vẽ các trường riêng ở đây; chỉnh qua Debug Inspector hoặc để mặc định.</remarks>
    public class ZoomScrollRect : ScrollRect
    {
        [SerializeField] float minZoom = 0.3f;
        [SerializeField] float maxZoom = 1.5f;
        [Tooltip("Mức zoom lúc mở và khi bấm Về gốc.")]
        [SerializeField] float defaultZoom = 0.8f;
        [Tooltip("Mỗi nấc lăn chuột phóng / thu chừng này phần.")]
        [SerializeField] float wheelStep = 0.12f;

        bool pinching;

        public float Zoom => content != null ? content.localScale.x : 1f;

        /// <summary>Về mức zoom mặc định, tâm Content (nút gốc của cây) giữa khung.</summary>
        public void ResetView()
        {
            StopMovement();
            if (content == null) return;
            content.localScale = Vector3.one * Mathf.Clamp(defaultZoom, minZoom, maxZoom);
            content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Zoom tới mức zoom (đã kẹp) mà điểm màn hình screenPos vẫn nằm yên dưới con trỏ.</summary>
        public void ZoomAt(Vector2 screenPos, float zoom)
        {
            if (content == null) return;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            if (Mathf.Approximately(zoom, Zoom)) return;

            Camera cam = EventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPos, cam, out Vector2 before);
            content.localScale = Vector3.one * zoom;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPos, cam, out Vector2 after);
            // điểm "before" của Content phải về lại dưới con trỏ: dời Content (đơn vị của cha = đơn vị local x zoom)
            content.anchoredPosition += (after - before) * zoom;
            StopMovement();
        }

        Camera EventCamera
        {
            get
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                canvas = canvas != null ? canvas.rootCanvas : null;
                return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }
        }

        // lăn chuột: zoom thay vì cuộn dọc
        public override void OnScroll(PointerEventData data)
        {
            if (!IsActive()) return;
            float delta = data.scrollDelta.y;
            if (Mathf.Abs(delta) < 0.01f) return;
            ZoomAt(data.position, Zoom * (1f + wheelStep * Mathf.Sign(delta)));
        }

        public override void OnBeginDrag(PointerEventData data)
        {
            if (!pinching) base.OnBeginDrag(data);
        }

        public override void OnDrag(PointerEventData data)
        {
            if (!pinching) base.OnDrag(data);
        }

        void Update()
        {
            var ts = Touchscreen.current;
            if (ts == null || content == null) return;

            TouchControl a = null, b = null;
            foreach (TouchControl t in ts.touches)
            {
                if (!t.isInProgress) continue;
                if (a == null) a = t;
                else if (b == null) b = t;
                else { b = null; break; }   // 3 ngón trở lên: bỏ qua
            }

            if (a == null || b == null)
            {
                pinching = false;
                return;
            }

            Vector2 pa = a.position.ReadValue(), pb = b.position.ReadValue();
            // chỉ pinch khi cả hai ngón nằm trong khung cây
            Camera cam = EventCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewport != null ? viewport : (RectTransform)transform, pa, cam)
                || !RectTransformUtility.RectangleContainsScreenPoint(viewport != null ? viewport : (RectTransform)transform, pb, cam))
                return;

            if (!pinching)
            {
                pinching = true;
                StopMovement();
            }

            Vector2 da = a.delta.ReadValue(), db = b.delta.ReadValue();
            float now = Vector2.Distance(pa, pb);
            float prev = Vector2.Distance(pa - da, pb - db);
            if (prev > 1f && now > 1f) ZoomAt((pa + pb) * 0.5f, Zoom * now / prev);
        }
    }
}
