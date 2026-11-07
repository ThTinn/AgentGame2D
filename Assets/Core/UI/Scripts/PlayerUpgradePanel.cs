using System.Collections.Generic;
using System.Text;
using Game.Players;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Cây nâng cấp nhân vật (panel UIID.PlayerUpgrade), mở từ thẻ InfoPlayer hoặc nút Upgrade trên HUD.
    /// Trái: khung cây <see cref="ZoomScrollRect"/>: kéo để đi ngang / dọc, lăn chuột hoặc pinch để zoom. Cây bắt đầu từ một nút gốc ở tâm và
    /// toả ra theo các hướng của <see cref="PlayerUpgradeTree.branchNames"/> (mặc định 5); vị trí mỗi nút theo toạ độ
    /// cực (nhánh, vòng, lệch ngang), dây nối nút cha - con, tên nhánh ở đầu mút mỗi hướng.
    /// Phải: chi tiết nút đang chọn (hiệu ứng, giá, điều kiện còn thiếu), nút Nâng cấp, tổng chỉ số đang cộng.
    /// Trên: cấp + điểm tiềm năng, Về gốc (cuộn về tâm), Đặt lại (hoàn điểm), Đóng.
    /// Nút dựng một lần theo cây; sau đó chỉ tô lại theo <see cref="PlayerProgress.Changed"/> khi panel đang mở.
    /// </summary>
    public class PlayerUpgradePanel : PanelBase
    {
        [Header("Thanh trên")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text pointsText;
        [SerializeField] TextBtn centerButton;
        [Tooltip("TẠM để test: lên cấp tối đa + đủ điểm tiềm năng của cấp đó. Chỉ hiện ở Editor / bản dev (Debug.isDebugBuild).")]
        [SerializeField] TextBtn maxLevelButton;
        [SerializeField] TextBtn resetButton;
        [SerializeField] TextBtn closeButton;
        [Header("Cây")]
        [SerializeField] ScrollRect scroll;
        [Tooltip("Content của ScrollRect: neo giữa, panel đặt cỡ theo bán kính cây. Các nút sinh vào đây.")]
        [SerializeField] RectTransform content;
        [SerializeField] TextBtn nodePrefab;
        [Tooltip("Sprite trắng (9-slice) thay cho sprite của prefab nút, để màu riêng của từng hướng hiện đúng. Trống = giữ sprite prefab.")]
        [SerializeField] Sprite nodeSprite;
        [Tooltip("Chữ dùng làm tên nhánh ở đầu mút mỗi hướng.")]
        [SerializeField] TMP_Text labelPrefab;
        [Header("Chi tiết")]
        [SerializeField] TMP_Text detailText;
        [SerializeField] TextBtn upgradeButton;
        [SerializeField] TMP_Text totalsText;

        [Header("Bố cục cây")]
        [SerializeField] float ringStep = 170f;
        [SerializeField] float lateralStep = 150f;
        [SerializeField] Vector2 nodeSize = new Vector2(124f, 62f);
        [SerializeField] Vector2 rootSize = new Vector2(150f, 78f);
        [Tooltip("Cỡ nút Đột phá và nút Đỉnh (to hơn nút thường cho dễ thấy mốc).")]
        [SerializeField] Vector2 majorSize = new Vector2(150f, 74f);
        [Tooltip("Cỡ nút Tinh thông (nâng nhiều lần, hiệu quả thấp).")]
        [SerializeField] Vector2 masterySize = new Vector2(112f, 54f);
        [SerializeField] float lineWidth = 5f;
        [Tooltip("Khoảng trống quanh cây trong vùng cuộn, để nút ở mép không dính sát khung.")]
        [SerializeField] float padding = 180f;

        static readonly Color LineOff = new Color(1f, 1f, 1f, 0.15f);
        static readonly Color LineOn = UIColors.WithAlpha(UIColors.TextMaxed, 0.8f);
        static readonly Color NodeSelected = new Color(0.35f, 0.5f, 0.85f);
        static readonly Color NodeMaxed = new Color(0.22f, 0.45f, 0.28f);
        static readonly Color NodeLocked = new Color(0.2f, 0.16f, 0.3f);
        // màu riêng của từng hướng (theo PlayerUpgradeNode.branch): pha vào nền nút và tô tên nhánh
        static readonly Color[] BranchTints =
        {
            new Color(0.85f, 0.32f, 0.3f),    // Sức mạnh
            new Color(0.3f, 0.75f, 0.85f),    // Tốc độ
            new Color(0.62f, 0.42f, 0.9f),    // Kinh nghiệm
            new Color(0.9f, 0.72f, 0.25f),    // Tài lộc
            new Color(0.85f, 0.52f, 0.25f),   // Sức mạnh đồ
        };
        // nền nút của các hướng: xám tối trung tính rồi pha màu hướng, để 5 hướng khác màu rõ (pha vào nền xanh của prefab
        // nút thì hướng nào cũng ngả xanh)
        static readonly Color NodeBase = new Color(0.18f, 0.13f, 0.32f);
        static readonly Color RootColor = new Color(0.22f, 0.5f, 0.55f);
        const float TintAmount = 0.55f;

        static readonly ColorBlock NeutralTint = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.88f, 0.88f, 0.88f),
            pressedColor = new Color(0.7f, 0.7f, 0.7f),
            selectedColor = Color.white,
            disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        static Color Tint(PlayerUpgradeNode n) =>
            n.ring > 0 ? BranchTints[((n.branch % BranchTints.Length) + BranchTints.Length) % BranchTints.Length] : Color.white;

        readonly List<TextBtn> nodeButtons = new List<TextBtn>();
        readonly List<Image> lines = new List<Image>();
        readonly List<TMP_Text> branchLabels = new List<TMP_Text>();
        readonly StringBuilder text = new StringBuilder(256);
        Color nodeNormal = Color.white;   // màu nền gốc của prefab nút, đọc lúc dựng

        PlayerProfile player;
        PlayerUpgradeTree builtFor;
        bool built;
        int selected;

        PlayerProgress Progress => player != null ? player.Progress : null;
        static PlayerUpgradeTree Tree => PlayerUpgradeTree.Current;

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (centerButton) centerButton.AddListener(CenterView);
            if (resetButton) resetButton.AddListener(ResetTree);
            if (upgradeButton) upgradeButton.AddListener(UpgradeSelected);
            if (maxLevelButton)
            {
                // nút test: bản build phát hành không có
                if (Debug.isDebugBuild) maxLevelButton.AddListener(MaxLevelForTest);
                else maxLevelButton.SetActive(false);
            }
        }

        void OnDestroy()
        {
            Bind(null);
            if (closeButton) closeButton.RemoveAllListeners();
            if (centerButton) centerButton.RemoveAllListeners();
            if (resetButton) resetButton.RemoveAllListeners();
            if (upgradeButton) upgradeButton.RemoveAllListeners();
            if (maxLevelButton) maxLevelButton.RemoveAllListeners();
        }

        /// <summary>
        /// TẠM để test: đưa người chơi lên cấp tối đa, điểm tiềm năng = tổng điểm của cấp đó trừ phần đã tiêu (như người
        /// chơi thật ở cấp tối đa). Cây đã nâng giữ nguyên; tiến trình được lưu như lúc chơi. Xoá tiến trình:
        /// Tools/Core/Player/Test/Xoá tiến trình, hoặc Đặt lại (hoàn điểm) rồi tự hạ cấp trong PlayerData.
        /// </summary>
        void MaxLevelForTest()
        {
            PlayerUpgradeTree tree = Tree;
            PlayerProgress p = Progress;
            if (tree == null || p == null) return;

            var saved = new List<NodeRank>();
            p.Save(saved);
            int points = Mathf.Max(p.Points, tree.TotalPointsAt(tree.maxLevel) - p.Spent());
            player.LoadProgress(tree.maxLevel, 0, points, saved);   // Changed > Refresh
            player.Refill();
            Debug.Log($"[Test] Lên cấp {p.Level}, {p.Points} điểm tiềm năng.");
        }

        public override void OnShow()
        {
            base.OnShow();
            Bind(CoreSystems.Me);
            // nút test chỉ đổi hồ sơ tại máy: online thì server ghi đè ở lần đồng bộ sau, nên ẩn đi
            if (maxLevelButton) maxLevelButton.SetActive(Debug.isDebugBuild && !Game.Core.NetSync.Online);
            if (!built || builtFor != Tree)
            {
                Build();
                CenterView();
            }
            Refresh();
        }

        public override void OnClose()
        {
            Bind(null);
            base.OnClose();
        }

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Cây nâng cấp|Upgrade tree");
            if (closeButton) closeButton.SetText(Lang.Close);
            if (centerButton) centerButton.SetText(Lang.GetSplitText("Về gốc|Center"));
            if (resetButton) resetButton.SetText(Lang.GetSplitText("Đặt lại|Reset"));
            if (upgradeButton) upgradeButton.SetText(Lang.GetSplitText("Nâng cấp|Upgrade"));
            if (maxLevelButton) maxLevelButton.SetText(Lang.GetSplitText("MAX cấp (test)|MAX level (test)"));
            if (built) Build();   // tên nhánh đổi theo ngôn ngữ
            Refresh();
        }

        void Bind(PlayerProfile target)
        {
            if (player == target) return;
            if (player != null) player.Progress.Changed -= Refresh;
            player = target;
            if (player != null) player.Progress.Changed += Refresh;
        }

        /// <summary>Cuộn khung cây về nút gốc ở tâm, zoom về mức mặc định.</summary>
        public void CenterView()
        {
            if (scroll is ZoomScrollRect zoom)
            {
                zoom.ResetView();
                return;
            }
            if (scroll) scroll.StopMovement();
            if (content) content.anchoredPosition = Vector2.zero;
        }

        // ---------------- cây ----------------

        void Build()
        {
            built = true;
            builtFor = Tree;
            if (content == null || nodePrefab == null) return;
            for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
            nodeButtons.Clear();
            lines.Clear();
            // sprite trắng thì màu nền là màu thật của nút; sprite của prefab thì giữ màu gốc của prefab cho nút gốc
            nodeNormal = nodeSprite != null ? RootColor : nodePrefab.image != null ? nodePrefab.image.color : Color.white;

            PlayerUpgradeTree tree = builtFor;
            if (tree == null) return;

            // vùng cuộn vuông, đủ chứa vòng xa nhất cộng tên nhánh
            float radius = 0f;
            foreach (PlayerUpgradeNode n in tree.nodes)
                radius = Mathf.Max(radius, tree.NodePosition(n, ringStep, lateralStep).magnitude);
            float size = 2f * (radius + ringStep * 0.8f + padding);
            content.sizeDelta = new Vector2(size, size);

            // dây trước để nằm dưới các nút
            foreach (PlayerUpgradeNode n in tree.nodes)
            {
                PlayerUpgradeNode parent = n.parent != 0 ? tree.Get(n.parent) : null;
                lines.Add(parent != null ? Line(Pos(tree, parent), Pos(tree, n)) : null);
            }

            BuildBranchLabels(tree, radius);

            for (int i = 0; i < tree.nodes.Count; i++)
            {
                int captured = i;
                PlayerUpgradeNode n = tree.nodes[i];
                TextBtn button = Instantiate(nodePrefab, content);
                button.name = "Node_" + n.name;
                if (nodeSprite != null && button.image != null)
                {
                    button.image.sprite = nodeSprite;
                    button.image.type = Image.Type.Sliced;
                    button.image.pixelsPerUnitMultiplier = 2f;
                    // tint của prefab nút là xanh (nhân đè lên màu nền) nên mọi hướng đều ngả xanh: đổi sang tint trắng / xám,
                    // màu nền do panel đặt (theo hướng, trạng thái) là màu thật của nút
                    button.colors = NeutralTint;
                    button.image.canvasRenderer.SetColor(Color.white);   // bỏ qua nhịp mờ dần từ tint xanh cũ
                }
                var rt = (RectTransform)button.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = SizeOf(n);
                rt.anchoredPosition = Pos(tree, n);
                if (button.nameTxt)
                {
                    button.nameTxt.fontSize = n.ring == 0 || IsMajor(n) ? 18f : n.kind == UpgradeNodeKind.Mastery ? 14f : 16f;
                    button.nameTxt.lineSpacing = -10f;
                }
                button.AddListener(() => Select(captured));
                nodeButtons.Add(button);
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, tree.nodes.Count - 1));
        }

        Vector2 Pos(PlayerUpgradeTree tree, PlayerUpgradeNode n) => tree.NodePosition(n, ringStep, lateralStep);

        static bool IsMajor(PlayerUpgradeNode n) => n.kind == UpgradeNodeKind.Breakthrough || n.kind == UpgradeNodeKind.Capstone;

        Vector2 SizeOf(PlayerUpgradeNode n) =>
            n.ring == 0 ? rootSize : IsMajor(n) ? majorSize : n.kind == UpgradeNodeKind.Mastery ? masterySize : nodeSize;

        /// <summary>Nhãn loại nút cho bảng chi tiết (rỗng với nút thường).</summary>
        static string KindLabel(PlayerUpgradeNode n)
        {
            switch (n.kind)
            {
                case UpgradeNodeKind.Breakthrough: return Lang.GetSplitText("Đột phá|Breakthrough");
                case UpgradeNodeKind.Capstone: return Lang.GetSplitText("Đỉnh nhánh|Capstone");
                case UpgradeNodeKind.Mastery: return Lang.GetSplitText("Tinh thông|Mastery");
                default: return "";
            }
        }

        // tên nhánh đặt ngoài vòng xa nhất, theo hướng của nhánh
        void BuildBranchLabels(PlayerUpgradeTree tree, float radius)
        {
            branchLabels.Clear();
            if (labelPrefab == null) return;
            for (int b = 0; b < tree.BranchCount; b++)
            {
                TMP_Text label = Instantiate(labelPrefab, content);
                branchLabels.Add(label);
                label.name = "Branch_" + b;
                label.text = Lang.GetSplitText(tree.branchNames[b]).ToUpperInvariant();   // RefreshNodes ghi thêm bậc Chuyên tinh
                label.fontSize = 24f;
                label.fontStyle = FontStyles.Bold;
                label.color = UIColors.WithAlpha(BranchTints[b % BranchTints.Length], 0.9f);
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
                var rt = label.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(300f, 64f);
                rt.anchoredPosition = tree.BranchDirection(b) * (radius + ringStep * 0.6f);
            }
        }

        Image Line(Vector2 from, Vector2 to)
        {
            var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 d = to - from;
            rt.sizeDelta = new Vector2(d.magnitude, lineWidth);
            rt.anchoredPosition = (from + to) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void Select(int index)
        {
            selected = index;
            RefreshNodes();
            RefreshDetail();
            RefreshTotals();   // Chuyên tinh ở khung tổng đi theo hướng của nút đang chọn
        }

        PlayerUpgradeNode SelectedNode =>
            Tree != null && selected >= 0 && selected < Tree.nodes.Count ? Tree.nodes[selected] : null;

        void UpgradeSelected()
        {
            PlayerUpgradeNode node = SelectedNode;
            if (node != null && Progress != null) Progress.Upgrade(node.id);   // Changed > Refresh
        }

        void ResetTree()
        {
            if (Progress != null && Progress.Spent() > 0) Progress.ResetTree();
        }

        // ---------------- hiển thị ----------------

        void Refresh()
        {
            if (!IsOpen) return;
            RefreshHeader();
            RefreshNodes();
            RefreshDetail();
            RefreshTotals();
        }

        void RefreshHeader()
        {
            PlayerProgress p = Progress;
            if (pointsText)
            {
                int points = p != null ? p.Points : 0;
                pointsText.text = Lang.GetSplitText("Cấp|Level") + " " + (p != null ? p.Level : 0) + "    "
                    + Lang.GetSplitText("Điểm tiềm năng|Potential") + ": "
                    + UIColors.Colored(points.ToString(), points > 0 ? UIColors.LevelUp : UIColors.TextMuted) + "    "
                    + Lang.GetSplitText("Vàng|Gold") + ": " + UIColors.Colored((player != null ? player.Gold : 0).ToString("N0"), UIColors.Gold);
                PlayerUpgradeTree tree = Tree;
                int next = tree != null && p != null ? tree.NextMilestone(p.Level) : 0;
                if (next > 0)
                    pointsText.text += "    <color=" + UIColors.LabelHex + ">" + Lang.GetSplitText("Mốc: cấp|Next: Lv") + " "
                                       + next + " (+" + tree.milestoneBonusPoints + ")</color>";
            }
            if (resetButton) resetButton.SetInteractable(p != null && p.Spent() > 0);
        }

        void RefreshNodes()
        {
            PlayerUpgradeTree tree = Tree;
            PlayerProgress p = Progress;
            if (tree == null) return;

            for (int i = 0; i < nodeButtons.Count && i < tree.nodes.Count; i++)
            {
                PlayerUpgradeNode n = tree.nodes[i];
                int rank = p != null ? p.Rank(n.id) : 0;
                PlayerProgress.Block block = p != null ? p.Check(n) : PlayerProgress.Block.NoNode;
                bool locked = rank == 0 && (block == PlayerProgress.Block.NeedParent || block == PlayerProgress.Block.NeedLevel);

                TextBtn button = nodeButtons[i];
                button.SetText(n.Label + "\n<size=80%>" + rank + "/" + n.maxRank + "</size>");
                if (button.nameTxt)
                    button.nameTxt.color = rank >= n.maxRank ? UIColors.TextMaxed
                        : block == PlayerProgress.Block.None ? UIColors.LevelUp   // nâng được ngay: vàng cho dễ thấy
                        : rank > 0 ? UIColors.TextRanked
                        : locked ? UIColors.TextLocked : UIColors.TextNormal;
                if (button.image)
                    button.image.color = i == selected ? NodeSelected
                        : rank >= n.maxRank ? NodeMaxed
                        : locked ? Color.Lerp(NodeLocked, Tint(n), IsMajor(n) ? 0.3f : 0.15f)
                        : n.ring > 0 ? Color.Lerp(NodeBase, Tint(n), IsMajor(n) ? 0.8f : n.kind == UpgradeNodeKind.Mastery ? 0.35f : TintAmount)
                        : nodeNormal;
                if (lines[i]) lines[i].color = rank > 0 ? LineOn : LineOff;
            }

            // tên hướng kèm bậc Chuyên tinh đang đạt
            for (int b = 0; b < branchLabels.Count && b < tree.BranchCount; b++)
            {
                int tier = p != null ? p.MasteryTier(b) : 0;
                int tiers = MasteryCount(tree, b);
                branchLabels[b].text = Lang.GetSplitText(tree.branchNames[b]).ToUpperInvariant()
                    + (tiers > 0 ? "\n<size=65%>" + Lang.GetSplitText("Chuyên tinh|Mastery") + " " + tier + "/" + tiers + "</size>" : "");
            }
        }

        void RefreshDetail()
        {
            PlayerUpgradeNode n = SelectedNode;
            PlayerProgress p = Progress;
            PlayerProgress.Block block = p != null ? p.Check(n) : PlayerProgress.Block.NoNode;
            if (upgradeButton) upgradeButton.SetInteractable(block == PlayerProgress.Block.None);
            if (!detailText) return;

            if (n == null)
            {
                detailText.text = Tree == null
                    ? Lang.GetSplitText("Chưa có cây nâng cấp (Tools/Core/Player/Build Upgrade Tree)|No upgrade tree")
                    : Lang.GetSplitText("Chọn một nút trên cây|Select a node");
                return;
            }

            int rank = p != null ? p.Rank(n.id) : 0;
            text.Clear();
            text.Append("<size=125%><b>").Append(n.Label).Append("</b></size>\n");
            if (n.ring > 0 && Tree.branchNames != null && n.branch < Tree.branchNames.Length)
            {
                string direction = Lang.GetSplitText(Tree.branchNames[n.branch]);
                text.Append("<color=").Append(UIColors.Hex(Tint(n))).Append('>').Append(direction);
                if (!string.IsNullOrEmpty(n.group) && n.group != direction) text.Append(" › ").Append(n.group);
                text.Append("</color><color=").Append(UIColors.LabelHex).Append(">  · ").Append(Lang.GetSplitText("vòng|ring"))
                    .Append(' ').Append(n.ring).Append("</color>\n");
            }
            string kind = KindLabel(n);
            if (kind.Length > 0) text.Append("<b>").Append(UIColors.Colored(kind, UIColors.LevelUp)).Append("</b>\n");
            text.Append('\n');
            text.Append(Lang.GetSplitText("Cấp|Rank")).Append(": ").Append(rank).Append('/').Append(n.maxRank).Append('\n');
            text.Append(Lang.GetSplitText("Giá|Cost")).Append(": ").Append(n.cost).Append(Lang.GetSplitText(" điểm / cấp| pt / rank")).Append('\n');
            text.Append(Lang.GetSplitText("Cần cấp|Requires level")).Append(": ").Append(n.requiredLevel).Append("\n\n");
            text.Append(Lang.GetSplitText("Mỗi cấp|Per rank")).Append(":\n");
            foreach (PlayerStatEffect e in n.effects) text.Append("  ").Append(PlayerStatText.Effect(e.stat, e.value)).Append('\n');
            if (!string.IsNullOrEmpty(n.description))
                text.Append('\n').Append("<color=").Append(UIColors.LabelHex).Append('>').Append(n.description).Append("</color>\n");

            string reason = BlockText(block, n);
            if (reason != null) text.Append('\n').Append(UIColors.Colored(reason, block == PlayerProgress.Block.Maxed ? UIColors.TextMaxed : UIColors.StatusBad)).Append('\n');
            detailText.text = text.ToString();
        }

        static int MasteryCount(PlayerUpgradeTree tree, int branch)
        {
            int n = 0;
            if (tree?.masteries != null) foreach (BranchMastery m in tree.masteries) if (m.branch == branch) n++;
            return n;
        }

        /// <summary>Chuyên tinh của hướng: điểm đã tiêu, bậc đang đạt, bậc kế cần bao nhiêu điểm và cộng gì.</summary>
        void AppendMastery(int branch)
        {
            PlayerUpgradeTree tree = Tree;
            PlayerProgress p = Progress;
            int tiers = MasteryCount(tree, branch);
            if (tiers == 0) return;

            int spent = p != null ? p.Spent(branch) : 0, tier = p != null ? p.MasteryTier(branch) : 0;
            text.Append("<b>").Append(Lang.GetSplitText("Chuyên tinh|Mastery")).Append("</b> ")
                .Append(tier).Append('/').Append(tiers).Append("  <color=").Append(UIColors.LabelHex).Append('>')
                .Append(spent).Append(Lang.GetSplitText(" điểm trong hướng| pt in this direction")).Append("</color>\n");
            foreach (BranchMastery m in tree.masteries)
            {
                if (m.branch != branch || spent >= m.points) continue;
                text.Append("<color=").Append(UIColors.LabelHex).Append('>').Append(Lang.GetSplitText("Bậc tiếp ở|Next at")).Append(' ')
                    .Append(m.points).Append(Lang.GetSplitText(" điểm:| pt:")).Append("</color> ");
                for (int i = 0; i < m.effects.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(PlayerStatText.Effect(m.effects[i].stat, m.effects[i].value));
                }
                text.Append('\n');
                break;
            }
        }

        void RefreshTotals()
        {
            if (!totalsText) return;
            if (player == null)
            {
                totalsText.text = "";
                return;
            }

            PlayerStats b = player.Bonus;
            PlayerStats gear = player.GearBonus;
            text.Clear();
            // Chuyên tinh của hướng đang chọn ở đầu khung (khung chi tiết phía trên không đủ chỗ)
            PlayerUpgradeNode sel = SelectedNode;
            if (sel != null && sel.ring > 0)
            {
                int before0 = text.Length;
                AppendMastery(sel.branch);
                if (text.Length > before0) text.Append('\n');
            }
            text.Append("<b>").Append(Lang.GetSplitText("Đang cộng (cây + đồ)|Total bonus (tree + gear)")).Append("</b>  <color=").Append(UIColors.LabelHex).Append('>')
                .Append(Lang.GetSplitText("đã tiêu|spent")).Append(' ').Append(Progress.Spent()).Append("</color>\n");
            int before = text.Length;
            foreach (PlayerStat s in PlayerStatText.Order)
            {
                float v = PlayerStatText.Value(b, s);
                if (v == 0f) continue;
                text.Append(PlayerStatText.Effect(s, v));
                // tổng gồm cả đồ đang mặc: tách phần của đồ để biết cây cộng bao nhiêu
                string fromGear = PlayerStatText.GearNote(s, gear.Get(s));
                string cap = PlayerStatText.CapNote(s);
                if (fromGear.Length > 0 || cap.Length > 0)
                {
                    text.Append("  <color=").Append(UIColors.LabelHex).Append("><size=85%>(").Append(fromGear);
                    if (fromGear.Length > 0 && cap.Length > 0) text.Append(", ");
                    text.Append(cap).Append(")</size></color>");
                }
                text.Append('\n');
            }
            if (text.Length == before) text.Append("<color=").Append(UIColors.LabelHex).Append('>').Append(Lang.GetSplitText("Chưa có chỉ số cộng thêm|Nothing yet")).Append("</color>");
            totalsText.text = text.ToString();
        }

        string BlockText(PlayerProgress.Block block, PlayerUpgradeNode n)
        {
            switch (block)
            {
                case PlayerProgress.Block.Maxed: return Lang.GetSplitText("Đã tối đa|Maxed");
                case PlayerProgress.Block.NeedParent:
                    PlayerUpgradeNode parent = Tree != null ? Tree.Get(n.parent) : null;
                    return Lang.GetSplitText("Cần mở trước|Requires") + ": " + (parent != null ? parent.Label : "?");
                case PlayerProgress.Block.NeedLevel: return Lang.GetSplitText("Cần cấp|Requires level") + " " + n.requiredLevel;
                case PlayerProgress.Block.NeedPoints: return Lang.GetSplitText("Không đủ điểm tiềm năng|Not enough points");
                default: return null;
            }
        }

    }
}
