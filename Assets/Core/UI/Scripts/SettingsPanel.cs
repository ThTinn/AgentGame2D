using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Bảng cài đặt (panel UIID.Settings), mở từ nút Settings trên HUD. Hiện có mục Ngôn ngữ: mỗi ngôn ngữ trong
    /// <see cref="Lang.langeStr"/> một nút (sinh lúc chạy, nên thêm ngôn ngữ trong Docs/Lang_Data.xlsx là có thêm nút),
    /// nút của ngôn ngữ đang dùng tô màu chọn. Bấm là đổi ngay qua <see cref="Lang.SetLanguage"/>: mọi panel đang mở
    /// nhận Lang.OnChanged và vẽ lại chữ, lựa chọn được lưu (PlayerPrefs) cho lần vào sau.
    /// Mục Góc nhìn camera: 3 nút Cố định / Chỉ zoom / Tự do, ghi vào <see cref="CameraViewSettings"/> (CameraManager áp ngay,
    /// lưu cho lần sau). Prefab chưa có mục này thì tự dựng lúc chạy bằng cách nhân bản mục Ngôn ngữ.
    /// Mục Âm lượng: 2 thanh trượt Nhạc nền / Hiệu ứng, ghi vào <see cref="AudioVolume"/> (AudioManager áp ngay, lưu ra đĩa
    /// khi đóng bảng). Prefab chưa có thì cũng tự dựng như mục camera.
    /// </summary>
    public class SettingsPanel : PanelBase
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TextBtn closeButton;
        [Tooltip("Nhãn của mục chọn ngôn ngữ.")]
        [SerializeField] TMP_Text languageLabel;
        [Tooltip("Nơi sinh nút ngôn ngữ (có GridLayoutGroup).")]
        [SerializeField] RectTransform languageRoot;
        [SerializeField] TextBtn languagePrefab;

        [Header("Góc nhìn camera")]
        [Tooltip("Nhãn mục camera. Để trống cùng cameraRoot: tự nhân bản mục Ngôn ngữ làm mục camera lúc chạy.")]
        [SerializeField] TMP_Text cameraLabel;
        [Tooltip("Nơi sinh 3 nút chế độ camera (có GridLayoutGroup). Dùng chung prefab nút với ngôn ngữ.")]
        [SerializeField] RectTransform cameraRoot;

        [Header("Âm lượng")]
        [Tooltip("Nhãn mục âm lượng. Để trống cùng 2 thanh trượt: tự dựng mục âm lượng lúc chạy.")]
        [SerializeField] TMP_Text audioLabel;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [Tooltip("Chữ cạnh thanh trượt (tên + %), không bắt buộc.")]
        [SerializeField] TMP_Text musicText, sfxText;

        readonly List<TextBtn> languageButtons = new List<TextBtn>();
        readonly List<TextBtn> cameraButtons = new List<TextBtn>();

        // thứ tự nút = thứ tự hiện; chữ "Việt|English"
        static readonly CameraViewMode[] CameraModes = { CameraViewMode.Locked, CameraViewMode.ZoomOnly, CameraViewMode.Free };
        static readonly string[] CameraModeNames = { "Cố định|Locked", "Chỉ zoom|Zoom only", "Tự do|Free" };
        const float SectionHeight = 140f, SectionGap = 12f;
        const float AudioSectionHeight = 170f, AudioRowHeight = 48f, AudioRowGap = 6f;

        // các mục xếp chồng trong hộp (mục Ngôn ngữ của prefab + mục dựng lúc chạy), cùng lề trên / dưới của hộp
        readonly List<(RectTransform rt, float height)> sections = new List<(RectTransform, float)>();
        float boxTop, boxBottom;

        public override void OnInit()
        {
            if (closeButton) closeButton.AddListener(Close);
            // dựng mục trước BuildLanguages để bản sao chưa có nút ngôn ngữ
            if (cameraRoot == null) CreateCameraSection();
            if (musicSlider == null && sfxSlider == null) CreateAudioSection();
            BuildLanguages();
            BuildCameraModes();
            BindAudio();
            CameraViewSettings.Changed += OnCameraModeChanged;
            AudioVolume.Changed += RefreshAudio;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AudioVolume.Save(); // kéo thanh trượt chỉ ghi vào bộ nhớ, đóng bảng mới ghi ra đĩa
        }

        void OnDestroy()
        {
            CameraViewSettings.Changed -= OnCameraModeChanged;
            AudioVolume.Changed -= RefreshAudio;
            if (musicSlider) musicSlider.onValueChanged.RemoveAllListeners();
            if (sfxSlider) sfxSlider.onValueChanged.RemoveAllListeners();
            if (closeButton) closeButton.RemoveAllListeners();
            foreach (TextBtn b in languageButtons) if (b) b.RemoveAllListeners();
            foreach (TextBtn b in cameraButtons) if (b) b.RemoveAllListeners();
        }

        public override void OnLoadLang()
        {
            if (titleText) titleText.text = Lang.GetSplitText("Cài đặt|Settings");
            if (closeButton) closeButton.SetText(Lang.Close);
            if (languageLabel) languageLabel.text = Lang.GetSplitText("Ngôn ngữ|Language");
            RefreshLanguages();
            RefreshCameraModes();
            RefreshAudio();
        }

        // ---------------- Mục dựng lúc chạy ----------------

        /// <summary>
        /// Nhân bản khung "Ngôn ngữ" (nhãn + lưới nút) làm mục mới, đặt dưới mục cuối cùng rồi xếp lại các mục, nới hộp cao
        /// ra cho vừa. Khoảng trống trên (tiêu đề) và dưới (nút Đóng) giữ như thiết kế. Null nếu prefab không đúng khung.
        /// </summary>
        RectTransform AddSection(string name, float height)
        {
            if (languageRoot == null) return null;
            RectTransform section = languageRoot.parent as RectTransform;
            RectTransform box = section != null ? section.parent as RectTransform : null;
            if (box == null) return null;

            if (sections.Count == 0)
            {
                boxTop = -section.offsetMax.y;
                boxBottom = section.offsetMin.y;
                sections.Add((section, SectionHeight));
            }
            RectTransform copy = Instantiate(section, box);
            copy.name = name;
            copy.SetSiblingIndex(sections[sections.Count - 1].rt.GetSiblingIndex() + 1);
            sections.Add((copy, height));

            float total = boxTop + boxBottom + SectionGap * (sections.Count - 1);
            foreach (var s in sections) total += s.height;
            box.sizeDelta = new Vector2(box.sizeDelta.x, total);
            float y = boxTop;
            foreach (var (rt, h) in sections)
            {
                rt.offsetMax = new Vector2(rt.offsetMax.x, -y);
                rt.offsetMin = new Vector2(rt.offsetMin.x, total - y - h);
                y += h + SectionGap;
            }
            return copy;
        }

        // ---------------- Camera ----------------

        /// <summary>
        /// Prefab chưa có mục camera: nhân bản khung "Ngôn ngữ" (<see cref="AddSection"/>) làm mục camera.
        /// Muốn tự bố trí thì tạo sẵn trong prefab rồi gán cameraLabel / cameraRoot, đoạn này sẽ bỏ qua.
        /// </summary>
        void CreateCameraSection()
        {
            RectTransform copy = AddSection("Camera", SectionHeight);
            if (copy == null) return;
            RectTransform box = (RectTransform)copy.parent;
            cameraRoot = copy.Find(languageRoot.name) as RectTransform;
            if (languageLabel != null)
            {
                Transform label = copy.Find(languageLabel.transform.name);
                if (label != null) cameraLabel = label.GetComponent<TMP_Text>();
            }

            // 3 nút một hàng: thu ô lại cho vừa bề ngang
            if (cameraRoot != null && cameraRoot.TryGetComponent(out GridLayoutGroup grid))
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = CameraModes.Length;
                float width = box.sizeDelta.x + copy.sizeDelta.x + cameraRoot.sizeDelta.x; // trừ lề của khung và lưới
                float cell = (width - grid.spacing.x * (CameraModes.Length - 1)) / CameraModes.Length;
                grid.cellSize = new Vector2(Mathf.Min(grid.cellSize.x, cell), grid.cellSize.y);
            }
        }

        void BuildCameraModes()
        {
            if (cameraRoot == null || languagePrefab == null) return;
            for (int i = cameraRoot.childCount - 1; i >= 0; i--) Destroy(cameraRoot.GetChild(i).gameObject);
            cameraButtons.Clear();

            for (int i = 0; i < CameraModes.Length; i++)
            {
                CameraViewMode mode = CameraModes[i];
                TextBtn b = Instantiate(languagePrefab, cameraRoot);
                b.name = "Camera_" + mode;
                b.AddListener(() => CameraViewSettings.Mode = mode);
                cameraButtons.Add(b);
            }
            RefreshCameraModes();
        }

        void OnCameraModeChanged(CameraViewMode _) => RefreshCameraModes();

        void RefreshCameraModes()
        {
            CameraViewMode cur = CameraViewSettings.Mode;
            if (cameraLabel)
                cameraLabel.text = Lang.GetSplitText("Góc nhìn camera|Camera view") + "  <size=80%><alpha=#99>"
                                   + Lang.GetSplitText(Hint(cur)) + "</size>";
            for (int i = 0; i < cameraButtons.Count && i < CameraModes.Length; i++)
            {
                cameraButtons[i].SetText(Lang.GetSplitText(CameraModeNames[i]));
                cameraButtons[i].SetColorText(CameraModes[i] == cur ? UIColors.TextSelected : UIColors.TextNormal);
            }
        }

        static string Hint(CameraViewMode mode)
        {
            switch (mode)
            {
                case CameraViewMode.Locked: return "(như ban đầu)|(default view)";
                case CameraViewMode.ZoomOnly: return "(chỉ zoom gần / xa)|(zoom in / out only)";
                default: return "(xoay + zoom)|(rotate + zoom)";
            }
        }

        // ---------------- Âm lượng ----------------

        /// <summary>
        /// Prefab chưa có mục âm lượng: nhân bản khung "Ngôn ngữ", bỏ lưới nút, thêm 2 hàng "tên + %  [thanh trượt]".
        /// Thanh trượt vẽ bằng Image trơn như các nút của bảng.
        /// </summary>
        void CreateAudioSection()
        {
            RectTransform copy = AddSection("Audio", AudioSectionHeight);
            if (copy == null) return;
            Transform grid = copy.Find(languageRoot.name);
            if (grid != null)
            {
                grid.gameObject.SetActive(false);
                Destroy(grid.gameObject);
            }
            TMP_Text labelTemplate = null;
            if (languageLabel != null)
            {
                Transform label = copy.Find(languageLabel.transform.name);
                if (label != null) audioLabel = labelTemplate = label.GetComponent<TMP_Text>();
            }

            float top = languageLabel != null ? -((RectTransform)languageLabel.transform).offsetMin.y : 52f;
            musicSlider = CreateSliderRow(copy, "Music", top, labelTemplate, out musicText);
            sfxSlider = CreateSliderRow(copy, "Sfx", top + AudioRowHeight + AudioRowGap, labelTemplate, out sfxText);
        }

        static Slider CreateSliderRow(RectTransform parent, string name, float top, TMP_Text labelTemplate, out TMP_Text text)
        {
            RectTransform row = NewRect(name, parent);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(20f, -top - AudioRowHeight);
            row.offsetMax = new Vector2(-20f, -top);

            text = null;
            const float textWidth = 230f;
            if (labelTemplate != null)
            {
                text = Instantiate(labelTemplate, row);
                text.name = "Text";
                text.fontSize = labelTemplate.fontSize * 0.9f;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                RectTransform rt = text.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = new Vector2(textWidth, 0f);
            }

            // thanh trượt: nền mảnh, phần đã chọn tô màu chọn, núm trắng; cả hàng cao nên dễ chạm trên điện thoại
            const float handleWidth = 28f, trackHeight = 12f;
            RectTransform sliderRt = NewRect("Slider", row);
            sliderRt.offsetMin = new Vector2(textWidth + 10f, 4f);
            sliderRt.offsetMax = new Vector2(-6f, -4f);

            Image track = NewRect("Background", sliderRt).gameObject.AddComponent<Image>();
            track.color = UITheme.TextOnButton;
            RectTransform trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.sizeDelta = new Vector2(0f, trackHeight);

            RectTransform fillArea = NewRect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.offsetMin = new Vector2(handleWidth / 2f, -trackHeight / 2f);
            fillArea.offsetMax = new Vector2(-handleWidth / 2f, trackHeight / 2f);
            Image fill = NewRect("Fill", fillArea).gameObject.AddComponent<Image>();
            fill.color = new Color32(171, 220, 244, 255);
            fill.raycastTarget = false;
            fill.rectTransform.offsetMin = new Vector2(-handleWidth / 2f, 0f); // tô từ mép trái của nền

            RectTransform handleArea = NewRect("Handle Slide Area", sliderRt);
            handleArea.offsetMin = new Vector2(handleWidth / 2f, 0f);
            handleArea.offsetMax = new Vector2(-handleWidth / 2f, 0f);
            Image handle = NewRect("Handle", handleArea).gameObject.AddComponent<Image>();
            handle.color = Color.white;
            if (UITheme.Instance) UITheme.Paint(handle, UITheme.Instance.circle);
            handle.preserveAspect = true;
            handle.rectTransform.sizeDelta = new Vector2(handleWidth, 0f);

            Slider slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        /// <summary>RectTransform con phủ kín cha (neo 4 góc, lề 0).</summary>
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        void BindAudio()
        {
            if (musicSlider) musicSlider.onValueChanged.AddListener(v => { AudioVolume.Music = v; RefreshAudio(); });
            if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => { AudioVolume.Sfx = v; RefreshAudio(); });
            RefreshAudio();
        }

        void RefreshAudio()
        {
            if (audioLabel) audioLabel.text = Lang.GetSplitText("Âm lượng|Volume");
            if (musicSlider) musicSlider.SetValueWithoutNotify(AudioVolume.Music);
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(AudioVolume.Sfx);
            if (musicText) musicText.text = Lang.GetSplitText("Nhạc nền|Music") + VolumeSuffix(AudioVolume.Music);
            if (sfxText) sfxText.text = Lang.GetSplitText("Hiệu ứng|Effects") + VolumeSuffix(AudioVolume.Sfx);
        }

        static string VolumeSuffix(float v) => "  <alpha=#99>" + Mathf.RoundToInt(v * 100f) + "%";

        void BuildLanguages()
        {
            if (languageRoot == null || languagePrefab == null) return;
            for (int i = languageRoot.childCount - 1; i >= 0; i--) Destroy(languageRoot.GetChild(i).gameObject);
            languageButtons.Clear();

            for (int i = 0; i < Lang.langeStr.Length; i++)
            {
                int lang = i;
                TextBtn b = Instantiate(languagePrefab, languageRoot);
                b.name = "Lang_" + i;
                // tên ngôn ngữ viết bằng chính ngôn ngữ đó, không dịch theo ngôn ngữ đang dùng
                b.SetText(Lang.langeStr[i]);
                b.AddListener(() => Lang.SetLanguage(lang));
                languageButtons.Add(b);
            }
            RefreshLanguages();
        }

        void RefreshLanguages()
        {
            for (int i = 0; i < languageButtons.Count; i++)
                languageButtons[i].SetColorText(i == Lang.curLange ? UIColors.TextSelected : UIColors.TextNormal);
        }
    }
}
