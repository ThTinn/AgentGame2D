using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Một dòng trong danh sách của <see cref="QuestLogPanel"/>: vạch màu loại nhiệm vụ bên trái, tên, dòng trạng thái, dấu
    /// thoi khi đang theo dõi. Cũng dùng cho dòng NPC ở tab Quan hệ. Panel giữ pool, dòng không tự biết dữ liệu.
    /// </summary>
    public class QuestListRow : MonoBehaviour
    {
        public BaseBtn button;
        [Tooltip("Nền dòng (đổi màu khi đang chọn).")]
        public Image background;
        [Tooltip("Vạch dọc bên trái: màu theo loại nhiệm vụ.")]
        public Image stripe;
        public TMP_Text title;
        public TMP_Text status;
        [Tooltip("Dấu thoi: nhiệm vụ đang được theo dõi / chỉ đường.")]
        public Image trackedMark;

        /// <summary>Khóa của dòng (QuestTemplate.name hoặc NpcTemplate.name), panel gán khi vẽ.</summary>
        [System.NonSerialized] public string key;

        public static readonly Color Normal = new Color(0.15f, 0.11f, 0.28f, 1f);
        public static readonly Color Selected = new Color(0.31f, 0.24f, 0.51f, 1f);

        public void Set(string titleText, string statusText, Color stripeColor, bool selected, bool tracked, float alpha = 1f)
        {
            if (title) title.text = titleText;
            if (status)
            {
                status.text = statusText;
                status.gameObject.SetActive(!string.IsNullOrEmpty(statusText));
            }
            if (stripe) stripe.color = stripeColor;
            if (background) background.color = selected ? Selected : Normal;
            if (trackedMark) trackedMark.gameObject.SetActive(tracked);
            var group = GetComponent<CanvasGroup>();
            if (group) group.alpha = alpha;
        }
    }
}
