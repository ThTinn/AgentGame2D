using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.Npcs;
using Game.Story;

namespace Game.UI
{
    /// <summary>
    /// Cửa sổ nhiệm vụ (UIID.QuestLog). Mở bằng mục Nhiệm vụ trong Menu HUD (HudMenu), chạm khung theo dõi
    /// (QuestTrackerPanel) hoặc <see cref="Open"/>. Prefab dựng bằng Tools/Core/UI/Build Quest UI (QuestUIBuilder).
    /// <list type="bullet">
    /// <item>Bốn tab: Cốt truyện (chia theo chương), Việc phụ, Hằng ngày (chia theo Chờ trả / Đang làm / Có thể nhận / Đã
    /// hoàn thành) và Quan hệ (thân thiện với từng NPC). Nhãn tab đếm số việc cần để ý (chờ trả, đang làm, nhận được).</item>
    /// <item>Danh sách bên trái: vạch màu loại, tên, dòng trạng thái; dấu thoi = việc đang chỉ đường.</item>
    /// <item>Chi tiết bên phải: loại / chương / người giao, trạng thái, bước tiếp theo, lời kể, mục tiêu có ô tích và tiến
    /// độ, phần thưởng dự kiến. Nút Chỉ đường (theo dõi việc này) và Bỏ nhiệm vụ.</item>
    /// </list>
    /// Dữ liệu và luật lấy ở <see cref="QuestSystem"/>; panel tự vẽ lại khi QuestSystem.Changed bắn trong lúc đang mở.
    /// </summary>
    public class QuestLogPanel : PanelBase
    {
        enum Tab { Main = 0, Side = 1, Daily = 2, Bonds = 3 }

        static string TabName(Tab t)
        {
            switch (t)
            {
                case Tab.Main: return Lang.GetSplitText("Cốt truyện|Story");
                case Tab.Side: return Lang.GetSplitText("Việc phụ|Side");
                case Tab.Daily: return Lang.GetSplitText("Hằng ngày|Daily");
                default: return Lang.GetSplitText("Quan hệ|Relations");
            }
        }

        /// <summary>Chữ trong dữ liệu (tên, lời thoại...) ở ngôn ngữ đang chọn.</summary>
        static string T(string s) => string.IsNullOrEmpty(s) ? s : Lang.GetSplitText(s);

        public static readonly Color MainColor = new Color(1f, 0.78f, 0.34f);
        public static readonly Color SideColor = new Color(0.55f, 0.78f, 1f);
        public static readonly Color DailyColor = new Color(0.49f, 0.89f, 0.55f);
        static readonly Color TabOn = Color.white;
        static readonly Color TabOff = new Color(0.55f, 0.5f, 0.72f);
        static readonly Color TextOnDark = new Color(0.92f, 0.94f, 1f);

        [Header("Khung")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [Tooltip("Bốn nút tab theo thứ tự: Cốt truyện, Việc phụ, Hằng ngày, Quan hệ.")]
        [SerializeField] TextBtn[] tabButtons = new TextBtn[4];

        [Header("Danh sách")]
        [SerializeField] ScrollRect listScroll;
        [SerializeField] RectTransform listContent;
        [Tooltip("Dòng mẫu (tắt sẵn), nhân bản cho từng nhiệm vụ / NPC.")]
        [SerializeField] QuestListRow rowTemplate;
        [Tooltip("Tiêu đề nhóm mẫu (tắt sẵn): \"Chương 1\", \"Đang làm\"...")]
        [SerializeField] TMP_Text sectionTemplate;
        [Tooltip("Hiện khi tab không có dòng nào.")]
        [SerializeField] TMP_Text emptyText;

        [Header("Chi tiết")]
        [SerializeField] ScrollRect detailScroll;
        [Tooltip("Phần chi tiết; ẩn khi chưa chọn gì.")]
        [SerializeField] GameObject detailRoot;
        [SerializeField] TMP_Text detailTitle;
        [SerializeField] TMP_Text detailMeta;
        [SerializeField] TMP_Text detailStatus;
        [SerializeField] TMP_Text detailNext;
        [SerializeField] TMP_Text detailStory;
        [SerializeField] TMP_Text objectivesHeader;
        [SerializeField] RectTransform objectivesRoot;
        [Tooltip("Dòng mục tiêu mẫu (tắt sẵn).")]
        [SerializeField] QuestObjectiveRow objectiveTemplate;
        [SerializeField] TMP_Text rewardsHeader;
        [SerializeField] TMP_Text rewardsText;

        [Header("Nút")]
        [Tooltip("Theo dõi / chỉ đường tới việc đang chọn; bấm lại để thôi theo.")]
        [SerializeField] TextBtn trackButton;
        [Tooltip("Bỏ việc đang chọn (chỉ hiện ở việc đang làm).")]
        [SerializeField] TextBtn abandonButton;

        readonly List<QuestTemplate> quests = new List<QuestTemplate>(32);
        readonly List<QuestListRow> rows = new List<QuestListRow>(32);
        readonly List<TMP_Text> sections = new List<TMP_Text>(8);
        readonly List<QuestObjectiveRow> objectiveRows = new List<QuestObjectiveRow>(8);
        readonly StringBuilder text = new StringBuilder(512);

        Tab tab = Tab.Main;
        string selected;            // QuestTemplate.name hoặc NpcTemplate.name của dòng đang chọn
        string shownDetail;         // dòng đang hiện chi tiết: đổi dòng thì cuộn chi tiết về đầu
        int usedRows, usedSections, order;

        static string pendingQuest;
        static QuestLogPanel openPanel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pendingQuest = null;
            openPanel = null;
        }

        /// <summary>Mở cửa sổ nhiệm vụ, chọn sẵn nhiệm vụ q (null = việc đang theo dõi).</summary>
        public static void Open(QuestTemplate q)
        {
            pendingQuest = q?.name;
            if (openPanel != null && openPanel.IsOpen)
            {
                openPanel.FocusPending();
                return;
            }
            if (UIManager.Instance != null) UIManager.Instance.SetVisible(UIID.QuestLog, true);
        }

        bool inited;

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            if (trackButton) trackButton.AddListener(ToggleTrack);
            if (abandonButton) abandonButton.AddListener(ConfirmAbandon);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (!tabButtons[i]) continue;
                Tab t = (Tab)i;
                tabButtons[i].AddListener(() => SetTab(t));
            }
            if (rowTemplate) rowTemplate.gameObject.SetActive(false);
            if (sectionTemplate) sectionTemplate.gameObject.SetActive(false);
            if (objectiveTemplate) objectiveTemplate.gameObject.SetActive(false);
            inited = true;
            OnLoadLang();   // chữ cố định (tiêu đề phần, nút) có ngay cả khi panel dựng lúc đang tắt
        }

        public override void OnShow()
        {
            base.OnShow();
            openPanel = this;
            FocusPending();
        }

        public override void OnClose()
        {
            if (openPanel == this) openPanel = null;
            base.OnClose();
        }

        // mở lên: chọn việc được nhờ mở (Open), không thì việc đang theo dõi, không nữa thì việc đáng làm nhất
        void FocusPending()
        {
            QuestDatabase db = QuestDatabase.Current;
            QuestTemplate q = pendingQuest != null && db != null ? db.Find(pendingQuest) : null;
            pendingQuest = null;
            if (q == null) q = QuestSystem.Tracked ?? QuestSystem.Suggest();
            tab = q != null ? TabOf(q) : Tab.Main;
            selected = q?.name;
            shownDetail = null;
            Rebuild();
            if (listScroll) listScroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            QuestSystem.Changed += Rebuild;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            QuestSystem.Changed -= Rebuild;
        }

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Nhiệm vụ|Quests");
            if (closeButton) closeButton.SetText("X");
            if (objectivesHeader) objectivesHeader.text = Lang.GetSplitText("Mục tiêu|Objectives");
            if (rewardsHeader) rewardsHeader.text = Lang.GetSplitText("Phần thưởng|Rewards");
            if (abandonButton) abandonButton.SetText(Lang.GetSplitText("Bỏ nhiệm vụ|Abandon"));
            if (inited && isActiveAndEnabled) Rebuild();
        }

        static Tab TabOf(QuestTemplate q) =>
            q.category == QuestCategory.Main ? Tab.Main : q.category == QuestCategory.Daily ? Tab.Daily : Tab.Side;

        void SetTab(Tab t)
        {
            if (tab == t) return;
            tab = t;
            selected = null;
            shownDetail = null;
            Rebuild();
            if (listScroll) listScroll.verticalNormalizedPosition = 1f;
        }

        // ---------------- nhãn chung (khung theo dõi dùng lại) ----------------

        public static Color CategoryColor(QuestTemplate q) =>
            q.category == QuestCategory.Main ? MainColor : q.category == QuestCategory.Daily ? DailyColor : SideColor;

        /// <summary>Nhãn loại có màu: [Chính] vàng, [Phụ] xanh lam, [Ngày] xanh lục.</summary>
        public static string CategoryTag(QuestTemplate q)
        {
            switch (q.category)
            {
                case QuestCategory.Main: return "<color=#FFC857>" + Lang.GetSplitText("[Chính]|[Main]") + "</color>";
                case QuestCategory.Daily: return "<color=#7CE38B>" + Lang.GetSplitText("[Ngày]|[Daily]") + "</color>";
                default: return "<color=#8CC8FF>" + Lang.GetSplitText("[Phụ]|[Side]") + "</color>";
            }
        }

        /// <summary>"Cốt truyện - Chương 2" / "Việc phụ" / "Hằng ngày".</summary>
        public static string CategoryName(QuestTemplate q)
        {
            switch (q.category)
            {
                case QuestCategory.Main:
                    return q.chapter > 0 ? Lang.Format("Cốt truyện - Chương {0}|Story - Chapter {0}", q.chapter) : Lang.GetSplitText("Cốt truyện|Story");
                case QuestCategory.Daily: return Lang.GetSplitText("Hằng ngày|Daily");
                default: return Lang.GetSplitText("Việc phụ|Side quest");
            }
        }

        // ---------------- danh sách ----------------

        void Rebuild()
        {
            if (!inited || QuestDatabase.Current == null) return;
            PaintTabs();

            usedRows = usedSections = order = 0;
            bool keep = false;
            if (tab == Tab.Bonds) keep = BuildNpcRows();
            else if (tab == Tab.Main) keep = BuildMainRows();
            else keep = BuildStatusRows(tab == Tab.Daily ? QuestCategory.Daily : QuestCategory.Side);
            for (int i = usedRows; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            for (int i = usedSections; i < sections.Count; i++) sections[i].gameObject.SetActive(false);

            if (emptyText)
            {
                emptyText.gameObject.SetActive(usedRows == 0);
                emptyText.text = EmptyLine();
            }
            // dòng đang chọn không còn trong tab: chọn việc đang theo dõi, không thì việc chưa xong đầu tiên, không nữa thì dòng đầu
            if (!keep) selected = DefaultRow();
            for (int i = 0; i < usedRows; i++) PaintRowSelection(rows[i]);
            ShowDetail();
        }

        void PaintTabs()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                TextBtn b = tabButtons[i];
                if (!b) continue;
                bool on = i == (int)tab;
                Paint(b, on ? TabOn : TabOff);
                b.SetColorText(on ? UIColors.TextSelected : TextOnDark);
                int n = i < 3 ? CountActionable((Tab)i) : 0;
                b.SetText(n > 0 ? TabName((Tab)i) + " <size=80%>(" + n + ")</size>" : TabName((Tab)i));
            }
        }

        static int CountActionable(Tab t)
        {
            QuestDatabase db = QuestDatabase.Current;
            if (db == null) return 0;
            int n = 0;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null || TabOf(q) != t) continue;
                QuestStatus st = QuestSystem.StatusOf(q);
                if (st == QuestStatus.Ready || st == QuestStatus.Active || (st == QuestStatus.Available && QuestSystem.TierAllows(q))) n++;
            }
            return n;
        }

        // cốt truyện: theo chương, trong chương theo thứ tự của bảng; việc chưa mở thì ẩn
        bool BuildMainRows()
        {
            QuestDatabase db = QuestDatabase.Current;
            bool keep = false;
            int maxChapter = 0;
            foreach (QuestTemplate q in db.quests) if (q != null && q.category == QuestCategory.Main) maxChapter = Mathf.Max(maxChapter, q.chapter);
            for (int c = 0; c <= maxChapter; c++)
            {
                bool header = false;
                foreach (QuestTemplate q in db.quests)
                {
                    if (q == null || q.category != QuestCategory.Main || q.chapter != c) continue;
                    QuestStatus st = QuestSystem.StatusOf(q);
                    if (st == QuestStatus.Locked) continue;
                    if (!header)
                    {
                        Section(c > 0 ? Lang.Format("Chương {0}|Chapter {0}", c) : Lang.GetSplitText("Cốt truyện|Story"));
                        header = true;
                    }
                    keep |= QuestRow(q, st);
                }
            }
            return keep;
        }

        static readonly QuestStatus[] StatusOrder = { QuestStatus.Ready, QuestStatus.Active, QuestStatus.Available, QuestStatus.Done };

        // việc phụ / hằng ngày: nhóm theo trạng thái, việc nên làm ngay lên trước
        bool BuildStatusRows(QuestCategory category)
        {
            QuestDatabase db = QuestDatabase.Current;
            bool keep = false;
            foreach (QuestStatus want in StatusOrder)
            {
                bool header = false;
                foreach (QuestTemplate q in db.quests)
                {
                    if (q == null || q.category != category) continue;
                    QuestStatus st = QuestSystem.StatusOf(q);
                    if (st != want) continue;
                    if (!header)
                    {
                        Section(SectionName(want));
                        header = true;
                    }
                    keep |= QuestRow(q, st);
                }
            }
            return keep;
        }

        static string SectionName(QuestStatus st)
        {
            switch (st)
            {
                case QuestStatus.Ready: return Lang.GetSplitText("Chờ trả|Ready to turn in");
                case QuestStatus.Active: return Lang.GetSplitText("Đang làm|In progress");
                case QuestStatus.Available: return Lang.GetSplitText("Có thể nhận|Available");
                default: return Lang.GetSplitText("Đã hoàn thành|Completed");
            }
        }

        bool BuildNpcRows()
        {
            NpcDatabase db = NpcDatabase.Current;
            bool keep = false;
            if (db == null) return false;
            foreach (NpcTemplate n in db.npcs)
            {
                if (n == null) continue;
                AffinityTier tier = QuestSystem.TierOf(n);
                QuestListRow row = NextRow(n.name);
                row.Set(T(n.Label) + (string.IsNullOrEmpty(n.role) ? "" : "  <size=75%><color=#AAB4C8>" + T(n.role) + "</color></size>"),
                    "<color=" + TierColor(tier) + ">" + Affinity.NameOf(tier) + "</color>  " + QuestSystem.AffinityOf(n) + "/" + Affinity.Max,
                    TierStripe(tier), n.name == selected, false);
                keep |= n.name == selected;
            }
            return keep;
        }

        bool QuestRow(QuestTemplate q, QuestStatus st)
        {
            QuestListRow row = NextRow(q.name);
            row.Set(T(q.title), StatusLine(q, st), CategoryColor(q), q.name == selected, QuestSystem.IsTracked(q), st == QuestStatus.Done ? 0.55f : 1f);
            return q.name == selected;
        }

        /// <summary>Dòng trạng thái dưới tên: bước tiếp / chờ trả / ai giao / đã xong.</summary>
        static string StatusLine(QuestTemplate q, QuestStatus st)
        {
            switch (st)
            {
                case QuestStatus.Ready:
                    return "<color=#8CF08C>" + Lang.Format("Xong - gặp {0}|Done - see {0}", QuestSystem.LabelOf(q.TurnInNpc)) + "</color>";
                case QuestStatus.Active:
                    return "<color=#7FD8FF>" + QuestSystem.GoalOf(q).label + "</color>";
                case QuestStatus.Available:
                    if (QuestSystem.TierAllows(q))
                        return "<color=#FFD966>" + Lang.Format("Gặp {0} để nhận|See {0}", QuestSystem.LabelOf(q.giver)) + "</color>";
                    return "<color=#FF8C8C>" + Lang.Format("Cần {0} với {1}|Needs {0} with {1}", Affinity.NameOf(q.minTier), QuestSystem.LabelOf(q.giver)) + "</color>";
                default:
                    int times = QuestSystem.TimesDone(q);
                    string s = q.Recurring && times > 1 ? Lang.Format("Đã xong {0} lần|Done {0} times", times) : Lang.GetSplitText("Đã hoàn thành|Completed");
                    if (q.IsDaily) s += " - " + Lang.GetSplitText("mai làm tiếp|again tomorrow");
                    QuestOutcome o = QuestSystem.OutcomeOf(q);
                    if (o != null && !string.IsNullOrEmpty(o.ending)) s = Lang.Format("Kết cục: {0}|Ending: {0}", T(o.ending));
                    return "<color=#AAB4C8>" + s + "</color>";
            }
        }

        QuestListRow NextRow(string key)
        {
            QuestListRow row;
            if (usedRows < rows.Count) row = rows[usedRows];
            else
            {
                row = Instantiate(rowTemplate, listContent);
                row.button.AddListener(() => Select(row.key));
                rows.Add(row);
            }
            usedRows++;
            row.key = key;
            row.gameObject.SetActive(true);
            row.transform.SetSiblingIndex(order++);
            return row;
        }

        void Section(string label)
        {
            TMP_Text s;
            if (usedSections < sections.Count) s = sections[usedSections];
            else
            {
                s = Instantiate(sectionTemplate, listContent);
                sections.Add(s);
            }
            usedSections++;
            s.text = label;
            s.gameObject.SetActive(true);
            s.transform.SetSiblingIndex(order++);
        }

        string DefaultRow()
        {
            if (usedRows == 0) return null;
            if (tab == Tab.Bonds) return rows[0].key;
            string firstOpen = null;
            for (int i = 0; i < usedRows; i++)
            {
                QuestTemplate q = FindQuest(rows[i].key);
                if (q == null) continue;
                if (QuestSystem.IsTracked(q)) return q.name;
                if (firstOpen == null && QuestSystem.StatusOf(q) != QuestStatus.Done) firstOpen = q.name;
            }
            return firstOpen ?? rows[0].key;
        }

        void PaintRowSelection(QuestListRow row)
        {
            if (row.background) row.background.color = row.key == selected ? QuestListRow.Selected : QuestListRow.Normal;
        }

        string EmptyLine()
        {
            switch (tab)
            {
                case Tab.Main: return Lang.GetSplitText("Chưa có nhiệm vụ cốt truyện.|No story quests yet.");
                case Tab.Side: return Lang.GetSplitText("Chưa có việc phụ.\nNPC có dấu (!) trên đầu đang có việc mới.|No side quests.\nNPCs marked (!) have new ones.");
                case Tab.Daily: return Lang.GetSplitText("Việc hằng ngày mở khi đi tiếp cốt truyện.|Daily quests unlock as the story goes on.");
                default: return Lang.GetSplitText("Chưa gặp ai.|You haven't met anyone yet.");
            }
        }

        void Select(string key)
        {
            selected = key;
            for (int i = 0; i < usedRows; i++) PaintRowSelection(rows[i]);
            ShowDetail();
        }

        // ---------------- chi tiết ----------------

        void ShowDetail()
        {
            QuestTemplate quest = tab != Tab.Bonds ? FindQuest(selected) : null;
            NpcTemplate npc = tab == Tab.Bonds && selected != null ? QuestSystem.NpcOf(selected) : null;
            bool any = quest != null || npc != null;
            if (detailRoot) detailRoot.SetActive(any);

            QuestStatus st = quest != null ? QuestSystem.StatusOf(quest) : QuestStatus.Locked;
            if (abandonButton) abandonButton.gameObject.SetActive(quest != null && (st == QuestStatus.Active || st == QuestStatus.Ready));
            if (trackButton)
            {
                bool canTrack = quest != null && (st == QuestStatus.Active || st == QuestStatus.Ready || st == QuestStatus.Available);
                trackButton.gameObject.SetActive(canTrack);
                if (canTrack)
                {
                    bool tracked = QuestSystem.IsTracked(quest);
                    trackButton.SetText(tracked ? Lang.GetSplitText("Bỏ theo dõi|Untrack") : Lang.GetSplitText("Chỉ đường|Guide me"));
                    Paint(trackButton, tracked ? TabOff : TabOn);
                }
            }
            if (!any) return;

            if (quest != null) QuestDetail(quest, st);
            else NpcDetail(npc);

            if (detailScroll && shownDetail != selected) detailScroll.verticalNormalizedPosition = 1f;
            shownDetail = selected;
        }

        void QuestDetail(QuestTemplate q, QuestStatus st)
        {
            SetText(detailTitle, T(q.title));

            text.Clear();
            text.Append(CategoryTag(q)).Append(' ').Append(CategoryName(q)).Append("<color=#AAB4C8>   ")
                .Append(Lang.Format("Giao: {0}|From: {0}", QuestSystem.LabelOf(q.giver)));
            if (q.TurnInNpc != q.giver) text.Append("   ").Append(Lang.Format("Trả: {0}|Turn in: {0}", QuestSystem.LabelOf(q.TurnInNpc)));
            if (q.repeatable) text.Append("   ").Append(Lang.GetSplitText("(lặp lại)|(repeatable)"));
            text.Append("</color>");
            SetText(detailMeta, text.ToString());

            SetText(detailStatus, StatusDetail(q, st));

            bool trackable = st == QuestStatus.Active || st == QuestStatus.Ready || st == QuestStatus.Available;
            string next = null;
            if (trackable)
            {
                next = "» " + Lang.Format("Bước tiếp: {0}|Next: {0}", QuestSystem.GoalOf(q).label);
                if (QuestSystem.IsTracked(q)) next += "  <size=80%><color=#AAB4C8>" + Lang.GetSplitText("(đang chỉ đường)|(guiding)") + "</color></size>";
            }
            SetText(detailNext, next);

            // đã xong: kể lại đoạn kết (theo cách giải quyết đã chọn); còn lại: lời giao việc
            string[] pages = q.offer;
            if (st == QuestStatus.Done)
            {
                QuestOutcome o = QuestSystem.OutcomeOf(q);
                pages = o != null && o.complete != null && o.complete.Length > 0 ? o.complete : q.complete;
            }
            text.Clear();
            if (pages != null)
                foreach (string p in pages)
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    if (text.Length > 0) text.Append("\n\n");
                    text.Append(T(p));
                }
            SetText(detailStory, text.Length > 0 ? "<i>" + text + "</i>" : null);

            bool showObjectives = st == QuestStatus.Active || st == QuestStatus.Ready || st == QuestStatus.Available;
            if (objectivesHeader) objectivesHeader.gameObject.SetActive(showObjectives);
            if (objectivesRoot) objectivesRoot.gameObject.SetActive(showObjectives);
            if (showObjectives) FillObjectives(q, st);

            bool showRewards = st != QuestStatus.Done;
            if (rewardsHeader) rewardsHeader.gameObject.SetActive(showRewards);
            SetText(rewardsText, showRewards ? QuestSystem.RewardPreview(q) : null);
        }

        static string StatusDetail(QuestTemplate q, QuestStatus st)
        {
            switch (st)
            {
                case QuestStatus.Ready:
                    return "<color=#8CF08C>" + Lang.Format("Đã đủ mục tiêu - đến gặp {0} để trả.|Objectives done - see {0} to turn in.", QuestSystem.LabelOf(q.TurnInNpc)) + "</color>";
                case QuestStatus.Active:
                    return "<color=#FFD966>" + Lang.GetSplitText("Đang làm|In progress") + "</color>";
                case QuestStatus.Available:
                    if (QuestSystem.TierAllows(q))
                        return "<color=#FFD966>" + Lang.Format("Gặp {0} để nhận việc.|See {0} to accept it.", QuestSystem.LabelOf(q.giver)) + "</color>";
                    return "<color=#FF8C8C>" + Lang.Format("Chưa nhận được: cần {0} với {1} (hiện {2}).|Locked: needs {0} with {1} (currently {2}).",
                        Affinity.NameOf(q.minTier), QuestSystem.LabelOf(q.giver), Affinity.NameOf(QuestSystem.TierOf(QuestSystem.NpcOf(q.giver)))) + "</color>";
                default:
                    int times = QuestSystem.TimesDone(q);
                    string s = times > 1 ? Lang.Format("Đã hoàn thành {0} lần.|Completed {0} times.", times) : Lang.GetSplitText("Đã hoàn thành.|Completed.");
                    if (q.IsDaily) s += " " + Lang.GetSplitText("Mai quay lại nhận tiếp.|Come back tomorrow.");
                    QuestOutcome o = QuestSystem.OutcomeOf(q);
                    if (o != null)
                    {
                        s += "\n" + Lang.Format("Đã chọn: <b>{0}</b>|Chosen: <b>{0}</b>", T(o.label));
                        if (!string.IsNullOrEmpty(o.ending)) s += "\n<color=#FFD966>" + Lang.Format("Kết cục: {0}|Ending: {0}", T(o.ending)) + "</color>";
                    }
                    return "<color=#8CF08C>" + s + "</color>";
            }
        }

        void FillObjectives(QuestTemplate q, QuestStatus st)
        {
            int used = 0;
            if (q.objectives.Count == 0)
            {
                // việc đưa tin: mục tiêu duy nhất là tới gặp người nhận
                bool ready = st == QuestStatus.Ready;
                ObjectiveRow(used++).Set(Lang.Format("Đến gặp {0}|Go see {0}", QuestSystem.LabelOf(q.TurnInNpc)), ready ? 1 : 0, 1, !ready);
            }
            else
            {
                bool nextMarked = false;
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    QuestObjective o = q.objectives[i];
                    // việc chưa nhận: tiến độ Kill / Reach / Talk chưa tính, Collect thì vẫn đếm túi
                    int have = st == QuestStatus.Available && o.Tracked ? 0 : QuestSystem.ProgressOf(q, i);
                    bool next = !nextMarked && have < o.count && st == QuestStatus.Active;
                    nextMarked |= next;
                    ObjectiveRow(used++).Set(QuestSystem.LabelOf(o), have, o.count, next);
                }
            }
            for (int i = used; i < objectiveRows.Count; i++) objectiveRows[i].gameObject.SetActive(false);
        }

        QuestObjectiveRow ObjectiveRow(int index)
        {
            while (objectiveRows.Count <= index) objectiveRows.Add(Instantiate(objectiveTemplate, objectivesRoot));
            QuestObjectiveRow row = objectiveRows[index];
            row.gameObject.SetActive(true);
            return row;
        }

        void NpcDetail(NpcTemplate npc)
        {
            int value = QuestSystem.AffinityOf(npc);
            AffinityTier tier = QuestSystem.TierOf(npc);   // tính cả cờ kẻ thù

            SetText(detailTitle, T(npc.Label));
            SetText(detailMeta, string.IsNullOrEmpty(npc.role) ? null : "<color=#AAB4C8>" + T(npc.role) + "</color>");
            SetText(detailStatus, Lang.Format("Thân thiện: <b>{0}</b> {1}/{2}|Affinity: <b>{0}</b> {1}/{2}", Affinity.NameOf(tier), value, Affinity.Max)
                                  + "\n" + Bar(value));
            SetText(detailNext, null);
            if (objectivesHeader) objectivesHeader.gameObject.SetActive(false);
            if (objectivesRoot) objectivesRoot.gameObject.SetActive(false);
            if (rewardsHeader) rewardsHeader.gameObject.SetActive(false);
            SetText(rewardsText, null);

            text.Clear();
            if (tier == AffinityTier.Enemy)
            {
                text.Append("<color=#FF8C8C>").Append(Lang.GetSplitText("Đã thành kẻ thù: không thèm nói chuyện, không giao việc, không nhận quà. Không gỡ lại được.|An enemy: won't talk, won't give quests, won't take gifts. This is permanent.")).Append("</color>");
                SetText(detailStory, text.ToString());
                return;
            }
            if (tier == AffinityTier.Disliked)
                text.Append("<color=#FF8C8C>").Append(Lang.Format("Ác cảm: vẫn nói chuyện nhưng không giao việc. Tụt tới {0} là thành kẻ thù vĩnh viễn.|Disliked: still talks but gives no quests. At {0} they become a permanent enemy.", Affinity.EnemyAt))
                    .Append("</color>\n");
            if (tier < AffinityTier.Bonded)
            {
                AffinityTier next = tier + 1;
                text.Append(Lang.Format("Còn {0} điểm nữa lên {1}|{0} more to reach {1}", Affinity.FloorOf(next) - value, Affinity.NameOf(next))).Append('\n');
            }
            text.Append(Lang.Format("Thưởng nhiệm vụ x{0}   Giá dịch vụ x{1}|Quest rewards x{0}   Service prices x{1}",
                Affinity.RewardScaleOf(tier).ToString("0.##"), Affinity.PriceScaleOf(tier).ToString("0.##"))).Append('\n');

            if (npc.gifts != null && npc.gifts.Count > 0)
            {
                text.Append("\n<b>").Append(Lang.GetSplitText("Thích nhận|Likes")).Append("</b>\n");
                for (int i = 0; i < npc.gifts.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(QuestSystem.ItemLabel(npc.gifts[i].item)).Append(" (+").Append(npc.gifts[i].affinity).Append(')');
                }
                text.Append('\n');
            }

            CountQuests(npc, out int active, out int available, out int done);
            text.Append("\n<b>").Append(Lang.GetSplitText("Nhiệm vụ|Quests")).Append("</b>\n")
                .Append(Lang.Format("{0} đang làm, {1} có thể nhận, {2} đã xong|{0} active, {1} available, {2} completed", active, available, done));
            SetText(detailStory, text.ToString());
        }

        static void CountQuests(NpcTemplate npc, out int active, out int available, out int done)
        {
            active = available = done = 0;
            QuestDatabase db = QuestDatabase.Current;
            if (db == null) return;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null || q.giver != npc.name) continue;
                QuestStatus st = QuestSystem.StatusOf(q);
                if (st == QuestStatus.Active || st == QuestStatus.Ready) active++;
                else if (st == QuestStatus.Available) available++;
                if (QuestSystem.TimesDone(q) > 0) done++;
            }
        }

        // thanh 20 vạch: thân thiện dương tô hồng; âm (ác cảm / kẻ thù) tô đỏ theo độ ghét; phần còn lại mờ
        static string Bar(int value)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(value) / 5f), 0, 20);
            string color = value < 0 ? "#FF6060" : "#FF9EB8";
            return "<color=" + color + ">" + new string('|', filled) + "</color><color=#FFFFFF40>" + new string('|', 20 - filled) + "</color>";
        }

        static string TierColor(AffinityTier tier) =>
            tier <= AffinityTier.Disliked ? "#FF8C8C" : tier >= AffinityTier.Trusted ? "#FF9EB8" : tier >= AffinityTier.Friendly ? "#8CF08C" : "#C8D2E6";

        static Color TierStripe(AffinityTier tier)
        {
            ColorUtility.TryParseHtmlString(TierColor(tier), out Color c);
            return c;
        }

        static void SetText(TMP_Text t, string value)
        {
            if (!t) return;
            bool show = !string.IsNullOrEmpty(value);
            if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
            if (show) t.text = value;
        }

        /// <summary>Tô nút phẳng (Image trắng + bộ màu của Button) bằng một màu.</summary>
        public static void Paint(Button b, Color c)
        {
            ColorBlock cb = b.colors;
            cb.normalColor = c;
            cb.selectedColor = c;
            cb.highlightedColor = Color.Lerp(c, Color.white, 0.15f);
            cb.pressedColor = Color.Lerp(c, Color.black, 0.2f);
            b.colors = cb;
        }

        QuestTemplate FindQuest(string key) =>
            key != null && QuestDatabase.Current != null ? QuestDatabase.Current.Find(key) : null;

        // ---------------- nút ----------------

        void ToggleTrack()
        {
            QuestTemplate q = FindQuest(selected);
            if (q == null) return;
            if (QuestSystem.IsTracked(q))
            {
                QuestSystem.Track(null);
                return;
            }
            if (QuestSystem.Track(q))
                Notify.Toast(Lang.Format("Chỉ đường: {0}|Guiding: {0}", T(q.title)), 2f);
        }

        void ConfirmAbandon()
        {
            QuestTemplate q = FindQuest(selected);
            if (q == null) return;
            PopupMN.Confirm(Lang.Format("Bỏ nhiệm vụ \"{0}\"?|Abandon \"{0}\"?", T(q.title)) + "\n<size=80%>"
                            + Lang.Format("{0} sẽ phật lòng (thân thiện -{1}).|{0} will be upset (affinity -{1}).", QuestSystem.LabelOf(q.giver), Affinity.AbandonPenalty)
                            + "</size>", () => QuestSystem.Abandon(q));
        }
    }
}
