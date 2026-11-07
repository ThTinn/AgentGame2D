using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Toggle có bộ ảnh nền và dấu tích tùy chỉnh.</summary>
    public class ImageTg : TextToggle
    {
        [SerializeField] Image bgImg, checkmarkImg;

        public void SetupImg(Sprite imgBG, Sprite imgCheckmark)
        {
            if (bgImg) bgImg.sprite = imgBG;
            if (checkmarkImg) checkmarkImg.sprite = imgCheckmark;
        }
    }
}
