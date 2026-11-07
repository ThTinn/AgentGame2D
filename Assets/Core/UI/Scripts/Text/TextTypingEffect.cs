using System;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Chữ hiện dần từng ký tự, xong thì đợi exitTime rồi báo stopAction.
    /// Thời gian gõ = duration + baseDurationPerChar * số ký tự. Bản 3D cộng dồn duration mỗi lần UpdateText nên câu sau chậm hơn câu trước; đã sửa.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextTypingEffect : MonoBehaviour
    {
        enum Phase { Idle, Typing, Exiting }

        public Action stopAction;
        public Action startAction;
        public float duration = 2f;
        public float exitTime = 1f;
        public float baseDurationPerChar = 0.01f;

        string content;
        Phase phase;
        float timer;
        float total;

        public TextMeshProUGUI txt { get; private set; }
        public bool IsRuning => phase != Phase.Idle;

        void Awake()
        {
            txt = GetComponent<TextMeshProUGUI>();
        }

        void OnDisable()
        {
            phase = Phase.Idle;
        }

        public void ResetText()
        {
            phase = Phase.Idle;
            txt.text = "";
        }

        public void UpdateText(string newText)
        {
            content = newText;
            if (string.IsNullOrWhiteSpace(content)) return;

            if (!txt) txt = GetComponent<TextMeshProUGUI>();
            txt.text = content;
            total = duration + baseDurationPerChar * content.Length;
            PlayTypingEffect();
        }

        public void PlayTypingEffect()
        {
            txt.maxVisibleCharacters = 0;
            timer = 0f;
            phase = Phase.Typing;
            startAction?.Invoke();
        }

        /// <summary>Kết thúc ngay: hiện đủ chữ và báo stopAction.</summary>
        public void StopAction()
        {
            phase = Phase.Idle;
            txt.maxVisibleCharacters = content != null ? content.Length : 0;
            stopAction?.Invoke();
        }

        void Update()
        {
            if (phase == Phase.Idle) return;

            timer += Time.deltaTime;

            if (phase == Phase.Typing)
            {
                if (timer >= total)
                {
                    txt.maxVisibleCharacters = content.Length;
                    timer = 0f;
                    phase = Phase.Exiting;
                    return;
                }
                txt.maxVisibleCharacters = Mathf.Min(content.Length, (int)(timer / total * content.Length));
            }
            else if (timer >= exitTime)
            {
                StopAction();
            }
        }
    }
}
