using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bộ sprite, font, màu của UI Kit cho phần UI dựng bằng code (UIFactory, ô ở HUD, popup tạo lúc chạy). Prefab đã mang sẵn sprite
    /// nên không cần đọc bảng này. Tạo và gán bởi Tools/Core/UI/Apply UI Kit, nằm ở Resources để bản build tải được.
    /// </summary>
    public class UITheme : ScriptableObject
    {
        /// <summary>Sprite kit dùng bản Large, canvas vẽ ở độ phân giải Medium: nhân đôi để lề 9-slice đúng kích thước.</summary>
        public const float SpriteScale = 2f;

        /// <summary>Nền panel, chữ tiêu đề, chữ trên nút màu của kit.</summary>
        public static readonly Color Scrim = new Color32(13, 9, 34, 170);
        public static readonly Color TextTitle = new Color32(236, 216, 183, 255);
        public static readonly Color TextOnButton = new Color32(39, 28, 71, 255);

        public TMP_FontAsset font;
        public Sprite panel;
        public Sprite panelSmall;
        public Sprite inset;
        public Sprite slot;
        public Sprite button;
        public Sprite buttonDark;
        public Sprite input;
        public Sprite circle;

        static UITheme instance;
        static bool loaded;

        public static UITheme Instance
        {
            get
            {
                if (!loaded)
                {
                    instance = Resources.Load<UITheme>("UITheme");
                    loaded = true;
                }
                return instance;
            }
        }

        /// <summary>Gán sprite 9-slice của kit cho Image. Bảng chưa tạo hoặc sprite trống thì giữ nguyên.</summary>
        public static void Paint(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = SpriteScale;
        }
    }
}
