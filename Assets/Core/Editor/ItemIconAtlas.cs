using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Game.Map;
using Game.Map.Editor;

namespace Game.EditorTools
{
    /// <summary>
    /// Tools/Core/Items/Build Icon Atlas: gom mọi sprite loại Icon trong SpriteData (icon vật phẩm, ngọc, chìa...) vào một
    /// Sprite Atlas (Data/Sprites/ItemIcons.spriteatlasv2).
    /// </summary>
    /// <remarks>
    /// Vì sao: mỗi icon là một texture riêng thì mỗi món đồ rơi trên đất (GroundLoot) và mỗi ô hành trang là một lượt vẽ
    /// riêng; chung một texture atlas thì Unity gộp được (cùng material + cùng texture). Trên mobile lượt vẽ là chi phí CPU
    /// chính. Thêm vật phẩm / icon mới (Build Potion Items, Excel) thì chạy lại menu này.
    /// Atlas chỉ có tác dụng khi Sprite Packer bật (Project Settings &gt; Editor &gt; Sprite Packer Mode): menu tự bật
    /// Sprite Atlas V2 nếu đang tắt. Chỉ sprite nằm trong atlas mới bị gom, sprite khác của project không đổi.
    /// </remarks>
    public static class ItemIconAtlas
    {
        public const string AtlasPath = MapPaths.Sprites + "/ItemIcons.spriteatlasv2";

        [MenuItem("Tools/Core/Items/Build Icon Atlas")]
        public static void Build()
        {
            SpriteData sprites = MapExport.LoadOrCreateAssets().sprites;
            var packables = new List<Object>();
            var seen = new HashSet<Sprite>();
            foreach (SpriteRecord r in sprites.OfType(SpriteType.Icon))
                if (r.sprite != null && seen.Add(r.sprite)) packables.Add(r.sprite);
            if (packables.Count == 0)
            {
                Debug.LogWarning("ItemIconAtlas: SpriteData chưa có sprite loại Icon nào.");
                return;
            }

            // ảnh gốc để không nén: atlas tự nén khi đóng gói, nén hai lần thì mờ / sai màu. Có atlas trong bản build thì
            // Unity chỉ đưa texture atlas vào build, ảnh gốc không vào, nên không tốn thêm dung lượng game.
            int uncompressed = 0;
            foreach (Object o in packables)
            {
                string path = AssetDatabase.GetAssetPath(o);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || ti.textureCompression == TextureImporterCompression.Uncompressed) continue;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                uncompressed++;
            }

            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
            {
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
                Debug.Log("ItemIconAtlas: bật Sprite Packer = Sprite Atlas V2 (trước đó đang tắt, atlas không có tác dụng).");
            }

            // dựng lại danh sách sprite mỗi lần (icon thêm / bớt), giữ file để GUID không đổi
            var atlas = new SpriteAtlasAsset();
            atlas.Add(packables.ToArray());
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                padding = 4,                 // chừa viền kẻo lọc bilinear ăn sang icon bên cạnh
                enableRotation = false,      // UI Image không chịu sprite xoay
                enableTightPacking = false,  // UI Image cần khung chữ nhật
                enableAlphaDilation = true,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                filterMode = FilterMode.Bilinear,
                generateMipMaps = false,     // icon UI vẽ gần 1:1; đồ rơi nhỏ, không cần mip (tốn thêm 1/3 bộ nhớ)
                sRGB = true,
                readable = false,
            };
            importer.SaveAndReimport();

            SpriteAtlas packed = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            SpriteAtlasUtility.PackAtlases(new[] { packed }, EditorUserBuildSettings.activeBuildTarget);
            Debug.Log($"ItemIconAtlas: {packables.Count} icon vào {AtlasPath}" + (uncompressed > 0 ? $", bỏ nén {uncompressed} ảnh gốc." : "."), packed);
        }
    }
}
