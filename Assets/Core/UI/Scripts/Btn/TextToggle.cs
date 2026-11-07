using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class TextToggle : BaseToggle
    {
        public TextMeshProUGUI _txt;

        public void SetText(string text)
        {
            if (_txt) _txt.text = text;
        }
    }
}
