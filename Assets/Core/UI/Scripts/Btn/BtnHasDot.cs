using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Nút có chấm đỏ thông báo.</summary>
    public class BtnHasDot : TextBtn
    {
        public GameObject notice;

        public void InitData(string buttonName, bool hasNotice)
        {
            SetText(buttonName);
            UpdateStatusNotice(hasNotice);
        }

        public void UpdateStatusNotice(bool hasNotice)
        {
            if (notice) notice.SetActive(hasNotice);
        }
    }
}
