using System;
using UnityEngine;

namespace Game.Skills
{
    /// <summary>
    /// Một skill người chơi đã học: chỉ giữ id skill và cấp hiện tại. Chỉ số của cấp đó tra trong SkillDatabase,
    /// nên nâng cấp chỉ là tăng <see cref="level"/>, và đổi số liệu trong bảng thì mọi người chơi nhận ngay.
    /// </summary>
    [Serializable]
    public class PlayerSkill
    {
        public int templateId;
        public int level = 1;

        [NonSerialized] float lastUseTime = -999f;
        /// <summary>Nhân vào hồi chiêu (cây nhân vật cắt bớt, 1 = nguyên). Chủ skill đặt, không lưu.</summary>
        [NonSerialized] public float cooldownScale = 1f;
        // Template tra một lần rồi giữ: UI hỏi cooldown mỗi frame cho từng ô. Tra lại khi đổi bảng hoặc đổi templateId.
        [NonSerialized] SkillTemplate cachedTemplate;
        [NonSerialized] SkillDatabase cachedDb;
        [NonSerialized] int cachedId;

        public PlayerSkill() { }

        public PlayerSkill(int templateId, int level = 1)
        {
            this.templateId = templateId;
            this.level = level;
        }

        public SkillTemplate Template
        {
            get
            {
                SkillDatabase db = SkillDatabase.Current;
                if (!ReferenceEquals(db, cachedDb) || cachedId != templateId)
                {
                    cachedDb = db;
                    cachedId = templateId;
                    cachedTemplate = db != null ? db.Get(templateId) : null;
                }
                return cachedTemplate;
            }
        }

        /// <summary>Chỉ số của cấp hiện tại. null khi chưa có bảng hoặc không có skill này.</summary>
        public SkillInfo Info => Template?.GetLevel(level);

        /// <summary>Chỉ số của cấp kế tiếp, để UI so sánh trước khi nâng. null khi đã max cấp.</summary>
        public SkillInfo NextInfo
        {
            get
            {
                SkillTemplate t = Template;
                return t != null && level < t.MaxLevel ? t.GetLevel(level + 1) : null;
            }
        }

        public bool IsMaxLevel => NextInfo == null;

        public bool IsReady
        {
            get
            {
                SkillInfo info = Info;
                return info != null && Time.time >= lastUseTime + info.cooldown * cooldownScale;
            }
        }

        /// <summary>Phần cooldown còn lại, từ 1 (vừa dùng) về 0 (dùng được). UI dùng để vẽ vòng đếm ngược.</summary>
        public float CooldownRatio
        {
            get
            {
                SkillInfo info = Info;
                float cooldown = info != null ? info.cooldown * cooldownScale : 0f;
                if (cooldown <= 0f) return 0f;
                return Mathf.Clamp01(1f - (Time.time - lastUseTime) / cooldown);
            }
        }

        public void MarkUsed() => lastUseTime = Time.time;

        /// <summary>Tăng một cấp. false khi đã max cấp.</summary>
        public bool LevelUp()
        {
            if (IsMaxLevel) return false;
            level++;
            return true;
        }
    }
}
