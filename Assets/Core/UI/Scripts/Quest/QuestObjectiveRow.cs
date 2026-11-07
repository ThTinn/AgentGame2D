using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Một mục tiêu trong phần chi tiết của <see cref="QuestLogPanel"/>: ô tích, mô tả, tiến độ bên phải.</summary>
    public class QuestObjectiveRow : MonoBehaviour
    {
        [Tooltip("Ô vuông viền.")]
        public Image box;
        [Tooltip("Phần tô trong ô: hiện khi mục tiêu xong.")]
        public Image check;
        public TMP_Text label;
        public TMP_Text count;

        static readonly Color Done = new Color(0.55f, 0.94f, 0.55f);
        static readonly Color Todo = new Color(0.85f, 0.88f, 0.95f);
        static readonly Color Next = new Color(0.5f, 0.85f, 1f);

        /// <param name="next">mục tiêu đang chỉ đường tới (mục tiêu đầu tiên chưa xong)</param>
        public void Set(string text, int have, int need, bool next)
        {
            bool done = have >= need;
            Color c = done ? Done : next ? Next : Todo;
            if (check) check.gameObject.SetActive(done);
            if (box) box.color = c;
            if (label)
            {
                label.text = done ? "<s>" + text + "</s>" : text;
                label.color = c;
            }
            if (count)
            {
                count.text = need > 1 ? have + "/" + need : done ? StoryOk : "";
                count.color = c;
            }
        }

        static string StoryOk => Lang.GetSplitText("Xong|Done");
    }
}
