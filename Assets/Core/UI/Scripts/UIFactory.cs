using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Dựng nhanh Canvas, chữ, nút, toggle bằng code (không cần prefab): dùng để thử giao diện, làm màn hình tạm hoặc test.
    /// Giao diện thật nên làm bằng prefab và gắn BaseBtn/TextBtn/TextToggle... qua Inspector.
    /// Cần TMP Essential Resources trong project (menu Tools > Core > Import TMP Essentials).
    /// </summary>
    public static class UIFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1280f, 720f);

        static readonly Color ButtonNormal = Color.white;
        static readonly Color ButtonHighlight = new Color(0.94f, 0.92f, 1f);
        static readonly Color ButtonPressed = new Color(0.72f, 0.69f, 0.85f);
        static readonly Color ButtonDisabled = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        public static Canvas CreateCanvas(string name = "Canvas", int sortingOrder = 0)
        {
            EnsureEventSystem();

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Tạo EventSystem dùng Input System nếu scene chưa có (không có thì UI không nhận bấm).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static TextMeshProUGUI CreateText(Transform parent, string text, float fontSize = 32f, Color? color = null,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color ?? Color.white;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        public static TextBtn CreateTextButton(Transform parent, string text, Vector2 size, UnityAction onClick = null)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;

            go.AddComponent<Image>();
            var button = go.AddComponent<TextBtn>();
            StyleButton(button);

            button.nameTxt = CreateText(go.transform, text, 28f);
            button.AddListener(onClick);
            return button;
        }

        /// <summary>Nút trơn (Image + BaseBtn) cùng kiểu với TextBtn, để gắn icon hoặc nội dung riêng.</summary>
        public static BaseBtn CreateBaseButton(Transform parent, string name, Vector2 size, UnityAction onClick = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;

            go.AddComponent<Image>();
            var button = go.AddComponent<BaseBtn>();
            StyleButton(button);
            button.AddListener(onClick);
            return button;
        }

        /// <summary>Đặt hình nền (Image cùng object) và bộ màu chung cho mọi nút. Prefab nút và nút dựng bằng code dùng chung.</summary>
        public static void StyleButton(Button button)
        {
            var image = button.GetComponent<Image>();
            button.targetGraphic = image;
            if (UITheme.Instance) UITheme.Paint(image, UITheme.Instance.button);
            button.colors = new ColorBlock
            {
                normalColor = ButtonNormal,
                highlightedColor = ButtonHighlight,
                pressedColor = ButtonPressed,
                selectedColor = ButtonHighlight,
                disabledColor = ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };
        }

        /// <summary>Toggle có nền, dấu tích và chữ bên phải. Truyền group để thành nhóm tab (chỉ một cái được chọn).</summary>
        public static TextToggle CreateTextToggle(Transform parent, string text, Vector2 size, ToggleGroup group = null)
        {
            var go = new GameObject(text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = size;

            var toggle = go.AddComponent<TextToggle>();
            var unityToggle = go.GetComponent<Toggle>();

            var bg = CreateImage(go.transform, "Background", ButtonNormal);
            if (UITheme.Instance) UITheme.Paint(bg, UITheme.Instance.button);
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            bg.rectTransform.pivot = new Vector2(0f, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(size.y * 0.7f, size.y * 0.7f);
            bg.rectTransform.anchoredPosition = new Vector2(8f, 0f);

            var check = CreateImage(bg.transform, "Checkmark", Color.white);
            Stretch(check.rectTransform);
            check.rectTransform.offsetMin = new Vector2(6f, 6f);
            check.rectTransform.offsetMax = new Vector2(-6f, -6f);

            unityToggle.targetGraphic = bg;
            unityToggle.graphic = check;
            unityToggle.group = group;

            toggle._txt = CreateText(go.transform, text, 26f, null, TextAlignmentOptions.MidlineLeft);
            toggle._txt.rectTransform.offsetMin = new Vector2(size.y + 16f, 0f);
            return toggle;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
