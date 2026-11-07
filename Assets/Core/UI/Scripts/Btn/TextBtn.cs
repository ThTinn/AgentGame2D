using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Nút có chữ. Nếu chưa gán nameTxt thì tự tìm TextMeshProUGUI con.</summary>
    public class TextBtn : BaseBtn
    {
        public TextMeshProUGUI nameTxt;

        protected override void Awake()
        {
            base.Awake();
            if (!nameTxt) nameTxt = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        public virtual void SetText(string buttonName)
        {
            if (!Resolve())
            {
                Debug.LogError("TextBtn " + name + ": chưa có nameTxt, không đặt được chữ '" + buttonName + "'");
                return;
            }
            nameTxt.text = buttonName;
        }

        public void SetColorText(Color color)
        {
            if (!Resolve())
            {
                Debug.LogError("TextBtn " + name + ": chưa có nameTxt, không đặt được màu");
                return;
            }
            nameTxt.color = color;
        }

        /// <summary>Tìm lại nameTxt nếu chưa có: cho phép gọi SetText trước khi Awake của nút chạy.</summary>
        bool Resolve()
        {
            if (!nameTxt) nameTxt = GetComponentInChildren<TextMeshProUGUI>(true);
            return nameTxt;
        }

        public string GetText() => nameTxt ? nameTxt.text : string.Empty;
    }
}
