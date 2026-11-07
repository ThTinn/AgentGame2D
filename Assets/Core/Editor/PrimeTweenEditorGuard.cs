using System;
using System.Reflection;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// Vá lỗi của PrimeTween 1.3.7 trong Editor: bấm Play đôi khi ném MissingReferenceException "PrimeTweenManager has been
    /// destroyed" (PrimeTweenManager.IniOnLoad, nhánh ExitingEditMode gọi _instance.gameObject).
    /// <para>Ở Edit mode PrimeTween tạo PrimeTweenManager khi có tween chạy; object đó có thể bị hủy (Test Runner dọn object, đổi
    /// cảnh...). HasInstance chỉ so tham chiếu C# (ReferenceEquals), nên vẫn coi là có rồi đụng vào object đã hủy.</para>
    /// Ở đây: còn ở Edit mode mà _instance đã bị hủy thì xóa tham chiếu, PrimeTween tự tạo lại khi cần. Gói nằm trong .tgz
    /// (Assets/Plugins/PrimeTween/internal) nên vá ở phía dự án, cập nhật gói không mất.
    /// </summary>
    [InitializeOnLoad]
    static class PrimeTweenEditorGuard
    {
        static readonly FieldInfo InstanceField =
            Type.GetType("PrimeTween.PrimeTweenManager, PrimeTween.Runtime")?.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

        static PrimeTweenEditorGuard()
        {
            if (InstanceField == null) return;   // gói đổi cấu trúc (bản mới đã sửa): thôi
            EditorApplication.update += ClearDestroyed;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) ClearDestroyed();
            };
        }

        static void ClearDestroyed()
        {
            // Application chứ không phải EditorApplication.isPlaying: lúc ExitingEditMode cái sau đã là true
            if (UnityEngine.Application.isPlaying) return;
            object value = InstanceField.GetValue(null);
            // tham chiếu C# còn nhưng object Unity đã hủy (so bằng == của UnityEngine.Object)
            if (value is UnityEngine.Object obj && obj == null) InstanceField.SetValue(null, null);
        }
    }
}
