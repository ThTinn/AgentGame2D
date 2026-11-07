using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Đếm ngược hiển thị bằng chữ ("3.5s" hoặc "01:20") và vòng tô (Image kiểu Filled) chạy từ đầy về rỗng.
    /// Bản 3D dùng DOTween (DOVirtual.Float) và đụng cooldownBG khi chưa gán sẽ lỗi; ở đây chạy bằng Update theo thời gian thực
    /// và chỉ viết lại chữ khi phần chục giây đổi (ghép ký tự vào bộ đệm, không tạo string mới).
    /// </summary>
    public class TextCooldown : MonoBehaviour
    {
        public float timeRemaining;
        public float timeOri;
        public float timeEnd;
        public float cooldownTime;

        [SerializeField] TextMeshProUGUI txtInfo;
        [SerializeField] Image cooldownBG;

        public Action onStartEvent;
        public Action onEndEvent;
        public Action<float> onUpdateEvent;

        bool running;
        int shownTenths = -1;

        public bool IsRunning => running;

        void Awake()
        {
            if (txtInfo == null) txtInfo = GetComponentInChildren<TextMeshProUGUI>();
        }

        void OnDisable()
        {
            running = false;
            if (txtInfo) txtInfo.gameObject.SetActive(false);
            if (cooldownBG) cooldownBG.gameObject.SetActive(false);
        }

        /// <summary>Dừng ngay và báo onEndEvent.</summary>
        public void StopCooldown()
        {
            running = false;
            OnEndTime();
        }

        /// <summary>Bắt đầu đếm timeRemaining giây. cooldownTime (nếu &gt; 0) là tổng thời gian để tính vòng tô, mặc định bằng timeRemaining.</summary>
        public void SetInfo(float timeRemaining, float cooldownTime = -1)
        {
            timeOri = this.timeRemaining = timeRemaining;
            timeEnd = Time.realtimeSinceStartup + timeRemaining;
            this.cooldownTime = cooldownTime > 0 ? cooldownTime : timeRemaining;

            if (this.timeRemaining <= 0f)
            {
                running = false;
                OnEndTime();
                return;
            }

            if (cooldownBG)
            {
                cooldownBG.gameObject.SetActive(true);
                cooldownBG.fillAmount = 1f;
            }
            shownTenths = -1;
            ShowTime(this.timeRemaining);

            running = true;
            onStartEvent?.Invoke();
        }

        void Update()
        {
            if (!running) return;

            float left = timeEnd - Time.realtimeSinceStartup;
            if (left <= 0f)
            {
                running = false;
                OnEndTime();
                return;
            }

            timeRemaining = left;
            ShowTime(left);
            if (cooldownBG) cooldownBG.fillAmount = left / cooldownTime;
            onUpdateEvent?.Invoke(left);
        }

        void OnEndTime()
        {
            timeRemaining = 0f;
            if (txtInfo) txtInfo.gameObject.SetActive(false);
            if (cooldownBG) cooldownBG.gameObject.SetActive(false);
            onEndEvent?.Invoke();
        }

        void ShowTime(float seconds)
        {
            if (!txtInfo) return;

            int tenths = (int)(seconds * 10f);
            if (tenths == shownTenths) return;
            shownTenths = tenths;

            if (seconds <= 0f)
            {
                txtInfo.gameObject.SetActive(false);
                return;
            }

            if (!txtInfo.gameObject.activeSelf) txtInfo.gameObject.SetActive(true);

            // ghép ký tự vào bộ đệm rồi SetCharArray: không tạo string mới ~10 lần / giây cho mỗi ô đang hồi chiêu.
            // Luôn dấu chấm thập phân ("3.5s") trên mọi máy, như bản dùng InvariantCulture trước đây.
            int len = 0;
            int minutes = (int)(seconds / 60f) % 60;
            if (minutes > 0)
            {
                int secs = (int)seconds % 60;
                len = Two(minutes, len);
                chars[len++] = ':';
                len = Two(secs, len);
            }
            else
            {
                int rounded = Mathf.RoundToInt(seconds * 10f);   // một chữ số lẻ, làm tròn như "N1"
                len = Digits(rounded / 10, len);
                chars[len++] = '.';
                chars[len++] = (char)('0' + rounded % 10);
                chars[len++] = 's';
            }
            txtInfo.SetCharArray(chars, 0, len);
        }

        readonly char[] chars = new char[16];

        int Two(int value, int at)
        {
            chars[at++] = (char)('0' + value / 10 % 10);
            chars[at++] = (char)('0' + value % 10);
            return at;
        }

        int Digits(int value, int at)
        {
            int start = at;
            do
            {
                chars[at++] = (char)('0' + value % 10);
                value /= 10;
            } while (value > 0 && at < chars.Length - 4);
            Array.Reverse(chars, start, at - start);
            return at;
        }
    }
}
