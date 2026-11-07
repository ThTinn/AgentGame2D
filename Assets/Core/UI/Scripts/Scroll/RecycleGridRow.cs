using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>Một hàng của <see cref="RecycleGrid"/> (lưới tự sinh): giữ các ô của hàng.</summary>
    public sealed class RecycleGridRow : RecycleCellView
    {
        internal RecycleGrid grid;
        internal readonly List<RecycleGridItem> items = new List<RecycleGridItem>(8);
        internal int layoutColumns = -1;
        internal float layoutWidth = -1f;

        public override void RefreshCellView()
        {
            if (grid != null) grid.BindRow(this);
        }
    }
}
