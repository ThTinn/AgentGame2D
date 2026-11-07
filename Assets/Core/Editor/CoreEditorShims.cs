using UnityEditor;
using UnityEngine;
using Game.Enemies;
using Game.Items;
using Game.Map;

namespace Game.EditorTools
{
    /// <summary>
    /// Game: bản GAME_2D gán bảng vào các field của GameController 2D trong scene. Bên Game, chỗ giữ bảng là
    /// Resources/LocalPlaySettings.asset (lớp LocalPlaySettings ở Assembly-CSharp, asmdef Editor này không thấy được) nên
    /// gán qua SerializedObject theo tên field. Tên field trùng với GameController 2D: items, drops, options, upgradeTree...
    /// </summary>
    public static class CoreHostAsset
    {
        public const string Path = "Assets/Resources/LocalPlaySettings.asset";

        /// <summary>Gán value vào field của LocalPlaySettings. onlyIfEmpty: chỉ gán khi ô đang trống. True khi có gán.</summary>
        public static bool Bind(string field, Object value, bool onlyIfEmpty)
        {
            var host = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Path);
            if (host == null)
            {
                Debug.LogWarning("Core: chưa có " + Path + ", nhớ gán " + (value != null ? value.name : "bảng") + " vào field " + field + " của nó.");
                return false;
            }
            var so = new SerializedObject(host);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null || prop.objectReferenceValue == value) return false;
            if (onlyIfEmpty && prop.objectReferenceValue != null) return false;
            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Core: đã gán " + (value != null ? value.name : "null") + " vào LocalPlaySettings." + field + ".", host);
            return true;
        }
    }

    /// <summary>Game: thay cho phần ChestTools (công cụ rương của map 2D) mà các công cụ vật phẩm dùng: đường dẫn + tạo ItemDatabase.</summary>
    public static class ChestTools
    {
        public const string ItemDatabasePath = Game.Map.Editor.MapPaths.Root + "/Items/ItemDatabase.asset";

        public static ItemDatabase EnsureItems()
        {
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            if (items != null) return items;
            Game.Map.Editor.MapPaths.EnsureFolder(Game.Map.Editor.MapPaths.Items);
            items = ScriptableObject.CreateInstance<ItemDatabase>();
            items.sprites = AssetDatabase.LoadAssetAtPath<SpriteData>(Game.Map.Editor.MapPaths.SpriteData);
            AssetDatabase.CreateAsset(items, ItemDatabasePath);
            AssetDatabase.SaveAssets();
            return items;
        }
    }

    /// <summary>Game: thay cho MonsterTools (công cụ quái 2D): chỉ cần đường dẫn bảng quái cho sheet Rơi đồ.</summary>
    public static class MonsterTools
    {
        public const string DatabasePath = Game.Map.Editor.MapPaths.Root + "/Monsters/MonsterDatabase.asset";
    }

    /// <summary>Game: thay cho MapExport (xuất map 2D): chỉ cần bảng ảnh SpriteData mà vật phẩm tra icon.</summary>
    public static class MapExport
    {
        public sealed class Assets
        {
            public SpriteData sprites;
        }

        public static Assets LoadOrCreateAssets()
        {
            var sprites = AssetDatabase.LoadAssetAtPath<SpriteData>(Game.Map.Editor.MapPaths.SpriteData);
            if (sprites == null)
            {
                Game.Map.Editor.MapPaths.EnsureFolder(Game.Map.Editor.MapPaths.Sprites);
                sprites = ScriptableObject.CreateInstance<SpriteData>();
                AssetDatabase.CreateAsset(sprites, Game.Map.Editor.MapPaths.SpriteData);
                AssetDatabase.SaveAssets();
            }
            return new Assets { sprites = sprites };
        }
    }
}
