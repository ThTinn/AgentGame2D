using System.Collections.Generic;
using System.Text;
using Game.Players;
using Game.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Cây nâng cấp của một vũ khí (panel UIID.WeaponUpgrade, mở từ bảng vũ khí qua <see cref="Open"/>). Trái: các nút xếp
    /// theo (cột, hàng) của WeaponUpgrade, dây nối nút cha - con. Phải: chi tiết nút đang chọn, nút Nâng cấp, chỉ số gộp
    /// của vũ khí và nút Đặt lại. Nâng cấp đang miễn phí; chỉ cần nút cha có ít nhất một cấp.
    /// Áp vào nhân vật qua <see cref="PlayerProfile.Upgrade"/>, GameController nghe sự kiện và ghi vào PlayerData.
    /// </summary>
    public class WeaponUpgradePanel : PanelBase
    {
        [Header("Nâng cấp vũ khí")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [Tooltip("Nơi đặt các nút của cây (neo trên-giữa).")]
        [SerializeField] RectTransform treeRoot;
        [SerializeField] TextBtn buttonPrefab;
        [Header("Chi tiết")]
        [SerializeField] TMP_Text detailText;
        [SerializeField] TextBtn upgradeButton;
        [Tooltip("Chỉ số gộp của vũ khí sau các nút đã mở.")]
        [SerializeField] TMP_Text statsText;
        [SerializeField] TextBtn resetButton;

        static readonly Color LineOff = new Color(1f, 1f, 1f, 0.15f);
        static readonly Color LineOn = UIColors.WithAlpha(UIColors.TextMaxed, 0.8f);

        static readonly Vector2 NodeSize = new Vector2(190f, 96f);
        const float ColumnStep = 220f, RowStep = 132f, TopPad = 20f, LineWidth = 6f;

        readonly List<TextBtn> nodeButtons = new List<TextBtn>();
        readonly List<Image> lines = new List<Image>();
        readonly StringBuilder text = new StringBuilder(256);

        PlayerProfile player;
        WeaponTemplate weapon;
        int selected;   // chỉ số trong weapon.upgrades

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (upgradeButton) upgradeButton.AddListener(UpgradeSelected);
            if (resetButton) resetButton.AddListener(ResetTree);
        }

        void OnDestroy()
        {
            if (closeButton) closeButton.RemoveAllListeners();
            if (upgradeButton) upgradeButton.RemoveAllListeners();
            if (resetButton) resetButton.RemoveAllListeners();
        }

        public override void OnShow()
        {
            base.OnShow();
            player = CoreSystems.Me;
            if (weapon == null && player != null && WeaponDatabase.Current != null) weapon = WeaponDatabase.Current.Get(player.WeaponId);
            Build();
        }

        /// <summary>Hiện cây của vũ khí này (gọi sau khi mở panel).</summary>
        public void Open(WeaponTemplate target)
        {
            weapon = target;
            selected = 0;
            Build();
        }

        public override void OnLoadLang()
        {
            if (closeButton) closeButton.SetText(Lang.Close);
            if (upgradeButton) upgradeButton.SetText(Lang.GetSplitText("Nâng cấp|Upgrade"));
            if (resetButton) resetButton.SetText(Lang.GetSplitText("Đặt lại|Reset"));
            RefreshAll();
        }

        // ---------------- cây ----------------

        void Build()
        {
            if (treeRoot == null || buttonPrefab == null) return;
            for (int i = treeRoot.childCount - 1; i >= 0; i--) Destroy(treeRoot.GetChild(i).gameObject);
            nodeButtons.Clear();
            lines.Clear();

            if (weapon != null)
            {
                int columns = 1;
                foreach (WeaponUpgrade u in weapon.upgrades) columns = Mathf.Max(columns, u.column + 1);

                // dây trước để nằm dưới các nút
                foreach (WeaponUpgrade u in weapon.upgrades)
                {
                    WeaponUpgrade parent = u.parent != 0 ? weapon.GetUpgrade(u.parent) : null;
                    lines.Add(parent != null ? Line(NodePos(parent, columns), NodePos(u, columns)) : null);
                }

                for (int i = 0; i < weapon.upgrades.Count; i++)
                {
                    int captured = i;
                    TextBtn button = Instantiate(buttonPrefab, treeRoot);
                    button.name = "Node_" + weapon.upgrades[i].name;
                    var rt = (RectTransform)button.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = NodeSize;
                    rt.anchoredPosition = NodePos(weapon.upgrades[i], columns);
                    if (button.nameTxt) button.nameTxt.fontSize = 20f;
                    button.AddListener(() => Select(captured));
                    nodeButtons.Add(button);
                }
            }

            selected = weapon != null && selected < weapon.upgrades.Count ? selected : 0;
            RefreshAll();
        }

        static Vector2 NodePos(WeaponUpgrade u, int columns) =>
            new Vector2((u.column - (columns - 1) * 0.5f) * ColumnStep, -TopPad - NodeSize.y * 0.5f - u.row * RowStep);

        Image Line(Vector2 from, Vector2 to)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(treeRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 d = to - from;
            rt.sizeDelta = new Vector2(d.magnitude, LineWidth);
            rt.anchoredPosition = (from + to) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void Select(int index)
        {
            selected = index;
            RefreshAll();
        }

        void UpgradeSelected()
        {
            if (player == null || weapon == null || selected >= weapon.upgrades.Count) return;
            if (player.Upgrade(weapon, weapon.upgrades[selected].id)) RefreshAll();
        }

        void ResetTree()
        {
            if (player == null || weapon == null) return;
            player.ResetUpgrades(weapon);
            RefreshAll();
        }

        // ---------------- hiển thị ----------------

        void RefreshAll()
        {
            if (titleText)
                titleText.text = Lang.GetSplitText("Nâng cấp|Upgrade") + (weapon != null ? ": " + weapon.Label : "");
            RefreshNodes();
            RefreshDetail();
            RefreshStats();
        }

        int Rank(WeaponUpgrade u) => player != null ? player.UpgradeRank(weapon.id, u.id) : 0;

        void RefreshNodes()
        {
            if (weapon == null) return;
            for (int i = 0; i < nodeButtons.Count && i < weapon.upgrades.Count; i++)
            {
                WeaponUpgrade u = weapon.upgrades[i];
                int rank = Rank(u);
                TextBtn button = nodeButtons[i];
                button.SetText(u.Label + "\n" + rank + "/" + u.maxRank);
                if (button.nameTxt)
                {
                    bool open = rank > 0 || (player != null && player.CanUpgrade(weapon, u));
                    button.nameTxt.color = i == selected ? UIColors.TextSelected
                        : rank >= u.maxRank ? UIColors.TextMaxed
                        : rank > 0 ? UIColors.TextRanked
                        : open ? UIColors.TextNormal : UIColors.TextLocked;
                }
                if (lines[i]) lines[i].color = rank > 0 ? LineOn : LineOff;
            }
        }

        void RefreshDetail()
        {
            WeaponUpgrade u = weapon != null && selected < weapon.upgrades.Count ? weapon.upgrades[selected] : null;
            if (upgradeButton) upgradeButton.SetInteractable(u != null && player != null && player.CanUpgrade(weapon, u));
            if (!detailText) return;
            if (u == null)
            {
                detailText.text = Lang.GetSplitText("Vũ khí này chưa có cây nâng cấp|This weapon has no upgrades");
                return;
            }

            int rank = Rank(u);
            text.Clear();
            text.Append("<b>").Append(u.Label).Append("</b>  ")
                .Append(Lang.GetSplitText("Cấp|Rank")).Append(' ').Append(rank).Append('/').Append(u.maxRank).Append('\n');
            if (!string.IsNullOrEmpty(u.description)) text.Append(u.description).Append('\n');
            text.Append('\n').Append(Lang.GetSplitText("Mỗi cấp|Per rank")).Append(":\n");
            foreach (PlayerStatEffect e in u.effects) text.Append("  ").Append(PlayerStatText.Effect(e.stat, e.value)).Append('\n');

            WeaponUpgrade parent = u.parent != 0 ? weapon.GetUpgrade(u.parent) : null;
            if (rank >= u.maxRank) text.Append('\n').Append(Lang.GetSplitText("Đã tối đa|Maxed"));
            else if (parent != null && Rank(parent) == 0)
                text.Append('\n').Append(Lang.GetSplitText("Cần mở trước|Requires")).Append(": ").Append(parent.Label);
            detailText.text = text.ToString();
        }

        void RefreshStats()
        {
            if (!statsText) return;
            if (player == null || weapon == null)
            {
                statsText.text = "";
                return;
            }
            statsText.text = StatsText(player.StatsOf(weapon), player.UpgradesSpent(weapon));
        }

        /// <summary>Chỉ số gộp của cây, chỉ các dòng khác 0. Popup thông tin vũ khí dùng chung.</summary>
        internal static string StatsText(WeaponStats s, int spent)
        {
            var sb = new StringBuilder(128);
            sb.Append(Lang.GetSplitText("Đã nâng|Upgraded")).Append(": ").Append(spent).Append('\n');
            if (s.damage != 0) sb.Append(PlayerStatText.Effect(PlayerStat.WeaponDamage, s.damage)).Append('\n');
            if (s.projectiles != 0) sb.Append(Lang.GetSplitText("Số viên mỗi phát|Projectiles")).Append(": ").Append(1 + s.projectiles).Append('\n');
            if (s.targets != 0) sb.Append(PlayerStatText.Effect(PlayerStat.AttackTargets, s.targets)).Append('\n');
            if (s.explosionRadius > 0f)
                sb.Append(Lang.GetSplitText("Nổ|Blast")).Append(": ").Append(s.explosionRadius.ToString("0.##"))
                  .Append(Lang.GetSplitText(" ô, | cells, ")).Append(s.ExplosionPercent).Append("%\n");
            if (s.range != 0f) sb.Append(PlayerStatText.Effect(PlayerStat.AttackRange, s.range)).Append('\n');
            if (s.attackSpeed != 0f) sb.Append(PlayerStatText.Effect(PlayerStat.AttackSpeed, s.attackSpeed * 100f)).Append('\n');
            if (s.arc != 0f) sb.Append(PlayerStatText.Effect(PlayerStat.Arc, s.arc)).Append('\n');
            return sb.ToString();
        }
    }
}
