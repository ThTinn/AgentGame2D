using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Áp Space Exploration GUI Kit vào prefab UI. Chạy lại bao nhiêu lần cũng được: Tools/Core/UI/Apply UI Kit.
    /// <list type="number">
    /// <item>Cắt 9-slice cho các sprite kit cần dùng (bản Large, kit để lề 0), tạo font Asap có tiếng Việt, tạo UITheme.</item>
    /// <item>Prefab nền theo thứ tự lồng nhau: chữ, nút, ô nhập, tab. Prefab con hưởng từ đây nếu không ghi đè.</item>
    /// <item>Panel, popup của Core (<see cref="StyleCore"/>): đoán vai trò theo tên object, màu cũ.</item>
    /// <item>Prefab UI cũ (<see cref="StyleOld"/>): đổi sprite cũ sang sprite kit theo bảng <see cref="OldSprites"/>.</item>
    /// </list>
    /// Righteous (font tiêu đề của kit) không có dấu tiếng Việt nên tiêu đề cũng dùng Asap, chỉ khác màu.
    /// </summary>
    public static class UIKitSetup
    {
        const string Kit = "Assets/Space_Exploration_GUI_Kit/";
        const string RingPath = "Assets/_Project/Core/Art/UI/kit-slot-ring.png";
        const string ThemePath = "Assets/_Project/Core/UI/Resources/UITheme.asset";
        const string FontDir = "Assets/_Project/Core/UI/Fonts";
        const string FontPath = FontDir + "/Asap-Bold SDF.asset";
        const string OutlinePath = FontDir + "/Asap-Bold Outline.mat";
        const string FontSource = Kit + "Fonts/Font Sources/static/Asap-Bold.ttf";
        const string CorePrefabs = "Assets/_Project/Core/UI/Prefabs";
        const string Vietnamese = "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ"
            + "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ";

        // Prefab nền, cha đứng trước con để biến thể hưởng kiểu mới.
        static readonly string[] BasePrefabs =
        {
            "Assets/_Project/Core/UI/Prefabs/Btn/TextPrefab.prefab",
            "Assets/_Project/Src/Prefabs/_misc/Text/Text_Regular_64.prefab",
            "Assets/_Project/Src/Prefabs/_misc/Text/NameTxt.prefab",
            "Assets/Resources/_prefab/_ui/Text/Text_Bold_64.prefab",
            "Assets/Resources/_prefab/_ui/Text/Text_Regular_Brown_64.prefab",
            "Assets/Resources/_prefab/_ui/Text/Text_Title_Bold_128.prefab",
            "Assets/_Project/Core/UI/Prefabs/Btn/BaseBtn.prefab",
            "Assets/_Project/Core/UI/Prefabs/Btn/TextBtn.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/BaseButton.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/TxtButton.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/ExitBtn.prefab",
            "Assets/_Project/Src/Prefabs/_misc/InputField.prefab",
            "Assets/Resources/_prefab/_ui/InputField.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/OutSidePanel.prefab",
            "Assets/Resources/_prefab/_ui/_button/OutSidePanel.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/BaseToggle.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/ToggleTab.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/ToggleTab Chat.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/ToggleTabLeft.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/ToggleTabFriend.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/TabInfo.prefab",
            "Assets/_Project/Src/Prefabs/_misc/_button/TgItem.prefab",
        };

        const string Panel = "Containers/Large/setting-container-large.png";
        const string PanelSmall = "Containers/Large/pause-container-large.png";
        const string Solid = "Containers/Large/shop-item-container-large.png";
        const string Inset = "Containers/Large/item-info-container-large.png";
        const string Bar = "Containers/Large/inventory-menu-container-large.png";
        const string Hud = "Grid_Components/Large/level-container-large.png";
        const string Slot = "Grid_Components/Large/inventory-item-container copy-large.png";
        const string SlotLocked = "Grid_Components/Large/inventory-locked-large.png";
        const string Flat = "Settings_&_Menu_Components/Large/short-button-flat-purple-large.png";
        const string FlatDark = "Settings_&_Menu_Components/Large/short-button-flat-dark-purple-large.png";
        const string Input = "Settings_&_Menu_Components/Large/username-password-container-selected-deselected-large.png";
        const string InputOn = "Settings_&_Menu_Components/Large/username-password-container-selected-large.png";
        const string Circle = "Settings_&_Menu_Components/Large/circle-button-blank-large.png";
        const string Dot = "Settings_&_Menu_Components/Large/notification-circle-large.png";
        const string ScrollTrack = "Settings_&_Menu_Components/Large/vertical-scroll-bar-container-large.png";
        const string ScrollHandle = "Settings_&_Menu_Components/Large/vertical-scroll-bar-handle-large.png";
        const string BarEmpty = "Settings_&_Menu_Components/Large/mission-bar-empty-large.png";
        const string CoinBar = "Settings_&_Menu_Components/Large/heart-gem-coin-bar-large.png";
        const string Check = "Settings_&_Menu_Components/Large/check-box-empty-large.png";
        const string CheckOn = "Settings_&_Menu_Components/Large/check-box-ticked-large.png";
        const string Backdrop = "Background_Images/large/background-1-large.png";
        const string Cross = "Picto_Icons/Dark_Purple/cross-128.png";
        const string Person = "Picto_Icons/Light_Purple/person-128.png";
        const string Lock = "Picto_Icons/Light_Purple/lock-128.png";
        const string UpArrow = "Picto_Icons/Dark_Purple/up-arrow-128.png";
        const string Bolt = "Picto_Icons/Dark_Purple/bolt-128.png";

        // Lề 9-slice theo pixel bản Large: trái, dưới, phải, trên.
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { Panel, Vector4.one * 88f },
            { PanelSmall, Vector4.one * 66f },
            { Solid, Vector4.one * 20f },
            { Inset, Vector4.one * 20f },
            { Bar, Vector4.one * 20f },
            { Hud, Vector4.one * 24f },
            { Slot, Vector4.one * 24f },
            { Flat, Vector4.one * 30f },
            { FlatDark, Vector4.one * 30f },
            { Input, Vector4.one * 30f },
            { InputOn, Vector4.one * 30f },
            { ScrollTrack, new Vector4(0f, 20f, 0f, 20f) },
            { ScrollHandle, new Vector4(0f, 20f, 0f, 20f) },
            { BarEmpty, new Vector4(20f, 12f, 20f, 12f) },
            { CoinBar, Vector4.one * 24f },
        };

        static readonly string[] ButtonColors = { "blue", "pink", "purple", "yellow" };
        static readonly string[] ButtonShapes = { "medium", "short", "square" };
        const float ButtonBorder = 36f;

        // Sprite cũ -> sprite kit. Sprite nút có thêm trạng thái, xử lý riêng ở StyleSelectable; icon vật phẩm giữ nguyên.
        static readonly Dictionary<string, string> OldSprites = new Dictionary<string, string>
        {
            { "Shape_17", Input }, { "Khung điền text", Input },
            { "Shape_12", PanelSmall },
            { "khung popup", Solid }, { "Viền khung chỉ số", Solid },
            { "Khung danh sách user", Inset }, { "Khung viền chú thích skill", Inset }, { "khung trang bị", Inset }, { "Khung lớn skill", Inset },
            { "Khung title", Bar }, { "KHung title skill", Bar }, { "Khung title chỉ số", Bar }, { "Khung title item info", Bar },
            { "Ô item thường", Slot }, { "Ô trang bị", Slot }, { "Nền skill phụ", Slot }, { "Khung ô skill 2", Slot }, { "Ô item block", SlotLocked },
            { "Nút tab", FlatDark }, { "tab chat", FlatDark }, { "Tab", FlatDark }, { "Tab vật phẩm", FlatDark },
            { "Nút tab focus", Flat }, { "tab chat focus", Flat }, { "Tab focus", Flat }, { "Tab vật phẩm focus", Flat },
            { "Thanh trượt", ScrollTrack }, { "Nút trượt", ScrollHandle },
            { "thông báo", Dot }, { "KHung đỏ", Dot },
            { "Shape_18", Check }, { "check", Check }, { "Shape_04", CheckOn }, { "check 1", CheckOn },
            { "Number_Other", Circle }, { "ShootBS", Circle }, { "ShootB", Circle }, { "ShootPS", Circle },
            { "NavB_01", Circle }, { "BubbleB", Circle }, { "Khung tròn", Circle }, { "Nút thoát và xóa", Circle },
            { "Khung coin, kim cương, ruby", CoinBar },
            { "MainBG", Backdrop },
            { "Icon_108", Person }, { "Icon_81", Lock }, { "Icon_32", UpArrow }, { "Icon_05", Bolt },
        };

        static readonly Dictionary<string, string> OldButtons = new Dictionary<string, string>
        {
            { "Nút xanh dương", "blue" }, { "Nút đỏ", "pink" }, { "Nút chức năng", "purple" },
        };

        // Màu nâu / be của giao diện cũ -> màu tím nhạt của kit (so theo RGB, giữ alpha).
        static readonly Dictionary<string, Color32> OldColors = new Dictionary<string, Color32>
        {
            { "683A04", new Color32(231, 225, 255, 255) }, { "933E00", new Color32(133, 125, 211, 255) },
            { "A66315", new Color32(236, 216, 183, 255) }, { "DCCBB3", new Color32(255, 255, 255, 255) },
            { "A98B8D", new Color32(201, 192, 238, 255) }, { "BE8D55", new Color32(201, 192, 238, 255) },
            { "BE8E55", new Color32(201, 192, 238, 255) }, { "764B18", new Color32(133, 125, 211, 255) },
            { "A96210", new Color32(133, 125, 211, 255) }, { "E7D2AA", new Color32(39, 28, 71, 255) },
            { "F4E7D6", new Color32(255, 255, 255, 255) }, { "B4E5D8", new Color32(39, 28, 71, 255) },
        };

        static readonly Color SecondaryScrim = new Color32(13, 9, 34, 120);
        static readonly Color InactiveTab = new Color(0.62f, 0.58f, 0.76f);
        const float FillBleed = 4f;        // sprite nền chừa lề trong suốt: nới ra cho khít viền khung
        const float MaxBoxHeight = 700f;   // cao hơn nữa thì khung lọt khỏi màn 16:9 (canvas Core cao 720)

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static TMP_FontAsset font;
        static Material outline;

        [MenuItem("Tools/Core/UI/Apply UI Kit")]
        public static void Apply()
        {
            ApplyBase();
            ApplyCore();
            ApplyOld();
            Debug.Log("UI Kit: xong.");
        }

        public static void ApplyBase()
        {
            Prepare();
            foreach (string path in BasePrefabs) Edit(path, StyleBase);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyCore()
        {
            Prepare();
            foreach (string path in Prefabs(CorePrefabs)) if (!BasePrefabs.Contains(path)) Edit(path, StyleCore);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyOld()
        {
            Prepare();
            foreach (string path in OldUiPrefabs()) Edit(path, StyleOld);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Áp lại kiểu cho một prefab Core vừa dựng lại bằng tool khác (Build Quest UI, Build Gear Upgrade UI).</summary>
        public static void RestyleCore(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath) == null) return;
            Prepare();
            Edit(path, StyleCore);
        }

        static void Prepare()
        {
            ImportSprites();
            cache.Clear();
            font = BuildFont();
            outline = BuildOutline();
            BuildTheme();
            UseAsDefaultFont();
        }

        #region Sprite, font, theme

        static IEnumerable<KeyValuePair<string, Vector4>> SlicedSprites()
        {
            foreach (var kv in Borders) yield return new KeyValuePair<string, Vector4>(Kit + kv.Key, kv.Value);
            yield return new KeyValuePair<string, Vector4>(RingPath, Vector4.one * 40f);
            Vector4 b = Vector4.one * ButtonBorder;
            foreach (string shape in ButtonShapes)
            {
                yield return new KeyValuePair<string, Vector4>(Kit + ButtonDisabled(shape), b);
                foreach (string color in ButtonColors)
                {
                    yield return new KeyValuePair<string, Vector4>(Kit + ButtonNormal(shape, color), b);
                    yield return new KeyValuePair<string, Vector4>(Kit + ButtonHighlight(shape, color), b);
                    yield return new KeyValuePair<string, Vector4>(Kit + ButtonPressed(shape, color), b);
                }
            }
        }

        static string ButtonNormal(string shape, string color) => $"Button_Images/Source_Image_Sprites/large/{shape}-{color}-large.png";
        static string ButtonHighlight(string shape, string color) => $"Button_Images/Highlighted_Sprite/large/{shape}-{color}-highlight-large.png";
        static string ButtonPressed(string shape, string color) => $"Button_Images/Pressed_Sprites/Large/{shape}-{color}-pressed-large.png";
        static string ButtonDisabled(string shape) => $"Button_Images/Disabled_Sprites/Large/{shape}-disabled-large.png";

        static void ImportSprites()
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();

            var ring = (TextureImporter)AssetImporter.GetAtPath(RingPath);
            if (ring.textureType != TextureImporterType.Sprite)
            {
                ring.textureType = TextureImporterType.Sprite;
                ring.spriteImportMode = SpriteImportMode.Single;
                ring.alphaIsTransparency = true;
                ring.mipmapEnabled = false;
                ring.SaveAndReimport();
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in SlicedSprites())
                {
                    var importer = AssetImporter.GetAtPath(kv.Key) as TextureImporter;
                    if (importer == null)
                    {
                        Debug.LogError("UI Kit: không thấy sprite " + kv.Key);
                        continue;
                    }
                    ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                    provider.InitSpriteEditorDataProvider();
                    SpriteRect[] rects = provider.GetSpriteRects();
                    if (rects.All(r => r.border == kv.Value)) continue;
                    foreach (SpriteRect r in rects) r.border = kv.Value;
                    provider.SetSpriteRects(rects);
                    provider.Apply();
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static TMP_FontAsset BuildFont()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (asset == null)
            {
                Directory.CreateDirectory(FontDir);
                asset = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(FontSource), 48, 5, GlyphRenderMode.SDFAA, 1024, 1024);
                asset.name = "Asap-Bold SDF";
                AssetDatabase.CreateAsset(asset, FontPath);
                asset.atlasTexture.name = asset.name + " Atlas";
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            asset.TryAddCharacters(new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + Vietnamese + "«»·–—…“”‘’•×");
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
            if (fallback != null)
            {
                if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!asset.fallbackFontAssetTable.Contains(fallback)) asset.fallbackFontAssetTable.Add(fallback);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        // Chữ trên nền 3D (HUD, số trên ô) cần viền mới đọc được.
        static Material BuildOutline()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "Asap-Bold Outline" };
                AssetDatabase.CreateAsset(mat, OutlinePath);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, UITheme.TextOnButton);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void BuildTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.font = font;
            theme.panel = Get(Panel);
            theme.panelSmall = Get(PanelSmall);
            theme.inset = Get(Inset);
            theme.slot = Get(Slot);
            theme.button = Get(Flat);
            theme.buttonDark = Get(FlatDark);
            theme.input = Get(Input);
            theme.circle = Get(Circle);
            EditorUtility.SetDirty(theme);
        }

        // Chữ dựng bằng code không chọn font thì lấy font mặc định của TMP.
        static void UseAsDefaultFont()
        {
            var so = new SerializedObject(TMP_Settings.instance);
            SerializedProperty p = so.FindProperty("m_defaultFontAsset");
            if (p == null || p.objectReferenceValue == font) return;
            p.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Sprite Get(string kitPath)
        {
            string path = kitPath.StartsWith("Assets/") ? kitPath : Kit + kitPath;
            if (cache.TryGetValue(path, out Sprite s)) return s;
            s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (s == null) Debug.LogError("UI Kit: không tải được sprite " + path);
            cache[path] = s;
            return s;
        }

        #endregion

        #region Prefab

        static IEnumerable<string> Prefabs(string folder) =>
            AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);

        static IEnumerable<string> OldUiPrefabs()
        {
            IEnumerable<string> all = Prefabs("Assets/Resources/_prefab/_ui")
                .Concat(Prefabs("Assets/_Project/Src/Prefabs/GameScene"))
                .Concat(Prefabs("Assets/_Project/Src/Prefabs/LoadingScene"))
                .Concat(Prefabs("Assets/_Project/Src/Prefabs/Popup"))
                .Concat(Prefabs("Assets/_Project/Src/Prefabs/_misc"))
                .Concat(Prefabs("Assets/_Project/Src/UI"))
                .Append("Assets/_Project/Src/Scripts/UI_Script/LoginScene/CanvasLogin.prefab");
            return all.Where(p => !BasePrefabs.Contains(p));
        }

        static void Edit(string path, Action<GameObject> style)
        {
            if (IsBroken(path))
            {
                Debug.LogWarning("UI Kit: bỏ qua prefab đã hỏng sẵn (thiếu script hoặc prefab con): " + path);
                return;
            }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                style(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Unity từ chối lưu prefab thiếu script, còn prefab thiếu con thì lưu được nhưng dễ mất chỗ ghi đè.
        static bool IsBroken(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Transform t in asset.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) return true;
            }
            foreach (string line in File.ReadLines(path))
            {
                int at = line.IndexOf("m_SourcePrefab: {fileID: ", StringComparison.Ordinal);
                if (at < 0) continue;
                int guid = line.IndexOf("guid: ", at, StringComparison.Ordinal);
                if (guid >= 0 && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(line.Substring(guid + 6, 32)))) return true;
            }
            return false;
        }

        static void Paint(Image image, string kitPath)
        {
            UITheme.Paint(image, Get(kitPath));
            image.preserveAspect = false;
        }

        static void StyleText(TMP_Text text)
        {
            string old = text.fontSharedMaterial ? text.fontSharedMaterial.name : string.Empty;
            bool outlined = old.Contains("Outline") || old.Contains("UnderLay") || old.Contains("ButtonTxt");
            if (text.font != font) text.font = font;
            Material want = outlined ? outline : font.material;
            if (text.fontSharedMaterial != want) text.fontSharedMaterial = want;
            text.fontStyle &= ~FontStyles.Bold;
            Recolor(text);
        }

        // Asap rộng hơn Myriad / Liberation: ô hẹp giữ một dòng, chữ tự co thay vì tràn hoặc xuống dòng
        static void FitOneLine(TMP_Text text, float maxSize)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = maxSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        static void Recolor(Graphic g)
        {
            string hex = ColorUtility.ToHtmlStringRGB(g.color);
            if (!OldColors.TryGetValue(hex, out Color32 c)) return;
            Color next = c;
            next.a = g.color.a;
            g.color = next;
        }

        static ColorBlock TintColors() => new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.94f, 0.92f, 1f),
            pressedColor = new Color(0.72f, 0.69f, 0.85f),
            selectedColor = Color.white,
            disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        /// <summary>Nút phẳng tím của kit: chữ trắng, đổi sắc khi rê / bấm nên code vẫn tô màu chữ được.</summary>
        static void FlatButton(Selectable b, Image image)
        {
            Paint(image, Flat);
            image.color = Color.white;
            b.targetGraphic = image;
            b.transition = Selectable.Transition.ColorTint;
            b.colors = TintColors();
            b.spriteState = default;
            foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
                if (t.color == UITheme.TextOnButton) t.color = Color.white;
        }

        /// <summary>Nút màu của kit: đủ ảnh trạng thái, chữ tối. Dáng chọn theo tỉ lệ nút để lượn sóng khỏi méo.</summary>
        static void PastelButton(Selectable b, Image image, string color)
        {
            Rect r = ((RectTransform)b.transform).rect;
            float ratio = r.height > 0f ? r.width / r.height : 3f;
            string shape = ratio >= 2.4f ? "medium" : ratio >= 1.4f ? "short" : "square";
            Paint(image, ButtonNormal(shape, color));
            image.color = Color.white;
            b.targetGraphic = image;
            b.transition = Selectable.Transition.SpriteSwap;
            b.colors = TintColors();
            b.spriteState = new SpriteState
            {
                highlightedSprite = Get(ButtonHighlight(shape, color)),
                selectedSprite = Get(ButtonNormal(shape, color)),
                pressedSprite = Get(ButtonPressed(shape, color)),
                disabledSprite = Get(ButtonDisabled(shape)),
            };
            foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
            {
                t.color = UITheme.TextOnButton;
                if (t.fontSharedMaterial != font.material) t.fontSharedMaterial = font.material;
            }
        }

        /// <summary>Nút tròn đóng của kit: chữ X đổi thành icon.</summary>
        static void CloseButton(Selectable b, Image image)
        {
            Paint(image, Circle);
            image.color = Color.white;
            image.preserveAspect = true;
            b.targetGraphic = image;
            b.transition = Selectable.Transition.ColorTint;
            b.colors = TintColors();
            b.spriteState = default;
            foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true)) t.enabled = false;

            Transform cross = b.transform.Find("KitCross");
            if (cross == null)
            {
                var go = new GameObject("KitCross", typeof(RectTransform));
                go.transform.SetParent(b.transform, false);
                cross = go.transform;
            }
            var rt = (RectTransform)cross;
            rt.anchorMin = new Vector2(0.22f, 0.22f);
            rt.anchorMax = new Vector2(0.78f, 0.78f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image icon = cross.GetComponent<Image>();
            if (icon == null) icon = cross.gameObject.AddComponent<Image>();
            icon.sprite = Get(Cross);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        #endregion

        #region UI cũ

        // Hai nút gốc của Core là nút trơn chưa có sprite nên bảng sprite cũ không với tới.
        static void StyleBase(GameObject root)
        {
            StyleOld(root);
            if (root.name == "OutSidePanel")
            {
                root.GetComponent<Image>().color = UITheme.Scrim;
                return;
            }
            if (root.TryGetComponent(out BaseBtn b) && b.image != null && b.image.sprite == null && b.transition == Selectable.Transition.ColorTint)
                FlatButton(b, b.image);
        }

        static void StyleOld(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                StyleText(text);
                if (text.name == "LoginTitle")
                {
                    text.color = UITheme.TextTitle;
                    text.fontSize = 84f;
                }
            }

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                Recolor(image);
                if (image.sprite == null)
                {
                    // vùng nội dung của khung chat đang để trắng
                    if (image.name == "main" || image.name == "content" || image.name == "inputHolder") image.color = UITheme.TextOnButton;
                    continue;
                }
                var b = image.GetComponent<Selectable>();
                string name = image.sprite.name;
                if (name == "Background" && image.color.a > 0f && image.color.a < 0.5f)
                {
                    Paint(image, Hud);
                    image.color = Color.white;
                }
                else if (name == "Background" && image.color == Color.black)
                {
                    Paint(image, Bar);
                    image.color = Color.white;
                }
                else if (name == "UISprite" && b is Button) FlatButton(b, image);
                else if (name == "Nút tắt 1" && b != null) CloseButton(b, image);
                else if (OldButtons.TryGetValue(name, out string color) && b != null) PastelButton(b, image, color);
                else if (OldSprites.TryGetValue(name, out string kit))
                {
                    bool preserve = kit == Circle || kit == Person || kit == Lock || kit == UpArrow || kit == Bolt;
                    Paint(image, kit);
                    image.preserveAspect = preserve;
                    if (b != null && b.transition == Selectable.Transition.SpriteSwap) b.spriteState = default;
                }
            }

            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                input.transition = Selectable.Transition.SpriteSwap;
                input.spriteState = new SpriteState { selectedSprite = Get(InputOn), highlightedSprite = Get(InputOn), pressedSprite = Get(InputOn) };
                if (input.image != null) input.image.color = Color.white;

                // ô nhập chat thấp: cỡ chữ gốc của prefab tràn ô, icon đè lên chữ
                float height = ((RectTransform)input.transform).rect.height;
                if (height <= 0f || height >= 60f) continue;
                if (input.textComponent != null) input.textComponent.fontSize = 24f;
                if (input.placeholder is TMP_Text hint) hint.fontSize = 24f;
                Transform icon = input.transform.Find("icon");
                if (icon != null) icon.gameObject.SetActive(false);
            }

            // tab chat hẹp: Asap rộng hơn Myriad nên chữ cỡ cũ tràn sang tab bên cạnh
            foreach (Toggle tab in root.GetComponentsInChildren<Toggle>(true))
            {
                if (((RectTransform)tab.transform).rect.width > 130f || tab.transform.Find("GameObject") == null) continue;
                foreach (TMP_Text t in tab.GetComponentsInChildren<TMP_Text>(true)) FitOneLine(t, 24f);
            }
        }

        #endregion

        #region Core

        static void StyleCore(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                StyleText(text);
                if (IsTitle(text)) text.color = UITheme.TextTitle;
                if (text.name == "Affinity") FitOneLine(text, text.fontSize);
            }
            foreach (Selectable b in root.GetComponentsInChildren<Selectable>(true)) StyleSelectable(b);
            foreach (Image image in root.GetComponentsInChildren<Image>(true)) StyleImage(image, root);
            foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (input.TryGetComponent(out Image image)) Paint(image, Input);
                input.image.color = Color.white;
                input.transition = Selectable.Transition.SpriteSwap;
                input.spriteState = new SpriteState { selectedSprite = Get(InputOn), highlightedSprite = Get(InputOn), pressedSprite = Get(InputOn) };
            }
        }

        static bool IsTitle(TMP_Text text)
        {
            if (text.name == "Title" || text.name == "PickerTitle") return text.fontSize >= 24f;
            Transform p = text.transform.parent;
            return p != null && (p.name == "Box" || p.name == "Title") && text.fontSize >= 30f;
        }

        static void StyleSelectable(Selectable b)
        {
            if (b is TMP_InputField || b.GetComponent<InventorySlot>() != null) return;
            if (b is not Button) return;
            var image = b.GetComponent<Image>();
            if (image == null || image.color.a <= 0f || b.transition == Selectable.Transition.None) return;

            string n = b.name;
            Rect r = ((RectTransform)b.transform).rect;
            // hàng nút hẹp của popup vật phẩm: Asap rộng hơn nên hạ cỡ chữ cho khỏi chạm viền
            if (b.transform.parent.name == "Buttons" && b is TextBtn && b.GetComponentInChildren<TMP_Text>(true) is TMP_Text label && r.width < 160f) label.fontSize = Mathf.Min(label.fontSize, 21f);
            if (r.width > 0f && r.width <= 60f && (n.Contains("Close") || n.Contains("Cancel"))) CloseButton(b, image);
            else if (n == "Btn_Discard" || n == "Btn_Abandon" || n == "Btn_Reset") PastelButton(b, image, "pink");
            else if (b.transform.parent.name != "Drawer" && (n == "Btn_Upgrade" || n == "Btn_Equip" || n == "Btn_Move" || n == "Btn_Accept" || n == "Btn_UpgradeTree")) PastelButton(b, image, "blue");
            else if (b is BaseBtn && b.GetComponent<QuestListRow>() == null && b.GetComponent<QuestTrackerRow>() == null)
            {
                FlatButton(b, image);
                // tab khung thông tin: script tô nút đang chọn màu trắng, nút còn lại giữ màu gốc của prefab
                if (n.StartsWith("Tab_") && b.GetComponentInParent<QuestLogPanel>(true) == null) image.color = InactiveTab;
            }
        }

        static void StyleImage(Image image, GameObject root)
        {
            Transform t = image.transform;
            string n = t.name;
            if (n == "KitFrame" || n == "KitFill" || n == "KitCross") return;

            var slot = t.GetComponent<InventorySlot>();
            if (slot != null)
            {
                Paint(image, RingPath);
                image.color = Color.white;
                slot.Button.transition = Selectable.Transition.ColorTint;
                slot.Button.colors = TintColors();
                return;
            }
            if (t.GetComponent<Selectable>() != null || t.GetComponentInParent<QuestObjectiveRow>(true) != null) return;
            if (n == "Box" || n == "PickerBox")
            {
                Frame(image);
                return;
            }
            if (image.color.a <= 0f) return;

            bool plain = image.sprite == null || image.sprite.name == "UISprite" || IsKitSprite(image.sprite);
            if (!plain) return;

            Color c = image.color;
            bool black = c.r + c.g + c.b < 0.01f;
            if (black && c.a >= 0.5f && IsStretched(t)) image.color = n == "Picker" ? SecondaryScrim : UITheme.Scrim;
            else if (n == "Body" && t.parent.GetComponent<Image>() != null) image.enabled = false;
            else if (n == "Drawer" || n == "Card" || n == "Body" || t == root.transform) { Paint(image, Hud); image.color = Color.white; }
            else if (n == "Inner" || n == "Portrait") { Paint(image, Slot); image.color = Color.white; }
            else if (n == "IconFrame") Paint(image, RingPath);
            else if (n == "Slot") { Paint(image, Slot); image.color = Color.white; }
            else if (n == "Divider" || n == "Line") image.color = new Color32(133, 125, 211, 160);
            else if (n == "NamePlate" || n == "AffinityPlate" || n == "Level") { Paint(image, Bar); image.color = Color.white; }
            else if (n == "HpBar" || n == "MpBar" || n == "ExpBar") { Paint(image, BarEmpty); image.color = Color.white; }
            else if (black) { Paint(image, Inset); image.color = Color.white; }
        }

        // chạy lại trên prefab đã áp kit: sprite kit vẫn phải qua các luật ở StyleImage
        static bool IsKitSprite(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            return path.StartsWith(Kit) || path == RingPath;
        }

        static bool IsStretched(Transform t)
        {
            var rt = (RectTransform)t;
            return rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
        }

        // Khung kit có viền sáng tỏa ra ngoài nên phải vẽ rộng hơn Box, nội dung trong Box giữ nguyên chỗ.
        static void Frame(Image box)
        {
            Rect r = box.rectTransform.rect;
            bool big = Mathf.Max(r.width, r.height) >= 800f;
            float pad = big ? 42f : 31f;
            if (r.height > MaxBoxHeight) box.rectTransform.localScale = Vector3.one * (MaxBoxHeight / r.height);

            box.sprite = null;
            box.type = Image.Type.Simple;
            box.color = new Color(0f, 0f, 0f, 0f);

            Transform frame = box.transform.Find("KitFrame");
            if (frame == null)
            {
                var go = new GameObject("KitFrame", typeof(RectTransform));
                go.transform.SetParent(box.transform, false);
                frame = go.transform;
            }
            frame.SetAsFirstSibling();
            var rt = (RectTransform)frame;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-pad, -pad);
            rt.offsetMax = new Vector2(pad, pad);

            Image image = frame.GetComponent<Image>();
            if (image == null) image = frame.gameObject.AddComponent<Image>();
            Paint(image, big ? Panel : PanelSmall);
            image.color = Color.white;
            image.raycastTarget = false;

            // nền kit trong suốt, thiếu lớp này panel nhìn xuyên thấy HUD
            Transform fill = box.transform.Find("KitFill");
            if (fill == null)
            {
                var go = new GameObject("KitFill", typeof(RectTransform));
                go.transform.SetParent(box.transform, false);
                fill = go.transform;
            }
            fill.SetAsFirstSibling();
            var fr = (RectTransform)fill;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = -Vector2.one * FillBleed;
            fr.offsetMax = Vector2.one * FillBleed;
            Image fillImage = fill.GetComponent<Image>();
            if (fillImage == null) fillImage = fill.gameObject.AddComponent<Image>();
            Paint(fillImage, Solid);
            fillImage.color = Color.white;
            fillImage.raycastTarget = false;
        }

        #endregion
    }
}
