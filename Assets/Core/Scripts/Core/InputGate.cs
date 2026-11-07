using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Màn UI đang mở và chặn điều khiển nhân vật. <c>PopupMN</c> và <c>UIManager</c> cài interface này.</summary>
    public interface IInputBlocker
    {
        /// <summary>True khi màn đang hiện chặn điều khiển (BlockPlayerMove).</summary>
        bool BlocksPlayerInput { get; }
    }

    /// <summary>
    /// Cổng chặn điều khiển nhân vật. Mỗi hệ UI tự đăng ký một <see cref="IInputBlocker"/>;
    /// <c>PlayerInputHandler</c> chỉ đọc <see cref="IsBlocked"/> nên assembly Game.Gameplay không cần biết
    /// PopupMN hay UIManager là gì. Thêm một hệ UI mới chỉ việc đăng ký thêm, không phải sửa phía input.
    /// </summary>
    public static class InputGate
    {
        static readonly List<IInputBlocker> blockers = new List<IInputBlocker>();

        /// <summary>Đăng ký khi màn quản lý UI được tạo (Awake). Gọi hai lần cùng một đối tượng cũng không sao.</summary>
        public static void Register(IInputBlocker blocker)
        {
            if (blocker != null && !blockers.Contains(blocker)) blockers.Add(blocker);
        }

        /// <summary>Gỡ khi bị hủy (OnDestroy), tránh giữ tham chiếu tới object của scene cũ.</summary>
        public static void Unregister(IInputBlocker blocker)
        {
            if (blocker != null) blockers.Remove(blocker);
        }

        /// <summary>True khi có ít nhất một màn UI đang chặn điều khiển.</summary>
        public static bool IsBlocked
        {
            get
            {
                for (int i = 0; i < blockers.Count; i++)
                    if (blockers[i].BlocksPlayerInput) return true;
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => blockers.Clear();
    }
}
