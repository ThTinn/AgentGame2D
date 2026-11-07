using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Players;
using Random = UnityEngine.Random;

namespace Game.ServerSim
{
    /// <summary>
    /// Quái và sống / chết của người chơi ở server giả lập, theo đúng luật chơi offline (LocalMonsterAI, LocalCombat.DamagePlayer):
    /// <list type="bullet">
    /// <item>Quái đi dạo trong bãi. Người chơi (cả bot) vào bãi và tới gần (aggroRange), hoặc đánh trúng nó, thì quái đuổi; tới tầm
    /// (monsterAttackRange) thì vung đòn (MOB.ATTACK), monsterHitDelay sau còn trong tầm thì trúng (MOB.HIT): giáp của hồ sơ cắt
    /// %, trừ máu ở hồ sơ server, PROFILE_SYNC báo máu về. Người chơi / quái ra ngoài leashScale lần bán kính bãi, hoặc người chơi chết /
    /// rời map, thì quái về chỗ cũ, tới nơi đầy máu (MOB.HP).</item>
    /// <item>Quái chạy liên tục ở server; MOB.MOVE mỗi MobSendInterval gửi điểm dẫn trước một nhịp kèm tốc độ để client chạy khớp,
    /// tới nơi thì MOB.STOP kèm chỗ đứng. Chết MOB.DIE, hồi sinh MOB.RESPAWN. Client không tự quyết gì về quái (OnlineMobs).</item>
    /// <item>Hết máu: S_PLAYER_DIE, thôi nhận đánh / di chuyển, playerRespawnSeconds sau hồi sinh ở chỗ xuất hiện đầy máu / mana
    /// (S_PLAYER_REVIVE).</item>
    /// <item>Còn sống thì hồi máu theo nút hồi phục của cây (Bonus.hpRegen) và mana theo LocalPlaySettings.manaRegen mỗi giây.</item>
    /// </list>
    /// Như mọi thứ khác của server giả lập: chỉ chạy khi map có người thật.
    /// </summary>
    public sealed partial class ServerWorld
    {
        const float MobSendInterval = 0.3f;
        const float MobLeadExtra = 0.05f;    // dẫn trước thêm chút: gói tới trễ thì client vẫn còn đường để chạy
        const float MobArrive = 0.15f;
        const float MobAttackHold = 0.6f;    // đứng lại sau cú vung cho hết anim đánh rồi mới chạy tiếp
        const float MobReachSlack = 0.7f;    // lùi nhẹ lúc quái vung vẫn trúng (như LocalMonsterAI)

        // ================================================================ quái

        void TickMonsters(ServerMap map, float now, float dt)
        {
            foreach (ServerMonster m in map.monsters)
            {
                if (m.dead)
                {
                    if (now >= m.respawnAt) RespawnMonster(map, m, now);
                    continue;
                }
                if (m.swingAt >= 0f)
                {
                    UpdateSwing(map, m, now);
                    continue;
                }
                if (now < m.holdUntil) continue;
                if (m.controlled)
                {
                    TickControlled(map, m, now, dt);
                    continue;
                }

                switch (m.mode)
                {
                    case MonsterMode.Wander:
                    {
                        ServerPlayer near = AggroTarget(map, m);
                        if (near != null)
                        {
                            StartChase(m, near);
                            break;
                        }
                        if (!m.wandering)
                        {
                            if (now < m.nextWander) break;
                            m.wanderTo = m.campCenter + Random.insideUnitCircle * m.radius * 0.7f;
                            m.wandering = true;
                            m.nextSend = 0f;
                        }
                        if (MoveMonster(map, m, m.wanderTo, 0f, settings.wanderSpeed, now, dt))
                        {
                            m.wandering = false;
                            m.nextWander = now + Random.Range(3f, 7f);
                        }
                        break;
                    }

                    case MonsterMode.Chase:
                    {
                        ServerPlayer t = m.target;
                        if (!Targetable(map, t) || !InLeash(m, Flat(t.pos)) || !InLeash(m, m.pos))
                        {
                            StartReturn(m);
                            break;
                        }
                        ChaseStep(map, m, t, settings.chaseSpeed, now, dt);
                        break;
                    }

                    case MonsterMode.Return:
                        if (MoveMonster(map, m, m.home, 0f, settings.chaseSpeed, now, dt))
                        {
                            m.mode = MonsterMode.Wander;
                            m.nextWander = now + Random.Range(2f, 5f);
                            if (m.hp < m.maxHp)
                            {
                                m.hp = m.maxHp;   // về tới nơi: đầy máu
                                SendMobHp(map, m);
                            }
                        }
                        break;
                }
            }
        }

        // tới tầm thì vung đòn (hết hồi chiêu), chưa tới thì chạy lại gần
        void ChaseStep(ServerMap map, ServerMonster m, ServerPlayer t, float speed, float now, float dt)
        {
            Vector2 tp = Flat(t.pos);
            float range = Mathf.Max(0.5f, settings.monsterAttackRange);
            if ((tp - m.pos).sqrMagnitude <= range * range)
            {
                if (now >= m.attackReadyAt) StartSwing(map, m, now);
            }
            else MoveMonster(map, m, tp, range * 0.8f, speed, now, dt);
        }

        void StartChase(ServerMonster m, ServerPlayer target)
        {
            m.mode = MonsterMode.Chase;
            m.target = target;
            m.wandering = false;
            m.nextSend = 0f;
        }

        void StartReturn(ServerMonster m)
        {
            m.mode = MonsterMode.Return;
            m.target = null;
            m.wandering = false;
            m.swingAt = -1f;
            m.nextSend = 0f;
        }

        /// <summary>Quái bị đánh trúng (ServerCombat.Resolve): đuổi người đánh, trừ khi đang về hoặc người đánh ở ngoài vùng bãi.</summary>
        void OnMonsterHit(ServerMonster m, ServerPlayer attacker)
        {
            if (m.dead || m.controlled || m.mode == MonsterMode.Return || attacker == null || attacker.dead) return;
            if (m.mode == MonsterMode.Chase && m.target != null && !m.target.dead) return;   // đang đánh người khác: giữ
            if (InLeash(m, Flat(attacker.pos))) StartChase(m, attacker);
        }

        // người gần nhất trong tầm phát hiện mà đang đứng trong bãi
        ServerPlayer AggroTarget(ServerMap map, ServerMonster m)
        {
            ServerPlayer best = null;
            float bestD = settings.aggroRange * settings.aggroRange;
            foreach (ServerPlayer p in map.players)
            {
                if (p.dead) continue;
                Vector2 pp = Flat(p.pos);
                if ((pp - m.campCenter).sqrMagnitude > m.radius * m.radius) continue;
                float d = (pp - m.pos).sqrMagnitude;
                if (d <= bestD) { bestD = d; best = p; }
            }
            return best;
        }

        static bool Targetable(ServerMap map, ServerPlayer p) => p != null && !p.dead && p.mapId == map.id && map.players.Contains(p);

        bool InLeash(ServerMonster m, Vector2 p)
        {
            float leash = m.radius * Mathf.Max(1f, settings.leashScale);
            return (p - m.campCenter).sqrMagnitude <= leash * leash;
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>
        /// Chạy về phía to, dừng cách stopAt mét. Mỗi MobSendInterval báo client điểm quái sẽ tới ở nhịp sau (kèm tốc độ) để client
        /// chạy cùng tốc độ, tới nơi đúng lúc gói kế tới; bước này tới nơi thì MOB.STOP. true khi đã tới.
        /// </summary>
        bool MoveMonster(ServerMap map, ServerMonster m, Vector2 to, float stopAt, float speed, float now, float dt)
        {
            speed = Mathf.Max(0.3f, speed);
            Vector2 d = to - m.pos;
            float dist = d.magnitude;
            float left = dist - stopAt;
            if (left <= MobArrive) return true;
            Vector2 dir = d / dist;
            float step = Mathf.Min(left, speed * dt);
            m.pos += dir * step;
            if (now >= m.nextSend)
            {
                m.nextSend = now + MobSendInterval;
                float lead = Mathf.Min(left - step, speed * (MobSendInterval + MobLeadExtra));
                SendMobMove(map, m, m.pos + dir * lead, speed);
            }
            if (left - step > MobArrive) return false;
            if (m.moveSent) SendMobStop(map, m);
            return true;
        }

        void StartSwing(ServerMap map, ServerMonster m, float now)
        {
            m.attackReadyAt = now + Mathf.Max(0.3f, settings.monsterAttackCooldown);
            m.swingAt = now;
            m.moveSent = false;
            int target = m.target.id;
            Vector2 at = m.pos;
            SendMob(map, Cmd_Message.MobSub.ATTACK, m, w =>
            {
                w.writeInt(target);
                WriteMobPos(w, at);
            });
        }

        void UpdateSwing(ServerMap map, ServerMonster m, float now)
        {
            if (now - m.swingAt < settings.monsterHitDelay) return;
            m.swingAt = -1f;
            m.holdUntil = now + MobAttackHold;
            ServerPlayer t = m.target;
            float reach = settings.monsterAttackRange + MobReachSlack;
            if (Targetable(map, t) && (Flat(t.pos) - m.pos).sqrMagnitude <= reach * reach) HitPlayer(map, m, t, now);
        }

        void RespawnMonster(ServerMap map, ServerMonster m, float now)
        {
            m.dead = false;
            m.hp = m.maxHp;
            m.pos = m.home;
            m.mode = MonsterMode.Wander;
            m.target = null;
            m.wandering = false;
            m.swingAt = -1f;
            m.controlled = false;   // hồi sinh là con mới: AI chạy lại
            m.hasOrder = false;
            m.moveSent = false;
            m.nextWander = now + Random.Range(1f, 3f);
            SendMobRespawn(map, m);
        }

        // ================================================================ gói MOB (định dạng ở Cmd_Message.MobSub)

        /// <summary>Gửi lệnh quái cho cả map: sub, short mobId rồi tới body.</summary>
        void SendMob(ServerMap map, byte sub, ServerMonster m, Action<MyWriter> body = null)
        {
            short id = m.id;
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.MOB, sub, w =>
                {
                    w.writeShort(id);
                    body?.Invoke(w);
                });
        }

        // server không nạp địa hình: y = GroundY, client tự đặt theo mặt đất
        static void WriteMobPos(MyWriter w, Vector2 p) => WriteVec(w, new Vector3(p.x, CoreNet.GroundY, p.y));

        void SendMobMove(ServerMap map, ServerMonster m, Vector2 aim, float speed)
        {
            m.moveSent = true;
            SendMob(map, Cmd_Message.MobSub.MOVE, m, w =>
            {
                WriteMobPos(w, aim);
                WriteFloat(w, speed);
            });
        }

        void SendMobStop(ServerMap map, ServerMonster m)
        {
            m.moveSent = false;
            Vector2 at = m.pos;
            SendMob(map, Cmd_Message.MobSub.STOP, m, w => WriteMobPos(w, at));
        }

        void SendMobHp(ServerMap map, ServerMonster m)
        {
            long hp = m.hp, maxHp = m.maxHp;
            SendMob(map, Cmd_Message.MobSub.HP, m, w =>
            {
                w.WriteLong(hp);
                w.WriteLong(maxHp);
            });
        }

        void SendMobRespawn(ServerMap map, ServerMonster m)
        {
            long hp = m.hp, maxHp = m.maxHp;
            Vector2 at = m.pos;
            SendMob(map, Cmd_Message.MobSub.RESPAWN, m, w =>
            {
                w.WriteLong(hp);
                w.WriteLong(maxHp);
                WriteMobPos(w, at);
            });
        }

        void SendMobTeleport(ServerMap map, ServerMonster m)
        {
            Vector2 at = m.pos;
            SendMob(map, Cmd_Message.MobSub.TELEPORT, m, w => WriteMobPos(w, at));
        }

        // ================================================================ người chơi: trúng đòn, chết, hồi sinh

        // như LocalCombat.DamagePlayer: giáp (Bonus.DefenseCut) cắt %, dao động ±10%, còn ít nhất 1
        void HitPlayer(ServerMap map, ServerMonster m, ServerPlayer p, float now)
        {
            PlayerProfile pr = p.profile;
            if (pr == null || pr.data == null) return;
            float cut = pr.Bonus.DefenseCut;
            long dmg = Math.Max(1, (long)Math.Round(m.damage * (1f - cut) * Random.Range(0.9f, 1.1f)));
            long hp = Math.Max(0, pr.data.curHp - dmg);
            long maxHp = pr.data.maxHp;
            using (CoreSystems.AsServer(pr, null)) pr.UpdateStats(Game.Core.TypeHP_KI.Type_Hp, hp);
            SendMob(map, Cmd_Message.MobSub.HIT, m, w =>
            {
                w.writeInt(p.id);
                w.WriteLong(dmg);
                w.WriteLong(hp);
                w.WriteLong(maxHp);
            });
            if (hp == 0) KillPlayer(map, p, now);
            Flush(p, 0);
        }

        void KillPlayer(ServerMap map, ServerPlayer p, float now)
        {
            float wait = Mathf.Max(0.5f, settings.playerRespawnSeconds);
            p.dead = true;
            p.reviveAt = now + wait;
            p.profile.isDie = true;
            using (CoreSystems.AsServer(p.profile, null)) p.profile.ClearBuffs();   // thuốc tăng chỉ số mất khi bị hạ
            ReleaseSeat(map, p, broadcast: true);
            foreach (ServerMonster m in map.monsters) if (m.target == p) StartReturn(m);
            short tenths = (short)Mathf.RoundToInt(wait * 10f);
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.COMBAT, Cmd_Message.CombatSub.DIE, w =>
                {
                    w.writeInt(p.id);
                    w.writeShort(tenths);
                });
            Debug.Log($"LocalServer: {p.Name} bị hạ, hồi sinh sau {wait:0.#}s.");
        }

        void RevivePlayer(ServerPlayer p)
        {
            PlayerProfile pr = p.profile;
            p.dead = false;
            pr.isDie = false;
            using (CoreSystems.AsServer(pr, null))
            {
                pr.UpdateStats(Game.Core.TypeHP_KI.Type_Hp, pr.data.maxHp);
                pr.UpdateStats(Game.Core.TypeHP_KI.Type_MP, pr.data.maxMp);
            }
            Vector2 at = settings.spawn + Random.insideUnitCircle * 1.5f;
            p.pos = new Vector3(at.x, CoreNet.GroundY, at.y);
            long hp = pr.data.curHp, maxHp = pr.data.maxHp;
            if (maps.TryGetValue(p.mapId, out ServerMap map))
                foreach (ServerPlayer o in map.players)
                    Send(o.conn, Cmd_Message.COMBAT, Cmd_Message.CombatSub.REVIVE, w =>
                    {
                        w.writeInt(p.id);
                        WriteFloat(w, at.x);
                        WriteFloat(w, at.y);
                        w.WriteLong(hp);
                        w.WriteLong(maxHp);
                    });
            OnBotRevived(p, at);
            Flush(p, 0);
        }

        /// <summary>Mỗi frame: hồi sinh người đã tới giờ, hồi máu / mana cho người còn sống (cả bot).</summary>
        void TickLife(float now, float dt)
        {
            foreach (ServerPlayer p in players.Values) TickLife(p, now, dt);
            foreach (ServerBot b in bots) TickLife(b.player, now, dt);
        }

        void TickLife(ServerPlayer p, float now, float dt)
        {
            PlayerProfile pr = p.profile;
            if (pr == null || pr.data == null) return;
            if (p.dead)
            {
                if (now >= p.reviveAt) RevivePlayer(p);
                return;
            }
            using (CoreSystems.AsServer(pr, null))
            {
                Regen(pr, Game.Core.TypeHP_KI.Type_Hp, pr.Bonus.hpRegen, pr.data.curHp, pr.data.maxHp, ref p.hpCarry, dt);
                Regen(pr, Game.Core.TypeHP_KI.Type_MP, settings.manaRegen, pr.data.curMp, pr.data.maxMp, ref p.mpCarry, dt);
            }
        }

        // như CoreSystems.Regen: rate mỗi giây, chưa đầy mới hồi, phần lẻ dồn tới đủ 1 mới cộng
        static void Regen(PlayerProfile pr, Game.Core.TypeHP_KI type, float rate, long cur, long max, ref float carry, float dt)
        {
            if (rate <= 0f || cur >= max)
            {
                carry = 0f;
                return;
            }
            carry += rate * dt;
            if (carry < 1f) return;
            long add = (long)carry;
            carry -= add;
            pr.UpdateStats(type, Math.Min(max, cur + add));
        }
    }
}
