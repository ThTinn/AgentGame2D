using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Items;
using Game.Players;

namespace Game.ServerSim
{
    /// <summary>
    /// Đánh nhau ở server giả lập, theo đúng luồng gói client đang đọc (GlobalMessageHandler.ReadBullet):
    ///  1. Client gửi ATTACK (skill, chỗ tung, chỗ nhắm; toạ độ x100).
    ///  2. Server gửi BULLET act 0 (tạo skill: hitID, người đánh, skill, đầu, cuối, tốc độ, timeMaxExist, mã skill + cấp trong
    ///     SkillDatabase) cho mọi người trong map, kể cả người đánh (online client không tự tạo skill). Mã skill để máy khác vẽ
    ///     đúng skill: id 0 / 1000+ô chỉ là ô của người đánh.
    ///  3. Tới lúc trúng: đạn (Bullet_Nor) bay tới con đầu tiên trên đường hoặc hết tầm thì BULLET act 1 (nổ: danh sách trúng);
    ///     cận chiến / phép vùng trúng ở mốc chém thì act 2 (trúng, không nổ), sau đó act 1 không mục tiêu để skill tự dọn.
    ///  4. Quái hết máu: MOB.DIE, thưởng người đánh như chơi local (CoreSystems.GrantKillRewards: EXP, vàng, đồ, đếm nhiệm vụ),
    ///     đồ rơi xuống đất (GROUND_ITEM), MOB.RESPAWN sau LocalPlaySettings.respawnSeconds.
    /// Sát thương tính như LocalCombat (chỉ số Core của hồ sơ người đánh). Hồi chiêu và mana server tự kiểm (CanCast); quái
    /// đánh người chơi ở ServerMonsters.
    /// </summary>
    public sealed partial class ServerWorld
    {
        const short BasicSkill = 0;
        const float MeleeDelay = 0.3f;
        const float AfterHit = 0.8f;
        const float CooldownSlack = 0.08f;   // gói tới lệch nhịp frame: cho sớm chừng này vẫn nhận

        enum HitKind { Bullet, Melee, Area, Other }

        sealed class PendingHit
        {
            public ServerMap map;
            public ServerPlayer attacker;
            public short hitId;
            public Skill_Template template;
            public HitKind kind;
            public Vector2 start, target, dir;
            public float hitAt, endAt;
            public bool hitDone;
        }

        readonly List<PendingHit> pending = new List<PendingHit>();
        short lastHitId;

        void Attack(ServerPlayer p, MyReader r)
        {
            short skillId = r.ReadShort();
            Vector3 start = r.ReadInt3() / 100f;
            Vector3 target = r.ReadInt3() / 100f;
            Attack(p, skillId, start, target);
        }

        /// <summary>Tung skill (người chơi gửi ATTACK, hoặc bot của server). Toạ độ mét; y = CoreNet.GroundY khi server không biết độ cao.</summary>
        void Attack(ServerPlayer p, short skillId, Vector3 start, Vector3 target)
        {
            if (p.dead || !maps.TryGetValue(p.mapId, out ServerMap map)) return;
            Skill_Template t = SkillOf(p, skillId);
            if (t == null)
            {
                if (p.conn != null) Debug.Log($"LocalServer: {p.Name} dùng skill {skillId} không có trên nút, bỏ.");
                return;
            }
            if (!CanCast(p, t)) return;
            Game.Skills.PlayerSkill ps = LocalSkillBridge.SkillOf(p.profile, skillId);   // null: đòn thường gốc

            HitKind kind = KindOf(t.type);
            Vector2 s2 = new Vector2(start.x, start.z);
            Vector2 t2 = new Vector2(target.x, target.z);
            Vector2 dir = t2 - s2;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            if (t.type == Skill_.TYPE.Cast2) { t2 = new Vector2(p.pos.x, p.pos.z); target = p.pos; }
            else if (t.type == Skill_.TYPE.Cast)
            {
                t2 = s2 + Vector2.ClampMagnitude(t2 - s2, t.rangeSkill);
                target = new Vector3(t2.x, target.y, t2.y);
            }
            else if (kind == HitKind.Bullet)
            {
                // đạn bay thẳng tới hết tầm (Skill_Template.rangeSkill), trúng con đầu tiên trên đường
                t2 = s2 + dir * Mathf.Max(0.5f, t.rangeSkill);
                target = new Vector3(t2.x, start.y, t2.y);
            }

            float speed = Mathf.Max(1f, settings.bulletSpeed);
            short hitId = NextHitId();
            int v = Mathf.Max(1, Mathf.RoundToInt(speed * 100f * Skill_MN.TICK));   // cm mỗi tick, như LocalCombat
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.BULLET, w =>
                {
                    w.writeByte(0);
                    w.writeShort(hitId);
                    w.writeInt(p.id);
                    w.writeShort(skillId);
                    WriteInt3(w, start * 100f);
                    WriteInt3(w, target * 100f);
                    w.writeInt(v);
                    w.writeByte((sbyte)-1);   // timeMaxExist: theo skill
                    w.writeShort((short)(ps != null ? ps.templateId : -1));
                    w.writeShort((short)(ps != null ? ps.level : 0));
                });

            var hit = new PendingHit { map = map, attacker = p, hitId = hitId, template = t, kind = kind, start = s2, target = t2, dir = dir };
            if (kind == HitKind.Bullet)
            {
                // con đầu tiên trên đường bay: trúng lúc đạn tới nó
                ServerMonster first = FirstOnSegment(map, s2, t2, settings.bulletHitRadius);
                if (first != null)
                {
                    Vector2 c = ClosestOnSegment(s2, t2, first.pos);
                    hit.target = c;
                }
                hit.hitAt = Now + (hit.target - s2).magnitude / speed;
            }
            else hit.hitAt = Now + Mathf.Max(MeleeDelay, settings.hitDelay);
            pending.Add(hit);
        }

        /// <summary>
        /// Skill id của người chơi p theo hồ sơ của chính họ (đòn thường theo vũ khí đang cầm, ô 1-4 theo skill gắn trên nút,
        /// cấp do server giữ). Không dùng Skill_Template.GetInfo: bảng đó là của client Unity chạy cùng tiến trình.
        /// null = p không có skill đó trên nút. Dựng lại khi skill / cấp của ô đổi.
        /// </summary>
        Skill_Template SkillOf(ServerPlayer p, short id)
        {
            Game.Skills.PlayerSkill skill = LocalSkillBridge.SkillOf(p.profile, id);
            if (skill == null)
                return id == BasicSkill ? LocalSkillBridge.DefaultBasic : null;   // không cầm vũ khí: đòn thường gốc
            if (p.skillCache.TryGetValue(id, out ServerPlayer.CachedSkill c) && c.skill == skill && c.level == skill.level)
                return c.template;
            Skill_Template t = LocalSkillBridge.TemplateOf(id, skill);
            p.skillCache[id] = new ServerPlayer.CachedSkill { skill = skill, level = skill.level, template = t };
            return t;
        }

        /// <summary>
        /// Kiểm như LocalCombat.DoAttack, trên hồ sơ của server: skill còn hồi chiêu thì bỏ (client cũng tự chặn, lệch là do gói tới
        /// sớm / sửa client); không đủ mana thì báo và gửi lại mana chuẩn. Được thì trừ mana, tính lượt hồi chiêu mới.
        /// </summary>
        bool CanCast(ServerPlayer p, Skill_Template t)
        {
            float now = Now;
            if (p.readyAt.TryGetValue(t.idTemplate, out float ready) && now < ready - CooldownSlack)
            {
                if (p.conn != null) Debug.Log($"LocalServer: {p.Name} dùng skill {t.idTemplate} khi còn hồi chiêu {ready - now:0.00}s, bỏ.");
                return false;
            }
            PlayerProfile pr = p.profile;
            if (t.manaUse > 0)
            {
                bool paid;
                using (CoreSystems.AsServer(pr, null)) paid = pr.TrySpendMana(t.manaUse);
                if (!paid)
                {
                    if (now >= p.nextDenyToast)
                    {
                        p.nextDenyToast = now + 1f;
                        SendToast(p.conn, Game.Story.StoryText.L("Không đủ mana ({0} MP)|Not enough MP ({0})", t.manaUse));
                    }
                    Flush(p, ProfileSync.Stats);   // client đoán sai mana: gửi lại số chuẩn
                    return false;
                }
            }
            p.readyAt[t.idTemplate] = now + Cooldown(p, t);
            return true;
        }

        // như LocalCombat.Cooldown: đòn thường theo vũ khí (hoặc basicCooldown) cắt theo tốc đánh, skill khác theo cây nhân vật
        float Cooldown(ServerPlayer p, Skill_Template t)
        {
            PlayerProfile pr = p.profile;
            if (t.idTemplate == BasicSkill)
            {
                float basicCd = t.fromProfile && t.cool_down > 0f ? t.cool_down : settings.basicCooldown;
                return basicCd * (1f - (pr != null ? pr.AttackSpeedCut : 0f));
            }
            return t.cool_down * (pr != null ? pr.Bonus.SkillCooldownScale : 1f);
        }

        void TickHits(float now)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingHit h = pending[i];
                if (!h.hitDone)
                {
                    if (now < h.hitAt) continue;
                    h.hitDone = true;
                    Resolve(h);
                    if (h.kind == HitKind.Bullet)
                    {
                        pending.RemoveAt(i);
                        continue;
                    }
                    h.endAt = now + AfterHit;
                    continue;
                }
                if (now < h.endAt) continue;
                // cận chiến / phép vùng: báo xong (không mục tiêu) để skill bên client tự dọn
                SendBullet(h, 1, new List<(ServerMonster, long)>(), h.target);
                pending.RemoveAt(i);
            }
        }

        void Resolve(PendingHit h)
        {
            var victims = new List<ServerMonster>();
            Vector2 center = h.target;
            switch (h.kind)
            {
                case HitKind.Bullet:
                    if (h.template.rangeDame > 0.01f) CollectCircle(h.map, center, h.template.rangeDame, victims);
                    else
                    {
                        ServerMonster m = FirstOnSegment(h.map, h.start, center + h.dir * 0.01f, settings.bulletHitRadius);
                        if (m != null) victims.Add(m);
                    }
                    break;
                case HitKind.Melee:
                    Vector2 origin = new Vector2(h.attacker.pos.x, h.attacker.pos.z);
                    CollectCone(h.map, origin, h.dir, Mathf.Max(1f, h.template.rangeDame + 0.5f), settings.meleeHalfAngle, victims);
                    center = origin + h.dir;
                    break;
                case HitKind.Area:
                    CollectCircle(h.map, center, Mathf.Max(0.5f, h.template.rangeDame), victims);
                    break;
            }

            var results = Damage(h.attacker, h.template, victims);
            SendBullet(h, (byte)(h.kind == HitKind.Bullet ? 1 : 2), results, center);

            // còn sống thì quay sang đuổi người đánh; hạ quái: thưởng người đánh (nhiệm vụ, EXP, vàng, đồ), đồ rơi xuống đất, hẹn hồi sinh
            foreach ((ServerMonster m, long _) in results)
            {
                if (!m.dead) OnMonsterHit(m, h.attacker);
                else MonsterDied(h.map, m, h.attacker);
            }
            Flush(h.attacker, 0);
        }

        /// <summary>Quái vừa hết máu (m.dead đã bật): báo cả map (MOB.DIE), có người hạ thì thưởng (nhiệm vụ, EXP, vàng, đồ) và
        /// rơi đồ của người đó, rồi hẹn hồi sinh. killer null (server giết bằng lệnh): không thưởng, không rơi đồ.</summary>
        void MonsterDied(ServerMap map, ServerMonster m, ServerPlayer killer)
        {
            m.hp = 0;
            m.target = null;
            m.hasOrder = false;
            m.swingAt = -1f;
            m.moveSent = false;
            int killerId = killer != null ? killer.id : -1;
            SendMob(map, Cmd_Message.MobSub.DIE, m, w => w.writeInt(killerId));
            if (killer != null && killer.profile != null)
            {
                var drops = new List<DropRoll>();
                toasts.Clear();
                using (CoreSystems.AsServer(killer.profile, toasts))
                    CoreSystems.GrantKillRewards(m.templateId, m.maxHp, 0.06f, drops, m.level);
                ForwardToasts(killer);
                DropItems(map, m.pos, drops, killer.id);
            }
            m.respawnAt = Now + Mathf.Max(0.5f, settings.respawnSeconds);
        }

        // như LocalCombat.Damage: gốc baseDamage (skill khác x skillDamagePercent %), nhân % sát thương của cây / đồ / vũ khí,
        // dao động ±10%, chí mạng; % sát thương đổi EXP
        List<(ServerMonster, long)> Damage(ServerPlayer p, Skill_Template t, List<ServerMonster> victims)
        {
            var hits = new List<(ServerMonster, long)>(victims.Count);
            PlayerProfile pr = p.profile;
            Game.Players.PlayerStats bonus = pr.Bonus;
            Game.Skills.WeaponStats weapon = pr.BoostedWeaponStats;
            bool basic = t.idTemplate == BasicSkill;
            int percent = t.damagePercent > 0 ? t.damagePercent : (basic ? 100 : settings.skillDamagePercent);
            float baseDamage = settings.baseDamage * percent / 100f;
            float scale = (1f + bonus.damage / 100f) * (basic ? 1f + bonus.weaponPower / 100f : 1f) * (1f + weapon.damage / 100f);
            float critChance = Mathf.Min(Game.Players.PlayerStats.MaxCritChance / 100f, settings.critChance + bonus.crit / 100f);
            long dealt = 0;
            foreach (ServerMonster m in victims)
            {
                if (m.dead) continue;
                float roll = baseDamage * scale * UnityEngine.Random.Range(0.9f, 1.1f);
                if (UnityEngine.Random.value < critChance) roll *= 2f + bonus.critDamage / 100f;
                long dmg = Math.Max(1, (long)Math.Round(roll));
                dealt += Math.Min(dmg, m.hp);
                m.hp = Math.Max(0, m.hp - dmg);
                if (m.hp == 0) m.dead = true;
                hits.Add((m, dmg));
            }
            if (bonus.hitExp > 0f && dealt > 0)
            {
                long exp = (long)(dealt * bonus.hitExp / 100f);
                if (exp > 0)
                    using (CoreSystems.AsServer(pr, null)) pr.Progress.AddExp(exp, 0f, false);
            }
            return hits;
        }

        // act 1: nổ / xong (int tickCount, long thời gian ở cuối); act 2: trúng, không nổ
        void SendBullet(PendingHit h, byte act, List<(ServerMonster, long)> hits, Vector2 at)
        {
            Vector3 pos = new Vector3(at.x, h.attacker.pos.y, at.y) * 100f;
            foreach (ServerPlayer o in h.map.players)
                Send(o.conn, Cmd_Message.BULLET, w =>
                {
                    w.writeByte(act);
                    w.writeShort(h.hitId);
                    WriteInt3(w, pos);
                    w.writeByte(hits.Count);
                    foreach ((ServerMonster m, long dmg) in hits)
                    {
                        w.writeShort(m.id);
                        w.WriteLong(dmg);
                        w.WriteLong(m.hp);   // chết / sống: MOB.DIE gửi riêng (MonsterDied)
                    }
                    if (act == 1)
                    {
                        w.writeInt(Environment.TickCount);
                        w.WriteLong(0);
                    }
                });
        }

        static HitKind KindOf(Skill_.TYPE type)
        {
            switch (type)
            {
                case Skill_.TYPE.Bullet_Nor: return HitKind.Bullet;
                case Skill_.TYPE.Melee: return HitKind.Melee;
                case Skill_.TYPE.Cast:
                case Skill_.TYPE.Cast2:
                case Skill_.TYPE.Target: return HitKind.Area;
                default: return HitKind.Other;
            }
        }

        short NextHitId()
        {
            lastHitId = (short)(lastHitId % 31999 + 1);
            return lastHitId;
        }

        static void WriteInt3(MyWriter w, Vector3 v)
        {
            w.writeInt(Mathf.RoundToInt(v.x));
            w.writeInt(Mathf.RoundToInt(v.y));
            w.writeInt(Mathf.RoundToInt(v.z));
        }

        // ---------------- tìm mục tiêu (mặt phẳng ngang) ----------------

        static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
            return a + ab * t;
        }

        static ServerMonster FirstOnSegment(ServerMap map, Vector2 a, Vector2 b, float radius)
        {
            ServerMonster best = null;
            float bestAlong = float.MaxValue;
            foreach (ServerMonster m in map.monsters)
            {
                if (m.dead) continue;
                Vector2 c = ClosestOnSegment(a, b, m.pos);
                if ((m.pos - c).sqrMagnitude > radius * radius) continue;
                float along = (c - a).sqrMagnitude;
                if (along < bestAlong) { bestAlong = along; best = m; }
            }
            return best;
        }

        static void CollectCircle(ServerMap map, Vector2 center, float radius, List<ServerMonster> into)
        {
            foreach (ServerMonster m in map.monsters)
                if (!m.dead && (m.pos - center).sqrMagnitude <= radius * radius) into.Add(m);
        }

        static void CollectCone(ServerMap map, Vector2 origin, Vector2 dir, float radius, float halfAngle, List<ServerMonster> into)
        {
            foreach (ServerMonster m in map.monsters)
            {
                if (m.dead) continue;
                Vector2 to = m.pos - origin;
                if (to.sqrMagnitude > radius * radius) continue;
                if (to.sqrMagnitude > 0.01f && Vector2.Angle(dir, to) > halfAngle) continue;
                into.Add(m);
            }
        }
    }
}
