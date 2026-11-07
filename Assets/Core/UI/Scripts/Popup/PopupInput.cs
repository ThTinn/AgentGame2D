using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Popup nhập số: tiêu đề, ô nhập (chỉ số nguyên), nút Xác nhận và Đóng.
    /// Giá trị được kẹp trong [min, max] nên nơi gọi không phải kiểm tra lại.
    /// </summary>
    public class PopupInput : PopupBase
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_InputField input;
        [SerializeField] TextBtn acceptButton;
        [SerializeField] TextBtn closeButton;

        Action<int> onAccept;
        int min;
        int max = int.MaxValue;

        public override void OnInit()
        {
            if (input) input.contentType = TMP_InputField.ContentType.IntegerNumber;
            if (acceptButton)
            {
                acceptButton.SetText(string.IsNullOrEmpty(Lang.Xac_nhan) ? "OK" : Lang.Xac_nhan);
                acceptButton.AddListener(Accept);
            }
            if (closeButton)
            {
                closeButton.SetText(string.IsNullOrEmpty(Lang.Close) ? "Close" : Lang.Close);
                closeButton.AddListener(Close);
            }
        }

        public void Open(string title, int defaultValue, Action<int> onAccept, int min = 0, int max = int.MaxValue)
        {
            this.onAccept = onAccept;
            this.min = min;
            this.max = Mathf.Max(min, max);

            if (titleText) titleText.text = string.IsNullOrEmpty(title) ? Lang.Nhap_so_luong : title;
            if (input)
            {
                input.text = Mathf.Clamp(defaultValue, this.min, this.max).ToString();
                input.ActivateInputField();
                input.caretPosition = input.text.Length;
            }
        }

        public void Accept()
        {
            if (input == null || !int.TryParse(input.text, out int value))
            {
                PopupMN.Toast(string.IsNullOrEmpty(Lang.So_luong_khong_hop_le) ? "Giá trị không hợp lệ" : Lang.So_luong_khong_hop_le);
                return;
            }

            value = Mathf.Clamp(value, min, max);
            Action<int> callback = onAccept;
            Close();
            callback?.Invoke(value);
        }

        public override void OnClose()
        {
            base.OnClose();
            onAccept = null;
        }

        public override void OnKey(Key key)
        {
            switch (key)
            {
                case Key.Enter:
                    Accept();
                    break;
                case Key.Escape:
                    Close();
                    break;
            }
        }
    }
}
