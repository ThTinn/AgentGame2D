using System;
using System.Text;
using UnityEngine;

namespace DungeonOnline.Net
{
    /// <summary>
    /// The 2D maps the server sends as template <see cref="TemplateType"/> (ServerJava/data/templates/maps.json). The server
    /// reads the same file for spawn points and monster camps, so both sides always agree. Units are metres.
    /// </summary>
    /// <example>
    /// <code>
    /// net.Data.TemplateReady += (type, bytes) => { if (type == MapTable.TemplateType) maps = MapTable.Parse(bytes); };
    /// MapInfo town = maps.Find(0);
    /// </code>
    /// </example>
    [Serializable]
    public sealed class MapTable
    {
        /// <summary>Template type of maps.json in the server manifest (WorldLoader.TEMPLATE_MAPS).</summary>
        public const int TemplateType = 5;

        public int version;
        public MapInfo[] maps = Array.Empty<MapInfo>();

        /// <summary>Reads maps.json bytes (UTF-8). Returns an empty table for null or empty input.</summary>
        public static MapTable Parse(byte[] utf8)
        {
            if (utf8 == null || utf8.Length == 0) return new MapTable();
            return JsonUtility.FromJson<MapTable>(Encoding.UTF8.GetString(utf8)) ?? new MapTable();
        }

        /// <summary>The map with this id, or null.</summary>
        public MapInfo Find(int id)
        {
            foreach (MapInfo map in maps)
                if (map != null && map.id == id) return map;
            return null;
        }
    }

    /// <summary>One 2D map: where players appear, what they stand on, where monsters live.</summary>
    [Serializable]
    public sealed class MapInfo
    {
        public int id;
        /// <summary>Display name "vi|en", read through Lang.GetSplitText.</summary>
        public string name;
        /// <summary>Walkable range between the walls; the server keeps bots inside it.</summary>
        public float xMin, xMax;
        /// <summary>Falling below this height returns the player to the spawn point.</summary>
        public float fallLimitY;
        public Point spawn;
        public Solid[] solids = Array.Empty<Solid>();
        public Camp[] camps = Array.Empty<Camp>();
    }

    [Serializable]
    public struct Point
    {
        public float x, y;

        public Vector2 ToVector() => new Vector2(x, y);
    }

    /// <summary>A box players collide with: ground, platform or wall. (x, y) is its centre, (w, h) its size.</summary>
    [Serializable]
    public sealed class Solid
    {
        public string name;
        public float x, y, w, h;
        /// <summary>False for walls: they block but are not drawn.</summary>
        public bool visible;
    }

    /// <summary>A monster camp. The server spawns and moves the monsters; the client only reads this for tools.</summary>
    [Serializable]
    public sealed class Camp
    {
        public int template, count, level;
        public float x, y, range;
        public long maxHp, damage;
    }
}
