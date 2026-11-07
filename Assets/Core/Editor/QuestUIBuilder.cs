using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Core/UI/Build Quest UI: dựng lại hai prefab nhiệm vụ rồi nối vào Panel_Config.
    /// <list type="bullet">
    /// <item>QuestLogPanel: cửa sổ 1120x640 giữa màn hình. Bốn tab trên cùng, danh sách cuộn bên trái (dòng mẫu
    /// <see cref="QuestListRow"/> + tiêu đề nhóm), chi tiết cuộn bên phải (mục tiêu <see cref="QuestObjectiveRow"/>),
    /// nút Chỉ đường / Bỏ nhiệm vụ ở góc dưới.</item>
    /// <item>QuestTrackerPanel: khung HUD rộng 236 bên trái (dưới menu HUD), tự cao theo nội dung; tiêu đề + nút Mở / thu gọn, các dòng
    /// <see cref="QuestTrackerRow"/>.</item>
    /// </list>
    /// Cùng kiểu với UI có sẵn: nền tối phẳng, nút là prefab TextBtn (màu qua ColorBlock), chữ dùng font của TextBtn (đủ dấu
    /// tiếng Việt). Prefab ghi đè đúng chỗ cũ (GUID giữ nguyên), Panel_Config được gán lại component mới.
    /// </summary>
    public static class QuestUIBuilder
    {
        const string LogPath = "Assets/_Project/Core/UI/Prefabs/Panel/QuestLogPanel.prefab";
        const string TrackerPath = "Assets/_Project/Core/UI/Prefabs/Panel/QuestTrackerPanel.prefab";
        const string TextBtnPath = "Assets/_Project/Core/UI/Prefabs/Btn/TextBtn.prefab";
        const string PanelConfigPath = "Assets/_Project/Core/UI/Data/Panel_Config.asset";

        static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
        static readonly Color Soft = new Color(0.82f, 0.85f, 0.92f);
        static readonly Color Muted = new Color(0.67f, 0.71f, 0.78f);
        static readonly Color Dark = new Color(0f, 0f, 0f, 0.25f);

        static TMP_FontAsset font;
        static GameObject textBtnPrefab;
        static Sprite rounded;

        [MenuItem("Tools/Core/UI/Build Quest UI")]
        public static void Build()
        {
            if (!Prepare()) return;

            QuestLogPanel log = Save(BuildLog(), LogPath).GetComponent<QuestLogPanel>();
            QuestTrackerPanel tracker = Save(BuildTracker(), TrackerPath).GetComponent<QuestTrackerPanel>();

            var config = AssetDatabase.LoadAssetAtPath<PanelConfig>(PanelConfigPath);
            if (config != null)
            {
                foreach (PanelEntry e in config.entries)
                {
                    if (e.id == UIID.QuestLog) e.prefab = log;
                    else if (e.id == UIID.QuestTracker) e.prefab = tracker;
                }
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            else Debug.LogWarning("QuestUIBuilder: không có " + PanelConfigPath + ", nhớ khai hai panel vào Panel_Config.");
            UIKitSetup.RestyleCore(LogPath);
            UIKitSetup.RestyleCore(TrackerPath);
            Debug.Log("QuestUIBuilder: đã dựng " + LogPath + " và " + TrackerPath + ".");
        }

        /// <summary>Nạp prefab nút, font và sprite bo góc mà các hàm dựng dùng chung. false khi thiếu prefab TextBtn.</summary>
        internal static bool Prepare()
        {
            textBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TextBtnPath);
            if (textBtnPrefab == null)
            {
                Debug.LogError("QuestUIBuilder: không có " + TextBtnPath);
                return false;
            }
            TMP_Text sample = textBtnPrefab.GetComponentInChildren<TMP_Text>(true);
            font = sample != null ? sample.font : null;
            rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            return true;
        }

        internal static GameObject Save(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ================================================================ cửa sổ nhiệm vụ

        static GameObject BuildLog()
        {
            RectTransform root = Rect("QuestLogPanel", null);
            Stretch(root);
            Img(root, new Color(0f, 0f, 0f, 0.6f), raycast: true, sliced: false);
            var panel = root.gameObject.AddComponent<QuestLogPanel>();
            panel.ID = UIID.QuestLog;
            panel.BlockPlayerMove = true;

            RectTransform box = Rect("Box", root);
            Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120f, 640f));
            Img(box, new Color(0.09f, 0.11f, 0.17f, 1f), raycast: true);

            TMP_Text title = Txt("Title", box, "Nhiệm vụ", 32f, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(500f, 52f));

            TextBtn close = Btn("Btn_Close", box, "X", new Vector2(52f, 48f), 26f);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(52f, 48f));
            QuestLogPanel.Paint(close, new Color(0.55f, 0.22f, 0.22f));

            RectTransform line = Rect("Divider", box);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.offsetMin = new Vector2(24f, -72f);
            line.offsetMax = new Vector2(-24f, -70f);
            Img(line, new Color(Gold.r, Gold.g, Gold.b, 0.35f), sliced: false);

            var tabs = new TextBtn[4];
            string[] tabNames = { "Cốt truyện", "Việc phụ", "Hằng ngày", "Quan hệ" };
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = Btn("Tab_" + i, box, tabNames[i], new Vector2(200f, 46f), 21f);
                Place((RectTransform)tabs[i].transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(24f + i * 208f, -84f), new Vector2(200f, 46f));
            }

            // ---- danh sách
            RectTransform listArea = Rect("List", box);
            Offsets(listArea, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(24f, 24f), new Vector2(424f, -142f));
            Img(listArea, Dark);
            ScrollRect listScroll = Scroll(listArea, out RectTransform listContent, new RectOffset(8, 8, 8, 8), 6f);

            TMP_Text empty = Txt("Empty", listArea, "", 18f, Muted, TextAlignmentOptions.Center, FontStyles.Normal);
            Offsets(empty.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -20f));
            empty.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text section = Txt("Section", listContent, "Chương 1", 19f, Gold, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            section.margin = new Vector4(6f, 0f, 0f, 2f);
            Layout(section.gameObject, preferredHeight: 34f);

            QuestListRow row = ListRow(listContent);

            // ---- chi tiết
            RectTransform detailArea = Rect("Detail", box);
            Offsets(detailArea, Vector2.zero, Vector2.one, new Vector2(440f, 24f), new Vector2(-24f, -142f));
            Img(detailArea, Dark);

            RectTransform detailScrollArea = Rect("Scroll", detailArea);
            Offsets(detailScrollArea, Vector2.zero, Vector2.one, new Vector2(0f, 74f), Vector2.zero);
            ScrollRect detailScroll = Scroll(detailScrollArea, out RectTransform detail, new RectOffset(22, 22, 18, 18), 10f);

            TMP_Text dTitle = Para("Title", detail, 30f, Color.white, FontStyles.Bold);
            TMP_Text dMeta = Para("Meta", detail, 17f, Muted, FontStyles.Normal);
            TMP_Text dStatus = Para("Status", detail, 19f, Color.white, FontStyles.Normal);
            TMP_Text dNext = Para("Next", detail, 19f, new Color(0.5f, 0.85f, 1f), FontStyles.Bold);
            TMP_Text dStory = Para("Story", detail, 18f, Soft, FontStyles.Normal);
            dStory.lineSpacing = 6f;
            TMP_Text objHeader = Para("ObjectivesHeader", detail, 21f, Gold, FontStyles.Bold);
            objHeader.text = "Mục tiêu";
            objHeader.margin = new Vector4(0f, 8f, 0f, 0f);

            RectTransform objectives = Rect("Objectives", detail);
            var objLayout = objectives.gameObject.AddComponent<VerticalLayoutGroup>();
            objLayout.spacing = 6f;
            objLayout.childControlWidth = objLayout.childControlHeight = true;
            objLayout.childForceExpandWidth = true;
            objLayout.childForceExpandHeight = false;
            QuestObjectiveRow objective = ObjectiveRow(objectives);

            TMP_Text rewHeader = Para("RewardsHeader", detail, 21f, Gold, FontStyles.Bold);
            rewHeader.text = "Phần thưởng";
            rewHeader.margin = new Vector4(0f, 8f, 0f, 0f);
            TMP_Text rewards = Para("Rewards", detail, 18f, Soft, FontStyles.Normal);

            TextBtn track = Btn("Btn_Track", detailArea, "Chỉ đường", new Vector2(210f, 50f), 21f);
            Place((RectTransform)track.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(210f, 50f));
            TextBtn abandon = Btn("Btn_Abandon", detailArea, "Bỏ nhiệm vụ", new Vector2(190f, 50f), 20f);
            Place((RectTransform)abandon.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-238f, 14f), new Vector2(190f, 50f));
            QuestLogPanel.Paint(abandon, new Color(0.45f, 0.22f, 0.22f));

            var so = new SerializedObject(panel);
            Ref(so, "titleText", title);
            Ref(so, "closeButton", close);
            SerializedProperty tabProp = so.FindProperty("tabButtons");
            tabProp.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++) tabProp.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            Ref(so, "listScroll", listScroll);
            Ref(so, "listContent", listContent);
            Ref(so, "rowTemplate", row);
            Ref(so, "sectionTemplate", section);
            Ref(so, "emptyText", empty);
            Ref(so, "detailScroll", detailScroll);
            Ref(so, "detailRoot", detail.gameObject);
            Ref(so, "detailTitle", dTitle);
            Ref(so, "detailMeta", dMeta);
            Ref(so, "detailStatus", dStatus);
            Ref(so, "detailNext", dNext);
            Ref(so, "detailStory", dStory);
            Ref(so, "objectivesHeader", objHeader);
            Ref(so, "objectivesRoot", objectives);
            Ref(so, "objectiveTemplate", objective);
            Ref(so, "rewardsHeader", rewHeader);
            Ref(so, "rewardsText", rewards);
            Ref(so, "trackButton", track);
            Ref(so, "abandonButton", abandon);
            so.ApplyModifiedPropertiesWithoutUndo();

            section.gameObject.SetActive(false);
            row.gameObject.SetActive(false);
            objective.gameObject.SetActive(false);
            return root.gameObject;
        }

        // dòng danh sách: nền + vạch màu trái + tên + trạng thái + dấu thoi bên phải
        static QuestListRow ListRow(Transform parent)
        {
            RectTransform rt = Rect("Row", parent);
            Image bg = Img(rt, new Color(0.14f, 0.17f, 0.25f, 1f), raycast: true);
            Layout(rt.gameObject, preferredHeight: 66f);
            rt.gameObject.AddComponent<CanvasGroup>();
            BaseBtn button = rt.gameObject.AddComponent<BaseBtn>();
            Tint(button, bg);

            RectTransform stripe = Rect("Stripe", rt);
            Offsets(stripe, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(6f, 0f));
            Image stripeImg = Img(stripe, Gold, sliced: false);

            TMP_Text title = Txt("Title", rt, "Tên nhiệm vụ", 21f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Offsets(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -36f), new Vector2(-34f, -6f));
            title.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text status = Txt("Status", rt, "Trạng thái", 16f, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            Offsets(status.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 6f), new Vector2(-34f, 30f));
            status.overflowMode = TextOverflowModes.Ellipsis;

            Image mark = Diamond(rt, new Vector2(-18f, 0f), new Vector2(1f, 0.5f), 14f);

            var row = rt.gameObject.AddComponent<QuestListRow>();
            row.button = button;
            row.background = bg;
            row.stripe = stripeImg;
            row.title = title;
            row.status = status;
            row.trackedMark = mark;
            return row;
        }

        // dòng mục tiêu: ô tích (viền màu + lòng tối + phần tô khi xong), mô tả, tiến độ
        static QuestObjectiveRow ObjectiveRow(Transform parent)
        {
            RectTransform rt = Rect("Objective", parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            Layout(rt.gameObject, minHeight: 30f);

            RectTransform box = Rect("Box", rt);
            Image boxImg = Img(box, Soft, sliced: false);
            Layout(box.gameObject, preferredWidth: 22f, preferredHeight: 22f);
            RectTransform hole = Rect("Hole", box);
            Offsets(hole, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Img(hole, new Color(0.07f, 0.08f, 0.12f, 1f), sliced: false);
            RectTransform check = Rect("Check", box);
            Offsets(check, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Img(check, new Color(0.55f, 0.94f, 0.55f), sliced: false);

            TMP_Text label = Txt("Label", rt, "Mục tiêu", 19f, Soft, TextAlignmentOptions.MidlineLeft, FontStyles.Normal);
            label.textWrappingMode = TextWrappingModes.Normal;
            Layout(label.gameObject, flexibleWidth: 1f);

            TMP_Text count = Txt("Count", rt, "0/5", 19f, Soft, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            Layout(count.gameObject, preferredWidth: 70f);

            var row = rt.gameObject.AddComponent<QuestObjectiveRow>();
            row.box = boxImg;
            row.check = check.GetComponent<Image>();
            row.label = label;
            row.count = count;
            return row;
        }

        // ================================================================ khung theo dõi (HUD)

        static GameObject BuildTracker()
        {
            RectTransform root = Rect("QuestTrackerPanel", null);
            // góc trái, ngay dưới nút Menu (menu thả xuống có Canvas riêng vẽ đè lên); bên phải là cụm nút skill
            Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -192f), new Vector2(236f, 0f));
            var panel = root.gameObject.AddComponent<QuestTrackerPanel>();
            panel.ID = UIID.QuestTracker;
            panel.BlockPlayerMove = false;

            RectTransform body = Rect("Body", root);
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = Vector2.zero;
            Img(body, new Color(0.08f, 0.1f, 0.16f, 0.85f));
            var v = body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(8, 8, 6, 8);
            v.spacing = 4f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform header = Rect("Header", body);
            var hh = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hh.spacing = 4f;
            hh.childAlignment = TextAnchor.MiddleLeft;
            hh.childControlWidth = hh.childControlHeight = true;
            hh.childForceExpandWidth = hh.childForceExpandHeight = false;
            Layout(header.gameObject, preferredHeight: 28f);
            TMP_Text headerText = Txt("Title", header, "Nhiệm vụ", 18f, Gold, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Layout(headerText.gameObject, flexibleWidth: 1f);
            TextBtn open = Btn("Btn_Open", header, "Mở", new Vector2(52f, 26f), 15f);
            Layout(open.gameObject, preferredWidth: 52f, preferredHeight: 26f);
            TextBtn collapse = Btn("Btn_Collapse", header, "–", new Vector2(28f, 26f), 18f);
            Layout(collapse.gameObject, preferredWidth: 28f, preferredHeight: 26f);

            RectTransform rows = Rect("Rows", body);
            var rv = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 4f;
            rv.childControlWidth = rv.childControlHeight = true;
            rv.childForceExpandWidth = true;
            rv.childForceExpandHeight = false;

            QuestTrackerRow row = TrackerRow(rows);

            TMP_Text more = Txt("More", body, "+ 2 việc khác", 13f, Muted, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            more.margin = new Vector4(10f, 0f, 0f, 0f);

            var so = new SerializedObject(panel);
            Ref(so, "body", body.gameObject);
            Ref(so, "headerText", headerText);
            Ref(so, "openButton", open);
            Ref(so, "collapseButton", collapse);
            Ref(so, "rowsRoot", rows);
            Ref(so, "rowTemplate", row);
            Ref(so, "moreText", more);
            so.ApplyModifiedPropertiesWithoutUndo();

            row.gameObject.SetActive(false);
            more.gameObject.SetActive(false);
            return root.gameObject;
        }

        static QuestTrackerRow TrackerRow(Transform parent)
        {
            RectTransform rt = Rect("Row", parent);
            Image bg = Img(rt, new Color(1f, 1f, 1f, 0.04f), raycast: true);
            BaseBtn button = rt.gameObject.AddComponent<BaseBtn>();
            Tint(button, bg);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 20, 5, 6);
            v.spacing = 1f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            RectTransform stripe = Rect("Stripe", rt);
            Offsets(stripe, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            Image stripeImg = Img(stripe, Gold, sliced: false);
            stripe.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            TMP_Text title = Txt("Title", rt, "Tên nhiệm vụ", 16f, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            title.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text detail = Txt("Detail", rt, "Hạ Slime 2/5", 14f, Soft, TextAlignmentOptions.TopLeft, FontStyles.Normal);
            detail.textWrappingMode = TextWrappingModes.Normal;

            Image mark = Diamond(rt, new Vector2(-10f, -12f), new Vector2(1f, 1f), 10f);
            mark.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var row = rt.gameObject.AddComponent<QuestTrackerRow>();
            row.button = button;
            row.background = bg;
            row.stripe = stripeImg;
            row.title = title;
            row.detail = detail;
            row.trackedMark = mark;
            return row;
        }

        // ================================================================ dựng nhanh

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        internal static void Stretch(RectTransform rt) => Offsets(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        internal static void Offsets(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        internal static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        internal static Image Img(RectTransform rt, Color color, bool raycast = false, bool sliced = true)
        {
            var image = rt.gameObject.AddComponent<Image>();
            if (sliced && rounded != null)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
            }
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        // hình thoi = ô vuông xoay 45 độ
        static Image Diamond(RectTransform parent, Vector2 pos, Vector2 anchor, float size)
        {
            RectTransform rt = Rect("Tracked", parent);
            Place(rt, anchor, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return Img(rt, new Color(0.5f, 0.85f, 1f), sliced: false);
        }

        internal static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // đoạn chữ trong cột chi tiết: xuống dòng, chiều cao theo nội dung (VerticalLayoutGroup đọc preferred height)
        static TextMeshProUGUI Para(string name, Transform parent, float size, Color color, FontStyles style)
        {
            TextMeshProUGUI t = Txt(name, parent, name, size, color, TextAlignmentOptions.TopLeft, style);
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        internal static TextBtn Btn(string name, Transform parent, string label, Vector2 size, float fontSize)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(textBtnPrefab, parent);
            go.name = name;
            ((RectTransform)go.transform).sizeDelta = size;
            TextBtn b = go.GetComponent<TextBtn>();
            TMP_Text t = go.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.text = label;
                t.fontSize = fontSize;
                t.enableAutoSizing = false;
            }
            return b;
        }

        // nút trùm cả dòng: Image là nền (màu do script đặt), bộ màu chỉ làm sáng / tối khi rê / bấm
        static void Tint(Button b, Graphic target)
        {
            b.targetGraphic = target;
            b.transition = Selectable.Transition.ColorTint;
            b.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f),
                pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f),
                colorMultiplier = 1.2f,
                fadeDuration = 0.08f,
            };
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
        }

        internal static ScrollRect Scroll(RectTransform area, out RectTransform content, RectOffset padding, float spacing)
        {
            RectTransform viewport = Rect("Viewport", area);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            Img(viewport, new Color(0f, 0f, 0f, 0f), raycast: true, sliced: false);   // nhận kéo cuộn ở chỗ trống

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = padding;
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = area.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        internal static void Layout(GameObject go, float minHeight = -1f, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f)
        {
            LayoutElement e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (minHeight >= 0f) e.minHeight = minHeight;
            if (preferredWidth >= 0f) e.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f) e.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0f) e.flexibleWidth = flexibleWidth;
        }

        internal static void Ref(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogError("QuestUIBuilder: không có field " + field);
            else p.objectReferenceValue = value;
        }
    }
}
