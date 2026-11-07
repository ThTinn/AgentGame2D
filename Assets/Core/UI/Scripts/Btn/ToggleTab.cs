using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Tab có chấm đỏ thông báo, chọn vào thì chấm tự tắt.</summary>
    public class ToggleTab : TextToggle
    {
        [SerializeField] GameObject dotGO;

        public override void ActionOn()
        {
            base.ActionOn();
            SetDot(false);
        }

        public void SetDot(bool status)
        {
            if (dotGO) dotGO.SetActive(status);
        }
    }
}
