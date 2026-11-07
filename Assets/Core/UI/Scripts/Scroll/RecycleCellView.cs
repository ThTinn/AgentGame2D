using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// View của một dòng trong <see cref="RecycleScroller"/>. Một view được dùng lại cho nhiều dòng khi cuộn: mọi thứ hiển thị
    /// phải gắn lại theo <see cref="dataIndex"/> mỗi lần (đừng giữ trạng thái riêng của dòng cũ).
    /// </summary>
    public class RecycleCellView : MonoBehaviour
    {
        [Tooltip("Loại view: view cùng loại mới dùng lại cho nhau. Trống = tên prefab.")]
        public string cellIdentifier;

        /// <summary>Dòng đang vẽ; -1 khi đang nằm trong kho.</summary>
        public int dataIndex = -1;

        /// <summary>Vẽ lại theo dữ liệu hiện tại của dòng (RecycleScroller.RefreshActiveCellViews gọi).</summary>
        public virtual void RefreshCellView() { }
    }
}
