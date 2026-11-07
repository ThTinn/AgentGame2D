using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Items;
using Game.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Cường hóa trang bị +N (panel UIID.GearUpgrade, mở từ <see cref="ItemInfoPopup"/> qua <see cref="Open"/>). Trái: icon,
    /// tên + cấp. Phải: chỉ số hiện tại > cấp sau, tỉ lệ, đá và vàng cần, hai nút bật đá may mắn / đá bảo vệ, nút Cường hóa.
    /// Áp qua <see cref="PlayerProfile.UpgradeGear"/>: online thì chờ server, trong lúc chờ khóa nút. Kết quả về qua
    /// <see cref="PlayerProfile.OnGearUpgraded"/>. Dựng prefab: Tools/Core/UI/Build Gear Upgrade UI.
    /// </summary>
    public class GearUpgradePanel : PanelBase
    {
        [Header("Cường hóa trang bị")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [SerializeField] Image border;
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text nameText;
        [Tooltip("Chỉ số hiện tại > cấp sau.")]
        [SerializeField] TMP_Text statsText;
        [Tooltip("Tỉ lệ, đá và vàng cần cho lần này.")]
        [SerializeField] TMP_Text costText;
        [SerializeField] TextBtn luckyButton;
        [SerializeField] TextBtn protectButton;
        [SerializeField] TextBtn upgradeButton;
        [Tooltip("Kết quả lần vừa rồi.")]
        [SerializeField] TMP_Text resultText;
        [Tooltip("Chuyển sang bảng khoét lỗ, khảm đá của cùng món.")]
        [SerializeField] TextBtn socketTabButton;

        readonly StringBuilder text = new StringBuilder(256);
        readonly List<PlayerStatEffect> now = new List<PlayerStatEffect>();
        readonly List<PlayerStatEffect> next = new List<PlayerStatEffect>();

        PlayerProfile player;
        int spot;
        int itemId;
        bool useLucky, useProtect;
        bool waiting;   // online: đã gửi lên server, chờ kết quả

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (luckyButton) luckyButton.AddListener(ToggleLucky);
            if (protectButton) protectButton.AddListener(ToggleProtect);
            if (upgradeButton) upgradeButton.AddListener(TryUpgrade);
            if (socketTabButton) socketTabButton.AddListener(OpenSockets);
        }

        void OnDestroy()
        {
            if (closeButton) closeButton.RemoveAllListeners();
            if (luckyButton) luckyButton.RemoveAllListeners();
            if (protectButton) protectButton.RemoveAllListeners();
            if (upgradeButton) upgradeButton.RemoveAllListeners();
            if (socketTabButton) socketTabButton.RemoveAllListeners();
            Unbind();
        }

        void OpenSockets()
        {
            if (player == null || UIManager.Instance == null) return;
            PlayerProfile owner = player;
            int at = spot;
            Close();
            UIManager.Instance.Show<GearSocketPanel>(UIID.GearSocket, p => p.Open(owner, at));
        }

        public override void OnClose()
        {
            Unbind();
            base.OnClose();
        }

        /// <summary>Cường hóa món ở <paramref name="at"/> (quy ước của <see cref="PlayerProfile.GearAt"/>).</summary>
        public void Open(PlayerProfile owner, int at)
        {
            Unbind();
            player = owner;
            spot = at;
            itemId = owner != null ? owner.GearAt(at).itemId : 0;
            useLucky = useProtect = waiting = false;
            if (resultText) resultText.text = "";
            if (player != null)
            {
                player.Inventory.Changed += Refresh;
                player.Equipment.Changed += HandleGearChanged;
                player.OnGoldChanged += HandleGoldChanged;
                player.OnGearUpgraded += HandleResult;
            }
            Refresh();
        }

        void Unbind()
        {
            if (player == null) return;
            player.Inventory.Changed -= Refresh;
            player.Equipment.Changed -= HandleGearChanged;
            player.OnGoldChanged -= HandleGoldChanged;
            player.OnGearUpgraded -= HandleResult;
            player = null;
        }

        void HandleGearChanged(EquipSlot _) => Refresh();

        void HandleGoldChanged(long _) => Refresh();

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Cường hóa trang bị|Enhance gear");
            if (closeButton) closeButton.SetText("X");
            if (upgradeButton) upgradeButton.SetText(Lang.GetSplitText("Cường hóa|Enhance"));
            if (socketTabButton) socketTabButton.SetText(Lang.GetSplitText("Khảm đá|Gems"));
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
            GearUpgradeStep step = rules.Step(gear.level);
            ItemTemplate lucky = table.FindStone(GearStone.Lucky);
            ItemTemplate protect = table.FindStone(GearStone.Protect);
            int luckyCount = lucky != null ? player.Inventory.Count(lucky.id) : 0;
            int protectCount = protect != null ? player.Inventory.Count(protect.id) : 0;
            useLucky &= luckyCount > 0;
            int protectNeed = step != null ? step.protectStones : 1;
            useProtect &= protectCount >= protectNeed;

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

            if (statsText) statsText.text = StatsText(item, gear.level, step != null);
            if (costText) costText.text = CostText(table, rules, step);

            bool risky = step != null && step.dropOnFail && gear.level > 0;
            if (luckyButton)
            {
                luckyButton.SetActive(step != null);
                luckyButton.SetInteractable(luckyCount > 0);
                luckyButton.SetColorText(useLucky ? UIColors.TextSelected : UIColors.TextNormal);
                luckyButton.SetText(Lang.Format("Đá may mắn +{0}% (có {1})|Lucky stone +{0}% (have {1})",
                    rules.luckyBonus.ToString("0.#"), luckyCount));
            }
            if (protectButton)
            {
                protectButton.SetActive(risky);
                protectButton.SetInteractable(protectCount >= protectNeed);
                protectButton.SetColorText(useProtect ? UIColors.TextSelected : UIColors.TextNormal);
                protectButton.SetText(Lang.Format("Đá bảo vệ: cần {0} (có {1})|Protect stones: need {0} (have {1})", protectNeed, protectCount));
            }
            if (upgradeButton)
                upgradeButton.SetInteractable(!waiting && player.CheckGearUpgrade(spot, useLucky, useProtect) == GearBlock.None);
        }

        string StatsText(ItemTemplate item, int level, bool canRaise)
        {
            GearUpgradeTable.EffectsOf(item, level, now);
            GearUpgradeTable.EffectsOf(item, level + 1, next);
            text.Clear();
            if (!canRaise)
            {
                foreach (PlayerStatEffect e in now) text.Append(PlayerStatText.Effect(e.stat, e.value)).Append('\n');
                return text.ToString();
            }

            string open = "<color=" + UIColors.Hex(UIColors.StatusGood) + ">";
            foreach (PlayerStatEffect e in next)
            {
                text.Append(PlayerStatText.Label(e.stat)).Append(' ');
                bool had = false;
                foreach (PlayerStatEffect o in now)
                    if (o.stat == e.stat)
                    {
                        text.Append(PlayerStatText.Amount(o.stat, o.value)).Append("  >  ");
                        had = true;
                    }
                if (!had) text.Append("+0  >  ");
                text.Append(open).Append(PlayerStatText.Amount(e.stat, e.value)).Append("</color>\n");
            }
            return text.ToString();
        }

        string CostText(ItemDatabase table, GearUpgradeTable rules, GearUpgradeStep step)
        {
            text.Clear();
            if (step == null) return Lang.GetSplitText("Đã đạt cấp tối đa|Max level reached");

            text.Append(Lang.GetSplitText("Tỉ lệ thành công|Success rate")).Append(": ")
                .Append(rules.ChanceOf(step, useLucky).ToString("0.#")).Append("%\n");

            ItemTemplate stone = table.FindStone(GearStone.Upgrade);
            AppendNeed(stone != null ? stone.Label : Lang.GetSplitText("Đá cường hóa|Upgrade stone"),
                stone != null ? player.Inventory.Count(stone.id) : 0, step.stones);
            AppendNeed(Lang.GetSplitText("Vàng|Gold"), player.Gold, step.gold);

            if (step.dropOnFail)
                text.Append(Lang.GetSplitText(useProtect ? "Trượt: đá bảo vệ giữ cấp|Fail: protect stone keeps the level"
                                                         : "Trượt: tụt 1 cấp|Fail: lose 1 level"));
            return text.ToString();
        }

        void AppendNeed(string label, long have, long need)
        {
            Color color = have >= need ? UIColors.TextNormal : UIColors.StatusBad;
            text.Append(label).Append(": <color=").Append(UIColors.Hex(color)).Append('>').Append(have).Append("</color> / ")
                .Append(need).Append('\n');
        }

        // ---------------- thao tác ----------------

        void ToggleLucky()
        {
            useLucky = !useLucky;
            Refresh();
        }

        void ToggleProtect()
        {
            useProtect = !useProtect;
            Refresh();
        }

        void TryUpgrade()
        {
            if (player == null || waiting) return;
            GearUpgradeTable rules = GearUpgradeTable.Current;
            GearUpgradeStep step = rules != null ? rules.Step(player.GearAt(spot).level) : null;
            if (step != null && step.dropOnFail && !useProtect && player.GearAt(spot).level > 0)
                PopupMN.Confirm(Lang.GetSplitText("Không dùng đá bảo vệ: trượt sẽ tụt 1 cấp. Vẫn cường hóa?|No protect stone: failing loses 1 level. Continue?"),
                    Upgrade);
            else Upgrade();
        }

        void Upgrade()
        {
            if (player == null) return;
            GearOutcome outcome = player.UpgradeGear(spot, useLucky, useProtect);
            waiting = outcome == GearOutcome.Pending;
            if (outcome == GearOutcome.Refused) ShowResult(Lang.GetSplitText("Chưa đủ điều kiện|Requirements not met"), UIColors.StatusBad);
            Refresh();
        }

        void HandleResult(GearOutcome outcome, int level)
        {
            waiting = false;
            switch (outcome)
            {
                case GearOutcome.Success:
                    ShowResult(Lang.Format("Thành công: +{0}|Success: +{0}", level), UIColors.StatusGood);
                    break;
                case GearOutcome.Failed:
                    ShowResult(Lang.GetSplitText("Thất bại, giữ nguyên cấp|Failed, level kept"), UIColors.StatusBad);
                    break;
                case GearOutcome.Dropped:
                    ShowResult(Lang.Format("Thất bại, tụt còn +{0}|Failed, dropped to +{0}", level), UIColors.StatusBad);
                    break;
                case GearOutcome.Protected:
                    ShowResult(Lang.GetSplitText("Thất bại, đá bảo vệ đã giữ cấp|Failed, protect stone kept the level"), UIColors.StatusBad);
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
