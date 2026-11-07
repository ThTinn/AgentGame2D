using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Nguồn dữ liệu của <see cref="RecycleGrid"/>: bao nhiêu phần tử, việc làm một lần khi một ô mới được tạo (nối nút bấm),
    /// và gắn dữ liệu phần tử index vào một ô. Một ô được dùng lại cho nhiều phần tử khi cuộn: BindItem phải gắn lại mọi thứ
    /// hiển thị (chữ, ảnh, màu, bật / tắt), đừng dựa vào trạng thái của lần gắn trước.
    /// </summary>
    public interface IRecycleGridDelegate
    {
        int GetItemCount(RecycleGrid grid);
        void OnItemCreated(RecycleGrid grid, RecycleGridItem item);
        void BindItem(RecycleGrid grid, RecycleGridItem item, int index);
    }

    /// <summary>
    /// Lưới / danh sách cuộn dùng chung, chỉ dựng ô cho các hàng đang nhìn thấy (<see cref="RecycleScroller"/> lo phần cuộn và
    /// tái sử dụng hàng; lớp này chia phần tử thành hàng và xếp ô trong hàng). Panel chỉ cần: gán <see cref="Delegate"/>, gọi
    /// <see cref="ReloadData"/> khi danh sách đổi, <see cref="RefreshActiveItems"/> khi chỉ trạng thái đổi (ô đang chọn...).
    /// <list type="bullet">
    /// <item><see cref="cellSize"/>.x &gt; 0: lưới, số cột = <see cref="columns"/> (0 = vừa bề ngang), ô căn giữa hàng.</item>
    /// <item><see cref="cellSize"/>.x = 0: danh sách một cột, ô giãn hết bề ngang (trừ padding), cao cellSize.y.</item>
    /// </list>
    /// Ô là bản sao của <see cref="itemTemplate"/> (tắt sẵn, nằm ngoài Content). Hàng do lưới tự sinh, không cần prefab.
    /// Dựng bằng UIBuildUtil.BuildRecycleList (Editor).
    /// </summary>
    [RequireComponent(typeof(RecycleScroller))]
    public class RecycleGrid : MonoBehaviour, IRecycleScrollerDelegate
    {
        [Tooltip("Ô mẫu (tắt sẵn, ngoài Content). Các ô của lưới là bản sao của nó.")]
        public RectTransform itemTemplate;
        [Tooltip("Cỡ một ô. x = 0: danh sách một cột, ô giãn hết bề ngang.")]
        public Vector2 cellSize = new Vector2(100f, 100f);
        [Tooltip("Khoảng cách giữa hai ô (x) và giữa hai hàng (y).")]
        public Vector2 spacing = new Vector2(10f, 10f);
        [Tooltip("Lề trong của danh sách (trái / phải quanh ô, trên / dưới quanh hàng đầu / cuối).")]
        public int paddingLeft = 12, paddingRight = 12, paddingTop = 12, paddingBottom = 12;
        [Tooltip("Số cột cố định. 0 = nhiều nhất vừa bề ngang.")]
        public int columns;

        /// <summary>Nguồn dữ liệu. Gán rồi gọi <see cref="ReloadData"/>.</summary>
        public IRecycleGridDelegate Delegate { get; set; }

        public RecycleScroller Scroller { get; private set; }
        public int ItemCount { get; private set; }
        public int Columns { get; private set; } = 1;

        RecycleGridRow rowPrototype;
        RectTransform content;

        bool Stretch => cellSize.x <= 0f;

        void Awake() => Setup();

        void Setup()
        {
            if (Scroller != null) return;
            Scroller = GetComponent<RecycleScroller>();
            Scroller.Delegate = this;
            content = Scroller.ScrollRect != null ? Scroller.ScrollRect.content : GetComponent<UnityEngine.UI.ScrollRect>().content;
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);

            // hàng mẫu tự sinh: RecycleScroller nhân bản nó; ô trong hàng thêm dần khi gắn dữ liệu
            var go = new GameObject("RowPrototype", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            rowPrototype = go.AddComponent<RecycleGridRow>();
            rowPrototype.cellIdentifier = "row";
        }

        /// <summary>
        /// Đọc lại số phần tử và dựng lại các hàng đang hiện. keepPosition: giữ chỗ đang cuộn (danh sách thêm / bớt phần tử);
        /// false = về đầu (đổi tab, mở panel).
        /// </summary>
        public void ReloadData(bool keepPosition = false)
        {
            Setup();
            ItemCount = Delegate != null ? Mathf.Max(0, Delegate.GetItemCount(this)) : 0;
            Columns = ComputeColumns();
            Scroller.spacing = spacing.y;
            Scroller.paddingTop = paddingTop;
            Scroller.paddingBottom = paddingBottom;
            Scroller.ReloadData(keepPosition);
        }

        /// <summary>Gắn lại dữ liệu cho mọi ô đang hiện (số phần tử giữ nguyên: đổi ô đang chọn, đổi số lượng...).</summary>
        public void RefreshActiveItems()
        {
            Setup();
            Scroller.RefreshActiveCellViews();
        }

        /// <summary>Cuộn để hàng chứa phần tử index nằm ở đỉnh khung nhìn.</summary>
        public void JumpToItem(int index)
        {
            Setup();
            Scroller.JumpToDataIndex(index / Mathf.Max(1, Columns));
        }

        int ComputeColumns()
        {
            if (Stretch) return 1;
            if (columns > 0) return columns;
            float width = (content != null ? content.rect.width : 0f) - paddingLeft - paddingRight;
            return Mathf.Max(1, Mathf.FloorToInt((width + spacing.x) / (cellSize.x + spacing.x)));
        }

        // khung đổi bề ngang: số cột tự khớp có thể đổi (nạp lại, giữ chỗ cuộn); không thì chỉ xếp lại ô trong hàng
        void OnRectTransformDimensionsChange()
        {
            if (Scroller == null || Delegate == null) return;
            if (ComputeColumns() != Columns) ReloadData(true);
            else Scroller.RefreshActiveCellViews();
        }

        // ---------------- IRecycleScrollerDelegate: mỗi dòng của scroller là một hàng ----------------

        public int GetNumberOfCells(RecycleScroller scroller) => (ItemCount + Columns - 1) / Columns;

        public float GetCellViewSize(RecycleScroller scroller, int dataIndex) => cellSize.y;

        public RecycleCellView GetCellView(RecycleScroller scroller, int dataIndex, int cellIndex)
        {
            var row = (RecycleGridRow)scroller.GetCellView(rowPrototype);
            row.grid = this;
            row.dataIndex = dataIndex;
            BindRow(row);
            return row;
        }

        /// <summary>Đủ ô cho hàng, xếp ô (khi số cột / bề ngang đổi) và gắn dữ liệu từng ô.</summary>
        internal void BindRow(RecycleGridRow row)
        {
            int cols = Columns;
            while (row.items.Count < cols) row.items.Add(CreateItem(row.transform));

            float width = content != null ? content.rect.width : 0f;
            if (row.layoutColumns != cols || !Mathf.Approximately(row.layoutWidth, width))
            {
                Layout(row, cols, width);
                row.layoutColumns = cols;
                row.layoutWidth = width;
            }

            int start = row.dataIndex * cols;
            for (int k = 0; k < row.items.Count; k++)
            {
                RecycleGridItem item = row.items[k];
                int index = start + k;
                bool on = k < cols && index < ItemCount;
                if (item.gameObject.activeSelf != on) item.gameObject.SetActive(on);
                if (!on)
                {
                    item.Index = -1;
                    continue;
                }
                item.Index = index;
                Delegate.BindItem(this, item, index);
            }
        }

        RecycleGridItem CreateItem(Transform row)
        {
            RectTransform rt = Instantiate(itemTemplate, row, false);
            rt.gameObject.SetActive(true);
            RecycleGridItem item = rt.GetComponent<RecycleGridItem>();
            if (item == null) item = rt.gameObject.AddComponent<RecycleGridItem>();
            Delegate?.OnItemCreated(this, item);
            return item;
        }

        void Layout(RecycleGridRow row, int cols, float width)
        {
            if (Stretch)
            {
                var rt = (RectTransform)row.items[0].transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(paddingLeft, -cellSize.y);
                rt.offsetMax = new Vector2(-paddingRight, 0f);
                return;
            }

            float total = cols * cellSize.x + (cols - 1) * spacing.x;
            float x0 = paddingLeft + Mathf.Max(0f, (width - paddingLeft - paddingRight - total) * 0.5f);
            for (int k = 0; k < cols; k++)
            {
                var rt = (RectTransform)row.items[k].transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = cellSize;
                rt.anchoredPosition = new Vector2(x0 + k * (cellSize.x + spacing.x), 0f);
            }
        }
    }
}
