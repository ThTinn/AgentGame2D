using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// Bấm xong nút bị khóa và đếm ngược, hết giờ mở lại. Đếm bằng Update theo thời gian thực (không bị Time.timeScale = 0 chặn),
    /// chỉ tạo chuỗi mới khi số giây đổi.
    /// Bản 3D dùng coroutine WaitForSeconds(1) và nếu nút bị tắt giữa chừng thì nút kẹt ở trạng thái khóa với chữ "(n)".
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonCooldown : MonoBehaviour
    {
        [SerializeField] float cooldownTime = 5f;
        [SerializeField] bool showCountdownText = true;

        Button button;
        TextMeshProUGUI buttonText;
        string originalText;

        bool running;
        float endTime;
        int shownSeconds;

        public bool IsCoolingDown => running;

        void Awake()
        {
            button = GetComponent<Button>();
            buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            button.onClick.AddListener(StartCooldown);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(StartCooldown);
        }

        void OnDisable()
        {
            if (running) Finish();
        }

        public void StartCooldown()
        {
            if (running) return;

            running = true;
            endTime = Time.unscaledTime + cooldownTime;
            shownSeconds = -1;
            button.interactable = false;
            if (buttonText != null) originalText = buttonText.text;
            UpdateText(Mathf.CeilToInt(cooldownTime));
        }

        void Update()
        {
            if (!running) return;

            float left = endTime - Time.unscaledTime;
            if (left <= 0f)
            {
                Finish();
                return;
            }
            UpdateText(Mathf.CeilToInt(left));
        }

        void UpdateText(int seconds)
        {
            if (!showCountdownText || buttonText == null || seconds == shownSeconds) return;

            shownSeconds = seconds;
            buttonText.text = originalText + " (" + NumberCache.Get(seconds) + ")";
        }

        void Finish()
        {
            running = false;
            button.interactable = true;
            if (showCountdownText && buttonText != null) buttonText.text = originalText;
        }
    }
}
