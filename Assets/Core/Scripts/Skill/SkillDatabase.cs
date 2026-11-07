using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Anim;
using Game.Items;
using Game.Map;
using Game.Net;
using Game.Players;

namespace Game.Skills
{
    /// <summary>Cách một skill gây tác dụng. Giá trị cố định vì được ghi xuống file và gói mạng.</summary>
    public enum SkillType : byte
    {
        Melee = 0,        // cận chiến: trúng mục tiêu trong hình nón phía trước
        Projectile = 1,   // bắn đạn bay theo hướng nhắm
        Area = 2,         // nổ vùng tròn tại điểm nhắm
        Dash = 3,         // lướt, gây sát thương trên đường đi
        Buff = 4,         // tăng chỉ số cho bản thân, không gây sát thương
    }

    /// <summary>
    /// Skill dùng ở đâu. Tách khỏi <see cref="SkillType"/> vì hai trục độc lập: đánh thường bắn cung là Basic + Projectile.
    /// Giá trị cố định vì được ghi xuống file và gói mạng.
    /// </summary>
    public enum SkillKind : byte
    {
        Active = 0,       // skill chủ động, gắn vào ô 1-4
        Basic = 1,        // đánh thường: nút Đánh / chuột trái, chạy combo attack1-4
    }

    /// <summary>
    /// Chỉ số của một cấp skill. Một skill 10 cấp có 10 SkillInfo trong <see cref="SkillTemplate.levels"/>.
    /// Những gì đổi theo cấp nằm ở đây; những gì cố định (tên, icon, anim) nằm ở SkillTemplate.
    /// </summary>
    [Serializable]
    public class SkillInfo
    {
        [Tooltip("Cấp, bắt đầu từ 1. Tự đánh lại theo vị trí trong danh sách, không cần gõ tay.")]
        public int level;
        [Tooltip("Id đòn đánh: ảnh đạn / hiệu ứng trúng / vụ nổ của cấp này, tra trong SpriteData (loại Effect). "
                 + "Cấp cao có thể đổi sang hiệu ứng to hơn. 0 = không có.")]
        public int idAtk;
        [Tooltip("Sát thương tính theo % sát thương cơ bản. 100 = bằng đòn thường.")]
        public int damage = 100;
        public int manaUse;
        [Tooltip("Thời gian hồi, giây.")]
        public float cooldown = 1f;
        [Tooltip("Tầm tung skill, đơn vị ô. Projectile: quãng đường đạn bay tối đa.")]
        public float range = 1.2f;
        [Tooltip("Bán kính vùng ảnh hưởng, đơn vị ô. Area: vùng nổ. Projectile: độ rộng đạn để tính trúng (0 = mặc định).")]
        public float radius;
        [Tooltip("Tốc độ đạn, ô/giây. Chỉ dùng cho Projectile.")]
        public float speed;
        [Tooltip("Số mục tiêu tối đa. 0 = không giới hạn. Projectile: số kẻ địch đạn xuyên qua trước khi mất.")]
        public int maxTarget;
        [Tooltip("Cấp nhân vật cần có để học / nâng lên cấp này.")]
        public int levelRequire;
        [Tooltip("Mô tả riêng của cấp này, ghép sau mô tả chung (vd \"Choáng 1 giây\").")]
        public string moreInfo;

        public SkillInfo Clone() => (SkillInfo)MemberwiseClone();
    }

    /// <summary>
    /// Một skill trong bảng. Id cố định, không bao giờ dùng lại cho skill khác.
    /// Ảnh không nằm ở đây: <see cref="idIcon"/> trỏ vào SpriteData (SpriteType.Icon), giống ItemTemplate.iconId.
    /// </summary>
    [Serializable]
    public class SkillTemplate
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd bomb_throw). Tên hiển thị lấy từ displayName.")]
        public string name;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Active: gắn vào ô 1-4. Basic: đòn đánh thường; idAnim 0 thì chạy combo attack1-4 người chơi đang mặc, "
                 + "khác 0 thì mọi bước combo chạy tư thế đó (vd Bow shoot).")]
        public SkillKind kind;
        public SkillType type;
        [Tooltip("Hành động nhân vật chạy khi tung skill (thường là Attack, Dash cho skill lướt).")]
        public AnimSlot animSlot = AnimSlot.Attack;
        [Tooltip("Id tư thế (AnimVariant.id) trong hành động animSlot của AnimData. 0 = bộ mặc định.")]
        public int idAnim;
        [Tooltip("Giây từ lúc anim bắt đầu đến lúc gây sát thương: khung vũ khí vung ra (Melee) / thả đạn (Projectile). "
                 + "Đi theo anim nên nằm ở đây. 0 = ngay lúc bấm.")]
        [Min(0f)] public float hitDelay;
        [Tooltip("Được vừa đi vừa tung skill này (tốc độ đi = Player.attackMoveScale). Tắt: đứng yên tới hết đòn. "
                 + "Thường bật cho bắn đạn (súng, cung), tắt cho cận chiến vì tay vung vẽ chung vào thân: bật thì nhân vật trượt.")]
        public bool canMoveWhileCasting;
        [Tooltip("Id ảnh trong SpriteData (loại Icon). 0 = chưa có ảnh.")]
        public int idIcon;
        [Tooltip("Bản 3D: id hiệu ứng (prefab Skill_<id>_... trong SkillPref) khi gắn skill này vào nút HUD. "
                 + "-1 = chọn theo loại: cận chiến 3, đạn 2, vùng 11, lướt 12, hỗ trợ 10.")]
        public int model3D = -1;
        [Header("Âm thanh: id trong SoundData, clip nạp từ bundle (Tools/Core/Audio/Generate Skill SFX)")]
        [Tooltip("Lúc bắt đầu tung: động tác tay / tụ phép. 0 = không phát.")]
        public int castSfx;
        [Tooltip("Lúc hiệu ứng phóng ra: đạn bay, vung chém, phép bùng lên. 0 = không phát.")]
        public int fireSfx;
        [Tooltip("Lúc trúng / nổ: đạn chạm, chém trúng, phép gây sát thương. 0 = không phát.")]
        public int hitSfx;
        [Tooltip("Mỗi phần tử là một cấp: phần tử 0 là cấp 1. Đòn đánh thường (Basic): damage, cooldown, range, maxTarget chỉ ghi "
                 + "phần tăng so với gốc, gốc là option của item vũ khí.")]
        public List<SkillInfo> levels = new List<SkillInfo>();

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;

        public int MaxLevel => levels.Count;

        /// <summary>Chỉ số của một cấp, ngoài khoảng thì kẹp về cấp gần nhất. null khi skill chưa có cấp nào.</summary>
        public SkillInfo GetLevel(int level)
        {
            if (levels.Count == 0) return null;
            return levels[Mathf.Clamp(level, 1, levels.Count) - 1];
        }

        /// <summary>
        /// Chỉ số một cấp để tính đòn. Skill ô 1-4 là <see cref="GetLevel"/>. Đòn đánh thường lấy gốc từ option của item vũ khí
        /// rồi cộng phần tăng theo cấp trong <see cref="levels"/>. null khi skill chưa có cấp nào.
        /// </summary>
        public SkillInfo Stats(int level)
        {
            SkillInfo s = GetLevel(level);
            if (s == null || kind != SkillKind.Basic) return s;

            WeaponTemplate weapon = WeaponDatabase.Current != null ? WeaponDatabase.Current.ForSkill(id) : null;
            ItemTemplate item = weapon != null && ItemDatabase.Current != null ? ItemDatabase.Current.FindWeapon(weapon.id) : null;
            if (item == null) return s;

            SkillInfo total = s.Clone();
            total.damage += Mathf.RoundToInt(item.OptionValue(PlayerStat.AttackDamage));
            total.cooldown += item.OptionValue(PlayerStat.AttackCooldown);
            total.range += item.OptionValue(PlayerStat.AttackRange);
            total.maxTarget += Mathf.RoundToInt(item.OptionValue(PlayerStat.AttackTargets));
            return total;
        }

        /// <summary>Đánh lại số cấp theo vị trí trong danh sách.</summary>
        public void Renumber()
        {
            for (int i = 0; i < levels.Count; i++) levels[i].level = i + 1;
        }
    }

    /// <summary>
    /// Danh mục skill của game. Một asset duy nhất ở Assets/Data/Skills/SkillDatabase.asset.
    /// Người chơi chỉ lưu (id skill, cấp), bảng này mới giữ nội dung.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "Core/Skill Database")]
    public class SkillDatabase : ScriptableObject
    {
        // 2: thêm SkillTemplate.kind, SkillInfo.speed. 3: thêm SkillTemplate.hitDelay. 4: thêm SkillTemplate.canMoveWhileCasting
        public const int CurrentSchema = 4;

        public int schemaVersion = CurrentSchema;
        [Tooltip("Bảng ảnh để tra idIcon. Cùng SpriteData mà map đang dùng (MapAssets.sprites).")]
        public SpriteData sprites;
        public List<SkillTemplate> skills = new List<SkillTemplate>();

        [NonSerialized] Dictionary<int, SkillTemplate> byId;

        static SkillDatabase current;

        /// <summary>Bảng skill của game. null khi scene chưa có ai giữ bảng.</summary>
        public static SkillDatabase Current => current;

        /// <summary>Đăng ký bảng skill cho cả game, gọi lúc Awake của GameController.</summary>
        public static void Use(SkillDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("SkillDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        // Static sống qua cả lần bấm Play sau nếu không xóa (Enter Play Mode Options tắt Reload Domain).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        void OnValidate()
        {
            foreach (var s in skills) s.Renumber();
            ClearCache();
        }

        void Index()
        {
            if (byId != null && byId.Count == skills.Count) return;
            byId = new Dictionary<int, SkillTemplate>(skills.Count);
            foreach (var s in skills) byId[s.id] = s;
        }

        public void ClearCache() => byId = null;

        public SkillTemplate Get(int id)
        {
            Index();
            return byId.TryGetValue(id, out SkillTemplate s) ? s : null;
        }

        /// <summary>Chỉ số của skill ở một cấp. null khi không có skill này.</summary>
        public SkillInfo Get(int id, int level) => Get(id)?.GetLevel(level);

        /// <summary>Icon của skill, tra idIcon qua SpriteData. null khi chưa gán bảng ảnh hoặc chưa có icon.</summary>
        public Sprite Icon(SkillTemplate skill) => sprites != null && skill != null ? sprites.GetSprite(skill.idIcon) : null;

        public Sprite Icon(int id) => Icon(Get(id));

        /// <summary>Ảnh đạn / hiệu ứng của một cấp skill (idAtk). null khi chưa gán bảng ảnh hoặc idAtk = 0.</summary>
        public Sprite Effect(SkillInfo info) => sprites != null && info != null && info.idAtk > 0 ? sprites.GetSprite(info.idAtk) : null;

        public SkillTemplate Find(string name)
        {
            foreach (var s in skills) if (s.name == name) return s;
            return null;
        }

        /// <summary>Thêm khi tên còn mới; id không bao giờ dùng lại. Dùng cho công cụ Editor.</summary>
        public SkillTemplate Add(SkillTemplate skill)
        {
            SkillTemplate old = Find(skill.name);
            if (old != null) return old;

            int max = 0;
            foreach (var s in skills) if (s.id > max) max = s.id;
            skill.id = max + 1;
            skill.Renumber();
            skills.Add(skill);
            ClearCache();
            return skill;
        }

        // Binary (MyWriter): short schema, short count
        //   { short id, UTF name, UTF displayName, UTF description, byte kind, byte type, byte animSlot, short idAnim, float hitDelay,
        //     bool canMoveWhileCasting, short idIcon,
        //     byte levels { short idAtk, int damage, int manaUse, float cooldown, float range, float radius, float speed,
        //                   short maxTarget, short levelRequire, UTF moreInfo } }
        // Cấp không ghi: là vị trí trong danh sách, bắt đầu từ 1.
        public byte[] ToBytes()
        {
            var w = new MyWriter(4 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)skills.Count));
            foreach (var s in skills)
            {
                w.WriteShort(checked((short)s.id));
                w.WriteUTF(s.name ?? "");
                w.WriteUTF(s.displayName ?? "");
                w.WriteUTF(s.description ?? "");
                w.WriteByte((byte)s.kind);
                w.WriteByte((byte)s.type);
                w.WriteByte((byte)s.animSlot);
                w.WriteShort(checked((short)s.idAnim));
                w.WriteFloat(s.hitDelay);
                w.WriteBool(s.canMoveWhileCasting);
                w.WriteShort(checked((short)s.idIcon));
                w.WriteByte(checked((byte)s.levels.Count));
                foreach (var l in s.levels)
                {
                    w.WriteShort(checked((short)l.idAtk));
                    w.WriteInt(l.damage);
                    w.WriteInt(l.manaUse);
                    w.WriteFloat(l.cooldown);
                    w.WriteFloat(l.range);
                    w.WriteFloat(l.radius);
                    w.WriteFloat(l.speed);
                    w.WriteShort(checked((short)l.maxTarget));
                    w.WriteShort(checked((short)l.levelRequire));
                    w.WriteUTF(l.moreInfo ?? "");
                }
            }
            return w.ToArray();
        }
    }
}
