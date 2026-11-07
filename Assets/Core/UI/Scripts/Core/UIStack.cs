using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Game.UI
{
    /// <summary>
    /// Phần lõi dùng chung cho PopupMN (popup) và UIManager (panel): tạo lười từ prefab và giữ lại để dùng lại,
    /// xếp lớp theo thứ tự mở (history), hàng đợi cho màn UseQueue, và định tuyến phím cho màn trên cùng.
    /// Bên Naruto hai lớp này chép gần y nhau; ở đây gom về một chỗ, lớp con chỉ lo cấu hình và API riêng của mình.
    /// </summary>
    public abstract class UIStack<TId, TView> : MonoBehaviour
        where TId : struct, Enum
        where TView : MonoBehaviour, IUIView<TId>
    {
        // Key là enum: EqualityComparer<TId>.Default là EnumEqualityComparer, không boxing trên IL2CPP.
        protected readonly Dictionary<TId, TView> prefabs = new Dictionary<TId, TView>();
        protected readonly Dictionary<TId, TView> alive = new Dictionary<TId, TView>();
        readonly Dictionary<TId, Queue<Action<TView>>> queues = new Dictionary<TId, Queue<Action<TView>>>();
        readonly List<TView> history = new List<TView>();
        readonly List<TId> pendingQueue = new List<TId>();

        /// <summary>Màn trên cùng (không tính toast/HUD). Null là không có gì đang mở.</summary>
        public TView Current { get; private set; }

        public TId CurrentID => Exists(Current) ? Current.ID : default;

        /// <summary>Có màn nào đang mở và chặn điều khiển nhân vật không.</summary>
        public bool IsBlocking => Exists(Current) && Current.BlockPlayerMove;

        /// <summary>Nơi đặt màn tạo mới. Mặc định là chính object này.</summary>
        protected virtual Transform ViewRoot => transform;

        /// <summary>Tên đứng đầu log, vd "[PopupMN]".</summary>
        protected abstract string LogTag { get; }

        // ---------------- Đăng ký ----------------

        /// <summary>Nạp bảng mã > prefab. Gọi lúc Awake của lớp con.</summary>
        protected void AddPrefab(TId id, TView prefab)
        {
            if (!Exists(prefab) || IsNone(id)) return;
            if (!prefabs.TryAdd(id, prefab))
                Debug.LogWarning(LogTag + " mã " + id + " khai báo hai lần trong config, giữ prefab đầu tiên.");
        }

        /// <summary>Đăng ký màn đã có sẵn trong scene/prefab (không tạo từ config).</summary>
        protected void Register(TView view)
        {
            if (!Exists(view)) return;

            TId id = view.ID;
            if (IsNone(id))
            {
                Debug.LogWarning(LogTag + " " + view.name + " chưa đặt ID, bỏ qua.");
                return;
            }

            if (alive.TryGetValue(id, out TView existing) && Exists(existing))
            {
                // đăng ký lại đúng object cũ thì bỏ qua, đừng gọi OnInit lần nữa (nhân đôi listener của nút)
                if (ReferenceEquals(existing, view)) return;

                Debug.LogWarning(LogTag + " mã " + id + " đã có " + existing.name + ", bỏ qua " + view.name + ".");
                return;
            }

            alive[id] = view;
            view.OnInit();
            if (view.gameObject.activeSelf && !view.SkipHistory) Push(view);
        }

        // ---------------- Mở ----------------

        /// <summary>Mở theo mã. Lần đầu mới Instantiate từ prefab, các lần sau dùng lại object cũ.</summary>
        public TView Show(TId id)
        {
            TView view = Get(id);
            if (!Exists(view))
            {
                if (!prefabs.TryGetValue(id, out TView prefab) || !Exists(prefab))
                {
                    Debug.LogError(LogTag + " không có prefab cho mã " + id + " trong config.");
                    return null;
                }

                view = Instantiate(prefab, ViewRoot);
                view.name = prefab.name;
                view.ID = id;
                view.OnInit();
                alive[id] = view;
            }

            view.OnShow();
            if (!view.SkipHistory) Push(view);
            return view;
        }

        public T Show<T>(TId id) where T : TView => Show(id) as T;

        /// <summary>
        /// Mở rồi đổ dữ liệu vào. Với màn UseQueue đang mở, yêu cầu được xếp hàng và hàm trả về null;
        /// nó chạy khi màn hiện tại đóng, nên luôn kiểm tra null trước khi dùng giá trị trả về.
        /// </summary>
        public T Show<T>(TId id, Action<T> init) where T : TView
        {
            T open = Get<T>(id);
            if (Exists(open) && open.UseQueue && open.gameObject.activeSelf)
            {
                if (!queues.TryGetValue(id, out Queue<Action<TView>> queue))
                    queues[id] = queue = new Queue<Action<TView>>();
                queue.Enqueue(v => init?.Invoke(v as T));
                return null;
            }

            T view = Show<T>(id);
            if (Exists(view)) init?.Invoke(view);
            return view;
        }

        public TView Get(TId id) => alive.TryGetValue(id, out TView view) && Exists(view) ? view : null;

        public T Get<T>(TId id) where T : TView => Get(id) as T;

        /// <summary>Mở được mã này không: có prefab trong config hoặc đã có sẵn trong scene.</summary>
        public bool Has(TId id) => prefabs.ContainsKey(id) || Get(id) != null;

        public bool IsOpen(TId id)
        {
            TView view = Get(id);
            return Exists(view) && view.gameObject.activeSelf;
        }

        // ---------------- Đóng ----------------

        public void Close(TId id)
        {
            TView view = Get(id);
            if (Exists(view)) Close(view);
        }

        public void Close(TView view)
        {
            if (!Exists(view)) return;

            TId id = view.ID;
            bool skip = view.SkipHistory;
            if (!skip) history.Remove(view);

            view.OnClose();
            if (view.DestroyOnClose && alive.TryGetValue(id, out TView cached) && ReferenceEquals(cached, view)) alive.Remove(id);

            if (!skip)
            {
                Prune();
                RefreshCurrent();
            }

            // Yêu cầu đang chờ xử lý ở LateUpdate, để các listener Close() nối tiếp chạy xong đã.
            if (queues.TryGetValue(id, out Queue<Action<TView>> queue) && queue.Count > 0 && !pendingQueue.Contains(id))
                pendingQueue.Add(id);
        }

        /// <summary>Đóng màn trên cùng. Trả về false khi không còn gì mở.</summary>
        public bool CloseTop()
        {
            if (!Exists(Current)) return false;
            Close(Current);
            return true;
        }

        /// <summary>Đóng sạch (trừ toast/HUD) và bỏ hàng đợi. Dùng khi mất kết nối, đổi map hoặc về menu.</summary>
        public void CloseAll()
        {
            queues.Clear();
            pendingQueue.Clear();
            for (int i = history.Count - 1; i >= 0; i--)
            {
                TView view = history[i];
                if (Exists(view)) Close(view);
            }
            history.Clear();
            RefreshCurrent();
        }

        public void SetVisible(TId id, bool visible)
        {
            if (visible) Show(id);
            else Close(id);
        }

        public void Toggle(TId id)
        {
            if (IsOpen(id)) Close(id);
            else Show(id);
        }

        // ---------------- Phím ----------------

        /// <summary>Chuyển phím cho màn trên cùng. Trả true nếu có màn nhận (đừng chuyển tiếp xuống lớp dưới nữa).</summary>
        public bool RouteKey(Key key)
        {
            TView top = Current;
            if (!Exists(top)) return false;

            top.OnKey(key);
            return true;
        }

        // ---------------- Bên trong ----------------

        void Push(TView view)
        {
            history.Remove(view);
            Prune();
            history.Add(view);
            RefreshCurrent();
        }

        void Prune()
        {
            // dọn màn đã bị hủy hoặc bị tắt từ nơi khác (không qua UIStack)
            history.RemoveAll(v => !Exists(v) || !v.gameObject.activeSelf);
        }

        void RefreshCurrent()
        {
            Current = history.Count > 0 ? history[history.Count - 1] : null;
        }

        /// <summary>
        /// Kiểm tra null "kiểu Unity" cho type parameter. Với T là tham số kiểu, toán tử == của UnityEngine.Object
        /// không được dùng, nên `view == null` sẽ bỏ sót object đã bị Destroy — phải ép về Object rồi mới so sánh.
        /// </summary>
        static bool Exists(TView view) => (Object)view != null;

        static bool IsNone(TId id) => EqualityComparer<TId>.Default.Equals(id, default);

        protected virtual void LateUpdate()
        {
            if (pendingQueue.Count == 0) return;

            for (int i = 0; i < pendingQueue.Count; i++) Drain(pendingQueue[i]);
            pendingQueue.Clear();
        }

        void Drain(TId id)
        {
            if (!queues.TryGetValue(id, out Queue<Action<TView>> queue) || queue.Count == 0) return;
            if (IsOpen(id)) return;   // đã mở lại rồi, chờ lần đóng sau

            Action<TView> next = queue.Dequeue();
            TView view = Show(id);
            if (Exists(view)) next(view);
            else queue.Clear();       // không mở được thì bỏ hàng đợi cho khỏi kẹt
        }
    }
}
