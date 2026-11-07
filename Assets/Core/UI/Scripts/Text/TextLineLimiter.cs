using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Khung chữ chỉ giữ maxLines dòng mới nhất (chat, log). Dòng cũ nhất bị đẩy ra khi thêm dòng mới.</summary>
    public class TextLineLimiter : MonoBehaviour
    {
        public TextMeshProUGUI textMeshProComponent;
        public int maxLines = 10;

        readonly Queue<string> lines = new Queue<string>();

        void Awake()
        {
            if (!textMeshProComponent) textMeshProComponent = GetComponent<TextMeshProUGUI>();
        }

        public void ResetText()
        {
            lines.Clear();
            if (textMeshProComponent) textMeshProComponent.text = "";
        }

        public void AddNewLine(string newLine)
        {
            if (string.IsNullOrEmpty(newLine)) return;

            lines.Enqueue(newLine);
            while (lines.Count > Mathf.Max(1, maxLines)) lines.Dequeue();

            if (textMeshProComponent) textMeshProComponent.text = string.Join("\n", lines);
        }
    }
}
