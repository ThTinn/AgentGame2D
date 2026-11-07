using UnityEditor;

namespace Game.Map.Editor
{
    // Where every map data asset lives. One sub-folder per kind of data under Assets/Data.
    //
    //   Assets/Data/MapAssets.asset          the bundle of tables below
    //   Assets/Data/Maps/                    Map_*.asset (MapData) and MapDatabase.asset
    //   Assets/Data/Sprites/                 SpriteData.asset; ObjectSprites/ and MapIcons/ hold the images made for it
    //   Assets/Data/Items/                   DecorItemDatabase.asset
    //   Assets/Data/Objects/                 MapObjectDatabase.asset
    //   Assets/Data/Tiles/                   TileTable.asset, TileLibrary.asset
    public static class MapPaths
    {
        public const string Root = "Assets/_Project/Core/Data";
        public const string Maps = Root + "/Maps";
        public const string Sprites = Root + "/Sprites";
        public const string Items = Root + "/Items";
        public const string Objects = Root + "/Objects";
        public const string Tiles = Root + "/Tiles";

        public const string ObjectSprites = Sprites + "/ObjectSprites";
        public const string MapIcons = Sprites + "/MapIcons";

        public const string MapAssets = Root + "/MapAssets.asset";
        public const string MapDatabase = Maps + "/MapDatabase.asset";
        public const string SpriteData = Sprites + "/SpriteData.asset";
        public const string DecorItems = Items + "/DecorItemDatabase.asset";
        public const string MapObjects = Objects + "/MapObjectDatabase.asset";
        public const string ObjectAnims = Objects + "/ObjectAnimData.asset";
        public const string TileTable = Tiles + "/TileTable.asset";
        public const string TileLibrary = Tiles + "/TileLibrary.asset";

        public static string Map(string name) => Maps + "/" + name + ".asset";

        // creates the folder and every missing parent
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
