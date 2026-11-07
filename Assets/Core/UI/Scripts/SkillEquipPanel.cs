using System.Collections.Generic;
using System.Text;
using Game.Anim;
using Game.Players;
using Game.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bảng trang bị skill (panel UIID.SkillEquip). Cột trái: bốn ô skill. Lưới giữa: các skill trong SkillDatabase
    /// (trừ đánh thường, vốn đi theo vũ khí) kèm ô Trống. Phải: ảnh động của skill đang chọn, tên, mô tả + chỉ số và nút
    /// Trang bị. Trang bị = <see cref="PlayerProfile.SetSkill"/>: gắn skill vào ô kéo theo anim của ô đó
    /// (<see cref="CharacterAnimator.SetSkillAnim"/>); GameController nghe sự kiện và ghi vào PlayerData.
    /// Lưới là <see cref="RecycleGrid"/> dùng chung: đổi ô skill chỉ vẽ lại ô đang hiện, không hủy / tạo lại nút.
    /// </summary>
    public class SkillEquipPanel : PanelBase, IRecycleGridDelegate
    {
        [Header("Trang bị skill")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [Tooltip("Nút xác nhận: gắn skill đang chọn vào ô đang chọn.")]
        [SerializeField] TextBtn equipButton;
        [Tooltip("Nơi sinh nút của bốn ô skill (có VerticalLayoutGroup).")]
        [SerializeField] RectTransform slotRoot;
        [Tooltip("Lưới skill (ô mẫu là IconLabelCell: ảnh trên, chữ dưới).")]
        [SerializeField] RecycleGrid grid;
        [Tooltip("Nút của cột bốn ô skill bên trái.")]
        [SerializeField] TextBtn buttonPrefab;
        [Header("Xem trước")]
        [SerializeField] Image previewImage;
        [SerializeField] TMP_Text previewName;
        [Tooltip("Mô tả skill và chỉ số ở cấp đang có.")]
        [SerializeField] TMP_Text infoText;
        [Tooltip("Hiện khi chưa có nhân vật hoặc chưa có bảng skill.")]
        [SerializeField] TMP_Text messageText;

        const float MaxRowHeight = 64f;

        /// <summary>Một ô của lưới: skill (null = ô Trống).</summary>
        struct Cell
        {
            public SkillTemplate skill;
            public string label;
            public Sprite icon;
            public AnimMotion anim;
        }

        readonly List<TextBtn> slotButtons = new List<TextBtn>();
        readonly List<Cell> cells = new List<Cell>();
        readonly StringBuilder info = new StringBuilder(256);

        PlayerProfile player;
        AnimPreview preview;
        int slot;       // ô skill đang chọn (0-3)
        int selected;   // chỉ số trong lưới
        int equipped = -1;   // chỉ số trong lưới của skill đang gắn ở ô đang chọn, tính một lần mỗi lần vẽ lại lưới

        static string SlotLabel(int index)
        {
            return Lang.Format("Kỹ năng {0}|Skill {0}", index + 1);
        }

        static int IdOf(Cell c) => c.skill != null ? c.skill.id : 0;

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (equipButton) equipButton.AddListener(Equip);
            if (grid) grid.Delegate = this;
            preview = AnimPreview.Attach(previewImage);
        }

        void OnDestroy()
        {
            if (closeButton) closeButton.RemoveAllListeners();
            if (equipButton) equipButton.RemoveAllListeners();
        }

        public override void OnShow()
        {
            base.OnShow();
            player = CoreSystems.Me;
            BuildCells();
            SelectSlot(slot);
        }

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Kỹ năng|Skills");
            if (closeButton) closeButton.SetText(Lang.Close);
            if (equipButton) equipButton.SetText(Lang.GetSplitText("Trang bị|Equip"));
            for (int i = 0; i < slotButtons.Count; i++) slotButtons[i].SetText(SlotLabel(i));
            if (cells.Count > 0) RefreshInfo();
        }

        /// <summary>Gắn skill đang chọn vào ô đang chọn (ô Trống = gỡ ô).</summary>
        public void Equip()
        {
            if (player == null || cells.Count == 0 || selected == EquippedIndex()) return;
            // một skill chỉ nằm ở một ô: gắn sang ô mới thì gỡ khỏi ô cũ, giữ nguyên cấp (PlayerProfile.EquipSkill; online báo server)
            player.EquipSkill(slot, IdOf(cells[selected]));

            RefreshSlotLabels();
            RefreshInfo();
            RefreshCellLabels();
            RefreshEquipButton();
        }

        int EquippedIndex()
        {
            if (player == null) return -1;
            int id = player.GetSkill(slot)?.templateId ?? 0;
            for (int i = 0; i < cells.Count; i++)
                if (IdOf(cells[i]) == id) return i;
            return -1;
        }

        // ---------------- cột trái: bốn ô skill ----------------

        void BuildSlots()
        {
            if (slotRoot == null || buttonPrefab == null) return;

            for (int i = slotRoot.childCount - 1; i >= 0; i--) Destroy(slotRoot.GetChild(i).gameObject);
            slotButtons.Clear();

            float height = MaxRowHeight;
            var layout = slotRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null && slotRoot.rect.height > 0f)
            {
                int n = AnimLayout.SkillCount;
                float free = slotRoot.rect.height - layout.padding.vertical - layout.spacing * (n - 1);
                height = Mathf.Min(MaxRowHeight, free / n);
            }

            for (int i = 0; i < AnimLayout.SkillCount; i++)
            {
                int captured = i;
                TextBtn button = Instantiate(buttonPrefab, slotRoot);
                button.name = "Skill_" + (i + 1);
                button.SetText(SlotLabel(i));
                ((RectTransform)button.transform).sizeDelta = new Vector2(0f, height);
                button.AddListener(() => SelectSlot(captured));
                slotButtons.Add(button);
            }
        }

        void SelectSlot(int index)
        {
            slot = Mathf.Clamp(index, 0, AnimLayout.SkillCount - 1);
            if (slotButtons.Count == 0) BuildSlots();
            RefreshSlotLabels();
            // mở ra là trỏ sẵn vào skill đang gắn ở ô, để ảnh xem trước không trống
            SelectCell(EquippedIndex());
        }

        void RefreshSlotLabels()
        {
            for (int i = 0; i < slotButtons.Count; i++)
                if (slotButtons[i].nameTxt) slotButtons[i].nameTxt.color = i == slot ? UIColors.TextSelected : UIColors.TextNormal;
        }

        // ---------------- lưới ----------------

        void CollectCells()
        {
            cells.Clear();
            if (player == null) return;
            cells.Add(new Cell { label = Lang.GetSplitText("Trống|Empty") });

            SkillDatabase db = SkillDatabase.Current;
            if (db == null) return;
            IAnimView target = player.Anim;
            foreach (SkillTemplate t in db.skills)
            {
                if (t.kind == SkillKind.Basic) continue;   // đánh thường đi theo vũ khí, không gắn vào ô 1-4
                AnimMotion anim = target != null && target.Data != null
                    ? target.Data.GetEntry(target.AnimId, t.animSlot)?.Get(t.idAnim)?.Motion(0) : null;
                Sprite icon = db.Icon(t);
                cells.Add(new Cell { skill = t, label = t.Label, icon = icon != null ? icon : anim?.Icon, anim = anim });
            }
        }

        void BuildCells()
        {
            CollectCells();
            if (messageText)
            {
                messageText.gameObject.SetActive(cells.Count <= 1);
                messageText.text = player == null
                    ? Lang.GetSplitText("Không tìm thấy nhân vật trong scene|No character in the scene")
                    : Lang.GetSplitText("Chưa có bảng skill|No skill table");
            }
            equipped = EquippedIndex();
            if (grid) grid.ReloadData();
        }

        // ---------------- RecycleGrid ----------------

        public int GetItemCount(RecycleGrid g) => cells.Count;

        public void OnItemCreated(RecycleGrid g, RecycleGridItem item)
        {
            IconLabelCell cell = item.As<IconLabelCell>();
            if (cell != null && cell.Button) cell.Button.AddListener(() => SelectCell(item.Index));
        }

        public void BindItem(RecycleGrid g, RecycleGridItem item, int index)
        {
            IconLabelCell cell = item.As<IconLabelCell>();
            cell.Set(cells[index].label, cells[index].icon);
            cell.SetColor(index == equipped ? UIColors.TextEquipped : (index == selected ? UIColors.TextSelected : UIColors.TextNormal));
        }

        void SelectCell(int index)
        {
            selected = cells.Count > 0 && index >= 0 && index < cells.Count ? index : 0;
            if (preview) preview.Set(cells.Count > 0 ? cells[selected].anim : null);
            RefreshInfo();
            RefreshCellLabels();
            RefreshEquipButton();
        }

        // tên + "(đang gắn)", mô tả, rồi chỉ số ở cấp người chơi đang có (chưa gắn thì cấp 1)
        void RefreshInfo()
        {
            if (cells.Count == 0)
            {
                if (previewName) previewName.text = Lang.Chon;
                if (infoText) infoText.text = "";
                return;
            }

            Cell c = cells[selected];
            if (previewName)
                previewName.text = selected == EquippedIndex()
                    ? c.label + "\n" + Lang.GetSplitText("(đang gắn)|(equipped)")
                    : c.label;
            if (!infoText) return;

            info.Clear();
            if (c.skill != null)
            {
                if (!string.IsNullOrEmpty(c.skill.description)) info.Append(c.skill.description).Append("\n\n");
                SkillInfo s = c.skill.GetLevel(LevelOf(c.skill.id));
                if (s != null)
                {
                    info.Append(Lang.GetSplitText("Kiểu đòn|Type")).Append(": ").Append(SkillText.TypeLabel(c.skill.type)).Append('\n');
                    info.Append(Lang.GetSplitText("Sát thương|Damage")).Append(": ").Append(s.damage).Append("%\n");
                    info.Append(Lang.GetSplitText("Mana|Mana")).Append(": ").Append(s.manaUse).Append('\n');
                    info.Append(Lang.GetSplitText("Tầm|Range")).Append(": ");
                    if (s.range > 0f) info.Append(s.range.ToString("0.#")).Append(Lang.GetSplitText(" ô| cells"));
                    else info.Append(Lang.GetSplitText("tại chỗ|self"));   // phép tại chỗ: nổ quanh người
                    info.Append('\n');
                    info.Append(Lang.GetSplitText("Hồi|Cooldown")).Append(": ").Append(s.cooldown.ToString("0.##")).Append("s\n");
                    info.Append(Lang.GetSplitText("Mục tiêu|Targets")).Append(": ")
                        .Append(s.maxTarget > 0 ? s.maxTarget.ToString() : Lang.GetSplitText("mọi kẻ trong tầm|all in range"));
                }
            }
            infoText.text = info.ToString();
        }

        // cấp của skill nếu đang gắn ở một ô nào đó, không thì cấp 1
        int LevelOf(int id)
        {
            if (player == null) return 1;
            for (int i = 0; i < AnimLayout.SkillCount; i++)
            {
                PlayerSkill s = player.GetSkill(i);
                if (s != null && s.templateId == id) return s.level;
            }
            return 1;
        }

        void RefreshCellLabels()
        {
            equipped = EquippedIndex();
            if (grid) grid.RefreshActiveItems();
        }

        void RefreshEquipButton()
        {
            if (equipButton) equipButton.SetInteractable(cells.Count > 0 && selected != EquippedIndex());
        }
    }
}
