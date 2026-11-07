using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Cách người chơi được điều khiển camera.</summary>
    public enum CameraViewMode
    {
        /// <summary>Cố định như ban đầu: không xoay, không zoom (camera về góc và khoảng cách mặc định).</summary>
        Locked = 0,
        /// <summary>Chỉ zoom gần / xa; hướng nhìn giữ mặc định.</summary>
        ZoomOnly = 1,
        /// <summary>Xoay tự do quanh nhân vật và zoom.</summary>
        Free = 2,
    }

    /// <summary>
    /// Chế độ camera đang chọn, lưu PlayerPrefs. Nằm ở Core.Data để bảng Cài đặt (Core.UI) và CameraManager
    /// (Assembly-CSharp) cùng đọc / ghi mà không phải tham chiếu nhau.
    /// </summary>
    public static class CameraViewSettings
    {
        const string PrefKey = "CameraViewMode";
        public const CameraViewMode Default = CameraViewMode.Free;

        static CameraViewMode? mode;

        /// <summary>Báo khi đổi chế độ (CameraManager nghe để áp ngay).</summary>
        public static event Action<CameraViewMode> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            mode = null;
            Changed = null;
        }

        public static CameraViewMode Mode
        {
            get
            {
                if (mode == null)
                {
                    int saved = PlayerPrefs.GetInt(PrefKey, (int)Default);
                    mode = Enum.IsDefined(typeof(CameraViewMode), saved) ? (CameraViewMode)saved : Default;
                }
                return mode.Value;
            }
            set
            {
                if (mode == value) return;
                mode = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        public static bool CanRotate => Mode == CameraViewMode.Free;
        public static bool CanZoom => Mode != CameraViewMode.Locked;
    }
}
