using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Toggle cơ sở dạng "tab": chọn thì gọi actionSelect, bỏ chọn thì gọi actionDeselect.
    /// IsFixedInteractable (mặc định bật): toggle đang được chọn thì khóa, không cho bấm lại để tắt (kiểu tab).
    /// Bản 3D có lỗi ở SetStatus(false): khóa luôn toggle đang tắt nên không bấm lại được; đã sửa.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class BaseToggle : MonoBehaviour
    {
        [SerializeField] protected Toggle _tg;
        public SFX_Sound soundClick;
        public bool IsFixedInteractable = true;

        protected Action actionSelect, actionDeselect;

        public bool isOn => _tg.isOn;

        protected virtual void Awake()
        {
            if (!_tg) _tg = GetComponent<Toggle>();
        }

        protected virtual void OnEnable()
        {
            _tg.onValueChanged.AddListener(OnValueChange);
        }

        protected virtual void OnDisable()
        {
            _tg.onValueChanged.RemoveListener(OnValueChange);
        }

        protected void OnValueChange(bool status)
        {
            if (status) ActionOn();
            else ActionOff();
        }

        public void RemoveAllListeners()
        {
            actionSelect = null;
            actionDeselect = null;
        }

        public void AddListener(Action actionSelect, Action actionDeselect = null)
        {
            this.actionSelect = actionSelect;
            if (actionDeselect != null) this.actionDeselect = actionDeselect;
        }

        public void SetGroup(ToggleGroup group) => _tg.group = group;

        public virtual void SetStatus(bool status)
        {
            _tg.isOn = status;
            if (IsFixedInteractable) SetInteractable(!status);
        }

        public void SetInteractable(bool interactable) => _tg.interactable = interactable;

        public void SetIsFixedInteractable(bool status) => IsFixedInteractable = status;

        public virtual void ActionOn()
        {
            UISound.Play(soundClick);
            actionSelect?.Invoke();
            if (IsFixedInteractable) _tg.interactable = false;
        }

        public virtual void ActionOff()
        {
            actionDeselect?.Invoke();
            if (IsFixedInteractable) _tg.interactable = true;
        }
    }
}
