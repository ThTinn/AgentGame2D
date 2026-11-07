using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Nút cơ sở: thêm vào Button của Unity API gọn (AddListener/SetActive/SetInteractable), giữ nút (AddActionHold) và âm thanh.
    /// Khi có giữ nút thì lần giữ không tính là click.
    /// </summary>
    public class BaseBtn : Button
    {
        public bool wasHeld = false;
        public SFX_Sound nameSFX_Down;
        public SFX_Sound nameSFX_Up;

        [SerializeField] bool allowHold = false;
        [SerializeField] float holdDuration = 0.5f;

        UnityEvent action_HoldButton;
        Coroutine holdCoroutine;

        public void AddListener(UnityAction action)
        {
            if (action != null) onClick.AddListener(action);
        }

        /// <summary>Đăng ký hành động khi giữ nút đủ holdDuration giây. Bật allowHold.</summary>
        public void AddActionHold(UnityAction action)
        {
            allowHold = true;
            if (action_HoldButton == null) action_HoldButton = new UnityEvent();
            action_HoldButton.AddListener(action);
        }

        public bool ActiveSelf() => gameObject.activeSelf;

        public void SetActive(bool active) => gameObject.SetActive(active);

        public void SetInteractable(bool value) => interactable = value;

        public void RemoveAllListeners()
        {
            allowHold = false;
            onClick?.RemoveAllListeners();
            action_HoldButton?.RemoveAllListeners();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CancelHold();
            wasHeld = false;
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            if (wasHeld)
            {
                wasHeld = false;
                return;
            }
            base.OnPointerClick(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            base.OnPointerDown(eventData);
            UISound.Play(nameSFX_Down);

            if (allowHold)
            {
                CancelHold();
                holdCoroutine = StartCoroutine(HoldRoutine(eventData));
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            base.OnPointerUp(eventData);
            // nhả nút chỉ kêu khi khác tiếng nhấn (prefab để cả hai là Click: một lần bấm khỏi kêu hai lần)
            if (nameSFX_Up != nameSFX_Down) UISound.Play(nameSFX_Up);
            if (allowHold) CancelHold();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (allowHold) CancelHold();
        }

        IEnumerator HoldRoutine(PointerEventData eventData)
        {
            yield return new WaitForSecondsRealtime(holdDuration);

            wasHeld = true;
            holdCoroutine = null;
            action_HoldButton?.Invoke();
            UISound.Play(nameSFX_Down);

            // Bỏ trạng thái "đang nhấn" để nút không kẹt hiệu ứng nhấn sau khi giữ.
            base.OnPointerExit(eventData);
        }

        void CancelHold()
        {
            if (holdCoroutine == null) return;

            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
    }
}
