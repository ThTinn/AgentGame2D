using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Map;
using Game.Net;
using Game.Players;

namespace Game.Skills
{
    /// <summary>
    /// Một loại vũ khí. Vũ khí quyết định đòn đánh thường: trang bị là đổi skill đánh thường sang <see cref="basicSkill"/>,
    /// nên kiểu đòn (chém, bắn) và anim (kèm hình vũ khí) theo skill đó. Sát thương, hồi, tầm, mục tiêu gốc là option của item
    /// vũ khí (<see cref="SkillTemplate.Stats"/>).
    /// Id cố định, không bao giờ dùng lại cho vũ khí khác. Ảnh: <see cref="idIcon"/> trỏ vào SpriteData (SpriteType.Icon).
    /// </summary>
    [Serializable]
    public class WeaponTemplate
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd sword). Tên hiển thị lấy từ displayName.")]
        public string name;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Id ảnh trong SpriteData (loại Icon). 0 = chưa có ảnh, UI lấy khung đầu của anim đánh.")]
        public int idIcon;
        [Tooltip("Id skill loại Basic trong SkillDatabase: đòn đánh thường khi cầm vũ khí này.")]
        public int basicSkill;
        [Tooltip("Cây nâng cấp: các nút cộng chỉ số vào đòn đánh thường (số đạn, nổ vùng, sát thương...).")]
        public List<WeaponUpgrade> upgrades = new List<WeaponUpgrade>();

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;

        public WeaponUpgrade GetUpgrade(int id)
        {
            foreach (WeaponUpgrade u in upgrades) if (u.id == id) return u;
            return null;
        }

        public WeaponUpgrade FindUpgrade(string upgradeName)
        {
            foreach (WeaponUpgrade u in upgrades) if (u.name == upgradeName) return u;
            return null;
        }

        /// <summary>Thêm nút khi tên còn mới; id không bao giờ dùng lại. Dùng cho công cụ Editor.</summary>
        public WeaponUpgrade AddUpgrade(WeaponUpgrade up)
        {
            WeaponUpgrade old = FindUpgrade(up.name);
            if (old != null) return old;
            int max = 0;
            foreach (WeaponUpgrade u in upgrades) if (u.id > max) max = u.id;
            up.id = max + 1;
            upgrades.Add(up);
            return up;
        }
    }

    /// <summary>
    /// Danh mục vũ khí của game. Một asset duy nhất ở Assets/Data/Skills/WeaponDatabase.asset.
    /// Người chơi chỉ lưu id vũ khí đang cầm (PlayerData.weaponId).
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponDatabase", menuName = "Core/Weapon Database")]
    public class WeaponDatabase : ScriptableObject
    {
        // 2: thêm cây nâng cấp (WeaponTemplate.upgrades). 3: effects dùng id option (PlayerStat)
        public const int CurrentSchema = 3;

        public int schemaVersion = CurrentSchema;
        [Tooltip("Bảng ảnh để tra idIcon. Cùng SpriteData mà map đang dùng (MapAssets.sprites).")]
        public SpriteData sprites;
        [Tooltip("Các khung hình vụ nổ khi đòn có nâng cấp nổ vùng, chạy một lượt.")]
        public Sprite[] explosionFrames;
        public List<WeaponTemplate> weapons = new List<WeaponTemplate>();

        [NonSerialized] Dictionary<int, WeaponTemplate> byId;

        static WeaponDatabase current;

        /// <summary>Bảng vũ khí của game. null khi scene chưa có ai giữ bảng.</summary>
        public static WeaponDatabase Current => current;

        /// <summary>Đăng ký bảng vũ khí cho cả game, gọi lúc Awake của GameController.</summary>
        public static void Use(WeaponDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("WeaponDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        void OnValidate() => ClearCache();

        void Index()
        {
            if (byId != null && byId.Count == weapons.Count) return;
            byId = new Dictionary<int, WeaponTemplate>(weapons.Count);
            foreach (var w in weapons) byId[w.id] = w;
        }

        public void ClearCache() => byId = null;

        public WeaponTemplate Get(int id)
        {
            Index();
            return byId.TryGetValue(id, out WeaponTemplate w) ? w : null;
        }

        public WeaponTemplate Find(string name)
        {
            foreach (var w in weapons) if (w.name == name) return w;
            return null;
        }

        /// <summary>Vũ khí có đòn đánh thường là skill này. null khi không có.</summary>
        public WeaponTemplate ForSkill(int skillId)
        {
            foreach (var w in weapons) if (w.basicSkill == skillId) return w;
            return null;
        }

        /// <summary>Icon của vũ khí, tra idIcon qua SpriteData. null khi chưa gán bảng ảnh hoặc chưa có icon.</summary>
        public Sprite Icon(WeaponTemplate weapon) => sprites != null && weapon != null ? sprites.GetSprite(weapon.idIcon) : null;

        /// <summary>Thêm khi tên còn mới; id không bao giờ dùng lại. Dùng cho công cụ Editor.</summary>
        public WeaponTemplate Add(WeaponTemplate weapon)
        {
            WeaponTemplate old = Find(weapon.name);
            if (old != null) return old;

            int max = 0;
            foreach (var w in weapons) if (w.id > max) max = w.id;
            weapon.id = max + 1;
            weapons.Add(weapon);
            ClearCache();
            return weapon;
        }

        // Binary (MyWriter): short schema, short count { short id, UTF name, UTF displayName, UTF description, short idIcon, short basicSkill,
        //   byte upgrades { short id, UTF name, UTF displayName, UTF description, short parent, byte maxRank, int cost,
        //                   byte column, byte row, byte effects { byte stat, float value } } }
        public byte[] ToBytes()
        {
            var w = new MyWriter(1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)weapons.Count));
            foreach (var t in weapons)
            {
                w.WriteShort(checked((short)t.id));
                w.WriteUTF(t.name ?? "");
                w.WriteUTF(t.displayName ?? "");
                w.WriteUTF(t.description ?? "");
                w.WriteShort(checked((short)t.idIcon));
                w.WriteShort(checked((short)t.basicSkill));
                w.WriteByte(checked((byte)t.upgrades.Count));
                foreach (WeaponUpgrade u in t.upgrades)
                {
                    w.WriteShort(checked((short)u.id));
                    w.WriteUTF(u.name ?? "");
                    w.WriteUTF(u.displayName ?? "");
                    w.WriteUTF(u.description ?? "");
                    w.WriteShort(checked((short)u.parent));
                    w.WriteByte(checked((byte)u.maxRank));
                    w.WriteInt(u.cost);
                    w.WriteByte(checked((byte)u.column));
                    w.WriteByte(checked((byte)u.row));
                    w.WriteByte(checked((byte)u.effects.Count));
                    foreach (PlayerStatEffect e in u.effects)
                    {
                        w.WriteByte((byte)e.stat);
                        w.WriteFloat(e.value);
                    }
                }
            }
            return w.ToArray();
        }
    }
}
