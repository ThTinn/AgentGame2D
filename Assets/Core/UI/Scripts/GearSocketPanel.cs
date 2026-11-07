using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Items;
using Game.Players;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Khoét lỗ và khảm đá (panel UIID.GearSocket, mở từ <see cref="GearUpgradePanel"/> qua <see cref="Open"/>). Trái: icon, tên,
    /// kết quả. Phải: mỗi lỗ một hàng, nút tùy trạng thái: Khoét lỗ (lỗ kế tiếp), Khảm (lỗ trống, chọn đá trong danh sách cuộn
    /// vì túi có thể có tới 18 loại), Gỡ (lỗ có đá). Khoét lỗ online thì chờ server (<see cref="PlayerProfile.OnSocketOpened"/>),
    /// khảm và gỡ thì làm ngay như các lệnh hồ sơ khác. Dựng prefab: Tools/Core/UI/Build Gear Upgrade UI.
    /// </summary>
    public class GearSocketPanel : PanelBase
    {
        [Header("Khoét lỗ, khảm đá")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [Tooltip("Quay về bảng cường hóa của cùng món.")]
        [SerializeField] TextBtn upgradeTabButton;
        [SerializeField] Image border;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [Tooltip("Kết quả lần vừa rồi.")]
        [SerializeField] TMP_Text resultText;
        [Header("Mỗi lỗ một hàng, cùng thứ tự")]
        [SerializeField] Image[] gemIcons = new Image[ItemStack.MaxSockets];
        [SerializeField] TMP_Text[] gemTexts = new TMP_Text[ItemStack.MaxSockets];
        [SerializeField] TextBtn[] gemButtons = new TextBtn[ItemStack.MaxSockets];
        [Header("Danh sách chọn đá")]
        [SerializeField] GameObject pickerRoot;
        [SerializeField] TMP_Text pickerTitle;
        [SerializeField] TextBtn pickerCancel;
        [SerializeField] ScrollRect pickerScroll;
        [Tooltip("Hàng mẫu, tắt sẵn; mỗi loại đá một bản sao.")]
        [SerializeField] TextBtn pickerRow;

        readonly StringBuilder text = new StringBuilder(128);
        readonly List<TextBtn> pickerRows = new List<TextBtn>();

        PlayerProfile player;
        int spot;
        int itemId;
        bool waiting;   // online: đã gửi lệnh khoét lỗ, chờ kết quả

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (upgradeTabButton) upgradeTabButton.AddListener(OpenUpgrade);
            if (pickerCancel) pickerCancel.AddListener(HidePicker);
            for (int i = 0; i < gemButtons.Length; i++)
            {
                int index = i;
                if (gemButtons[i]) gemButtons[i].AddListener(() => Press(index));
            }
        }

        void OnDestroy()
        {
            if (closeButton) closeButton.RemoveAllListeners();
            if (upgradeTabButton) upgradeTabButton.RemoveAllListeners();
            if (pickerCancel) pickerCancel.RemoveAllListeners();
            foreach (TextBtn b in gemButtons) if (b) b.RemoveAllListeners();
            foreach (TextBtn b in pickerRows) if (b) b.RemoveAllListeners();
            Unbind();
        }

        public override void OnClose()
        {
            HidePicker();
            Unbind();
            base.OnClose();
        }

        public override void OnKey(Key key)
        {
            if (key == Key.Escape && pickerRoot && pickerRoot.activeSelf) HidePicker();
            else base.OnKey(key);
        }

        /// <summary>Khoét lỗ, khảm đá cho món ở <paramref name="at"/> (quy ước của <see cref="PlayerProfile.GearAt"/>).</summary>
        public void Open(PlayerProfile owner, int at)
        {
            Unbind();
            player = owner;
            spot = at;
            itemId = owner != null ? owner.GearAt(at).itemId : 0;
            waiting = false;
            if (resultText) resultText.text = "";
            HidePicker();
            if (player != null)
            {
                player.Inventory.Changed += Refresh;
                player.Equipment.Changed += HandleGearChanged;
                player.OnGoldChanged += HandleGoldChanged;
                player.OnSocketOpened += HandleResult;
            }
            Refresh();
        }

        void Unbind()
        {
            if (player == null) return;
            player.Inventory.Changed -= Refresh;
            player.Equipment.Changed -= HandleGearChanged;
            player.OnGoldChanged -= HandleGoldChanged;
            player.OnSocketOpened -= HandleResult;
            player = null;
        }

        void HandleGearChanged(EquipSlot _) => Refresh();

        void HandleGoldChanged(long _) => Refresh();

        void OpenUpgrade()
        {
            if (player == null || UIManager.Instance == null) return;
            PlayerProfile owner = player;
            int at = spot;
            Close();
            UIManager.Instance.Show<GearUpgradePanel>(UIID.GearUpgrade, p => p.Open(owner, at));
        }

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Khoét lỗ, khảm đá|Sockets and gems");
            if (closeButton) closeButton.SetText("X");
            if (upgradeTabButton) upgradeTabButton.SetText(Lang.GetSplitText("Cường hóa|Enhance"));
            if (pickerCancel) pickerCancel.SetText("X");
            if (player != null) Refresh();
        }

        // ---------------- vẽ ----------------

        void Refresh()
        {
            ItemDatabase table = ItemDatabase.Current;
            GearUpgradeTable rules = GearUpgradeTable.Current;
            ItemStack gear = player != null ? player.GearAt(spot) : default;
            // món rời chỗ (sắp xếp, đổi đồ từ server): bảng này không còn đúng nữa
            if (gear.IsEmpty || gear.itemId != itemId || table == null || rules == null)
            {
                Close();
                return;
            }

            ItemTemplate item = table.Get(itemId);
            Color tint = item != null ? ItemTemplate.RarityTint(item.rarity) : Color.gray;
            if (border) border.color = tint;
            if (iconImage)
            {
                Sprite sprite = table.Icon(item);
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }
            if (nameText)
            {
                nameText.text = (item != null ? item.Label : "#" + itemId) + (gear.level > 0 ? " +" + gear.level : "");
                nameText.color = tint;
            }

            for (int i = 0; i < gemTexts.Length; i++) RefreshRow(i, gear, table, rules);
        }

        void RefreshRow(int i, in ItemStack gear, ItemDatabase table, GearUpgradeTable rules)
        {
            TextBtn button = gemButtons[i];
            ItemTemplate gem = i < gear.sockets && gear.Gem(i) > 0 ? table.Get(gear.Gem(i)) : null;
            if (gemIcons[i])
            {
                Sprite sprite = gem != null ? table.Icon(gem) : null;
                gemIcons[i].sprite = sprite;
                gemIcons[i].enabled = sprite != null;
            }

            text.Clear();
            bool show = true, enabled = true;
            string label = "";
            if (gem != null)
            {
                text.Append(gem.Label);
                foreach (PlayerStatEffect e in gem.options) text.Append('\n').Append(PlayerStatText.Effect(e.stat, e.value));
                label = Lang.Format("Gỡ ({0} vàng)|Remove ({0} gold)", rules.removeGemGold);
                enabled = player.CheckRemoveGem(spot, i) == GearBlock.None;
            }
            else if (i < gear.sockets)
            {
                text.Append(Lang.Format("Lỗ {0}: trống|Socket {0}: empty", i + 1));
                label = Lang.GetSplitText("Khảm|Inlay");
                enabled = HasAnyGem();
            }
            else if (i == gear.sockets && rules.SocketStep(i) != null)
            {
                GearSocketStep step = rules.SocketStep(i);
                ItemTemplate drill = table.FindStone(GearStone.Drill);
                int have = drill != null ? player.Inventory.Count(drill.id) : 0;
                text.Append(Lang.Format("Khoét lỗ {0}: tỉ lệ {1}%|Socket {0}: {1}% chance", i + 1, step.chance.ToString("0.#"))).Append('\n');
                AppendNeed(drill != null ? drill.Label : Lang.GetSplitText("Đá khoét lỗ|Drill stone"), have, step.stones);
                text.Append(", ");
                AppendNeed(Lang.GetSplitText("Vàng|Gold"), player.Gold, step.gold);
                label = Lang.GetSplitText("Khoét lỗ|Drill");
                enabled = !waiting && player.CheckOpenSocket(spot) == GearBlock.None;
            }
            else
            {
                text.Append(Lang.Format("Lỗ {0}: chưa khoét|Socket {0}: not drilled", i + 1));
                show = false;
            }

            if (gemTexts[i]) gemTexts[i].text = text.ToString();
            if (button == null) return;
            button.SetActive(show);
            button.SetText(label);
            button.SetInteractable(enabled);
        }

        void AppendNeed(string label, long have, long need)
        {
            Color color = have >= need ? UIColors.TextNormal : UIColors.StatusBad;
            text.Append(label).Append(": <color=").Append(UIColors.Hex(color)).Append('>').Append(have).Append("</color> / ").Append(need);
        }

        bool HasAnyGem()
        {
            ItemDatabase table = ItemDatabase.Current;
            foreach (KeyValuePair<int, int> pair in player.Inventory.All)
            {
                ItemTemplate it = table.Get(pair.Key);
                if (it != null && it.kind == ItemKind.Jewel) return true;
            }
            return false;
        }

        // ---------------- thao tác ----------------

        void Press(int index)
        {
            if (player == null) return;
            ItemStack gear = player.GearAt(spot);
            if (index < gear.sockets)
            {
                if (gear.Gem(index) > 0) ConfirmRemove(index);
                else ChooseGem(index);
            }
            else if (index == gear.sockets) Drill();
        }

        void Drill()
        {
            if (waiting) return;
            GearOutcome outcome = player.OpenSocket(spot);
            waiting = outcome == GearOutcome.Pending;
            if (outcome == GearOutcome.Refused) ShowResult(Lang.GetSplitText("Chưa đủ điều kiện|Requirements not met"), UIColors.StatusBad);
            Refresh();
        }

        void ConfirmRemove(int index)
        {
            long gold = GearUpgradeTable.Current != null ? GearUpgradeTable.Current.removeGemGold : 0;
            PopupMN.Confirm(Lang.Format("Gỡ đá ở lỗ {0}, mất {1} vàng?|Remove the gem from socket {0} for {1} gold?", index + 1, gold), () =>
            {
                if (player != null && !player.RemoveGem(spot, index))
                    ShowResult(Lang.GetSplitText("Không gỡ được|Could not remove"), UIColors.StatusBad);
            });
        }

        // danh sách đá đang có trong túi, mỗi loại một hàng; bậc cao lên trên
        void ChooseGem(int index)
        {
            if (pickerRoot == null || pickerRow == null) return;
            ItemDatabase table = ItemDatabase.Current;
            var gems = new List<ItemTemplate>();
            foreach (KeyValuePair<int, int> pair in player.Inventory.All)
            {
                ItemTemplate it = table.Get(pair.Key);
                if (it != null && it.kind == ItemKind.Jewel) gems.Add(it);
            }
            gems.Sort((a, b) => a.value != b.value ? b.value.CompareTo(a.value) : a.id.CompareTo(b.id));

            for (int i = 0; i < gems.Count; i++)
            {
                if (i == pickerRows.Count) pickerRows.Add(Instantiate(pickerRow, pickerRow.transform.parent));
                ItemTemplate it = gems[i];
                int gemId = it.id;
                text.Clear();
                text.Append(it.Label).Append(" x").Append(player.Inventory.Count(gemId));
                foreach (PlayerStatEffect e in it.options) text.Append("   ").Append(PlayerStatText.Effect(e.stat, e.value));
                bool same = player.CheckInlay(spot, index, gemId) == GearBlock.SameJewel;
                if (same) text.Append("   ").Append(Lang.GetSplitText("(đã khảm loại này)|(already inlaid)"));

                TextBtn row = pickerRows[i];
                row.gameObject.SetActive(true);
                row.SetText(text.ToString());
                row.SetInteractable(!same);
                row.RemoveAllListeners();
                row.AddListener(() =>
                {
                    HidePicker();
                    Inlay(index, gemId);
                });
            }
            for (int i = gems.Count; i < pickerRows.Count; i++) pickerRows[i].gameObject.SetActive(false);

            if (pickerTitle) pickerTitle.text = Lang.Format("Khảm đá nào vào lỗ {0}?|Inlay which gem into socket {0}?", index + 1);
            pickerRoot.SetActive(true);
            if (pickerScroll) pickerScroll.verticalNormalizedPosition = 1f;
        }

        void HidePicker()
        {
            if (pickerRoot) pickerRoot.SetActive(false);
        }

        void Inlay(int index, int gemId)
        {
            if (player != null && !player.InlayGem(spot, index, gemId))
                ShowResult(Lang.GetSplitText("Không khảm được|Could not inlay"), UIColors.StatusBad);
        }

        void HandleResult(GearOutcome outcome, int sockets)
        {
            waiting = false;
            switch (outcome)
            {
                case GearOutcome.Success:
                    ShowResult(Lang.Format("Thành công: {0} lỗ|Success: {0} sockets", sockets), UIColors.StatusGood);
                    break;
                case GearOutcome.Failed:
                    ShowResult(Lang.GetSplitText("Thất bại, mất đá và vàng|Failed, stones and gold lost"), UIColors.StatusBad);
                    break;
                case GearOutcome.Refused:
                    ShowResult(Lang.GetSplitText("Chưa đủ điều kiện|Requirements not met"), UIColors.StatusBad);
                    break;
            }
            Refresh();
        }

        void ShowResult(string message, Color color)
        {
            if (resultText == null) return;
            resultText.text = message;
            resultText.color = color;
        }
    }
}
