using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;

namespace Game.Map
{
    // What a sprite is used for. Editor pickers list only the sprites of the type they need.
    // Decor is 0 so records saved before types existed stay valid.
    // Effect: bullets / hit effects of skills (SkillInfo.idAtk).
    public enum SpriteType : byte { Decor, Object, Tile, Icon, Effect }

    [Serializable]
    public class SpriteRecord
    {
        public int id;
        public string name;
        public SpriteType type;
        public Sprite sprite;
    }

    // What the exported SpriteData contains: ids, names and types only (images stay in the game build).
    public struct SpriteRow { public int id; public string name; public SpriteType type; }

    // Authoring asset: every image a decor item can use, with a stable id. DecorItemDatabase refers to images by these ids.
    // Ids are never reused, so add new sprites with "Sync from TileLibrary" (existing ids stay the same).
    [CreateAssetMenu(fileName = "SpriteData", menuName = "Map/Sprite Data")]
    public class SpriteData : ScriptableObject
    {
        public const int CurrentSchema = 2;   // 2: sprites carry a type

        public int schemaVersion = CurrentSchema;
        public List<SpriteRecord> sprites = new List<SpriteRecord>();

        [NonSerialized] Dictionary<int, SpriteRecord> byId;
        [NonSerialized] Dictionary<string, SpriteRecord> byName;

        void Index()
        {
            if (byId != null && byId.Count == sprites.Count) return;
            byId = new Dictionary<int, SpriteRecord>(sprites.Count);
            byName = new Dictionary<string, SpriteRecord>(sprites.Count);
            foreach (var s in sprites) { byId[s.id] = s; byName[s.name] = s; }
        }

        public void ClearCache() { byId = null; byName = null; }

        public SpriteRecord Get(int id) { Index(); byId.TryGetValue(id, out var r); return r; }
        public Sprite GetSprite(int id) => Get(id)?.sprite;
        public bool Contains(int id) => Get(id) != null;

        public int FindId(string name) { Index(); return byName.TryGetValue(name, out var r) ? r.id : 0; }

        public SpriteType TypeOf(int id) => Get(id)?.type ?? SpriteType.Decor;

        // every sprite of one type, for pickers
        public List<SpriteRecord> OfType(SpriteType type)
        {
            var list = new List<SpriteRecord>();
            foreach (var s in sprites) if (s.type == type) list.Add(s);
            return list;
        }

        // adds the sprite when its name is new; returns the id either way. An existing sprite keeps its id and takes the given type.
        public int Add(string name, Sprite sprite, SpriteType type = SpriteType.Decor)
        {
            int id = FindId(name);
            if (id > 0)
            {
                if (type != SpriteType.Decor) byId[id].type = type;
                return id;
            }
            int max = 0;
            foreach (var s in sprites) if (s.id > max) max = s.id;
            sprites.Add(new SpriteRecord { id = max + 1, name = name, type = type, sprite = sprite });
            ClearCache();
            return max + 1;
        }

        // like Add, but an existing sprite also takes the new image (for tools that regenerate their images); returns the id
        public int Set(string name, Sprite sprite, SpriteType type)
        {
            int id = Add(name, sprite, type);
            Get(id).sprite = sprite;   // Get, not byId: Add clears the cache when the name is new
            return id;
        }

        // Binary format (MyWriter/MyReader): short schema, short count { short id, UTF name, byte type }   (type: schema 2+)
        public byte[] ToBytes()
        {
            var w = new MyWriter(8 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)sprites.Count));
            foreach (var s in sprites) { w.WriteShort(checked((short)s.id)); w.WriteUTF(s.name); w.WriteByte((byte)s.type); }
            return w.ToArray();
        }

        // for tools and servers that only need the id <-> name (<-> type) table; schema 1 files have no types (all Decor)
        public static List<SpriteRow> ReadTable(byte[] bytes, out string error)
        {
            error = null;
            var rows = new List<SpriteRow>();
            try
            {
                var r = new MyReader(bytes);
                int schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "sprite schema too new"; return null; }
                int n = r.ReadShort();
                for (int i = 0; i < n; i++)
                {
                    var row = new SpriteRow { id = r.ReadShort(), name = r.ReadUTF() };
                    if (schema >= 2) row.type = (SpriteType)r.ReadByte();
                    rows.Add(row);
                }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return null; }
            }
            catch (Exception e) { error = "bad sprite data: " + e.Message; return null; }
            return rows;
        }
    }
}
