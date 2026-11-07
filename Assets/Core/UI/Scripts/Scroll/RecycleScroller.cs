using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Nguồn dữ liệu của <see cref="RecycleScroller"/> (cùng khuôn với IEnhancedScrollerDelegate của EnhancedScroller):
    /// có bao nhiêu dòng, mỗi dòng cao bao nhiêu, và dựng view cho một dòng (lấy view rảnh qua <see cref="RecycleScroller.GetCellView"/>
    /// rồi gắn dữ liệu của dòng đó vào).
    /// </summary>
    public interface IRecycleScrollerDelegate
    {
        int GetNumberOfCells(RecycleScroller scroller);
        float GetCellViewSize(RecycleScroller scroller, int dataIndex);
        RecycleCellView GetCellView(RecycleScroller scroller, int dataIndex, int cellIndex);
    }

    /// <summary>
    /// Danh sách cuộn dọc chỉ dựng view cho các dòng đang nhìn thấy (cộng <see cref="lookAhead"/> mỗi phía): cuộn tới đâu,
    /// dòng vừa khuất được tắt đi và đem gắn dữ liệu cho dòng vừa hiện. 10 dòng hay 10 000 dòng đều chỉ có chừng ấy view.
    /// <list type="bullet">
    /// <item>Đặt cạnh ScrollRect (dọc). Content của ScrollRect KHÔNG được có LayoutGroup / ContentSizeFitter: scroller tự đặt
    /// chiều cao content và vị trí từng dòng (neo trên, giãn ngang).</item>
    /// <item>Vị trí các dòng tính sẵn một lần lúc <see cref="ReloadData"/>; mỗi lần cuộn chỉ tìm nhị phân dòng đầu / cuối đang thấy,
    /// phạm vi không đổi thì không làm gì. Không tạo rác GC khi cuộn.</item>
    /// <item>Dữ liệu đổi mà số dòng / cỡ dòng giữ nguyên: <see cref="RefreshActiveCellViews"/> (chỉ vẽ lại dòng đang hiện).
    /// Đổi số dòng: <see cref="ReloadData"/>.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class RecycleScroller : MonoBehaviour
    {
        [Tooltip("Khoảng cách giữa hai dòng.")]
        public float spacing = 10f;
        public float paddingTop = 12f;
        public float paddingBottom = 12f;
        [Tooltip("Dựng thêm dòng trong chừng này pixel ngoài khung nhìn mỗi phía, để cuộn nhanh không thấy dòng hiện trễ.")]
        public float lookAhead = 60f;

        /// <summary>Nguồn dữ liệu. Gán rồi gọi <see cref="ReloadData"/>.</summary>
        public IRecycleScrollerDelegate Delegate { get; set; }

        public ScrollRect ScrollRect { get; private set; }
        RectTransform content;
        RectTransform viewport;

        float[] offsets = new float[0];   // mép trên của từng dòng (tính từ đỉnh content)
        float[] sizes = new float[0];
        int count;

        // view đang hiện, theo thứ tự dòng; phạm vi [first, last] (first > last = không có)
        readonly List<RecycleCellView> active = new List<RecycleCellView>(16);
        int first = 0, last = -1;
        // view đã tắt chờ dùng lại, theo loại (cellIdentifier)
        readonly Dictionary<string, Stack<RecycleCellView>> recycled = new Dictionary<string, Stack<RecycleCellView>>();
        // view vừa rời phạm vi trong lần cập nhật này, vẫn đang bật: dòng mới vào lấy chúng trước (chỉ đổi vị trí + dữ liệu),
        // khỏi tắt rồi bật lại — với UGUI mỗi lần SetActive là mọi Image / TMP con đăng ký lại và Canvas phải dựng lại
        readonly List<RecycleCellView> leaving = new List<RecycleCellView>(8);
        float lastViewportHeight;

        public int NumberOfCells => count;

        /// <summary>Vị trí cuộn tính bằng pixel từ đỉnh (0 = trên cùng).</summary>
        public float ScrollPosition
        {
            get => content != null ? content.anchoredPosition.y : 0f;
            set
            {
                if (content == null) return;
                float max = Mathf.Max(0f, content.rect.height - ViewportHeight);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(value, 0f, max));
                UpdateVisible(false);
            }
        }

        float ViewportHeight => viewport != null ? viewport.rect.height : 0f;

        void Awake() => Setup();

        void Setup()
        {
            if (ScrollRect != null) return;
            ScrollRect = GetComponent<ScrollRect>();
            content = ScrollRect.content;
            viewport = ScrollRect.viewport != null ? ScrollRect.viewport : (RectTransform)transform;
            ScrollRect.onValueChanged.AddListener(OnScrolled);
            if (content != null)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }
        }

        void OnDestroy()
        {
            if (ScrollRect != null) ScrollRect.onValueChanged.RemoveListener(OnScrolled);
        }

        void OnScrolled(Vector2 _) => UpdateVisible(false);

        // khung nhìn đổi cỡ (xoay màn hình, panel mở lần đầu): số dòng cần hiện đổi theo
        void OnRectTransformDimensionsChange()
        {
            if (ScrollRect == null || Mathf.Approximately(ViewportHeight, lastViewportHeight)) return;
            UpdateVisible(true);
        }

        /// <summary>
        /// Đọc lại toàn bộ dữ liệu từ <see cref="Delegate"/>: số dòng, cỡ từng dòng, dựng lại các dòng đang hiện.
        /// keepPosition: giữ chỗ đang cuộn (kẹp lại nếu danh sách ngắn đi); false = về đầu danh sách.
        /// </summary>
        public void ReloadData(bool keepPosition = false)
        {
            Setup();
            float position = keepPosition ? ScrollPosition : 0f;
            RecycleAll();

            count = Delegate != null ? Mathf.Max(0, Delegate.GetNumberOfCells(this)) : 0;
            if (offsets.Length < count)
            {
                int cap = Mathf.Max(count, offsets.Length * 2);
                offsets = new float[cap];
                sizes = new float[cap];
            }
            float y = paddingTop;
            for (int i = 0; i < count; i++)
            {
                float s = Mathf.Max(0f, Delegate.GetCellViewSize(this, i));
                offsets[i] = y;
                sizes[i] = s;
                y += s + (i < count - 1 ? spacing : 0f);
            }
            float height = count > 0 ? y + paddingBottom : 0f;
            if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, height);

            ScrollRect.StopMovement();
            float max = Mathf.Max(0f, height - ViewportHeight);
            if (content != null) content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(position, 0f, max));
            UpdateVisible(true);
        }

        /// <summary>Vẽ lại các dòng đang hiện (dữ liệu đổi, số dòng giữ nguyên).</summary>
        public void RefreshActiveCellViews()
        {
            for (int i = 0; i < active.Count; i++) active[i].RefreshCellView();
        }

        /// <summary>Cuộn để dòng dataIndex nằm ở đỉnh khung nhìn.</summary>
        public void JumpToDataIndex(int dataIndex)
        {
            if (count == 0) return;
            ScrollRect.StopMovement();
            ScrollPosition = offsets[Mathf.Clamp(dataIndex, 0, count - 1)] - paddingTop;
        }

        /// <summary>
        /// Lấy một view rảnh cùng loại với prefab (trong kho), không có thì nhân bản mới vào content. Delegate gọi trong
        /// GetCellView rồi gắn dữ liệu vào view trả về.
        /// </summary>
        public RecycleCellView GetCellView(RecycleCellView prefab)
        {
            string id = string.IsNullOrEmpty(prefab.cellIdentifier) ? prefab.name : prefab.cellIdentifier;
            for (int i = leaving.Count - 1; i >= 0; i--)
            {
                RecycleCellView v = leaving[i];
                if (v == null || v.cellIdentifier != id) continue;
                leaving[i] = leaving[leaving.Count - 1];
                leaving.RemoveAt(leaving.Count - 1);
                return v;   // còn bật sẵn
            }
            if (recycled.TryGetValue(id, out Stack<RecycleCellView> stack))
                while (stack.Count > 0)
                {
                    RecycleCellView v = stack.Pop();
                    if (v == null) continue;
                    v.gameObject.SetActive(true);
                    return v;
                }

            RecycleCellView created = Instantiate(prefab, content, false);
            created.cellIdentifier = id;
            created.gameObject.SetActive(true);
            var rt = (RectTransform)created.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            return created;
        }

        // ---------------- bên trong ----------------

        void UpdateVisible(bool force)
        {
            if (content == null) return;
            lastViewportHeight = ViewportHeight;

            int newFirst = 0, newLast = -1;
            if (count > 0 && Delegate != null)
            {
                float top = content.anchoredPosition.y - lookAhead;
                float bottom = content.anchoredPosition.y + lastViewportHeight + lookAhead;
                newFirst = FirstEndingBelow(top);
                newLast = LastStartingAbove(bottom);
                if (newLast < newFirst) newLast = newFirst - 1;
            }
            if (!force && newFirst == first && newLast == last) return;

            if (force || newLast < first || newFirst > last)
            {
                // không chồng lên phạm vi cũ: bỏ hết, dựng lại
                RecycleAll();
                for (int i = newFirst; i <= newLast; i++) active.Add(Place(i));
            }
            else
            {
                // bỏ dòng ra khỏi phạm vi ở hai đầu, thêm dòng mới vào hai đầu
                while (active.Count > 0 && active[0].dataIndex < newFirst) { Recycle(active[0]); active.RemoveAt(0); }
                while (active.Count > 0 && active[active.Count - 1].dataIndex > newLast) { Recycle(active[active.Count - 1]); active.RemoveAt(active.Count - 1); }
                for (int i = Mathf.Min(first, newLast + 1) - 1; i >= newFirst; i--) active.Insert(0, Place(i));
                for (int i = Mathf.Max(last, newFirst - 1) + 1; i <= newLast; i++) active.Add(Place(i));
            }
            first = newFirst;
            last = newLast;
            FlushLeaving();
        }

        RecycleCellView Place(int dataIndex)
        {
            RecycleCellView v = Delegate.GetCellView(this, dataIndex, active.Count);
            v.dataIndex = dataIndex;
            var rt = (RectTransform)v.transform;
            rt.anchoredPosition = new Vector2(0f, -offsets[dataIndex]);
            rt.sizeDelta = new Vector2(0f, sizes[dataIndex]);
            return v;
        }

        // dòng đầu tiên có mép dưới thấp hơn y
        int FirstEndingBelow(float y)
        {
            int lo = 0, hi = count - 1, ans = count;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (offsets[mid] + sizes[mid] >= y) { ans = mid; hi = mid - 1; }
                else lo = mid + 1;
            }
            return Mathf.Min(ans, count - 1);
        }

        // dòng cuối cùng có mép trên cao hơn y
        int LastStartingAbove(float y)
        {
            int lo = 0, hi = count - 1, ans = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (offsets[mid] <= y) { ans = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ans;
        }

        void RecycleAll()
        {
            for (int i = 0; i < active.Count; i++) Recycle(active[i]);
            active.Clear();
            first = 0;
            last = -1;
        }

        // dòng rời phạm vi: chờ tới cuối lần cập nhật, dòng mới vào có thể lấy luôn (FlushLeaving tắt phần còn thừa)
        void Recycle(RecycleCellView v)
        {
            if (v == null) return;
            v.dataIndex = -1;
            leaving.Add(v);
        }

        void FlushLeaving()
        {
            for (int i = 0; i < leaving.Count; i++)
            {
                RecycleCellView v = leaving[i];
                if (v == null) continue;
                v.gameObject.SetActive(false);
                if (!recycled.TryGetValue(v.cellIdentifier, out Stack<RecycleCellView> stack))
                    recycled[v.cellIdentifier] = stack = new Stack<RecycleCellView>(8);
                stack.Push(v);
            }
            leaving.Clear();
        }
    }
}
