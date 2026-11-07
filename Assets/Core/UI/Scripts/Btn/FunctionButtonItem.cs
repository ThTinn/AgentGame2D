using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>Nút chức năng có huy hiệu số thông báo (ẩn khi bằng 0).</summary>
    public class FunctionButtonItem : TextBtn
    {
        public GameObject amountGO;
        public TextMeshProUGUI amountTxt;

        public void InitData(string buttonName, int amountNotice = 0)
        {
            SetText(buttonName);
            UpdateNotice(amountNotice);
        }

        public void UpdateNotice(int amountNotice)
        {
            if (!amountGO) return;

            if (amountNotice > 0)
            {
                if (amountTxt) amountTxt.text = NumberCache.Get(amountNotice);
                amountGO.SetActive(true);
            }
            else
            {
                amountGO.SetActive(false);
            }
        }
    }
}
