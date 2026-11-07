using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Tab dạng icon: đổi màu icon khi chọn, có chấm đỏ.</summary>
    public class ToggleTabLeft : BaseToggle
    {
        [SerializeField] Image iconImg;
        [SerializeField] Color defaultColor = Color.white, focusColor = Color.yellow;
        [SerializeField] GameObject dotGO;

        public void SetDot(bool status)
        {
            if (dotGO) dotGO.SetActive(status);
        }

        public void ResetColor()
        {
            if (iconImg) iconImg.color = defaultColor;
        }

        public void FocusColor()
        {
            if (iconImg) iconImg.color = focusColor;
        }

        public override void ActionOn()
        {
            base.ActionOn();
            FocusColor();
            SetDot(false);
        }

        public override void ActionOff()
        {
            base.ActionOff();
            ResetColor();
        }
    }
}
