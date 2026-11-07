using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Màn chờ: nền mờ chặn thao tác, chữ "Xin chờ" và ba dấu chấm chạy. Không đóng được bằng Esc,
    /// code nào mở thì code đó gọi PopupMN.HideLoading() (hoặc UIManager.CloseEverything khi mất kết nối).
    /// </summary>
    public class PopupLoading : PopupBase
    {
        [SerializeField] TMP_Text contentText;
        [SerializeField] TMP_Text dotsText;
        [Tooltip("Giây giữa hai bước của dấu chấm chạy.")]
        [SerializeField] float step = 0.35f;

        static readonly string[] Dots = { "", ".", "..", "..." };

        float nextStep;
        int dotIndex;

        public void Show(string content = null)
        {
            if (contentText) contentText.text = string.IsNullOrEmpty(content)
                ? (string.IsNullOrEmpty(Lang.Xin_cho) ? "Xin chờ" : Lang.Xin_cho)
                : content;

            dotIndex = 0;
            nextStep = 0f;
            if (dotsText) dotsText.text = Dots[0];
        }

        void Update()
        {
            if (dotsText == null) return;

            // dùng unscaledTime để màn chờ vẫn chạy khi game đang tạm dừng
            if (Time.unscaledTime < nextStep) return;

            nextStep = Time.unscaledTime + step;
            dotIndex = (dotIndex + 1) % Dots.Length;
            dotsText.text = Dots[dotIndex];
        }

        /// <summary>Màn chờ không cho đóng bằng phím.</summary>
        public override void OnKey(Key key) { }
    }
}
