using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DungeonOnline.UI
{
    /// <summary>
    /// Small helpers to build UGUI panels in code on top of Core's <see cref="UIFactory"/>, the way GameB builds its
    /// server-IP row. Used by <see cref="LoadingPanel"/> and <see cref="LoginPanel"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// RectTransform root = PanelBuilder.FullScreen(canvas.transform, "LoadingPanel", PanelBuilder.Backdrop);
    /// RectTransform box = PanelBuilder.Box(root, new Vector2(640, 480));
    /// TMP_InputField name = PanelBuilder.Input(box, "Tên|Name", false);
    /// </code>
    /// </example>
    public static class PanelBuilder
    {
        public static readonly Color Backdrop = new Color(0.06f, 0.05f, 0.1f, 1f);
        public static readonly Color BoxColor = new Color(0.15f, 0.12f, 0.24f, 0.96f);
        public static readonly Color InputColor = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color BarBack = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color BarFill = new Color(0.36f, 0.78f, 0.52f, 1f);

        /// <summary>A rect filling its parent with a solid colour that blocks clicks to what is behind.</summary>
        public static RectTransform FullScreen(Transform parent, string name, Color color)
        {
            var rt = NewRect(parent, name);
            Stretch(rt);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        /// <summary>A centred box of <paramref name="size"/> with a vertical layout for its children.</summary>
        public static RectTransform Box(Transform parent, Vector2 size)
        {
            var rt = NewRect(parent, "Box");
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.gameObject.AddComponent<Image>().color = BoxColor;
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>A text line of fixed <paramref name="height"/> inside a layout.</summary>
        public static TextMeshProUGUI Label(Transform parent, string text, float fontSize, float height,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var row = Row(parent, "Label", height);
            var label = UIFactory.CreateText(row, text, fontSize, null, alignment);
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        /// <summary>A button of fixed height inside a layout.</summary>
        public static TextBtn Button(Transform parent, string text, float height, UnityEngine.Events.UnityAction onClick)
        {
            TextBtn button = UIFactory.CreateTextButton(parent, text, new Vector2(0f, height), onClick);
            Fix(button.gameObject, height);
            return button;
        }

        /// <summary>A single-line text field with a placeholder; <paramref name="password"/> hides what is typed.</summary>
        public static TMP_InputField Input(Transform parent, string placeholder, bool password, float height = 56f)
        {
            var back = Row(parent, "Input", height);
            back.gameObject.AddComponent<Image>().color = InputColor;
            back.gameObject.SetActive(false);   // TMP_InputField needs its viewport and text before it enables

            var area = NewRect(back, "TextArea");
            Stretch(area);
            area.offsetMin = new Vector2(14f, 6f);
            area.offsetMax = new Vector2(-14f, -6f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI text = UIFactory.CreateText(area, "", 26f, null, TextAlignmentOptions.MidlineLeft);
            TextMeshProUGUI hint = UIFactory.CreateText(area, placeholder, 24f, new Color(1f, 1f, 1f, 0.45f),
                TextAlignmentOptions.MidlineLeft);
            hint.fontStyle = FontStyles.Italic;

            var input = back.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.characterLimit = 64;
            back.gameObject.SetActive(true);
            return input;
        }

        /// <summary>A horizontal bar; set its fill with <see cref="SetBar"/>.</summary>
        public static RectTransform Bar(Transform parent, float height)
        {
            var back = Row(parent, "Bar", height);
            back.gameObject.AddComponent<Image>().color = BarBack;
            var fill = NewRect(back, "Fill");
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = BarFill;
            return back;
        }

        /// <summary>Fills a bar from <see cref="Bar"/> to <paramref name="progress"/> (0-1).</summary>
        public static void SetBar(RectTransform bar, float progress)
        {
            var fill = (RectTransform)bar.GetChild(0);
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        /// <summary>A layout child of fixed height.</summary>
        public static RectTransform Row(Transform parent, string name, float height)
        {
            var rt = NewRect(parent, name);
            Fix(rt.gameObject, height);
            return rt;
        }

        private static void Fix(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();   // explicit: ?? misses Unity's fake null
            element.minHeight = element.preferredHeight = height;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
