using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Một ô của <see cref="RecycleGrid"/> (gắn tự động lên mỗi bản sao của ô mẫu). <see cref="Index"/> là phần tử ô đang vẽ,
    /// đổi mỗi lần ô được dùng lại: nút bấm nối trong OnItemCreated nên đọc Index lúc bấm, đừng chụp lại chỉ số.
    /// </summary>
    public sealed class RecycleGridItem : MonoBehaviour
    {
        /// <summary>Phần tử đang vẽ; -1 khi ô đang ẩn.</summary>
        public int Index { get; internal set; } = -1;

        Component view;

        /// <summary>Component chính của ô (InventorySlot, TextBtn, IconLabelCell...), tìm một lần rồi giữ lại.</summary>
        public T As<T>() where T : Component
        {
            if (view is T cached) return cached;
            var found = GetComponent<T>();
            view = found;
            return found;
        }
    }
}
