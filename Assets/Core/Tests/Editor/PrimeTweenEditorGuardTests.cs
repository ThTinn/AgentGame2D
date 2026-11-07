using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// PrimeTweenEditorGuard (Core.Editor): PrimeTweenManager tạo ở Edit mode bị hủy thì tham chiếu tĩnh phải được xóa, kẻo bấm
    /// Play ném MissingReferenceException trong PrimeTween. Gọi qua reflection vì Core.Tests không tham chiếu Core.Editor /
    /// PrimeTween, còn PrimeTweenManager là internal.
    /// </summary>
    public class PrimeTweenEditorGuardTests
    {
        const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void DestroyedEditModeManager_IsCleared()
        {
            Type manager = Type.GetType("PrimeTween.PrimeTweenManager, PrimeTween.Runtime");
            Type guard = Type.GetType("Game.EditorTools.PrimeTweenEditorGuard, Core.Editor");
            if (manager == null || guard == null) Assert.Ignore("Không có PrimeTween hoặc Core.Editor.");
            FieldInfo field = manager.GetField("_instance", StaticAny);
            PropertyInfo instance = manager.GetProperty("Instance", StaticAny);
            MethodInfo clear = guard.GetMethod("ClearDestroyed", StaticAny);
            Assert.NotNull(field, "PrimeTween đổi cấu trúc: xem lại PrimeTweenEditorGuard");
            Assert.NotNull(clear);

            // chạy ngay sau khi dừng Play (Editor chưa tới EnteredEditMode): PrimeTween còn giữ manager của Play và chặn mọi lời
            // gọi; đó là trạng thái tạm, không phải tình huống cần thử
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) Assert.Ignore("Editor đang chuyển chế độ Play.");
            clear.Invoke(null, null);
            // Edit mode: đọc Instance thì PrimeTween tự tạo manager (HideAndDontSave)
            Component created;
            try { created = (Component)instance.GetValue(null); }
            catch (TargetInvocationException e) { Assert.Ignore("PrimeTween chưa sẵn sàng: " + e.InnerException?.Message); return; }
            Assert.NotNull(created);
            UnityEngine.Object.DestroyImmediate(created.gameObject);

            object stale = field.GetValue(null);
            Assert.IsFalse(ReferenceEquals(stale, null), "tham chiếu C# vẫn còn sau khi hủy (đúng tình huống lỗi)");
            Assert.IsTrue((UnityEngine.Object)stale == null, "object Unity đã hủy");

            clear.Invoke(null, null);
            Assert.IsTrue(ReferenceEquals(field.GetValue(null), null), "guard phải xóa tham chiếu đã hủy");
        }
    }
}
