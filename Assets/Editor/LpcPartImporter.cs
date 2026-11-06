using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoinHop.Characters;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CoinHop.EditorTools
{
    /// <summary>
    /// Imports LPC part sheets exported by the Universal LPC Spritesheet Character Generator
    /// ("ZIP: Split by animation and item"). Each part lives in its own folder under
    /// <see cref="PARTS_ROOT"/> with one PNG per animation sheet and an <c>lpc-part.json</c>
    /// describing its slot and draw order. Tools &gt; LPC &gt; Build Parts:
    /// <list type="number">
    /// <item>crops every full sheet down to the right-facing frames the animations use, trimmed to
    /// one shared box, and records the box in a <c>&lt;sheet&gt;.crop.json</c> beside it (destructive:
    /// keep the generator export to recover other rows or columns);</item>
    /// <item>slices each strip with the feet pivot (this postprocessor, on import);</item>
    /// <item>builds one <see cref="LpcPart"/> per folder, refills the wardrobe, packs every frame
    /// into one sprite atlas, and dresses the Player prefab.</item>
    /// </list>
    /// </summary>
    public sealed class LpcPartImporter : AssetPostprocessor
    {
        /// <summary>Folder holding one sub-folder per part.</summary>
        public const string PARTS_ROOT = "Assets/Art/Characters/Lpc/Parts";

        /// <summary>Animation data asset created on first build, then tuned by hand.</summary>
        public const string ANIMATIONS_PATH = "Assets/Data/LpcAnimations.asset";

        /// <summary>The prefab dressed by Build Parts.</summary>
        public const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player.prefab";

        /// <summary>Wardrobe catalog refilled with every part on each build.</summary>
        public const string WARDROBE_PATH = "Assets/Data/LpcWardrobe.asset";

        /// <summary>Layout of the temporary outfit panel.</summary>
        public const string WARDROBE_UXML_PATH = "Assets/UI/Wardrobe.uxml";

        /// <summary>Panel settings shared with the HUD.</summary>
        public const string PANEL_SETTINGS_PATH = "Assets/UI/HudPanelSettings.asset";

        /// <summary>Sprite atlas holding every part frame; only this ships in builds, not the source strips.</summary>
        public const string ATLAS_PATH = "Assets/Art/Characters/Lpc/LpcParts.spriteatlasv2";

        /// <summary>Suffix of the crop record written beside a cropped sheet.</summary>
        public const string CROP_SUFFIX = ".crop.json";

        /// <summary>LPC frames are 64 x 64 pixels.</summary>
        public const int FRAME_SIZE = LpcSheetLayout.FRAME_SIZE;

        /// <summary>LPC rows run up, left, down, right; the platformer uses right and mirrors it for left.</summary>
        public const int RIGHT_ROW_FROM_TOP = 3;

        /// <summary>The character stands about 48 px tall, so 48 px per unit makes it one unit tall like the old Player block.</summary>
        public const float PIXELS_PER_UNIT = 48f;

        /// <summary>Feet rest on pixel row 62 of the frame; the pivot sits on that line.</summary>
        public static readonly Vector2 FEET_PIVOT = new Vector2(0.5f, 2f / FRAME_SIZE);

        const string PART_JSON = "lpc-part.json";
        const string VISUAL_CHILD = "Visual";

        [Serializable]
        class PartJson
        {
            public string slot;
            public int sortingOrder;
            public string displayName;
            public bool equippedByDefault;
        }

        static bool IsPartSheet(string path) =>
            path.Replace('\\', '/').StartsWith(PARTS_ROOT + "/", StringComparison.Ordinal)
            && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (!IsPartSheet(assetPath)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var sheet = Path.GetFileNameWithoutExtension(assetPath);
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var crop = ReadCrop(assetPath);
            var rects = new List<SpriteRect>();

            if (crop != null)
            {
                // a cropped strip: one box-sized frame per kept column, left to right
                var pivot = LpcSheetLayout.FeetPivot(crop);
                for (var k = 0; k < crop.Columns.Length; k++)
                    rects.Add(MakeRect(FrameName(sheet, crop.Columns[k]), new Rect(k * crop.Width, 0, crop.Width, crop.Height), pivot, existing));
            }
            else
            {
                // a full LPC sheet not cropped yet: every frame of the right-facing row
                if (height < (RIGHT_ROW_FROM_TOP + 1) * FRAME_SIZE)
                {
                    Debug.LogError($"LPC: {assetPath} is {width}x{height}; expected 4 rows of {FRAME_SIZE} px or a {CROP_SUFFIX}");
                    return;
                }
                var y = height - (RIGHT_ROW_FROM_TOP + 1) * FRAME_SIZE;
                for (var column = 0; column < width / FRAME_SIZE; column++)
                    rects.Add(MakeRect(FrameName(sheet, column), new Rect(column * FRAME_SIZE, y, FRAME_SIZE, FRAME_SIZE), FEET_PIVOT, existing));
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                ?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
        }

        static SpriteRect MakeRect(string name, Rect rect, Vector2 pivot, Dictionary<string, GUID> existing) =>
            new SpriteRect
            {
                name = name,
                rect = rect,
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate(),
            };

        static string CropPathFor(string pngPath) => pngPath.Substring(0, pngPath.Length - ".png".Length) + CROP_SUFFIX;

        static LpcSheetLayout.Crop ReadCrop(string pngPath)
        {
            var path = CropPathFor(pngPath);
            return File.Exists(path) ? JsonUtility.FromJson<LpcSheetLayout.Crop>(File.ReadAllText(path)) : null;
        }

        /// <summary>Sprite name for a sheet column, e.g. <c>run_03</c>.</summary>
        public static string FrameName(string sheet, int column) => $"{sheet}_{column:D2}";

        static bool TryParseColumn(string spriteName, string sheet, out int column)
        {
            column = -1;
            var prefix = sheet + "_";
            return spriteName.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(spriteName.Substring(prefix.Length), out column);
        }

        /// <summary>Crops and re-cuts every part sheet, builds the part assets and atlas, and dresses the Player prefab.</summary>
        [MenuItem("Tools/LPC/Build Parts")]
        public static void BuildParts()
        {
            var animations = EnsureAnimations();
            var (cropped, before, after) = CropSheets(UsedColumns(animations));

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PARTS_ROOT }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            var all = new List<LpcPart>();
            var defaults = new List<LpcPart>();
            foreach (var folder in AssetDatabase.GetSubFolders(PARTS_ROOT))
            {
                var jsonPath = $"{folder}/{PART_JSON}";
                if (!File.Exists(jsonPath)) continue;
                var part = BuildPart(folder, JsonUtility.FromJson<PartJson>(File.ReadAllText(jsonPath)), out var byDefault);
                all.Add(part);
                if (byDefault) defaults.Add(part);
            }

            var wardrobe = RefillWardrobe(all);
            AssetDatabase.SaveAssets();
            var atlasSize = EnsureAtlas();
            DressPlayer(animations, wardrobe, defaults.OrderBy(p => p.SortingOrder));
            if (cropped > 0)
                Debug.Log($"LPC: cropped {cropped} sheets, {before / 1024 / 1024f:F2} MB -> {after / 1024f:F1} KB as RGBA32");
            Debug.Log($"LPC: built {all.Count} parts, {defaults.Count} worn by default; atlas {atlasSize}; Player prefab dressed");
        }

        // Every sheet column some animation shows, per sheet name.
        static Dictionary<string, List<int>> UsedColumns(LpcAnimationSet animations)
        {
            var used = new Dictionary<string, SortedSet<int>>();
            foreach (var entry in animations.Entries)
            {
                if (string.IsNullOrEmpty(entry.Sheet) || entry.Frames == null) continue;
                if (!used.TryGetValue(entry.Sheet, out var set)) used[entry.Sheet] = set = new SortedSet<int>();
                set.UnionWith(entry.Frames);
            }
            return used.ToDictionary(p => p.Key, p => p.Value.ToList());
        }

        // Crops full sheets (no crop record yet) to the used right-facing frames. Returns how many
        // sheets were cropped and their RGBA32 sizes before and after, in bytes.
        static (int count, long before, long after) CropSheets(Dictionary<string, List<int>> used)
        {
            int count = 0; long before = 0, after = 0;
            foreach (var folder in AssetDatabase.GetSubFolders(PARTS_ROOT))
            {
                foreach (var file in Directory.GetFiles(folder, "*.png"))
                {
                    var pngPath = file.Replace('\\', '/');
                    if (File.Exists(CropPathFor(pngPath))) continue;
                    var sheet = Path.GetFileNameWithoutExtension(pngPath);
                    if (!used.TryGetValue(sheet, out var columns) || columns.Count == 0) continue;

                    var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        if (!source.LoadImage(File.ReadAllBytes(pngPath)))
                            throw new InvalidDataException($"LPC: cannot read {pngPath}");
                        var pixels = source.GetPixels32();
                        var crop = LpcSheetLayout.FindCrop(pixels, source.width, source.height, RIGHT_ROW_FROM_TOP, columns);
                        var strip = LpcSheetLayout.BuildStrip(pixels, source.width, source.height, RIGHT_ROW_FROM_TOP, crop);

                        var output = new Texture2D(crop.Width * crop.Columns.Length, crop.Height, TextureFormat.RGBA32, false);
                        try
                        {
                            output.SetPixels32(strip);
                            output.Apply();
                            // record first, so an interrupted run never leaves a strip without its crop
                            File.WriteAllText(CropPathFor(pngPath), JsonUtility.ToJson(crop, true));
                            File.WriteAllBytes(pngPath, output.EncodeToPNG());
                        }
                        finally { UnityEngine.Object.DestroyImmediate(output); }

                        count++;
                        before += (long)source.width * source.height * 4;
                        after += (long)strip.Length * 4;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(source); }
                }
            }
            if (count > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            return (count, before, after);
        }

        // One atlas over the whole parts folder: tight packing drops the transparent space, point
        // filtering and no compression keep the pixel art crisp, and builds ship only the atlas.
        static string EnsureAtlas()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(ATLAS_PATH) == null)
            {
                var asset = new UnityEditor.U2D.SpriteAtlasAsset();
                asset.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(PARTS_ROOT) });
                UnityEditor.U2D.SpriteAtlasAsset.Save(asset, ATLAS_PATH);
                AssetDatabase.ImportAsset(ATLAS_PATH);
            }

            var importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(ATLAS_PATH);
            importer.packingSettings = new UnityEditor.U2D.SpriteAtlasPackingSettings
            {
                enableTightPacking = true,
                enableRotation = false,
                padding = 2,
                blockOffset = 1,
            };
            importer.textureSettings = new UnityEditor.U2D.SpriteAtlasTextureSettings
            {
                filterMode = FilterMode.Point,
                generateMipMaps = false,
                sRGB = true,
                readable = false,
            };
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.maxTextureSize = 2048;
            importer.SetPlatformSettings(platform);
            importer.includeInBuild = true;
            importer.SaveAndReimport();

            // report the packed page size from one packed sprite
            var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(ATLAS_PATH);
            if (atlas == null || atlas.spriteCount == 0) return "empty";
            var packed = new Sprite[atlas.spriteCount];
            atlas.GetSprites(packed);
            var texture = packed[0].texture;
            var size = texture != null ? $"{atlas.spriteCount} sprites on {texture.width}x{texture.height}" : $"{atlas.spriteCount} sprites";
            foreach (var sprite in packed) UnityEngine.Object.DestroyImmediate(sprite);
            return size;
        }

        static LpcPart BuildPart(string folder, PartJson json, out bool equippedByDefault)
        {
            if (!Enum.TryParse(json.slot, out LpcSlot slot))
                throw new InvalidDataException($"LPC: {folder}/{PART_JSON} has unknown slot '{json.slot}'");

            var sheets = new List<LpcPart.Sheet>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path).Replace('\\', '/') != folder) continue;
                var sheetName = Path.GetFileNameWithoutExtension(path);
                // frames are indexed by original sheet column; a cropped strip only has the used ones
                var byColumn = new Dictionary<int, Sprite>();
                foreach (var sprite in AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>())
                    if (TryParseColumn(sprite.name, sheetName, out var column)) byColumn[column] = sprite;
                var frames = new Sprite[byColumn.Count == 0 ? 0 : byColumn.Keys.Max() + 1];
                foreach (var pair in byColumn) frames[pair.Key] = pair.Value;
                sheets.Add(new LpcPart.Sheet { Name = sheetName, Frames = frames });
            }

            var assetPath = $"{folder}/{Path.GetFileName(folder)}.asset";
            var part = AssetDatabase.LoadAssetAtPath<LpcPart>(assetPath);
            if (part == null)
            {
                part = ScriptableObject.CreateInstance<LpcPart>();
                AssetDatabase.CreateAsset(part, assetPath);
            }
            part.Configure(slot, json.sortingOrder, json.displayName, sheets);
            EditorUtility.SetDirty(part);
            equippedByDefault = json.equippedByDefault;
            return part;
        }

        // Starting values only: once the asset exists it is tuned in the Inspector and never overwritten.
        static LpcAnimationSet EnsureAnimations()
        {
            var set = AssetDatabase.LoadAssetAtPath<LpcAnimationSet>(ANIMATIONS_PATH);
            if (set != null) return set;

            set = ScriptableObject.CreateInstance<LpcAnimationSet>();
            set.MovingSpeedThreshold = 0.1f;
            set.Entries = new List<LpcAnimationSet.Entry>
            {
                new LpcAnimationSet.Entry { Name = "idle", Sheet = "idle", Frames = new[] { 0, 0, 1 }, FramesPerSecond = 3f, Loop = true },
                new LpcAnimationSet.Entry { Name = "walk", Sheet = "walk", Frames = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, FramesPerSecond = 10f, Loop = true },
                new LpcAnimationSet.Entry { Name = "run", Sheet = "run", Frames = new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, FramesPerSecond = 12f, Loop = true },
                // jump column 2 is the take-off pose, column 3 the airborne tuck
                new LpcAnimationSet.Entry { Name = "jump", Sheet = "jump", Frames = new[] { 2 }, FramesPerSecond = 10f, Loop = false },
                new LpcAnimationSet.Entry { Name = "fall", Sheet = "jump", Frames = new[] { 3 }, FramesPerSecond = 10f, Loop = false },
            };
            AssetDatabase.CreateAsset(set, ANIMATIONS_PATH);
            return set;
        }

        // Parts are refilled every build; OptionalSlots keeps whatever was set in the Inspector.
        static LpcWardrobeCatalog RefillWardrobe(List<LpcPart> parts)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LpcWardrobeCatalog>(WARDROBE_PATH);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LpcWardrobeCatalog>();
                AssetDatabase.CreateAsset(catalog, WARDROBE_PATH);
            }
            catalog.Parts = parts.OrderBy(p => p.Slot).ThenBy(p => p.DisplayName).ToList();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        // scene edits made in Play mode are thrown away on exit, so the menu is off while playing
        [MenuItem("Tools/LPC/Add Wardrobe UI", true)]
        static bool AddWardrobeUIValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>Adds the temporary outfit panel to the open scene, wired to the player's outfit. Edit mode only.</summary>
        [MenuItem("Tools/LPC/Add Wardrobe UI")]
        public static void AddWardrobeUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("LPC: exit Play mode first; scene changes made while playing are lost");
                return;
            }
            var outfit = UnityEngine.Object.FindFirstObjectByType<OutfitController>();
            if (outfit == null)
            {
                Debug.LogError("LPC: no OutfitController in the open scene; run Tools > LPC > Build Parts first");
                return;
            }

            var view = UnityEngine.Object.FindFirstObjectByType<CoinHop.UI.WardrobeView>();
            GameObject go;
            if (view != null) go = view.gameObject;
            else
            {
                go = new GameObject("Wardrobe UI");
                Undo.RegisterCreatedObjectUndo(go, "Add Wardrobe UI");
            }

            var document = go.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (document == null) document = go.AddComponent<UnityEngine.UIElements.UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(PANEL_SETTINGS_PATH);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(WARDROBE_UXML_PATH);
            document.sortingOrder = 1;

            if (view == null) view = go.AddComponent<CoinHop.UI.WardrobeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_outfit").objectReferenceValue = outfit;
            serialized.ApplyModifiedProperties();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log("LPC: Wardrobe UI added to the scene; save the scene to keep it");
        }

        static void DressPlayer(LpcAnimationSet animations, LpcWardrobeCatalog wardrobe, IEnumerable<LpcPart> outfit)
        {
            using var scope = new PrefabUtility.EditPrefabContentsScope(PLAYER_PREFAB_PATH);
            var root = scope.prefabContentsRoot;

            // the old block stays for reference but no longer draws; the collider is untouched
            var block = root.GetComponent<SpriteRenderer>();
            if (block != null) block.enabled = false;

            var visual = root.transform.Find(VISUAL_CHILD);
            if (visual == null)
            {
                visual = new GameObject(VISUAL_CHILD).transform;
                visual.SetParent(root.transform, false);
            }

            // feet on the collider's bottom edge; undo the root's non-uniform scale so pixels stay square
            var box = root.GetComponent<BoxCollider2D>();
            var bottom = box != null ? box.offset.y - box.size.y / 2f : -0.5f;
            var scale = root.transform.localScale;
            visual.localPosition = new Vector3(0f, bottom, 0f);
            visual.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f);

            // explicit null checks: GetComponent returns Unity's fake null in the editor, which ?? misses
            var character = visual.GetComponent<LayeredCharacter>();
            if (character == null) character = visual.gameObject.AddComponent<LayeredCharacter>();
            var serialized = new SerializedObject(character);
            serialized.FindProperty("_sortingLayerName").stringValue = block != null ? block.sortingLayerName : "Default";
            serialized.FindProperty("_sortingOrderBase").intValue = block != null ? block.sortingOrder : 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            character.Setup(animations, outfit);
            EditorUtility.SetDirty(character);

            var outfitController = visual.GetComponent<OutfitController>();
            if (outfitController == null) outfitController = visual.gameObject.AddComponent<OutfitController>();
            var outfitSerialized = new SerializedObject(outfitController);
            outfitSerialized.FindProperty("_character").objectReferenceValue = character;
            outfitSerialized.FindProperty("_catalog").objectReferenceValue = wardrobe;
            outfitSerialized.ApplyModifiedPropertiesWithoutUndo();

            var driver = root.GetComponent<PlayerAnimationDriver>();
            if (driver == null) driver = root.AddComponent<PlayerAnimationDriver>();
            var driverSerialized = new SerializedObject(driver);
            driverSerialized.FindProperty("_character").objectReferenceValue = character;
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
