using System;
using Game.Anim;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Sân khấu xem trước: bản sao nhân vật + camera riêng, render vào <see cref="Texture"/>.
    /// Cài ở Assembly-CSharp (AnimPreviewStage: cần Player, tải clip bundle) và gán vào <see cref="AnimPreview.CreateStage"/>.
    /// Dispose = khung không dùng nữa; bên cài có thể cất lại để lần sau dùng tiếp (AnimPreviewStage giữ một cái).
    /// </summary>
    public interface IAnimPreviewStage : IDisposable
    {
        Texture Texture { get; }
        /// <summary>Chạy lặp clip từ đầu; null = không chạy gì (khung trống).</summary>
        void Play(AnimMotion motion);
    }

    /// <summary>
    /// Khung xem trước một clip (<see cref="AnimMotion"/>) trên UI: nhân vật của người chơi (đúng đồ đang mặc) chạy clip,
    /// hiện qua một RawImage con phủ Image khung có sẵn. Gắn bằng <see cref="Attach"/>, đổi clip bằng <see cref="Set"/>.
    /// </summary>
    /// <remarks>
    /// Vòng đời: khung hiện (OnEnable) thì xin một sân khấu từ <see cref="CreateStage"/>, khung ẩn (OnDisable) thì trả lại,
    /// nên panel đóng không tốn camera nào. Chưa có sân khấu (chưa có nhân vật, scene test UI) thì hiện icon tĩnh
    /// (AnimMotion.Icon) lên chính Image khung.
    /// Game: bản GAME_2D chồng bốn lớp sprite (thân, vũ khí, tay, chớp lửa). Bản 3D trước đây chạy các frame render sẵn
    /// trong Editor nên không thấy đồ đang mặc; giờ render model thật.
    /// </remarks>
    public class AnimPreview : MonoBehaviour
    {
        /// <summary>Tạo sân khấu cỡ (rộng, cao) pixel; trả null khi chưa dựng được (vd. chưa có nhân vật).</summary>
        public static Func<Vector2Int, IAnimPreviewStage> CreateStage;

        const int MinSize = 128, MaxSize = 1024;   // cỡ RenderTexture (pixel), mỗi chiều

        Image image;               // Image khung có sẵn trên prefab: chỉ dùng hiện icon khi không có sân khấu
        RawImage view;             // con của khung, hiện texture của sân khấu
        IAnimPreviewStage stage;
        AnimMotion motion;         // clip đang xem, giữ để chạy lại khi khung hiện lại

        /// <summary>Gắn lên Image khung (một lần) và trả component.</summary>
        public static AnimPreview Attach(Image frameImage)
        {
            if (frameImage == null) return null;
            var preview = frameImage.GetComponent<AnimPreview>();
            if (preview == null) preview = frameImage.gameObject.AddComponent<AnimPreview>();
            preview.image = frameImage;
            frameImage.preserveAspect = true;
            // AddComponent lúc khung đang hiện thì OnEnable chạy trước khi có image: dựng ở đây
            if (preview.isActiveAndEnabled) preview.Begin();
            return preview;
        }

        /// <summary>Chạy clip từ đầu; null (hoặc clip rỗng) = để trống.</summary>
        public void Set(AnimMotion m)
        {
            motion = m != null && m.HasClip ? m : null;
            if (stage == null && isActiveAndEnabled) Begin();   // khung mở trước khi có nhân vật: thử lại
            else if (stage != null) stage.Play(motion);
            Show();
        }

        void OnEnable()
        {
            if (image != null) Begin();
        }

        void OnDisable() => End();

        void OnDestroy() => End();

        void Begin()
        {
            if (stage != null || CreateStage == null) return;
            stage = CreateStage(PixelSize());
            if (stage == null) return;

            EnsureView();
            view.texture = stage.Texture;
            // layout chưa tính xong lúc mở thì cỡ khung có thể sai: giữ tỉ lệ texture, không kéo méo hình
            view.GetComponent<AspectRatioFitter>().aspectRatio = (float)stage.Texture.width / stage.Texture.height;
            stage.Play(motion);
            Show();
        }

        void End()
        {
            if (stage == null) return;
            stage.Dispose();
            stage = null;
            if (view) view.texture = null;
            Show();
        }

        // có sân khấu: RawImage hiện nhân vật, Image khung tắt. Không có: icon tĩnh lên Image khung.
        void Show()
        {
            if (view) view.enabled = stage != null && motion != null;
            if (image == null) return;
            Sprite icon = stage == null ? motion?.Icon : null;
            image.sprite = icon;
            image.enabled = icon != null;
        }

        void EnsureView()
        {
            if (view) return;
            var go = new GameObject("Live", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            view = go.GetComponent<RawImage>();
            view.raycastTarget = false;
        }

        // cỡ texture bằng cỡ khung trên màn hình (đã nhân scale của canvas): hình không vỡ, cũng không phí pixel
        Vector2Int PixelSize()
        {
            var rt = (RectTransform)transform;
            // lossyScale của canvas overlay = scaleFactor của canvas gốc nhân các khung phóng (UIScaleRoot) ở giữa
            float scale = Mathf.Abs(rt.lossyScale.x);
            if (scale <= 0f) scale = 1f;
            Vector2 size = rt.rect.size * scale;
            float fit = Mathf.Min(1f, MaxSize / Mathf.Max(size.x, size.y, 1f));   // khung quá to: thu cả hai chiều, giữ tỉ lệ
            return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(size.x * fit), MinSize, MaxSize),
                                  Mathf.Clamp(Mathf.RoundToInt(size.y * fit), MinSize, MaxSize));
        }
    }
}
