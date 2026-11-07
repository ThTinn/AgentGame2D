using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Một nút trong hộp thoại NPC. enabled = false thì nút mờ, không bấm được.</summary>
    public struct DialogChoice
    {
        public string label;
        public Action action;
        public bool enabled;

        public DialogChoice(string label, Action action, bool enabled = true)
        {
            this.label = label;
            this.action = action;
            this.enabled = enabled;
        }
    }

    /// <summary>
    /// Khung hội thoại kiểu RPG: khung ngang dưới màn hình với ảnh + tên + độ thân thiện của NPC, chữ hiện dần từng ký tự,
    /// các lựa chọn xếp dọc phía trên bên phải khung. Chỉ là phần hiển thị: nói gì, có nút nào do <see cref="NpcDialog"/> quyết.
    ///  - <see cref="Say"/>: gõ một câu, gõ xong hiện các nút.
    ///  - <see cref="Pages"/>: gõ từng trang; chạm khung / Space / Enter: đang gõ thì hiện hết, gõ xong thì sang trang.
    /// Prefab dựng bằng Tools/Core/UI/Build Npc Dialog Popup.
    /// </summary>
    public class NpcDialogPopup : PopupBase
    {
        [SerializeField] Image portrait;
        [SerializeField] TMP_Text nameText;
        [Tooltip("Dòng độ thân thiện cạnh bảng tên (bậc + điểm).")]
        [SerializeField] TMP_Text affinityText;
        [Tooltip("Nền của dòng thân thiện: ẩn cùng chữ khi NPC không có độ thân thiện. Trống = chỉ ẩn chữ.")]
        [SerializeField] GameObject affinityPlate;
        [SerializeField] TMP_Text contentText;
        [Tooltip("Nút trong suốt phủ khung chữ: chạm để hiện hết chữ / sang trang.")]
        [SerializeField] BaseBtn advanceButton;
        [Tooltip("Dấu >> ở góc khung: hiện khi còn trang để đọc.")]
        [SerializeField] GameObject nextHint;
        [Tooltip("Nơi chứa các nút lựa chọn (VerticalLayoutGroup).")]
        [SerializeField] RectTransform choiceHolder;
        [SerializeField] TextBtn choicePrefab;
        [Tooltip("Tốc độ gõ chữ (ký tự / giây). 0 = hiện ngay.")]
        [SerializeField] float charsPerSecond = 45f;

        static readonly Color DisabledText = new Color(1f, 1f, 1f, 0.45f);

        readonly List<TextBtn> pool = new List<TextBtn>();
        readonly List<DialogChoice> choices = new List<DialogChoice>();

        string[] pages;
        int page;
        Action onPagesDone;
        bool typing;
        float visible;
        int total;

        /// <summary>Người chơi vừa đóng hộp thoại (Tạm biệt, Esc, mở panel khác).</summary>
        public event Action Closed;

        public override void OnInit()
        {
            if (choiceHolder) choiceHolder.GetComponentsInChildren(true, pool);
            for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
            if (advanceButton) advanceButton.AddListener(Advance);
        }

        // ---------------- Cách dùng ----------------

        /// <summary>Người đang nói: tên (kèm vai trò), ảnh (null = ẩn khung ảnh), dòng thân thiện (null = ẩn).</summary>
        public void SetSpeaker(string speaker, Sprite face, string affinity)
        {
            if (nameText) nameText.text = speaker;
            if (portrait)
            {
                portrait.sprite = face;
                portrait.enabled = face != null;
            }
            SetAffinity(affinity);
        }

        public void SetAffinity(string affinity)
        {
            if (!affinityText) return;
            affinityText.text = affinity ?? "";
            (affinityPlate ? affinityPlate : affinityText.gameObject).SetActive(!string.IsNullOrEmpty(affinity));
        }

        /// <summary>Gõ một câu rồi hiện các nút. instant: hiện chữ ngay (quay lại menu, không gõ lại câu đã đọc).</summary>
        public void Say(string text, List<DialogChoice> buttons, bool instant = false)
        {
            pages = null;
            onPagesDone = null;
            choices.Clear();
            if (buttons != null) choices.AddRange(buttons);
            HideChoices();
            Type(text, instant);
        }

        /// <summary>Đọc từng trang; hết trang thì chạy onDone (thường là Say lại menu).</summary>
        public void Pages(string[] contents, Action onDone)
        {
            if (contents == null || contents.Length == 0)
            {
                onDone?.Invoke();
                return;
            }
            pages = contents;
            page = 0;
            onPagesDone = onDone;
            choices.Clear();
            HideChoices();
            Type(pages[0], false);
        }

        // ---------------- Bên trong ----------------

        /// <summary>Chạm khung, Space hoặc Enter: đang gõ thì hiện hết chữ, đang đọc trang thì sang trang.</summary>
        void Advance()
        {
            if (typing)
            {
                FinishTyping();
                return;
            }
            if (pages == null) return;

            page++;
            if (page < pages.Length)
            {
                Type(pages[page], false);
                return;
            }

            Action done = onPagesDone;
            pages = null;
            onPagesDone = null;
            done?.Invoke();
        }

        void ShowChoices()
        {
            HideChoices();
            for (int i = 0; i < choices.Count; i++)
            {
                DialogChoice c = choices[i];
                TextBtn button = Take();
                button.SetText(c.label);
                button.SetInteractable(c.enabled);
                button.SetColorText(c.enabled ? Color.white : DisabledText);
                Action action = c.action;
                if (action != null) button.AddListener(() => action());
            }
        }

        void HideChoices()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
                pool[i].RemoveAllListeners();
            }
        }

        /// <summary>Lấy một nút rảnh trong pool, hết thì tạo thêm và giữ lại cho lần sau.</summary>
        TextBtn Take()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                TextBtn button = pool[i];
                if (button.gameObject.activeSelf) continue;
                button.gameObject.SetActive(true);
                button.transform.SetAsLastSibling();
                return button;
            }

            TextBtn created = Instantiate(choicePrefab, choiceHolder);
            created.gameObject.SetActive(true);
            pool.Add(created);
            return created;
        }

        // ---------------- Gõ chữ ----------------

        void Type(string text, bool instant)
        {
            if (nextHint) nextHint.SetActive(false);
            if (!contentText)
            {
                OnTyped();
                return;
            }

            contentText.text = text ?? "";
            contentText.ForceMeshUpdate();
            total = contentText.textInfo.characterCount;   // đếm sau khi bỏ thẻ rich text
            visible = 0f;
            typing = !instant && charsPerSecond > 0f && total > 0;
            if (typing) contentText.maxVisibleCharacters = 0;
            else FinishTyping();
        }

        void FinishTyping()
        {
            typing = false;
            if (contentText) contentText.maxVisibleCharacters = int.MaxValue;
            OnTyped();
        }

        void OnTyped()
        {
            if (pages != null)
            {
                if (nextHint) nextHint.SetActive(true);
            }
            else ShowChoices();
        }

        void Update()
        {
            if (!typing) return;

            visible += Time.unscaledDeltaTime * charsPerSecond;
            if (visible >= total) FinishTyping();
            else contentText.maxVisibleCharacters = (int)visible;
        }

        // ---------------- Vòng đời ----------------

        public override void OnClose()
        {
            base.OnClose();
            HideChoices();
            choices.Clear();
            pages = null;
            onPagesDone = null;
            typing = false;
            Action closed = Closed;
            Closed = null;   // mỗi lần mở một cuộc nói chuyện mới, người nghe đăng ký lại
            closed?.Invoke();
        }

        public override void OnKey(Key key)
        {
            switch (key)
            {
                case Key.Escape:
                    Close();
                    break;
                case Key.Space:
                case Key.Enter:
                    Advance();
                    break;
            }
        }
    }
}
