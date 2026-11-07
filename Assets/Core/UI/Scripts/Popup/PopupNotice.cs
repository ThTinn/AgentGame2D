using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Thông báo trôi (toast): hiện mờ dần, đứng yên vài giây rồi tan. Không vào history, không chặn thao tác,
    /// nên gọi liên tục cũng được: lần sau ghi đè lần trước thay vì xếp chồng.
    /// </summary>
    public class PopupNotice : PopupBase
    {
        [SerializeField] TMP_Text contentText;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] float fadeIn = 0.15f;
        [SerializeField] float fadeOut = 0.3f;

        Sequence sequence;

        public override bool IsToast => true;

        public override void OnInit()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup) canvasGroup.blocksRaycasts = false;   // toast không được nuốt thao tác
        }

        public void Show(string content, float seconds = 2f)
        {
            if (sequence.isAlive) sequence.Stop();
            if (contentText) contentText.text = content;
            canvasGroup.alpha = 0f;

            sequence = Sequence.Create()
                .Chain(Tween.Alpha(canvasGroup, 1f, fadeIn))
                .ChainDelay(seconds)
                .Chain(Tween.Alpha(canvasGroup, 0f, fadeOut))
                .OnComplete(this, popup => popup.Close());
        }

        public override void OnClose()
        {
            if (sequence.isAlive) sequence.Stop();
            base.OnClose();
        }

        /// <summary>Toast không nhận phím.</summary>
        public override void OnKey(Key key) { }
    }
}
