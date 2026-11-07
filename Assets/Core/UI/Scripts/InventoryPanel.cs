using System.Collections.Generic;
using Game.Anim;
using Game.Core;
using Game.Items;
using Game.Players;
using Game.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bảng hành trang (panel UIID.Inventory), hai nửa:
    /// <list type="bullet">
    /// <item>Trái: hình nhân vật (ảnh động đòn đánh của vũ khí đang cầm, tay không thì đứng yên) và 8 ô trang bị xung quanh
    /// (trái: nón, áo, quần, giày; phải: vũ khí, găng, dây chuyền, nhẫn) cùng ô Skin ở giữa đáy. Bấm ô có đồ để xem / tháo.
    /// Đổi vũ khí hay skin thì hình nhân vật chạy lại anim theo màu / vũ khí mới.</item>
    /// <item>Phải: hàng tab lọc (Tất cả + mỗi <see cref="ItemKind"/> một tab, sinh lúc chạy) và lưới ô cuộn được. Tab Tất cả
    /// vẽ đủ mọi ô kể cả ô trống; tab theo loại chỉ vẽ ô có đồ loại đó. Bấm ô để xem / mặc / bỏ.
    /// Lưới là <see cref="RecycleGrid"/> (dùng chung): chỉ dựng ô cho các hàng đang nhìn thấy; cuộn thì hàng khuất được dùng lại
    /// cho hàng mới (túi lớn mấy cũng chỉ chừng ấy ô).</item>
    /// </list>
    /// Mặc/tháo đều qua <see cref="ItemInfoPopup"/> > PlayerProfile.Equip / Unequip. Panel nghe hành trang, ô trang bị và đổi vũ khí;
    /// nhiều thay đổi trong cùng một khung chỉ vẽ lại một lần ở LateUpdate.
    /// </summary>
    public class InventoryPanel : PanelBase, IRecycleGridDelegate
    {
        [Header("Hành trang")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [Tooltip("Dồn đồ lên đầu và xếp theo loại > phẩm chất.")]
        [SerializeField] TextBtn sortButton;
        [Tooltip("Hàng tab lọc (có HorizontalLayoutGroup). Tab sinh lúc chạy từ tabPrefab.")]
        [SerializeField] RectTransform tabRoot;
        [SerializeField] TextBtn tabPrefab;
        [Tooltip("Lưới cuộn tái sử dụng ô (ô mẫu là một InventorySlot, cỡ ô / số cột đặt trên RecycleGrid).")]
        [SerializeField] RecycleGrid grid;
        [Tooltip("Số ô đã dùng / tổng số ô.")]
        [SerializeField] TMP_Text capacityText;
        [Tooltip("Hiện khi không có nhân vật hoặc tab đang chọn không có món nào.")]
        [SerializeField] TMP_Text messageText;

        [Header("Trang bị")]
        [Tooltip("9 ô trang bị, phần tử i là ô EquipSlot i+1 (nón, áo, quần, giày, vũ khí, găng, dây chuyền, nhẫn, skin).")]
        [SerializeField] InventorySlot[] equipSlots = new InventorySlot[Equipment.SlotCount];
        [Tooltip("Nhãn tên ô dưới mỗi ô trang bị, cùng thứ tự với equipSlots.")]
        [SerializeField] TMP_Text[] equipLabels = new TMP_Text[Equipment.SlotCount];
        [Tooltip("Khung hình nhân vật: AnimPreview render bản sao nhân vật (đúng đồ đang mặc) vào đây.")]
        [SerializeField] Image previewImage;
        [Tooltip("Tên + cấp dưới hình nhân vật.")]
        [SerializeField] TMP_Text nameText;
        [Tooltip("Dòng vũ khí đang cầm dưới tên.")]
        [SerializeField] TMP_Text weaponText;

        const int AllKinds = -1;
        const float TabFontSize = 24f;

        // mỗi tab một bộ lọc: AllKinds hoặc (int)ItemKind
        readonly List<int> filters = new List<int>();
        readonly List<TextBtn> tabs = new List<TextBtn>();
        // chỉ số ô (trong Inventory.Slots) đang vẽ, theo thứ tự trên lưới
        readonly List<int> visible = new List<int>();

        PlayerProfile player;
        Inventory bag;
        Equipment gear;
        AnimPreview preview;
        int previewWeapon = -1;   // vũ khí và bộ anim (skin) mà hình nhân vật đang vẽ: chỉ chạy lại anim khi một trong hai đổi
        int previewSet = -1;
        int filter = AllKinds;
        bool dirty;
        int shownCount = -1;        // số ô lưới đang có: đổi thì ReloadData, giữ nguyên thì chỉ vẽ lại ô đang hiện
        bool filterChanged = true;  // đổi tab / mở panel: về đầu danh sách

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (sortButton) sortButton.AddListener(SortBag);
            if (grid) grid.Delegate = this;
            preview = AnimPreview.Attach(previewImage);
            for (int i = 0; i < equipSlots.Length; i++)
            {
                if (equipSlots[i] == null || equipSlots[i].Button == null) continue;
                var slot = (EquipSlot)(i + 1);
                equipSlots[i].Button.AddListener(() => OpenEquipped(slot));
            }
            BuildTabs();
        }

        void OnDestroy()
        {
            if (closeButton) closeButton.RemoveAllListeners();
            if (sortButton) sortButton.RemoveAllListeners();
            foreach (InventorySlot s in equipSlots)
                if (s != null && s.Button != null) s.Button.RemoveAllListeners();
            Unbind();
        }

        public override void OnShow()
        {
            base.OnShow();
            Bind(CoreSystems.Me);
            previewWeapon = -1;
            previewSet = -1;
            filterChanged = true;
            Refresh();
        }

        public override void OnClose()
        {
            Unbind();
            base.OnClose();
        }

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Hành trang|Inventory");
            if (closeButton) closeButton.SetText(Lang.Close);
            if (sortButton) sortButton.SetText(Lang.GetSplitText("Sắp xếp|Sort"));
            for (int i = 0; i < tabs.Count; i++) tabs[i].SetText(FilterLabel(filters[i]));
            for (int i = 0; i < equipLabels.Length; i++)
                if (equipLabels[i]) equipLabels[i].text = SlotLabel((EquipSlot)(i + 1));
            if (bag != null || player != null) Refresh();
        }

        /// <summary>Tên loại vật phẩm cho tab và popup.</summary>
        internal static string KindLabel(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Gem: return Lang.GetSplitText("Ngọc|Gems");
                case ItemKind.Key: return Lang.GetSplitText("Chìa khóa|Keys");
                case ItemKind.Weapon: return Lang.GetSplitText("Vũ khí|Weapons");
                case ItemKind.Armor: return Lang.GetSplitText("Đồ mặc|Gear");
                case ItemKind.Skin: return Lang.GetSplitText("Skin|Skins");
                case ItemKind.HpPotion: return Lang.GetSplitText("Bình máu|HP potion");
                case ItemKind.MpPotion: return Lang.GetSplitText("Bình mana|MP potion");
                case ItemKind.Buff: return Lang.GetSplitText("Thuốc tăng chỉ số|Buff potion");
                case ItemKind.Stone: return Lang.GetSplitText("Đá|Stones");
                case ItemKind.Jewel: return Lang.GetSplitText("Đá khảm|Gem stone");
                default: return kind.ToString();
            }
        }

        // bình máu, bình mana, thuốc tăng chỉ số chung một tab "Tiêu hao" (bộ lọc mang giá trị của HpPotion);
        // đá cường hóa và đá khảm chung tab "Đá" (bộ lọc mang giá trị của Stone)
        static int FilterOf(ItemKind kind) =>
            ItemTemplate.IsUsable(kind) ? (int)ItemKind.HpPotion : kind == ItemKind.Jewel ? (int)ItemKind.Stone : (int)kind;

        /// <summary>Tên ô trang bị cho popup.</summary>
        internal static string SlotLabel(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return Lang.GetSplitText("Nón|Head");
                case EquipSlot.Body: return Lang.GetSplitText("Áo|Body");
                case EquipSlot.Legs: return Lang.GetSplitText("Quần|Legs");
                case EquipSlot.Feet: return Lang.GetSplitText("Giày dép|Feet");
                case EquipSlot.Weapon: return Lang.GetSplitText("Vũ khí|Weapon");
                case EquipSlot.Hands: return Lang.GetSplitText("Găng tay|Hands");
                case EquipSlot.Neck: return Lang.GetSplitText("Dây chuyền|Neck");
                case EquipSlot.Ring: return Lang.GetSplitText("Nhẫn|Ring");
                case EquipSlot.Skin: return Lang.GetSplitText("Skin|Skin");
                default: return "";
            }
        }

        static string FilterLabel(int f)
        {
            if (f == AllKinds) return Lang.GetSplitText("Tất cả|All");
            if (f == (int)ItemKind.HpPotion) return Lang.GetSplitText("Tiêu hao|Potions");
            return KindLabel((ItemKind)f);
        }

        // ---------------- nguồn dữ liệu ----------------

        void Bind(PlayerProfile p)
        {
            Unbind();
            player = p;
            bag = p != null ? p.Inventory : null;
            gear = p != null ? p.Equipment : null;
            if (bag != null) bag.Changed += MarkDirty;
            if (gear != null) gear.Changed += HandleGearChanged;
            if (player != null) player.OnWeaponChanged += HandleWeaponChanged;
        }

        void Unbind()
        {
            if (bag != null) bag.Changed -= MarkDirty;
            if (gear != null) gear.Changed -= HandleGearChanged;
            if (player != null) player.OnWeaponChanged -= HandleWeaponChanged;
            bag = null;
            gear = null;
            player = null;
        }

        void MarkDirty() => dirty = true;

        void HandleGearChanged(EquipSlot _) => dirty = true;

        void HandleWeaponChanged(int _) => dirty = true;

        void LateUpdate()
        {
            if (!dirty) return;
            Refresh();
        }

        void SortBag()
        {
            player?.SortBag();
        }

        // ---------------- tab lọc ----------------

        void BuildTabs()
        {
            filters.Clear();
            filters.Add(AllKinds);
            foreach (ItemKind kind in System.Enum.GetValues(typeof(ItemKind)))
                if (!filters.Contains(FilterOf(kind))) filters.Add(FilterOf(kind));

            if (tabRoot == null || tabPrefab == null) return;
            for (int i = tabRoot.childCount - 1; i >= 0; i--) Destroy(tabRoot.GetChild(i).gameObject);
            tabs.Clear();

            foreach (int f in filters)
            {
                int captured = f;
                TextBtn tab = Instantiate(tabPrefab, tabRoot);
                tab.name = "Tab_" + (f == AllKinds ? "All" : ((ItemKind)f).ToString());
                tab.SetText(FilterLabel(f));
                if (tab.nameTxt) tab.nameTxt.fontSize = TabFontSize;
                tab.AddListener(() => SelectFilter(captured));
                tabs.Add(tab);
            }
        }

        void SelectFilter(int f)
        {
            if (filter == f) return;
            filter = f;
            filterChanged = true;
            Refresh();
        }

        void RefreshTabs()
        {
            for (int i = 0; i < tabs.Count; i++)
                tabs[i].SetColorText(filters[i] == filter ? UIColors.TextSelected : UIColors.TextNormal);
        }

        // ---------------- vẽ ----------------

        void Refresh()
        {
            dirty = false;
            ItemDatabase table = ItemDatabase.Current;
            RefreshTabs();
            RefreshGrid(table);
            RefreshEquipment(table);
            RefreshCharacter();
        }

        void RefreshGrid(ItemDatabase table)
        {
            visible.Clear();
            if (bag != null)
            {
                IReadOnlyList<ItemStack> slots = bag.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (filter == AllKinds) { visible.Add(i); continue; }
                    if (slots[i].IsEmpty) continue;
                    ItemTemplate info = table != null ? table.Get(slots[i].itemId) : null;
                    if (info != null && FilterOf(info.kind) == filter) visible.Add(i);
                }
            }

            // số ô đổi (đổi tab, thêm / bớt món khi lọc theo loại): nạp lại; không thì chỉ vẽ lại ô đang hiện
            if (grid != null)
            {
                if (filterChanged || visible.Count != shownCount)
                {
                    shownCount = visible.Count;
                    grid.ReloadData(!filterChanged);
                }
                else grid.RefreshActiveItems();
            }
            filterChanged = false;

            if (capacityText)
                capacityText.text = bag != null
                    ? Lang.GetSplitText("Ô|Slots") + ": " + NumberCache.Get(bag.UsedSlots) + " / " + NumberCache.Get(bag.Capacity)
                    : "";
            if (sortButton) sortButton.SetInteractable(bag != null && bag.UsedSlots > 0);

            if (messageText)
            {
                bool empty = bag == null || visible.Count == 0;
                messageText.gameObject.SetActive(empty);
                if (empty)
                    messageText.text = bag == null
                        ? Lang.GetSplitText("Không tìm thấy nhân vật trong scene|No character in the scene")
                        : Lang.GetSplitText("Chưa có vật phẩm loại này|No items of this kind");
            }
        }

        void RefreshEquipment(ItemDatabase table)
        {
            for (int i = 0; i < equipSlots.Length; i++)
            {
                InventorySlot cell = equipSlots[i];
                if (cell == null) continue;

                var slot = (EquipSlot)(i + 1);
                int id = gear != null ? gear[slot] : 0;
                ItemTemplate info = id > 0 && table != null ? table.Get(id) : null;
                if (id <= 0) cell.BindEmpty((int)slot);
                else cell.Bind((int)slot, info, table != null ? table.Icon(info) : null, 1, gear.Level(slot));
            }
        }

        /// <summary>Tên, cấp, vũ khí đang cầm, và hình nhân vật (chỉ chạy lại anim khi đổi vũ khí).</summary>
        void RefreshCharacter()
        {
            if (nameText)
                nameText.text = player != null
                    ? player.DisplayName + "  <size=80%>Lv " + NumberCache.Get(player.Progress.Level) + "</size>"
                    : "";

            WeaponTemplate weapon = player != null && WeaponDatabase.Current != null ? WeaponDatabase.Current.Get(player.WeaponId) : null;
            if (weaponText)
                weaponText.text = player == null ? ""
                    : Lang.GetSplitText("Vũ khí|Weapon") + ": "
                      + (weapon != null ? weapon.Label : Lang.GetSplitText("tay không|bare hands"))
                      // máu tối đa và giáp đang có: mặc / tháo đồ là thấy đổi ngay
                      + "\n" + PlayerStatText.Label(PlayerStat.MaxHp) + " " + (player.data != null ? NumberCache.Get(player.data.maxHp) : "?")
                      // cột giữa hẹp: mỗi chỉ số một dòng, không để dấu "·" treo ở cuối dòng khi xuống dòng
                      + "\n" + PlayerStatText.Label(PlayerStat.Defense) + " "
                      + (player.Bonus.DefenseCut * 100f).ToString("0.#") + "%";

            int weaponId = weapon != null ? weapon.id : 0;
            int setId = player != null && player.Anim != null ? player.Anim.AnimId : 0;
            if (preview == null || (weaponId == previewWeapon && setId == previewSet)) return;
            previewWeapon = weaponId;
            previewSet = setId;
            preview.Set(PreviewMotion(weapon));
        }

        /// <summary>Đòn đánh thường của vũ khí đang cầm; tay không thì tư thế đứng yên đang mặc.</summary>
        AnimMotion PreviewMotion(WeaponTemplate weapon)
        {
            IAnimView ca = player != null ? player.Anim : null;
            if (ca == null || ca.Data == null) return null;

            SkillTemplate skill = weapon != null && SkillDatabase.Current != null ? SkillDatabase.Current.Get(weapon.basicSkill) : null;
            AnimMotion attack = skill != null ? ca.Data.GetEntry(ca.AnimId, skill.animSlot)?.Get(skill.idAnim)?.Motion(0) : null;
            if (attack != null) return attack;
            return ca.Data.GetEntry(ca.AnimId, AnimSlot.Idle)?.Get(ca.GetVariant(AnimSlot.Idle, 0))?.Motion(0);
        }

        // ---------------- lưới (RecycleGrid) ----------------

        public int GetItemCount(RecycleGrid g) => bag != null ? visible.Count : 0;

        public void OnItemCreated(RecycleGrid g, RecycleGridItem item)
        {
            InventorySlot cell = item.As<InventorySlot>();
            if (cell != null && cell.Button) cell.Button.AddListener(() => OpenInfo(cell.SlotIndex));
        }

        /// <summary>Ô thứ index của lưới = ô visible[index] của túi (tab Tất cả: mọi ô; tab theo loại: ô có đồ loại đó).</summary>
        public void BindItem(RecycleGrid g, RecycleGridItem item, int index)
        {
            InventorySlot cell = item.As<InventorySlot>();
            int slot = visible[index];
            ItemStack st = bag.Slots[slot];
            if (st.IsEmpty)
            {
                cell.BindEmpty(slot);
                return;
            }
            ItemDatabase table = ItemDatabase.Current;
            ItemTemplate info = table != null ? table.Get(st.itemId) : null;
            cell.Bind(slot, info, table != null ? table.Icon(info) : null, st.count, st.level);
        }

        void OpenInfo(int slot)
        {
            if (bag == null || bag[slot].IsEmpty) return;
            PlayerProfile p = player;
            PopupMN.Open<ItemInfoPopup>(PopupID.ItemInfo, popup => popup.Open(p, slot));
        }

        void OpenEquipped(EquipSlot slot)
        {
            if (gear == null || gear[slot] <= 0) return;
            PlayerProfile p = player;
            PopupMN.Open<ItemInfoPopup>(PopupID.ItemInfo, popup => popup.OpenEquipped(p, slot));
        }
    }
}
