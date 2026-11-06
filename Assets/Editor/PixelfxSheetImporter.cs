using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace CoinHop.EditorTools
{
    /// <summary>
    /// Imports sprite sheets exported by pixelfx (<c>node tools/render.mjs &lt;id&gt; --sheet</c>).
    /// Reads the Aseprite-style JSON next to the PNG, configures the texture for pixel art, slices
    /// every frame with the effect's pivot, and builds an AnimationClip, AnimatorController and prefab
    /// beside the sheet.
    /// </summary>
    public static class PixelfxSheetImporter
    {
        /// <summary>Pixels per unit, matching the project's existing sprites.</summary>
        public const float PIXELS_PER_UNIT = 64f;

        /// <summary>Folder searched by <see cref="ImportFromCommandLine"/> when no path is given.</summary>
        public const string DEFAULT_FOLDER = "Assets/Art/VFX";

        const string MENU_PATH = "Tools/pixelfx/Import Sheet";
        const string PATH_ARG = "-pixelfxPath";

        [Serializable]
        class SheetJson { public SheetMeta meta; }

        [Serializable]
        class SheetMeta { public PixelfxMeta pixelfx; }

        /// <summary>The <c>meta.pixelfx</c> block of a pixelfx sheet JSON.</summary>
        [Serializable]
        public class PixelfxMeta
        {
            /// <summary>Effect id, e.g. <c>explosions/plasma-explosion-gold</c>.</summary>
            public string id;
            /// <summary>Playback rate in frames per second.</summary>
            public int fps;
            /// <summary>Whether the effect loops; one-shots end on an empty frame.</summary>
            public bool loop;
            /// <summary>Number of frames in the sheet.</summary>
            public int frames;
            /// <summary>Frame width and height in pixels.</summary>
            public int[] frameSize;
            /// <summary>Effect origin in pixels from the frame's top-left corner.</summary>
            public int[] pivot;
            /// <summary>Frames per row in the sheet.</summary>
            public int columns;
        }

        [MenuItem(MENU_PATH)]
        static void ImportSelected()
        {
            foreach (var path in SelectedSheetPaths())
                Import(path);
        }

        [MenuItem(MENU_PATH, true)]
        static bool ImportSelectedValidate() => SelectedSheetPaths().Any();

        static IEnumerable<string> SelectedSheetPaths() =>
            Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && File.Exists(JsonPathFor(p)));

        /// <summary>
        /// Batchmode entry point. Imports every pixelfx sheet under the folder given by
        /// <c>-pixelfxPath &lt;folder&gt;</c>, or <see cref="DEFAULT_FOLDER"/>.
        /// </summary>
        public static void ImportFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, PATH_ARG);
            var folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : DEFAULT_FOLDER;

            var sheets = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => File.Exists(JsonPathFor(p)))
                .ToList();
            if (sheets.Count == 0)
                throw new InvalidOperationException($"pixelfx: no sheet with a JSON beside it under {folder}");
            foreach (var path in sheets)
                Import(path);
        }

        /// <summary>Imports one sheet: texture settings, slicing, clip, controller and prefab.</summary>
        /// <param name="pngPath">Project-relative path of the sheet PNG.</param>
        public static void Import(string pngPath)
        {
            var meta = ReadMeta(JsonPathFor(pngPath));
            var baseName = Path.GetFileNameWithoutExtension(pngPath);
            var assetName = PascalCase(baseName);
            var folder = Path.GetDirectoryName(pngPath).Replace('\\', '/');

            ConfigureTexture(pngPath);
            Slice(pngPath, baseName, meta);
            var sprites = LoadFrames(pngPath, baseName, meta.frames);
            var clip = BuildClip($"{folder}/{assetName}.anim", sprites, meta);
            var controller = BuildController($"{folder}/{assetName}.controller", clip);
            BuildPrefab($"{folder}/{assetName}.prefab", assetName, sprites[0], controller);

            AssetDatabase.SaveAssets();
            Debug.Log($"pixelfx: imported {meta.id} -> {folder}/{assetName} ({sprites.Length} frames @ {meta.fps} fps)");
        }

        /// <summary>
        /// The sprite rect of frame <paramref name="index"/> in Unity's bottom-left texture space.
        /// </summary>
        public static Rect FrameRect(PixelfxMeta meta, int index, int textureHeight)
        {
            int w = meta.frameSize[0], h = meta.frameSize[1];
            int col = index % meta.columns, row = index / meta.columns;
            return new Rect(col * w, textureHeight - (row + 1) * h, w, h);
        }

        /// <summary>The effect origin as a normalized sprite pivot (Unity's y axis points up).</summary>
        public static Vector2 NormalizedPivot(PixelfxMeta meta) =>
            new Vector2((float)meta.pivot[0] / meta.frameSize[0], 1f - (float)meta.pivot[1] / meta.frameSize[1]);

        /// <summary>Converts <c>plasma-explosion-gold</c> to <c>PlasmaExplosionGold</c>.</summary>
        public static string PascalCase(string kebab) =>
            string.Concat(kebab.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1)));

        static string JsonPathFor(string pngPath) => Path.ChangeExtension(pngPath, ".json");

        static PixelfxMeta ReadMeta(string jsonPath)
        {
            var meta = JsonUtility.FromJson<SheetJson>(File.ReadAllText(jsonPath))?.meta?.pixelfx;
            if (meta == null || meta.frames <= 0 || meta.columns <= 0 || meta.fps <= 0
                || meta.frameSize == null || meta.frameSize.Length != 2 || meta.pivot == null || meta.pivot.Length != 2)
                throw new InvalidDataException($"pixelfx: {jsonPath} has no valid meta.pixelfx block");
            return meta;
        }

        static void ConfigureTexture(string pngPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        static void Slice(string pngPath, string baseName, PixelfxMeta meta)
        {
            var importer = AssetImporter.GetAtPath(pngPath);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var pivot = NormalizedPivot(meta);

            var rects = new SpriteRect[meta.frames];
            for (int i = 0; i < meta.frames; i++)
            {
                var name = FrameName(baseName, i);
                rects[i] = new SpriteRect
                {
                    name = name,
                    rect = FrameRect(meta, i, texture.height),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate(),
                };
            }
            provider.SetSpriteRects(rects);

            // keep sprite file IDs stable across re-imports so the clip's references survive
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));

            provider.Apply();
            importer.SaveAndReimport();
        }

        static string FrameName(string baseName, int index) => $"{baseName}_{index:D2}";

        static Sprite[] LoadFrames(string pngPath, string baseName, int count)
        {
            var byName = AssetDatabase.LoadAllAssetRepresentationsAtPath(pngPath).OfType<Sprite>().ToDictionary(s => s.name);
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                if (!byName.TryGetValue(FrameName(baseName, i), out sprites[i]))
                    throw new InvalidOperationException($"pixelfx: frame {FrameName(baseName, i)} missing after slicing {pngPath}");
            }
            return sprites;
        }

        static AnimationClip BuildClip(string path, Sprite[] sprites, PixelfxMeta meta)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = meta.fps;

            // one key per frame, plus a closing key so the last frame holds for its full duration
            var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < sprites.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = (float)i / meta.fps, value = sprites[i] };
            keys[sprites.Length] = new ObjectReferenceKeyframe { time = (float)sprites.Length / meta.fps, value = sprites[sprites.Length - 1] };

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = meta.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static AnimatorController BuildController(string path, AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null)
                return controller; // keep any states the designer added; the clip asset updates in place
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
        }

        static void BuildPrefab(string path, string name, Sprite firstFrame, AnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return; // the prefab may have been customised; don't overwrite it

            var go = new GameObject(name);
            try
            {
                go.AddComponent<SpriteRenderer>().sprite = firstFrame;
                go.AddComponent<Animator>().runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
