using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>Một nút trong SelectionPopup: chữ, hành động, có thể bấm hay không và thời gian chờ trước khi bấm được.</summary>
    public class BtnData
    {
        public short id;
        public string content;
        public UnityAction action;
        public bool interactable = true;
        /// <summary>Giây phải chờ trước khi nút bấm được. Chữ trên nút hiện đếm ngược.</summary>
        public float cooldown;
        /// <summary>Màu chữ riêng (null là để mặc định của prefab).</summary>
        public Color? textColor;

        public BtnData(short id, string content, UnityAction action = null, float cooldown = 0f)
        {
            this.id = id;
            this.content = content;
            this.action = action;
            this.cooldown = cooldown;
        }
    }

    /// <summary>
    /// Popup hỏi/đáp dùng chung: thông báo OK, xác nhận Yes/No, danh sách lựa chọn, hội thoại nhiều trang.
    /// Nút được lấy từ pool trong buttonHolder (hết thì Instantiate thêm và giữ lại), nên mở đi mở lại không sinh rác.
    /// UseQueue = true: gọi khi đang mở thì yêu cầu xếp hàng, không đè mất nội dung người chơi đang đọc.
    /// </summary>
    public class SelectionPopup : PopupBase
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text contentText;
        [Tooltip("Nơi chứa các nút lựa chọn (nên có LayoutGroup).")]
        [SerializeField] RectTransform buttonHolder;
        [SerializeField] TextBtn buttonPrefab;
        [Tooltip("Nút sang trang cho hội thoại nhiều trang.")]
        [SerializeField] TextBtn nextButton;

        readonly List<TextBtn> pool = new List<TextBtn>();
        readonly List<Cooldown> cooldowns = new List<Cooldown>();

        string[] pages;
        string pagesTitle;
        int pageIndex = -1;
        UnityAction onPagesDone;
        UnityAction onCancel;

        public override bool UseQueue => true;

        struct Cooldown
        {
            public TextBtn button;
            public string label;
            public float remain;
        }

        public override void OnInit()
        {
            // gom sẵn các nút đã đặt tay trong prefab làm pool ban đầu
            buttonHolder.GetComponentsInChildren(true, pool);
            for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
            if (nextButton)
            {
                pool.Remove(nextButton);
                nextButton.AddListener(NextPage);
                nextButton.gameObject.SetActive(false);
            }
        }

        // ---------------- Cách dùng ----------------

        /// <summary>Thông báo một nút OK.</summary>
        public void ShowOk(string content, UnityAction onOk = null)
        {
            ClearButtons();
            SetContent(null, content);
            TextBtn button = Take();
            button.SetText(Text(Lang.Ok, "OK"));
            button.AddListener(Close);
            if (onOk != null) button.AddListener(onOk);
        }

        /// <summary>Hỏi Xác nhận / Hủy bỏ. onNo chạy cả khi người chơi bấm Esc.</summary>
        public void ShowYesNo(string content, UnityAction onYes, UnityAction onNo = null)
        {
            ClearButtons();
            SetContent(null, content);

            TextBtn yes = Take();
            yes.SetText(Text(Lang.Xac_nhan, "Yes"));
            yes.AddListener(Close);
            if (onYes != null) yes.AddListener(onYes);

            TextBtn no = Take();
            no.SetText(Text(Lang.Huy_bo, "No"));
            no.AddListener(Close);
            if (onNo != null) no.AddListener(onNo);

            onCancel = onNo;
        }

        /// <summary>Danh sách lựa chọn tự đặt. Nút nào cũng tự đóng popup sau khi chạy hành động của nó. title: dòng tiêu đề (null = không có).</summary>
        public void ShowChoices(string content, List<BtnData> buttons, string title = null)
        {
            ClearButtons();
            SetContent(title, content);
            if (buttons == null) return;

            for (int i = 0; i < buttons.Count; i++)
            {
                BtnData data = buttons[i];
                if (data == null) continue;

                TextBtn button = Take();
                button.SetText(data.content);
                if (data.textColor.HasValue) button.SetColorText(data.textColor.Value);
                button.AddListener(Close);
                if (data.action != null) button.AddListener(data.action);

                if (data.cooldown > 0f)
                {
                    button.SetInteractable(false);
                    cooldowns.Add(new Cooldown { button = button, label = data.content, remain = data.cooldown });
                }
                else
                {
                    button.SetInteractable(data.interactable);
                }
            }
        }

        /// <summary>Hội thoại nhiều trang: bấm nút tiếp (hoặc Space) sang trang, hết trang thì chạy onDone và đóng. title giữ nguyên qua các trang.</summary>
        public void ShowPages(string[] contents, UnityAction onDone = null, string title = null)
        {
            if (contents == null || contents.Length == 0)
            {
                onDone?.Invoke();
                Close();
                return;
            }

            pages = contents;
            pageIndex = 0;
            pagesTitle = title;
            onPagesDone = onDone;
            DrawPage();
        }

        /// <summary>Đặt tiêu đề cho lần hiện tại (gọi sau các hàm Show...).</summary>
        public void SetTitle(string title)
        {
            if (!titleText) return;
            titleText.text = title;
            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
        }

        // ---------------- Bên trong ----------------

        void DrawPage()
        {
            ClearButtons();
            SetContent(pagesTitle, pages[pageIndex]);
            if (nextButton) nextButton.gameObject.SetActive(true);
        }

        void NextPage()
        {
            if (pages == null)
            {
                Close();
                return;
            }

            pageIndex++;
            if (pageIndex >= pages.Length)
            {
                UnityAction done = onPagesDone;
                Close();
                done?.Invoke();
                return;
            }

            DrawPage();
        }

        void SetContent(string title, string content)
        {
            SetTitle(title);
            if (contentText) contentText.text = content;
        }

        /// <summary>Dọn nội dung lần trước: tắt hết nút, bỏ listener, bỏ đếm ngược.</summary>
        void ClearButtons()
        {
            cooldowns.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
                pool[i].RemoveAllListeners();
            }
            if (nextButton) nextButton.gameObject.SetActive(false);
            onCancel = null;
        }

        /// <summary>Lấy một nút rảnh trong pool, hết thì tạo thêm và giữ lại cho lần sau.</summary>
        TextBtn Take()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                TextBtn button = pool[i];
                if (button.gameObject.activeSelf) continue;

                button.gameObject.SetActive(true);
                button.SetInteractable(true);
                button.SetColorText(Color.white);
                button.transform.SetAsLastSibling();
                return button;
            }

            TextBtn created = Instantiate(buttonPrefab, buttonHolder);
            created.gameObject.SetActive(true);
            pool.Add(created);
            return created;
        }

        void Update()
        {
            if (cooldowns.Count == 0) return;   // không có nút chờ thì thoát ngay

            float dt = Time.unscaledDeltaTime;
            for (int i = cooldowns.Count - 1; i >= 0; i--)
            {
                Cooldown task = cooldowns[i];
                task.remain -= dt;

                if (task.remain <= 0f)
                {
                    task.button.SetText(task.label);
                    task.button.SetInteractable(true);
                    cooldowns.RemoveAt(i);
                    continue;
                }

                task.button.SetText(task.label + " (" + Mathf.CeilToInt(task.remain) + "s)");
                cooldowns[i] = task;
            }
        }

        public override void OnClose()
        {
            base.OnClose();
            pages = null;
            pagesTitle = null;
            pageIndex = -1;
            onPagesDone = null;
            onCancel = null;
            cooldowns.Clear();
        }

        public override void OnKey(Key key)
        {
            switch (key)
            {
                case Key.Escape:
                    UnityAction cancel = onCancel;
                    Close();
                    cancel?.Invoke();
                    break;
                case Key.Space:
                    if (pages != null) NextPage();
                    break;
            }
        }

        // Lang chưa nạp (chuỗi rỗng) thì dùng chữ dự phòng để nút không trống trơn.
        static string Text(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
