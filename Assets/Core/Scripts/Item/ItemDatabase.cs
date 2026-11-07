using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Map;
using Game.Net;
using Game.Players;

namespace Game.Items
{
    /// <summary>Loại vật phẩm trong hành trang. Khác với DecorItemTemplate của Game.Map (đồ trang trí đặt trên map).</summary>
    public enum ItemKind : byte
    {
        Gem = 0,      // ngọc: chiến lợi phẩm, chỉ để đếm
        Key = 1,      // chìa khóa: mở rương cùng màu
        Weapon = 2,   // vũ khí: value = id trong WeaponDatabase, mỗi cây một ô (không xếp chồng)
        Armor = 3,    // đồ mặc (nón, áo, quần, giày, găng, dây chuyền, nhẫn): value = EquipSlot
        Skin = 4,     // ngoại hình nhân vật: value = id bộ anim (AnimSet.id trong AnimData), mặc vào ô Skin
        HpPotion = 5, // bình máu: options có PlayerStat.RestoreHp = máu hồi ngay khi dùng
        MpPotion = 6, // bình mana: options có PlayerStat.RestoreMp = mana hồi ngay khi dùng
        Buff = 7,     // thuốc tăng chỉ số: value = số giây hiệu lực, options = chỉ số cộng thêm trong lúc đó
        Stone = 8,    // đá cường hóa / khoét lỗ: value = GearStone
        Jewel = 9,    // đá khảm: value = bậc (1-3), options = chỉ số cộng cho món được khảm
    }

    /// <summary>
    /// Ô trang bị trên người. Giá trị cố định vì được ghi xuống PlayerData; None = 0.
    /// Vũ khí luôn vào ô Weapon; đồ mặc (ItemKind.Armor) vào ô ghi ở value.
    /// </summary>
    public enum EquipSlot : byte
    {
        None = 0,
        Head = 1,     // nón
        Body = 2,     // áo
        Legs = 3,     // quần
        Feet = 4,     // giày dép
        Weapon = 5,   // vũ khí
        Hands = 6,    // găng tay
        Neck = 7,     // dây chuyền
        Ring = 8,     // nhẫn
        Skin = 9,     // ngoại hình (ItemKind.Skin)
    }

    /// <summary>Phẩm chất vật phẩm. Giá trị cố định vì được ghi xuống file và gói mạng.</summary>
    public enum ItemRarity : byte
    {
        Common = 0,      // thường
        Uncommon = 1,    // hiếm
        Rare = 2,        // quý
        Epic = 3,        // sử thi
        Legendary = 4,   // huyền thoại
    }

    /// <summary>Màu của ngọc. None = 0 để giá trị mặc định là "không màu".</summary>
    public enum ItemColor : byte
    {
        None = 0,
        Blue = 1,
        Green = 2,
        Orange = 3,
        Purple = 4,
        Red = 5,
        Yellow = 6,
    }

    /// <summary>
    /// Bốn kiểu rương, theo bốn bộ sprite có anim của gói RafaelMatos (phân biệt bằng viền: lam nhạt, lam xám, vàng, ngọc).
    /// Mỗi kiểu có một chìa khóa riêng. None = 0.
    /// </summary>
    public enum ChestKind : byte
    {
        None = 0,
        Small = 1,    // chest1: rương nhỏ, viền lam nhạt
        Iron = 2,     // chest2: viền kim loại lam xám
        Gold = 3,     // chest3: viền vàng
        Large = 4,    // chest4: rương lớn, viền ngọc lam
    }

    /// <summary>
    /// Một loại vật phẩm trong bảng. Id cố định, không bao giờ dùng lại cho vật phẩm khác.
    /// Ảnh không nằm ở đây: <see cref="iconId"/> trỏ vào SpriteData (SpriteType.Icon), giống cách
    /// DecorItemTemplate.spriteId và MapData.iconSprite trỏ sprite bằng id.
    /// </summary>
    [Serializable]
    public class ItemTemplate
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd gem_red). Tên hiển thị lấy từ displayName.")]
        public string name;
        public string displayName;
        [Tooltip("Mô tả hiện trong popup thông tin vật phẩm.")]
        [TextArea] public string description;
        [Tooltip("Loại vật phẩm: ngọc, chìa khóa, vũ khí...")]
        public ItemKind kind;
        [Tooltip("Phẩm chất: thường, hiếm, quý, sử thi, huyền thoại.")]
        public ItemRarity rarity;
        [Tooltip("Id ảnh trong SpriteData (loại Icon). 0 = chưa có ảnh.")]
        public int iconId;
        [Tooltip("Giá trị riêng theo loại, mỗi loại tự hiểu. Gem: ItemColor. Key: ChestKind. Weapon: id trong WeaponDatabase. "
                 + "Armor: EquipSlot (1 nón, 2 áo, 3 quần, 4 giày, 6 găng, 7 dây chuyền, 8 nhẫn). Skin: id bộ anim trong AnimData. "
                 + "Buff: số giây hiệu lực. Stone: GearStone (1 nâng cấp, 2 may mắn, 3 bảo vệ, 4 khoét lỗ). "
                 + "Jewel: bậc đá khảm (1-3). Chỉ số không nằm ở đây mà ở options.")]
        public int value;
        [Tooltip("Số lượng tối đa trong một ô. 0 = không giới hạn, 1 = không xếp chồng (vũ khí).")]
        public int maxStack;
        [Tooltip("Mọi chỉ số của vật phẩm: mỗi dòng một option trong OptionDatabase kèm giá trị. Đồ mặc: chỉ tính khi đang nằm ở ô trang bị. "
                 + "Buff: tính trong lúc thuốc còn hiệu lực. Jewel: tính khi đã khảm vào món đang mặc. Bình máu / mana: lượng hồi khi uống.")]
        public List<PlayerStatEffect> options = new List<PlayerStatEffect>();

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;

        /// <summary>Tổng giá trị của một option trên vật phẩm, 0 khi không có.</summary>
        public float OptionValue(PlayerStat stat)
        {
            float total = 0f;
            foreach (PlayerStatEffect e in options) if (e.stat == stat) total += e.value;
            return total;
        }

        /// <summary>Xếp chồng được: nhiều cái cùng loại nằm chung một ô.</summary>
        public bool Stackable => maxStack != 1;

        /// <summary>Số tối đa trong một ô, maxStack 0 (không giới hạn) thành int.MaxValue.</summary>
        public int StackLimit => maxStack <= 0 ? int.MaxValue : maxStack;

        // value đọc theo từng loại. Một trường chung thay cho mỗi loại một trường riêng, giống MapObjectDef.param:
        // thêm loại mới (vũ khí, thuốc...) không làm row phình thêm trường bỏ trống.

        /// <summary>Màu của viên ngọc. None khi vật phẩm không phải ngọc.</summary>
        public ItemColor GemColor => kind == ItemKind.Gem ? (ItemColor)value : ItemColor.None;

        /// <summary>Kiểu rương mà chìa này mở được. None khi vật phẩm không phải chìa khóa.</summary>
        public ChestKind KeyChest => kind == ItemKind.Key ? (ChestKind)value : ChestKind.None;

        /// <summary>Id vũ khí (WeaponDatabase) mà vật phẩm này đại diện. 0 khi vật phẩm không phải vũ khí.</summary>
        public int WeaponId => kind == ItemKind.Weapon ? value : 0;

        /// <summary>Id bộ anim (AnimSet trong AnimData) mà skin này đổi sang. -1 khi vật phẩm không phải skin.</summary>
        public int SkinSetId => kind == ItemKind.Skin ? value : -1;

        /// <summary>Máu hồi khi uống. 0 khi vật phẩm không phải bình máu.</summary>
        public int RestoreHp => kind == ItemKind.HpPotion ? Math.Max(0, Mathf.RoundToInt(OptionValue(PlayerStat.RestoreHp))) : 0;

        /// <summary>Mana hồi khi uống. 0 khi vật phẩm không phải bình mana.</summary>
        public int RestoreMp => kind == ItemKind.MpPotion ? Math.Max(0, Mathf.RoundToInt(OptionValue(PlayerStat.RestoreMp))) : 0;

        /// <summary>Số giây thuốc tăng chỉ số còn tác dụng sau khi uống. 0 khi vật phẩm không phải Buff.</summary>
        public float BuffSeconds => kind == ItemKind.Buff ? Math.Max(0, value) : 0f;

        /// <summary>Dùng được từ hành trang / ô dùng nhanh (bình máu, bình mana, thuốc tăng chỉ số): dùng xong mất một cái.</summary>
        public bool Usable => IsUsable(kind);

        public static bool IsUsable(ItemKind kind) => kind == ItemKind.HpPotion || kind == ItemKind.MpPotion || kind == ItemKind.Buff;

        /// <summary>Ô trang bị mà vật phẩm này mặc/cầm vào. None khi không trang bị được (ngọc, chìa).</summary>
        public EquipSlot Slot
        {
            get
            {
                if (kind == ItemKind.Weapon) return EquipSlot.Weapon;
                if (kind == ItemKind.Skin) return EquipSlot.Skin;
                if (kind == ItemKind.Armor && value > 0 && value <= (int)EquipSlot.Ring && value != (int)EquipSlot.Weapon)
                    return (EquipSlot)value;
                return EquipSlot.None;
            }
        }

        public bool Equippable => Slot != EquipSlot.None;

        /// <summary>Loại đá cường hóa. None khi vật phẩm không phải đá.</summary>
        public GearStone Stone => kind == ItemKind.Stone ? (GearStone)value : GearStone.None;

        /// <summary>Bậc đá khảm (1-3). 0 khi vật phẩm không phải đá khảm.</summary>
        public int JewelTier => kind == ItemKind.Jewel ? value : 0;

        /// <summary>Cùng loại đá khảm (cộng cùng chỉ số), khác bậc vẫn tính là một loại.</summary>
        public bool SameJewel(ItemTemplate other) =>
            kind == ItemKind.Jewel && other != null && other.kind == ItemKind.Jewel
            && options.Count > 0 && other.options.Count > 0 && options[0].stat == other.options[0].stat;

        /// <summary>
        /// Cường hóa và khoét lỗ được: vũ khí và đồ mặc không xếp chồng, cấp và lỗ nằm ở từng món. Skin thì không.
        /// </summary>
        public bool Upgradable => (kind == ItemKind.Weapon || kind == ItemKind.Armor) && !Stackable && Equippable;

        /// <summary>Tên tiếng Việt của loại vật phẩm.</summary>
        public static string KindName(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Gem: return "ngọc";
                case ItemKind.Key: return "chìa khóa";
                case ItemKind.Weapon: return "vũ khí";
                case ItemKind.Armor: return "đồ mặc";
                case ItemKind.Skin: return "skin";
                case ItemKind.HpPotion: return "bình máu";
                case ItemKind.MpPotion: return "bình mana";
                case ItemKind.Buff: return "thuốc tăng chỉ số";
                case ItemKind.Stone: return "đá cường hóa";
                case ItemKind.Jewel: return "đá khảm";
                default: return kind.ToString();
            }
        }

        /// <summary>Tên tiếng Việt của ô trang bị.</summary>
        public static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "nón";
                case EquipSlot.Body: return "áo";
                case EquipSlot.Legs: return "quần";
                case EquipSlot.Feet: return "giày dép";
                case EquipSlot.Weapon: return "vũ khí";
                case EquipSlot.Hands: return "găng tay";
                case EquipSlot.Neck: return "dây chuyền";
                case EquipSlot.Ring: return "nhẫn";
                case EquipSlot.Skin: return "skin";
                default: return "";
            }
        }

        /// <summary>Tên tiếng Việt của màu ngọc, để ghép vào thông báo.</summary>
        public static string ColorName(ItemColor color)
        {
            switch (color)
            {
                case ItemColor.Blue: return "lam";
                case ItemColor.Green: return "lục";
                case ItemColor.Orange: return "cam";
                case ItemColor.Purple: return "tím";
                case ItemColor.Red: return "đỏ";
                case ItemColor.Yellow: return "vàng";
                default: return "";
            }
        }

        /// <summary>Tên tiếng Việt của kiểu rương, để ghép vào thông báo ("Cần chìa rương vàng").</summary>
        public static string ChestName(ChestKind chest)
        {
            switch (chest)
            {
                case ChestKind.Small: return "rương nhỏ";
                case ChestKind.Iron: return "rương sắt";
                case ChestKind.Gold: return "rương vàng";
                case ChestKind.Large: return "rương lớn";
                default: return "rương";
            }
        }

        /// <summary>Tên tiếng Việt của phẩm chất.</summary>
        public static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return "hiếm";
                case ItemRarity.Rare: return "quý";
                case ItemRarity.Epic: return "sử thi";
                case ItemRarity.Legendary: return "huyền thoại";
                default: return "thường";
            }
        }

        /// <summary>Màu tô cho chữ/khung theo phẩm chất, theo thang quen thuộc xám &gt; lục &gt; lam &gt; tím &gt; cam.</summary>
        public static Color RarityTint(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return new Color(0.4f, 0.85f, 0.4f);
                case ItemRarity.Rare: return new Color(0.35f, 0.6f, 1f);
                case ItemRarity.Epic: return new Color(0.7f, 0.45f, 1f);
                case ItemRarity.Legendary: return new Color(1f, 0.6f, 0.2f);
                default: return new Color(0.8f, 0.8f, 0.8f);
            }
        }

        /// <summary>Màu tô theo màu ngọc, khớp với sprite của gói HyperLuminal.</summary>
        public static Color Tint(ItemColor color)
        {
            switch (color)
            {
                case ItemColor.Blue: return new Color(0.35f, 0.6f, 1f);
                case ItemColor.Green: return new Color(0.4f, 0.85f, 0.4f);
                case ItemColor.Orange: return new Color(1f, 0.6f, 0.2f);
                case ItemColor.Purple: return new Color(0.7f, 0.45f, 1f);
                case ItemColor.Red: return new Color(1f, 0.4f, 0.4f);
                case ItemColor.Yellow: return new Color(1f, 0.9f, 0.3f);
                default: return Color.white;
            }
        }
    }

    /// <summary>
    /// Danh mục vật phẩm của game (ngọc, chìa khóa, vũ khí, đồ mặc, skin, bình máu / mana, thuốc tăng chỉ số).
    /// Một asset duy nhất ở Assets/Data/Items/ItemDatabase.asset, dựng bằng Tools/Map/Build Chests And Items (ngọc, chìa), Tools/Core/Items/Build Weapon Items (vũ khí) và
    /// Tools/Core/Items/Build Potion Items (bình máu / mana, thuốc tăng chỉ số).
    /// Map chỉ lưu id, bảng này mới giữ nội dung.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Core/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        /// <summary>2: vật phẩm mang rarity, iconId (thay cho tham chiếu Sprite) và value (thay cho color/chest).
        /// 3: thêm description, loại Weapon và Armor, options (chỉ số của đồ).</summary>
        public const int CurrentSchema = 3;

        public int schemaVersion = CurrentSchema;
        [Tooltip("Bảng ảnh để tra iconId. Cùng SpriteData mà map đang dùng (MapAssets.sprites).")]
        public SpriteData sprites;
        public List<ItemTemplate> items = new List<ItemTemplate>();

        [NonSerialized] Dictionary<int, ItemTemplate> byId;

        static ItemDatabase current;

        /// <summary>
        /// Bảng vật phẩm của game. Vật phẩm là dữ liệu nền nên lấy thẳng ở đây, không phải đi vòng qua
        /// ChestSystem hay hệ thống nào khác. null khi scene chưa có ai giữ bảng.
        /// </summary>
        public static ItemDatabase Current => current;

        /// <summary>
        /// Đăng ký bảng vật phẩm cho cả game, gọi lúc Awake của component giữ tham chiếu (GameController,
        /// ChestSystem). Cả game chỉ nên có một bảng; gán chồng bằng một bảng khác sẽ có cảnh báo.
        /// </summary>
        public static void Use(ItemDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("ItemDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        // Static sống qua cả lần bấm Play sau nếu không xóa (Enter Play Mode Options tắt Reload Domain).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        void Index()
        {
            if (byId != null && byId.Count == items.Count) return;
            byId = new Dictionary<int, ItemTemplate>(items.Count);
            foreach (var it in items) byId[it.id] = it;
        }

        public void ClearCache() => byId = null;

        public ItemTemplate Get(int id)
        {
            Index();
            return byId.TryGetValue(id, out ItemTemplate info) ? info : null;
        }

        /// <summary>Ảnh của một vật phẩm, tra iconId qua SpriteData. null khi chưa gán bảng ảnh hoặc chưa có icon.</summary>
        public Sprite Icon(ItemTemplate item) => sprites != null && item != null ? sprites.GetSprite(item.iconId) : null;

        public Sprite Icon(int itemId) => Icon(Get(itemId));

        public ItemTemplate Find(string name)
        {
            foreach (var it in items) if (it.name == name) return it;
            return null;
        }

        /// <summary>Vật phẩm theo loại và value, vd Find(ItemKind.Gem, (int)ItemColor.Red).</summary>
        public ItemTemplate Find(ItemKind kind, int value)
        {
            foreach (var it in items) if (it.kind == kind && it.value == value) return it;
            return null;
        }

        /// <summary>Viên ngọc có màu này.</summary>
        public ItemTemplate FindGem(ItemColor color) => Find(ItemKind.Gem, (int)color);

        /// <summary>Chìa khóa của một kiểu rương.</summary>
        public ItemTemplate FindKey(ChestKind chest) => Find(ItemKind.Key, (int)chest);

        /// <summary>Vật phẩm đại diện cho một vũ khí (id trong WeaponDatabase).</summary>
        public ItemTemplate FindWeapon(int weaponId) => Find(ItemKind.Weapon, weaponId);

        /// <summary>Viên đá cường hóa loại này.</summary>
        public ItemTemplate FindStone(GearStone stone) => Find(ItemKind.Stone, (int)stone);

        public List<ItemTemplate> OfKind(ItemKind kind)
        {
            var list = new List<ItemTemplate>();
            foreach (var it in items) if (it.kind == kind) list.Add(it);
            return list;
        }

        /// <summary>Thêm khi tên còn mới; id không bao giờ dùng lại. Dùng cho công cụ Editor.</summary>
        public ItemTemplate Add(ItemTemplate info)
        {
            ItemTemplate old = Find(info.name);
            if (old != null) return old;

            int max = 0;
            foreach (var it in items) if (it.id > max) max = it.id;
            info.id = max + 1;
            items.Add(info);
            ClearCache();
            return info;
        }

        // Binary (MyWriter/MyReader, big-endian), file items.bin gửi cho server:
        //   short schema, short count { short id, UTF name, UTF displayName, UTF description, byte kind, byte rarity,
        //                               short iconId, int value, short maxStack, byte optionCount { byte stat, float value } }
        // float ghi dạng int x100 (MyWriter.WriteFloat): option giữ được 0.01 (hồi máu 0.5/giây).
        public byte[] ToBytes()
        {
            var w = new MyWriter(4 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)items.Count));
            foreach (ItemTemplate it in items)
            {
                w.WriteShort(checked((short)it.id));
                w.WriteUTF(it.name ?? "");
                w.WriteUTF(it.displayName ?? "");
                w.WriteUTF(it.description ?? "");
                w.WriteByte((byte)it.kind);
                w.WriteByte((byte)it.rarity);
                w.WriteShort(checked((short)it.iconId));
                w.WriteInt(it.value);
                w.WriteShort(checked((short)it.maxStack));
                int n = it.options != null ? it.options.Count : 0;
                w.WriteByte(checked((byte)n));
                for (int i = 0; i < n; i++)
                {
                    w.WriteByte((byte)it.options[i].stat);
                    w.WriteFloat(it.options[i].value);
                }
            }
            return w.ToArray();
        }

        /// <summary>
        /// Đọc bảng từ <see cref="ToBytes"/> vào danh sách (không tạo ScriptableObject, server C# dùng lại được).
        /// Trả false và lý do khi dữ liệu hỏng, schema mới hơn, hay id / name trùng.
        /// </summary>
        public static bool Read(byte[] bytes, List<ItemTemplate> into, out int schema, out string error)
        {
            schema = 0;
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty item data"; return false; }
            try
            {
                var r = new MyReader(bytes);
                schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "item schema too new (" + schema + ")"; return false; }
                int count = r.ReadShort();
                if (count < 0 || count > 30000) { error = "bad item count"; return false; }
                var ids = new HashSet<int>();
                var names = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var it = new ItemTemplate
                    {
                        id = r.ReadShort(), name = r.ReadUTF(), displayName = r.ReadUTF(), description = r.ReadUTF(),
                        kind = (ItemKind)r.ReadByte(), rarity = (ItemRarity)r.ReadByte(), iconId = r.ReadShort(),
                        value = r.ReadInt(), maxStack = r.ReadShort(),
                    };
                    int n = r.ReadByte();
                    for (int k = 0; k < n; k++) it.options.Add(new PlayerStatEffect((PlayerStat)r.ReadByte(), r.ReadFloat()));
                    if (it.id <= 0 || !ids.Add(it.id)) { error = "bad or duplicate item id " + it.id; return false; }
                    if (!names.Add(it.name)) { error = "duplicate item name " + it.name; return false; }
                    into.Add(it);
                }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad item data: " + e.Message; return false; }
            return true;
        }

        /// <summary>Bảng mới (CreateInstance) từ bytes; null và lý do khi hỏng.</summary>
        public static ItemDatabase FromBytes(byte[] bytes, out string error)
        {
            var list = new List<ItemTemplate>();
            if (!Read(bytes, list, out int schema, out error)) return null;
            var db = CreateInstance<ItemDatabase>();
            db.schemaVersion = schema;
            db.items = list;
            return db;
        }
    }
}
