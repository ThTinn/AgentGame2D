using System;
using Game.Core;
using Game.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Một ô dùng nhanh trên HUD (<see cref="QuickItemBar"/>): vòng tròn viền màu phẩm chất, icon vật phẩm, số còn trong túi,
    /// vòng hồi (quét theo thời gian chờ dùng lại), chữ phím tắt. Bấm = <see cref="Tapped"/>, giữ <see cref="HoldSeconds"/>
    /// giây = <see cref="Held"/> (đổi vật phẩm). Dựng bằng code (<see cref="Create"/>), không cần prefab.
    /// </summary>
    public class QuickUseSlot : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float HoldSeconds = 0.5f;

        static readonly Color EmptyRing = new Color32(187, 143, 226, 160);
        static readonly Color Background = new Color32(39, 28, 71, 200);
        static readonly Color CooldownTint = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color OutTint = new Color(1f, 1f, 1f, 0.35f);
        static readonly Color OutCount = new Color(1f, 0.45f, 0.45f);

        Image ring, icon, cooldown;    // ring: viền màu phẩm chất (cũng là vùng nhận bấm); cooldown: lớp tối quét tròn
        TMP_Text countText, keyText, plusText;
        float shownCooldown;           // fillAmount đang vẽ: đổi mới gán, khỏi làm bẩn lưới UI mỗi frame khi ô không chờ

        // bấm / giữ: pressed từ lúc nhấn tới lúc nhả (hoặc kéo ra ngoài); held = đã báo Held trong lần nhấn này
        bool pressed, held;
        float pressedAt;

        /// <summary>Bấm nhả nhanh.</summary>
        public event Action Tapped;
        /// <summary>Giữ đủ lâu.</summary>
        public event Action Held;

        /// <summary>Dựng một ô cỡ <paramref name="size"/> dưới <paramref name="parent"/>. font null = font mặc định của TMP.</summary>
        /// <remarks>
        /// Thứ tự lớp (dưới lên trên): Ring (Image của chính ô, nhận bấm) &gt; Bg tối thụt vào 4 &gt; Icon &gt; Cooldown quét tròn
        /// &gt; chữ "+" / số lượng / phím. Chỉ Ring bật raycastTarget: một Image nhận bấm cho cả ô, các lớp trên không chặn.
        /// Cả ô dùng chung một sprite tròn (UISprites.Circle) nên chỉ một texture.
        /// </remarks>
        public static QuickUseSlot Create(Transform parent, string name, float size, TMP_FontAsset font, string keyLabel)
        {
            Sprite circle = UISprites.Circle;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);

            var slot = go.AddComponent<QuickUseSlot>();
            slot.ring = go.AddComponent<Image>();
            slot.ring.sprite = circle;
            slot.ring.color = EmptyRing;

            Image bg = NewImage(rt, "Bg", circle, Background, 4f);
            bg.raycastTarget = false;
            slot.icon = NewImage(rt, "Icon", null, Color.white, size * 0.16f);
            slot.icon.preserveAspect = true;
            slot.cooldown = NewImage(rt, "Cooldown", circle, CooldownTint, 4f);
            slot.cooldown.type = Image.Type.Filled;
            slot.cooldown.fillMethod = Image.FillMethod.Radial360;
            slot.cooldown.fillOrigin = (int)Image.Origin360.Top;
            slot.cooldown.fillClockwise = false;
            slot.cooldown.fillAmount = 0f;

            slot.plusText = NewText(rt, "Plus", font, "+", size * 0.5f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.5f));
            slot.countText = NewText(rt, "Count", font, "", size * 0.26f, TextAlignmentOptions.BottomRight, Color.white);
            slot.countText.rectTransform.offsetMax = new Vector2(-size * 0.06f, 0f);
            slot.keyText = NewText(rt, "Key", font, keyLabel, size * 0.24f, TextAlignmentOptions.TopLeft, new Color(1f, 0.88f, 0.54f));
            slot.keyText.rectTransform.offsetMin = new Vector2(size * 0.08f, 0f);
            slot.keyText.rectTransform.offsetMax = new Vector2(0f, -size * 0.02f);
            slot.BindEmpty();
            return slot;
        }

        static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text NewText(RectTransform parent, string name, TMP_FontAsset font, string text, float fontSize,
            TextAlignmentOptions alignment, Color color)
        {
            TextMeshProUGUI t = UIFactory.CreateText(parent, text, fontSize, color, alignment);
            t.name = name;
            if (font != null) t.font = font;
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }

        /// <summary>Ô có vật phẩm: count = số còn trong túi (0 thì mờ đi, vẫn giữ chỗ), cooldown01 = phần thời gian chờ còn lại.</summary>
        public void Bind(ItemTemplate item, Sprite sprite, int count)
        {
            ring.color = ItemTemplate.RarityTint(item.rarity);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = count > 0 ? Color.white : OutTint;
            plusText.gameObject.SetActive(sprite == null);
            if (sprite == null) plusText.text = "?";
            countText.gameObject.SetActive(true);
            countText.text = NumberCache.Get(count);
            countText.color = count > 0 ? Color.white : OutCount;
        }

        /// <summary>Ô trống: dấu + mời gán vật phẩm.</summary>
        public void BindEmpty()
        {
            ring.color = EmptyRing;
            icon.enabled = false;
            plusText.gameObject.SetActive(true);
            plusText.text = "+";
            countText.gameObject.SetActive(false);
            SetCooldown(0f);
        }

        /// <summary>Ô không có vật phẩm, giữa ô là một chữ (vd "Bỏ" trong bảng chọn).</summary>
        public void BindText(string text, float fontScale)
        {
            BindEmpty();
            plusText.text = text;
            plusText.fontSize *= fontScale;
        }

        /// <summary>Vòng hồi: 1 = vừa dùng, 0 = dùng được. Gọi mỗi frame được: số không đổi thì không đụng tới Image.</summary>
        public void SetCooldown(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Approximately(fraction, shownCooldown)) return;
            shownCooldown = fraction;
            cooldown.fillAmount = fraction;
        }

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            held = false;
            pressedAt = Time.unscaledTime;
        }

        // nhả tay trước HoldSeconds = bấm; đã giữ đủ lâu (Held đã báo) thì nhả không tính là bấm nữa
        public void OnPointerUp(PointerEventData e)
        {
            if (pressed && !held) Tapped?.Invoke();
            pressed = false;
        }

        // kéo ngón tay ra khỏi ô = hủy, không bấm cũng không giữ
        public void OnPointerExit(PointerEventData e) => pressed = false;

        // giữ: đếm bằng thời gian thực (không bị Time.timeScale chặn), báo Held đúng một lần mỗi lần nhấn
        void Update()
        {
            if (!pressed || held || Time.unscaledTime - pressedAt < HoldSeconds) return;
            held = true;
            Held?.Invoke();
        }
    }

    /// <summary>Sprite dựng bằng code cho UI tạo không cần prefab.</summary>
    public static class UISprites
    {
        static Sprite circle;

        /// <summary>Hình tròn trắng mép mịn 128px (tô màu bằng Image.color, cắt quạt bằng Image.Type.Filled).</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "UICircle", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[n * n];
                float r = n * 0.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - r, dy = y + 0.5f - r;
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));   // mép mịn 1px
                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                circle.name = "UICircle";
                return circle;
            }
        }
    }
}
