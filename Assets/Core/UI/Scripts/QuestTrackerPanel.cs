using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Game.Core;
using Game.Story;

namespace Game.UI
{
    /// <summary>
    /// Khung theo dõi nhiệm vụ ở góc phải HUD, ngay dưới minimap (UIID.QuestTracker, IsHud: UIManager mở sẵn lúc vào game).
    /// Prefab dựng bằng Tools/Core/UI/Build Quest UI (QuestUIBuilder).
    /// <list type="bullet">
    /// <item>Việc đang theo dõi (QuestSystem.Tracked, có mũi tên chỉ đường) lên đầu, nền sáng + dấu thoi, ghi bước tiếp
    /// theo; rồi việc chờ trả, rồi việc đang làm kèm tiến độ. Mỗi dòng có vạch màu loại (Chính / Phụ / Ngày).</item>
    /// <item>Chạm một dòng: chỉ đường tới việc đó; chạm lại dòng đang chỉ đường: mở cửa sổ nhiệm vụ ở việc đó.</item>
    /// <item>Nút Mở: cửa sổ nhiệm vụ. Nút – / +: thu gọn chỉ còn tiêu đề (nhớ trong PlayerPrefs).</item>
    /// </list>
    /// Không có việc nào thì ẩn cả khung. Vẽ lại mỗi khi <see cref="QuestSystem.Changed"/> bắn.
    /// </summary>
    public class QuestTrackerPanel : PanelBase
    {
        [Header("Theo dõi nhiệm vụ")]
        [Tooltip("Phần hiện được (nền + chữ): ẩn khi không có việc nào. Gốc panel giữ nguyên để UIManager bật/tắt.")]
        [SerializeField] GameObject body;
        [SerializeField] TMP_Text headerText;
        [Tooltip("Mở cửa sổ nhiệm vụ.")]
        [SerializeField] TextBtn openButton;
        [Tooltip("Thu gọn / mở danh sách.")]
        [SerializeField] TextBtn collapseButton;
        [Tooltip("Cha của các dòng; ẩn khi thu gọn.")]
        [SerializeField] RectTransform rowsRoot;
        [Tooltip("Dòng mẫu (tắt sẵn).")]
        [SerializeField] QuestTrackerRow rowTemplate;
        [Tooltip("\"+ n việc khác\" khi nhiều hơn maxShown.")]
        [SerializeField] TMP_Text moreText;
        [Tooltip("Hiện tối đa chừng này việc.")]
        [SerializeField, Min(1)] int maxShown = 3;

        const string CollapsedKey = "hud.quest.collapsed";

        readonly List<QuestTemplate> quests = new List<QuestTemplate>(8);
        readonly List<QuestTemplate> active = new List<QuestTemplate>(8);
        readonly List<QuestTrackerRow> rows = new List<QuestTrackerRow>(4);
        bool collapsed;

        public override bool IsHud => true;

        public override void OnInit()
        {
            if (openButton) openButton.AddListener(() => QuestLogPanel.Open(null));
            if (collapseButton) collapseButton.AddListener(ToggleCollapsed);
            if (rowTemplate) rowTemplate.gameObject.SetActive(false);
            collapsed = PlayerPrefs.GetInt(CollapsedKey, 0) == 1;
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            QuestSystem.Changed += Refresh;
            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            QuestSystem.Changed -= Refresh;
        }

        void ToggleCollapsed()
        {
            collapsed = !collapsed;
            PlayerPrefs.SetInt(CollapsedKey, collapsed ? 1 : 0);
            Refresh();
        }

        public void Refresh()
        {
            // việc đang theo dõi lên đầu (kể cả việc chưa nhận: chỉ đường tới người giao); rồi chờ trả, rồi đang làm
            QuestTemplate tracked = QuestSystem.Tracked;
            QuestSystem.QuestsWith(quests, QuestStatus.Ready);
            QuestSystem.QuestsWith(active, QuestStatus.Active);
            quests.AddRange(active);
            if (tracked != null)
            {
                quests.Remove(tracked);
                quests.Insert(0, tracked);
            }

            bool any = quests.Count > 0;
            if (body && body.activeSelf != any) body.SetActive(any);
            if (!any) return;

            if (headerText) headerText.text = Lang.Format("Nhiệm vụ ({0})|Quests ({0})", quests.Count);
            if (collapseButton) collapseButton.SetText(collapsed ? "+" : "–");
            if (rowsRoot) rowsRoot.gameObject.SetActive(!collapsed);

            int shown = collapsed || !rowTemplate || !rowsRoot ? 0 : Mathf.Min(quests.Count, maxShown);
            for (int i = 0; i < shown; i++)
            {
                QuestTemplate q = quests[i];
                QuestTrackerRow row = Row(i);
                row.key = q.name;
                row.Set("<b>" + T(q.title) + "</b>", Detail(q, q == tracked), QuestLogPanel.CategoryColor(q), q == tracked);
            }
            for (int i = shown; i < rows.Count; i++) rows[i].gameObject.SetActive(false);

            if (moreText)
            {
                bool more = !collapsed && quests.Count > shown;
                moreText.gameObject.SetActive(more);
                if (more) moreText.text = Lang.Format("+ {0} việc khác|+ {0} more", quests.Count - shown);
            }
        }

        static string Detail(QuestTemplate q, bool tracked)
        {
            if (tracked) return "<color=#7FD8FF>» " + QuestSystem.GoalOf(q).label + "</color>";
            if (QuestSystem.StatusOf(q) == QuestStatus.Ready)
                return "<color=#8CF08C>" + Lang.Format("Xong - gặp {0}|Done - see {0}", QuestSystem.LabelOf(q.TurnInNpc)) + "</color>";
            return "<color=#C8D2E6>" + QuestSystem.ProgressText(q) + "</color>";
        }

        QuestTrackerRow Row(int index)
        {
            while (rows.Count <= index)
            {
                QuestTrackerRow row = Instantiate(rowTemplate, rowsRoot);
                row.button.AddListener(() => Tap(row.key));
                rows.Add(row);
            }
            QuestTrackerRow r = rows[index];
            r.gameObject.SetActive(true);
            r.transform.SetSiblingIndex(index + 1);   // sau dòng mẫu (đang tắt)
            return r;
        }

        // chạm dòng: chỉ đường tới việc đó; đang chỉ đường rồi thì mở chi tiết
        static void Tap(string key)
        {
            QuestTemplate q = key != null && QuestDatabase.Current != null ? QuestDatabase.Current.Find(key) : null;
            if (q == null) return;
            if (QuestSystem.IsTracked(q))
            {
                QuestLogPanel.Open(q);
                return;
            }
            if (QuestSystem.Track(q)) Notify.Toast(Lang.Format("Chỉ đường: {0}|Guiding: {0}", T(q.title)), 2f);
        }

        /// <summary>Đổi ngôn ngữ thì vẽ lại.</summary>
        public override void OnLoadLang()
        {
            if (openButton) openButton.SetText(Lang.GetSplitText("Mở|Open"));
            Refresh();
        }

        /// <summary>Chữ trong dữ liệu (tên nhiệm vụ, NPC) ở ngôn ngữ đang chọn.</summary>
        static string T(string s) => string.IsNullOrEmpty(s) ? s : Lang.GetSplitText(s);
    }
}
