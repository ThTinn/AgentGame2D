using System.Collections.Generic;
using System.Text;
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
    /// Thông tin một vật phẩm (popup PopupID.ItemInfo), mở từ <see cref="InventoryPanel"/>, cho một ô hành trang
    /// (<see cref="Open"/>) hoặc một ô trang bị (<see cref="OpenEquipped"/>):
    /// icon trong khung màu phẩm chất, tên, loại · phẩm chất, số lượng, mô tả và phần riêng theo loại
    /// (ngọc: màu; chìa: mở rương nào; đồ mặc: ô nào; vũ khí: chỉ số đòn đánh thường + nâng cấp; bình: máu / mana hồi;
    /// thuốc: số giây tác dụng).
    /// Nút (hàng nút tự co lại khi ẩn bớt): Trang bị / Tháo (<see cref="PlayerProfile.Equip"/> / <see cref="PlayerProfile.Unequip"/>)
    /// hoặc Dùng (<see cref="PlayerProfile.UseItem"/>), Nâng cấp (vũ khí, mở cây nâng cấp) hoặc Gán ô nhanh (vật phẩm dùng
    /// được), Cường hóa (vũ khí, đồ mặc: mở <see cref="GearUpgradePanel"/>), Bỏ (chỉ ô hành trang), Đóng.
    /// Popup đọc lại mỗi khi hành trang / ô trang bị đổi; món nó đang xem rời chỗ (mặc lên, tháo ra, bỏ hết) thì tự đóng.
    /// </summary>
    public class ItemInfoPopup : PopupBase
    {
        [SerializeField] Image border;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [Tooltip("Dòng loại · phẩm chất.")]
        [SerializeField] TMP_Text typeText;
        [Tooltip("Số lượng, mô tả và phần riêng theo loại.")]
        [SerializeField] TMP_Text infoText;
        [Tooltip("Trang bị (món trong túi) hoặc Tháo (món đang mặc).")]
        [SerializeField] TextBtn equipButton;
        [Tooltip("Vũ khí: mở cây nâng cấp.")]
        [SerializeField] TextBtn upgradeButton;
        [Tooltip("Vũ khí, đồ mặc: mở bảng cường hóa +N.")]
        [SerializeField] TextBtn gearButton;
        [SerializeField] TextBtn discardButton;
        [SerializeField] TextBtn closeButton;

        readonly StringBuilder info = new StringBuilder(256);
        readonly List<PlayerStatEffect> effects = new List<PlayerStatEffect>();

        PlayerProfile player;
        Inventory bag;
        Equipment gear;
        // đang xem ô hành trang (slot >= 0) hay ô trang bị (equipSlot != None)
        int slot = -1;
        EquipSlot equipSlot;
        int itemId;
        ItemTemplate item;

        bool FromGear => equipSlot != EquipSlot.None;

        public override void OnInit()
        {
            if (equipButton) equipButton.AddListener(EquipOrUnequip);
            if (upgradeButton) upgradeButton.AddListener(OpenUpgrades);
            if (gearButton) gearButton.AddListener(OpenGearUpgrade);
            if (discardButton) discardButton.AddListener(Discard);
            if (closeButton) closeButton.AddListener(Close);
        }

        void OnDestroy()
        {
            if (equipButton) equipButton.RemoveAllListeners();
            if (upgradeButton) upgradeButton.RemoveAllListeners();
            if (gearButton) gearButton.RemoveAllListeners();
            if (discardButton) discardButton.RemoveAllListeners();
            if (closeButton) closeButton.RemoveAllListeners();
            Unbind();
        }

        public override void OnClose()
        {
            Unbind();
            base.OnClose();
        }

        /// <summary>Xem ô <paramref name="bagSlot"/> trong hành trang của <paramref name="owner"/>.</summary>
        public void Open(PlayerProfile owner, int bagSlot)
        {
            Bind(owner);
            slot = bagSlot;
            equipSlot = EquipSlot.None;
            itemId = bag != null ? bag[slot].itemId : 0;
            Refresh();
        }

        /// <summary>Xem món đang mặc ở ô trang bị <paramref name="at"/>.</summary>
        public void OpenEquipped(PlayerProfile owner, EquipSlot at)
        {
            Bind(owner);
            slot = -1;
            equipSlot = at;
            itemId = gear != null ? gear[at] : 0;
            Refresh();
        }

        void Bind(PlayerProfile owner)
        {
            Unbind();
            player = owner;
            bag = owner != null ? owner.Inventory : null;
            gear = owner != null ? owner.Equipment : null;
            if (bag != null) bag.Changed += Refresh;
            if (gear != null) gear.Changed += HandleGearChanged;
        }

        void Unbind()
        {
            if (bag != null) bag.Changed -= Refresh;
            if (gear != null) gear.Changed -= HandleGearChanged;
            bag = null;
            gear = null;
            player = null;
        }

        void HandleGearChanged(EquipSlot _) => Refresh();

        /// <summary>Chỗ của món đang xem, theo quy ước của <see cref="PlayerProfile.GearAt"/>.</summary>
        int Spot => FromGear ? PlayerProfile.GearSpot(equipSlot) : slot;

        /// <summary>Số cái của món đang xem ở chỗ của nó; 0 khi món đã rời chỗ.</summary>
        int CurrentCount()
        {
            if (FromGear) return gear != null && gear[equipSlot] == itemId && itemId > 0 ? 1 : 0;
            ItemStack s = bag != null ? bag[slot] : default;
            return !s.IsEmpty && s.itemId == itemId ? s.count : 0;
        }

        void Refresh()
        {
            int count = CurrentCount();
            // mặc lên / tháo ra / bỏ hết / sắp xếp làm món khác chiếm chỗ: popup này không còn đúng nữa
            if (count <= 0)
            {
                Close();
                return;
            }

            ItemDatabase table = ItemDatabase.Current;
            item = table != null ? table.Get(itemId) : null;
            Color tint = item != null ? ItemTemplate.RarityTint(item.rarity) : Color.gray;
            int level = player != null ? player.GearAt(Spot).level : 0;

            if (border) border.color = tint;
            if (iconImage)
            {
                Sprite sprite = table != null ? table.Icon(item) : null;
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }
            if (nameText)
            {
                nameText.text = (item != null ? item.Label : "#" + itemId) + (level > 0 ? " +" + level : "");
                nameText.color = tint;
            }
            if (typeText)
            {
                typeText.text = item != null ? InventoryPanel.KindLabel(item.kind) + "  ·  " + RarityLabel(item.rarity) : "";
                if (FromGear) typeText.text += "  ·  " + Lang.GetSplitText("Đang mặc|Equipped");
            }

            if (infoText)
            {
                info.Clear();
                AppendBody(count, level);
                infoText.text = info.ToString();
            }

            bool equippable = item != null && item.Equippable;
            // vật phẩm dùng được (bình máu / mana, thuốc): nút Trang bị thành Dùng, nút Nâng cấp thành Gán ô dùng nhanh
            bool usable = item != null && item.Usable && !FromGear;
            if (equipButton)
            {
                equipButton.SetActive(equippable || usable);
                if (usable) equipButton.SetText(Lang.GetSplitText("Dùng|Use"));
                else if (equippable)
                    equipButton.SetText(FromGear ? Lang.GetSplitText("Tháo|Unequip") : Lang.GetSplitText("Trang bị|Equip"));
                equipButton.SetInteractable(player != null);
            }
            if (upgradeButton)
            {
                upgradeButton.SetActive(Weapon() != null || usable);
                upgradeButton.SetText(usable ? Lang.GetSplitText("Gán ô nhanh|Quick slot") : Lang.GetSplitText("Nâng cấp|Upgrade"));
            }
            if (gearButton)
            {
                gearButton.SetActive(item != null && item.Upgradable && GearUpgradeTable.Current != null);
                gearButton.SetText(Lang.GetSplitText("Cường hóa|Enhance"));
            }
            if (discardButton)
            {
                discardButton.SetActive(!FromGear);   // đồ đang mặc phải tháo ra rồi mới bỏ
                discardButton.SetText(Lang.GetSplitText("Bỏ|Discard"));
            }
            if (closeButton) closeButton.SetText(Lang.Close);
        }

        WeaponTemplate Weapon() =>
            item != null && item.kind == ItemKind.Weapon && WeaponDatabase.Current != null ? WeaponDatabase.Current.Get(item.WeaponId) : null;

        void AppendBody(int count, int level)
        {
            if (item == null || item.Stackable)
            {
                info.Append(Lang.GetSplitText("Số lượng|Amount")).Append(": ").Append(NumberCache.Get(count));
                if (item != null && item.maxStack > 1) info.Append(" / ").Append(NumberCache.Get(item.maxStack));
                info.Append('\n');
            }
            if (item == null) return;

            switch (item.kind)
            {
                case ItemKind.Gem:
                    info.Append(Lang.GetSplitText("Màu|Color")).Append(": ").Append(ItemTemplate.ColorName(item.GemColor)).Append('\n');
                    break;
                case ItemKind.Key:
                    info.Append(Lang.GetSplitText("Mở được|Opens")).Append(": ").Append(ItemTemplate.ChestName(item.KeyChest)).Append('\n');
                    break;
                case ItemKind.Armor:
                    info.Append(Lang.GetSplitText("Ô trang bị|Slot")).Append(": ").Append(InventoryPanel.SlotLabel(item.Slot)).Append('\n');
                    break;
                case ItemKind.Buff:
                    info.Append(Lang.Format("Tác dụng trong {0} giây|Lasts {0} seconds", Mathf.RoundToInt(item.BuffSeconds))).Append('\n');
                    break;
            }

            GearUpgradeTable.EffectsOf(item, level, effects);
            if (effects.Count > 0)
            {
                // option của đồ theo cấp cường hóa: cùng tên / đơn vị với bảng cây nâng cấp (OptionDatabase)
                string open = "<color=" + UIColors.Hex(UIColors.StatusGood) + ">";
                foreach (PlayerStatEffect e in effects)
                    info.Append(open).Append(PlayerStatText.Effect(e.stat, e.value)).Append("</color>\n");
            }
            AppendSockets();

            if (!string.IsNullOrEmpty(item.description)) info.Append('\n').Append(item.description).Append('\n');

            if (item.kind == ItemKind.Weapon)
            {
                WeaponTemplate w = Weapon();
                SkillTemplate skill = w != null && SkillDatabase.Current != null ? SkillDatabase.Current.Get(w.basicSkill) : null;
                if (w != null)
                {
                    info.Append('\n');
                    SkillText.AppendWeaponStats(info, w, skill, player);
                }
                else info.Append('\n').Append(Lang.GetSplitText("Không có trong bảng vũ khí|Not in the weapon table"));
            }
        }

        // lỗ của món: đá đã khảm kèm chỉ số, lỗ trống ghi rõ
        void AppendSockets()
        {
            ItemStack stack = player != null ? player.GearAt(Spot) : default;
            ItemDatabase table = ItemDatabase.Current;
            for (int i = 0; i < stack.sockets; i++)
            {
                ItemTemplate gem = table != null && stack.Gem(i) > 0 ? table.Get(stack.Gem(i)) : null;
                info.Append(Lang.Format("Lỗ {0}|Socket {0}", i + 1)).Append(": ");
                if (gem == null)
                {
                    info.Append(Lang.GetSplitText("trống|empty")).Append('\n');
                    continue;
                }
                info.Append(gem.Label);
                foreach (PlayerStatEffect e in gem.options)
                    info.Append("  <color=").Append(UIColors.Hex(UIColors.StatusGood)).Append('>')
                        .Append(PlayerStatText.Effect(e.stat, e.value)).Append("</color>");
                info.Append('\n');
            }
        }

        static string RarityLabel(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Uncommon: return Lang.GetSplitText("Hiếm|Uncommon");
                case ItemRarity.Rare: return Lang.GetSplitText("Quý|Rare");
                case ItemRarity.Epic: return Lang.GetSplitText("Sử thi|Epic");
                case ItemRarity.Legendary: return Lang.GetSplitText("Huyền thoại|Legendary");
                default: return Lang.GetSplitText("Thường|Common");
            }
        }

        /// <summary>
        /// Mặc (món trong túi) hoặc tháo (món đang mặc); vật phẩm dùng được thì dùng một cái. Thành công thì món rời chỗ (hết
        /// thì popup tự đóng).
        /// </summary>
        void EquipOrUnequip()
        {
            if (player == null || item == null) return;
            if (item.Usable && !FromGear)
            {
                player.UseItem(item.id);
                return;
            }
            if (!item.Equippable) return;
            if (FromGear) player.Unequip(equipSlot);
            else player.Equip(slot);
        }

        /// <summary>
        /// Vũ khí: mở cây nâng cấp. Đóng popup trước: panel nằm dưới lớp popup về phím Esc.
        /// Vật phẩm dùng được: chọn ô dùng nhanh để gán (<see cref="QuickItemBar"/>).
        /// </summary>
        void OpenUpgrades()
        {
            if (item != null && item.Usable && !FromGear)
            {
                ChooseQuickSlot();
                return;
            }
            WeaponTemplate w = Weapon();
            if (w == null || UIManager.Instance == null) return;
            Close();
            UIManager.Instance.Show<WeaponUpgradePanel>(UIID.WeaponUpgrade, p => p.Open(w));
        }

        /// <summary>Mở bảng cường hóa cho món đang xem. Đóng popup trước: panel nằm dưới lớp popup.</summary>
        void OpenGearUpgrade()
        {
            if (player == null || UIManager.Instance == null) return;
            PlayerProfile owner = player;
            int spot = Spot;
            Close();
            UIManager.Instance.Show<GearUpgradePanel>(UIID.GearUpgrade, p => p.Open(owner, spot));
        }

        void ChooseQuickSlot()
        {
            PlayerProfile owner = player;
            int id = item.id;
            var buttons = new List<BtnData>();
            for (int i = 0; i < PlayerProfile.QuickSlotCount; i++)
            {
                int slot = i;
                ItemTemplate cur = ItemDatabase.Current != null ? ItemDatabase.Current.Get(owner.GetQuickItem(i)) : null;
                string label = Lang.Format("Ô {0}|Slot {0}", i + 1) + (cur != null ? "  (" + cur.Label + ")" : "");
                buttons.Add(new BtnData((short)i, label, () => owner.SetQuickItem(slot, id)));
            }
            buttons.Add(new BtnData((short)buttons.Count, Lang.Close));
            string question = Lang.Format("Gán {0} vào ô dùng nhanh nào?|Put {0} into which quick slot?", item.Label);
            Close();
            PopupMN.Choices(question, buttons);
        }

        void Discard()
        {
            if (bag == null || FromGear) return;
            ItemStack s = bag[slot];
            if (s.IsEmpty) return;

            PlayerProfile owner = player;   // qua PlayerProfile.Discard: online thì báo server
            int at = slot;
            string label = item != null ? item.Label : "#" + s.itemId;
            if (s.count > 1)
                PopupMN.InputNumber(Lang.Format("Bỏ bao nhiêu {0}?|Discard how many {0}?", label),
                    s.count, n => owner.Discard(at, n), 1, s.count);
            else
                PopupMN.Confirm(Lang.Format("Bỏ {0}?|Discard {0}?", label), () => owner.Discard(at, 1));
        }
    }
}
