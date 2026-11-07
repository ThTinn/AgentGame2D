using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Một việc trong khung theo dõi ở HUD (<see cref="QuestTrackerPanel"/>): vạch màu loại, tên, bước tiếp / tiến độ.
    /// Việc đang chỉ đường có nền sáng hơn và dấu thoi.
    /// </summary>
    public class QuestTrackerRow : MonoBehaviour
    {
        public BaseBtn button;
        public Image background;
        public Image stripe;
        public TMP_Text title;
        public TMP_Text detail;
        public Image trackedMark;

        [System.NonSerialized] public string key;

        static readonly Color Normal = new Color(1f, 1f, 1f, 0.04f);
        static readonly Color Tracked = new Color(0.5f, 0.85f, 1f, 0.16f);

        public void Set(string titleText, string detailText, Color stripeColor, bool tracked)
        {
            if (title) title.text = titleText;
            if (detail) detail.text = detailText;
            if (stripe) stripe.color = stripeColor;
            if (background) background.color = tracked ? Tracked : Normal;
            if (trackedMark) trackedMark.gameObject.SetActive(tracked);
        }
    }
}
