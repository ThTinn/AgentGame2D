using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Items;
using Game.Players;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Hai ô dùng nhanh trên HUD (<see cref="PlayerProfile.QuickSlotCount"/>): gán bình máu / mana hay thuốc tăng chỉ số (tốc đánh,
    /// tốc chạy...) rồi bấm ô hoặc phím Z / X để dùng (<see cref="PlayerProfile.UseQuickSlot"/>). Ô trống bấm thì mở bảng icon các
    /// vật phẩm dùng được trong túi để gán; giữ ô thì mở bảng đó để đổi / bỏ trống. Ô giữ id vật phẩm nên hết rồi nhặt thêm vẫn
    /// dùng tiếp. Phía trên hai ô là dòng thuốc đang có tác dụng và số giây còn lại.
    /// Dựng bằng code dưới HudRoot của GameUIManager (<see cref="Ensure"/>), nối vào <see cref="CoreSystems.Me"/>.
    /// </summary>
    public class QuickItemBar : MonoBehaviour
    {
        [Tooltip("Vị trí góc dưới phải của thanh so với góc dưới phải màn hình (đơn vị canvas 1280x720): nằm bên trái cụm nút skill.")]
        [SerializeField] Vector2 offset = new Vector2(-372f, 128f);
        [SerializeField] float slotSize = 64f;
        [SerializeField] float spacing = 12f;

        static readonly Key[] Keys = { Key.Z, Key.X };
        static readonly string[] KeyLabels = { "Z", "X" };

        // bảng chọn vật phẩm
        const int PickerColumns = 5;
        const float PickerCell = 58f, PickerGap = 10f, PickerPad = 12f, PickerTitle = 30f;
        static readonly Color PickerBg = new Color32(39, 28, 71, 240);
        static readonly Color Dim = new Color(0f, 0f, 0f, 0.3f);

        readonly List<QuickUseSlot> slots = new List<QuickUseSlot>();
        // vật phẩm của từng ô, tra một lần lúc Refresh: vòng hồi chạy mỗi frame khỏi tra lại bảng
        readonly ItemTemplate[] slotItems = new ItemTemplate[PlayerProfile.QuickSlotCount];
        readonly StringBuilder buffLine = new StringBuilder(64);
        TMP_FontAsset font;
        TMP_Text buffText;
        GameObject picker;          // bảng chọn đang mở (nền mờ + bảng icon); null = đóng
        PlayerProfile me;
        bool dirty;                 // túi / ô / thuốc vừa đổi: vẽ lại ô ở frame này (gộp nhiều sự kiện trong một frame)
        int shownBuffs = -1;        // khóa của dòng thuốc đang hiện, đổi mới dựng lại chuỗi

        /// <summary>Thêm thanh vào HUD của GameUIManager đang có (đã có thì thôi). null khi chưa có UI Core.</summary>
        public static QuickItemBar Ensure()
        {
            GameUIManager ui = GameUIManager.Instance;
            if (ui == null) return null;
            Transform root = ui.HudRoot != null ? ui.HudRoot : ui.transform;
            QuickItemBar bar = root.GetComponentInChildren<QuickItemBar>(true);
            if (bar != null) return bar;

            var go = new GameObject("QuickItemBar", typeof(RectTransform));
            go.transform.SetParent(root, false);
            bar = go.AddComponent<QuickItemBar>();
            bar.Build(FindFont(ui.transform));
            return bar;
        }

        // cùng font với chữ của UI Core (font mặc định của TMP có thể thiếu dấu tiếng Việt)
        static TMP_FontAsset FindFont(Transform root)
        {
            TMP_Text sample = root.GetComponentInChildren<TMP_Text>(true);
            return sample != null ? sample.font : null;
        }

        void Build(TMP_FontAsset textFont)
        {
            font = textFont;
            int n = PlayerProfile.QuickSlotCount;
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(n * slotSize + (n - 1) * spacing, slotSize);
            rt.anchoredPosition = offset;

            for (int i = 0; i < n; i++)
            {
                QuickUseSlot slot = QuickUseSlot.Create(transform, "QuickSlot" + (i + 1), slotSize, font, i < KeyLabels.Length ? KeyLabels[i] : "");
                var srt = (RectTransform)slot.transform;
                srt.anchorMin = srt.anchorMax = srt.pivot = Vector2.zero;
                srt.anchoredPosition = new Vector2(i * (slotSize + spacing), 0f);
                int index = i;
                slot.Tapped += () => Tap(index);
                slot.Held += () => OpenPicker(index);
                slots.Add(slot);
            }

            // dòng thuốc đang có tác dụng, ngay trên hai ô; canh phải theo mép ô và dài sang trái, không tràn sang cụm nút skill
            buffText = NewText(transform, "Buffs", "", 15f, new Color(0.75f, 1f, 0.6f), TextAlignmentOptions.BottomRight);
            RectTransform brt = buffText.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 0f);
            brt.offsetMin = new Vector2(-160f, 2f);
            brt.offsetMax = new Vector2(0f, 26f);
        }

        TextMeshProUGUI NewText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI t = UIFactory.CreateText(parent, text, size, color, align);
            t.name = name;
            if (font != null) t.font = font;
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        void OnEnable()
        {
            CoreSystems.MeChanged += Bind;
            Lang.OnChanged += MarkDirty;
            Bind(CoreSystems.Me);
        }

        void OnDisable()
        {
            CoreSystems.MeChanged -= Bind;
            Lang.OnChanged -= MarkDirty;
            Bind(null);
        }

        void OnDestroy() => ClosePicker();

        void Bind(PlayerProfile p)
        {
            if (me != null)
            {
                me.Inventory.Changed -= MarkDirty;
                me.OnQuickSlotChanged -= HandleQuickSlotChanged;
                me.OnBuffsChanged -= MarkDirty;
            }
            ClosePicker();
            me = p;
            if (me != null)
            {
                me.Inventory.Changed += MarkDirty;
                me.OnQuickSlotChanged += HandleQuickSlotChanged;
                me.OnBuffsChanged += MarkDirty;
            }
            dirty = true;
        }

        void MarkDirty() => dirty = true;

        void HandleQuickSlotChanged(int slot, int itemId) => dirty = true;

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (picker != null)
            {
                // bảng chọn đang mở: Esc đóng, Z / X không dùng đồ
                if (kb != null && kb.escapeKey.wasPressedThisFrame) ClosePicker();
            }
            else if (me != null && kb != null)
            {
                for (int i = 0; i < slots.Count && i < Keys.Length; i++)
                {
                    if (!kb[Keys[i]].wasPressedThisFrame) continue;
                    // chỉ xét UI chặn / đang gõ chữ khi thật sự có bấm (IsTyping gọi GetComponent, khỏi chạy mỗi frame)
                    if (InputGate.IsBlocked || UIManager.IsTyping()) break;
                    Tap(i);
                }
            }

            if (dirty) Refresh();
            RefreshCooldowns();
            RefreshBuffs(false);
        }

        // ---------------- bấm ----------------

        void Tap(int slot)
        {
            if (me == null) return;
            if (me.GetQuickItem(slot) <= 0) OpenPicker(slot);
            else me.UseQuickSlot(slot);
        }

        // ---------------- bảng chọn vật phẩm ----------------

        /// <summary>
        /// Bảng icon nổi phía trên thanh: mọi vật phẩm dùng được đang có trong túi (kèm số lượng), thêm ô "Bỏ" khi ô đang có
        /// vật phẩm. Bấm một icon là gán vào ô; bấm ra ngoài hoặc Esc thì đóng.
        /// </summary>
        void OpenPicker(int slot)
        {
            ClosePicker();
            if (me == null) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            canvas = canvas.rootCanvas;

            var choices = new List<ItemTemplate>();
            var seen = new HashSet<int>();
            ItemDatabase table = ItemDatabase.Current;
            foreach (ItemStack s in me.Inventory.Slots)
            {
                if (s.IsEmpty || !seen.Add(s.itemId)) continue;
                ItemTemplate item = table != null ? table.Get(s.itemId) : null;
                if (item != null && item.Usable) choices.Add(item);
            }
            int current = me.GetQuickItem(slot);
            int cells = choices.Count + (current > 0 ? 1 : 0);

            // nền mờ phủ cả màn hình, nằm trên mọi thứ của UI Core: bấm vào là đóng. Gắn vào khung Core (UIScaleRoot) để cỡ chữ /
            // ô theo cùng tỉ lệ thiết kế với thanh; không có khung thì gắn thẳng vào canvas gốc
            UIScaleRoot coreRoot = GetComponentInParent<UIScaleRoot>();
            Transform host = coreRoot != null ? coreRoot.transform : canvas.transform;
            picker = new GameObject("QuickItemPicker", typeof(RectTransform));
            picker.transform.SetParent(host, false);
            picker.transform.SetAsLastSibling();
            var dimRt = (RectTransform)picker.transform;
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
            picker.AddComponent<Image>().color = Dim;
            var close = picker.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(ClosePicker);

            int cols = Mathf.Clamp(cells, 1, PickerColumns);
            int rows = Mathf.Max(1, Mathf.CeilToInt(cells / (float)PickerColumns));
            float width = Mathf.Max(260f, cols * PickerCell + (cols - 1) * PickerGap + PickerPad * 2f);
            float height = PickerTitle + (cells > 0 ? rows * PickerCell + (rows - 1) * PickerGap : 34f) + PickerPad * 2f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(picker.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.pivot = new Vector2(1f, 0f);
            prt.sizeDelta = new Vector2(width, height);
            panel.AddComponent<Image>().color = PickerBg;   // chặn bấm xuyên xuống nền mờ
            // góc dưới phải của bảng = góc trên phải của thanh, cách lên một chút
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            prt.position = corners[2] + Vector3.up * (34f * host.lossyScale.y);

            string key = slot < KeyLabels.Length ? KeyLabels[slot] : "";
            TextMeshProUGUI title = NewText(prt, "Title",
                Lang.Format("Ô dùng nhanh {0} (phím {1})|Quick slot {0} (key {1})", slot + 1, key), 17f, new Color(1f, 0.88f, 0.54f),
                TextAlignmentOptions.Top);
            title.rectTransform.offsetMax = new Vector2(0f, -PickerPad * 0.6f);

            if (cells == 0)
            {
                TextMeshProUGUI empty = NewText(prt, "Empty",
                    Lang.GetSplitText("Hành trang chưa có bình máu, bình mana hay thuốc nào.|No potions in the inventory."), 15f,
                    Color.white, TextAlignmentOptions.Center);
                empty.rectTransform.offsetMax = new Vector2(0f, -PickerTitle);
                return;
            }

            int index = 0;
            foreach (ItemTemplate item in choices)
            {
                QuickUseSlot cell = PickerCellAt(prt, index++, item.name);
                cell.Bind(item, table.Icon(item), me.Inventory.Count(item.id));
                if (item.id == current) cell.transform.localScale = Vector3.one * 1.08f;   // món đang gán to hơn chút
                int itemId = item.id;
                cell.Tapped += () => Assign(slot, itemId);
            }
            if (current > 0)
            {
                QuickUseSlot clear = PickerCellAt(prt, index, "Clear");
                clear.BindText(Lang.GetSplitText("Bỏ|Clear"), 0.42f);
                clear.Tapped += () => Assign(slot, 0);
            }
        }

        QuickUseSlot PickerCellAt(RectTransform panel, int index, string name)
        {
            QuickUseSlot cell = QuickUseSlot.Create(panel, name, PickerCell, font, "");
            var rt = (RectTransform)cell.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            int col = index % PickerColumns, row = index / PickerColumns;
            rt.anchoredPosition = new Vector2(PickerPad + col * (PickerCell + PickerGap), -(PickerTitle + PickerPad * 0.5f + row * (PickerCell + PickerGap)));
            return cell;
        }

        void Assign(int slot, int itemId)
        {
            me?.SetQuickItem(slot, itemId);
            ClosePicker();
        }

        void ClosePicker()
        {
            if (picker != null) Destroy(picker);
            picker = null;
        }

        // ---------------- vẽ ----------------

        // Vẽ lại hai ô (icon, viền phẩm chất, số còn trong túi) và chốt vật phẩm của từng ô cho RefreshCooldowns.
        void Refresh()
        {
            dirty = false;
            ItemDatabase table = ItemDatabase.Current;
            for (int i = 0; i < slots.Count; i++)
            {
                ItemTemplate item = me != null && table != null ? table.Get(me.GetQuickItem(i)) : null;
                slotItems[i] = item;
                if (item == null) slots[i].BindEmpty();
                else slots[i].Bind(item, table.Icon(item), me.Inventory.Count(item.id));
            }
            RefreshBuffs(true);
        }

        // Vòng hồi mỗi frame: phần thời gian chờ còn lại / tổng thời gian chờ (QuickUseSlot tự bỏ qua khi số không đổi).
        void RefreshCooldowns()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ItemTemplate item = slotItems[i];
                slots[i].SetCooldown(item != null && me != null ? me.UseCooldownLeft(item) / PlayerProfile.UseCooldown : 0f);
            }
        }

        // "Thuốc cuồng nộ 42s · Thuốc gió 30s". Chạy mỗi frame nhưng chỉ dựng chuỗi (và để TMP dựng lại lưới chữ) khi khóa đổi:
        // khóa gộp id thuốc + số giây làm tròn lên, nên một giây mới dựng lại một lần. Duyệt bằng chỉ số chứ không foreach:
        // foreach trên IReadOnlyList tạo enumerator (rác GC) mỗi frame.
        void RefreshBuffs(bool force)
        {
            if (buffText == null) return;
            IReadOnlyList<PlayerProfile.ActiveBuff> buffs = me != null ? me.Buffs : null;
            int count = buffs != null ? buffs.Count : 0;
            int key = 17;
            for (int i = 0; i < count; i++) key = key * 31 + buffs[i].itemId * 1000 + Mathf.CeilToInt(buffs[i].remaining);
            if (!force && key == shownBuffs) return;
            shownBuffs = key;

            buffLine.Clear();
            ItemDatabase table = ItemDatabase.Current;
            for (int i = 0; i < count && table != null; i++)
            {
                ItemTemplate item = table.Get(buffs[i].itemId);
                if (item == null) continue;
                if (buffLine.Length > 0) buffLine.Append("  ·  ");
                buffLine.Append(item.Label).Append(' ').Append(Mathf.CeilToInt(buffs[i].remaining)).Append('s');
            }
            buffText.SetText(buffLine);   // SetText(StringBuilder): TMP chép thẳng ký tự, không tạo string mới
        }
    }
}
