using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.ServerSim
{
    /// <summary>
    /// Server điều khiển quái bằng lệnh (thử kịch bản, GM, cửa sổ Tools/Local Server/Monster Control), thay cho AI ở ServerMonsters:
    /// <list type="bullet">
    /// <item>MonsterMoveTo / MonsterAttack / MonsterStop: quái chuyển sang "bị điều khiển": AI tắt (không đi dạo, không tự đuổi, bị
    /// đánh không quay lại, không bị kéo về bãi), chỉ làm lệnh cuối cùng. Xong lệnh (tới nơi, mục tiêu chết / rời map) thì đứng chờ.</item>
    /// <item>MonsterRelease: trả về AI (quái chạy về bãi, tới nơi đầy máu). Hồi sinh cũng trả về AI.</item>
    /// <item>MonsterTeleport / MonsterSetHp / MonsterKill / MonsterRespawn: dùng được cả khi đang theo AI.</item>
    /// </list>
    /// monsterId = <see cref="AllMonsters"/>: mọi con còn sống trong map (MonsterRespawn: mọi con). Trả về số con đã nhận lệnh,
    /// 0 khi map chưa ai vào (chưa dựng quái) / không có con đó / con đó đã chết. Gọi trên main thread (như mọi luật của ServerWorld).
    /// </summary>
    public sealed partial class ServerWorld
    {
        public const short AllMonsters = -1;

        /// <summary>Map đã dựng quái (đã có người vào ít nhất một lần).</summary>
        public IEnumerable<short> LoadedMaps => maps.Keys;

        /// <summary>Quái của map, rỗng khi map chưa dựng. Chỉ đọc: đổi trạng thái thì qua các hàm Monster* bên dưới.</summary>
        public IReadOnlyList<ServerMonster> MonstersIn(short mapId) =>
            maps.TryGetValue(mapId, out ServerMap map) ? map.monsters : (IReadOnlyList<ServerMonster>)Array.Empty<ServerMonster>();

        public ServerMonster FindMonster(short mapId, short monsterId) => maps.TryGetValue(mapId, out ServerMap map) ? map.Monster(monsterId) : null;

        /// <summary>Người chơi (cả bot) đang ở map, để chọn mục tiêu cho MonsterAttack.</summary>
        public IReadOnlyList<ServerPlayer> PlayersIn(short mapId) =>
            maps.TryGetValue(mapId, out ServerMap map) ? map.players : (IReadOnlyList<ServerPlayer>)Array.Empty<ServerPlayer>();

        /// <summary>Đi tới to (x, z) rồi đứng chờ. speed &lt;= 0: tốc đuổi (LocalPlaySettings.chaseSpeed).</summary>
        public int MonsterMoveTo(short mapId, short monsterId, Vector2 to, float speed = 0f) =>
            Each(mapId, monsterId, (map, m) =>
            {
                TakeControl(m);
                m.hasOrder = true;
                m.orderTo = to;
                m.orderSpeed = speed;
            });

        /// <summary>Đuổi đánh người chơi playerId (ở cùng map) tới khi người đó chết / rời map, không bị kéo về bãi.</summary>
        public int MonsterAttack(short mapId, short monsterId, int playerId, float speed = 0f)
        {
            ServerPlayer p = FindPlayer(playerId);
            if (p == null || !maps.TryGetValue(mapId, out ServerMap target) || !Targetable(target, p)) return 0;
            return Each(mapId, monsterId, (map, m) =>
            {
                TakeControl(m);
                m.target = p;
                m.mode = MonsterMode.Chase;
                m.orderSpeed = speed;
            });
        }

        /// <summary>Đứng yên tại chỗ (bỏ đòn đang vung), chờ lệnh.</summary>
        public int MonsterStop(short mapId, short monsterId) =>
            Each(mapId, monsterId, (map, m) =>
            {
                TakeControl(m);
                SendMobStop(map, m);
            });

        /// <summary>Trả về AI: chạy về bãi, tới nơi đầy máu rồi đi dạo như cũ.</summary>
        public int MonsterRelease(short mapId, short monsterId) =>
            Each(mapId, monsterId, (map, m) =>
            {
                if (!m.controlled) return;
                m.controlled = false;
                m.hasOrder = false;
                StartReturn(m);
            });

        /// <summary>Đặt ngay tới at (x, z), bỏ lệnh đi / đòn đang vung. Theo AI mà ra ngoài vùng bãi thì quái tự chạy về.</summary>
        public int MonsterTeleport(short mapId, short monsterId, Vector2 at) =>
            Each(mapId, monsterId, (map, m) =>
            {
                m.pos = at;
                m.hasOrder = false;
                m.wandering = false;
                m.swingAt = -1f;
                m.nextSend = 0f;
                m.moveSent = false;
                SendMobTeleport(map, m);
            });

        /// <summary>Đặt máu (giữ trong 1..maxHp). hp &lt;= 0: như MonsterKill không người hạ.</summary>
        public int MonsterSetHp(short mapId, short monsterId, long hp)
        {
            if (hp <= 0) return MonsterKill(mapId, monsterId);
            return Each(mapId, monsterId, (map, m) =>
            {
                m.hp = Math.Min(hp, m.maxHp);
                SendMobHp(map, m);
            });
        }

        /// <summary>Giết ngay, hẹn hồi sinh như bị đánh chết. killerId là người chơi trong map thì người đó nhận thưởng / đồ rơi
        /// như tự hạ; -1: không ai được gì.</summary>
        public int MonsterKill(short mapId, short monsterId, int killerId = -1)
        {
            ServerPlayer killer = killerId >= 0 ? FindPlayer(killerId) : null;
            if (killer != null && killer.mapId != mapId) killer = null;
            int n = Each(mapId, monsterId, (map, m) =>
            {
                m.dead = true;
                MonsterDied(map, m, killer);   // MOB.DIE, thưởng người hạ, hẹn hồi sinh
            });
            if (n > 0 && killer != null) Flush(killer, 0);
            return n;
        }

        /// <summary>Hồi sinh ngay ở chỗ cũ, đầy máu, trả về AI (con còn sống cũng được: đặt lại như mới).</summary>
        public int MonsterRespawn(short mapId, short monsterId)
        {
            if (!maps.TryGetValue(mapId, out ServerMap map)) return 0;
            float now = Now;
            int n = 0;
            foreach (ServerMonster m in map.monsters)
            {
                if (monsterId != AllMonsters && m.id != monsterId) continue;
                RespawnMonster(map, m, now);
                n++;
            }
            return n;
        }

        // ================================================================ trong vòng AI (ServerMonsters.TickMonsters)

        // đang có mục tiêu thì đuổi đánh; không thì đi tới điểm được lệnh; xong hết thì đứng yên
        void TickControlled(ServerMap map, ServerMonster m, float now, float dt)
        {
            float speed = m.orderSpeed > 0f ? m.orderSpeed : settings.chaseSpeed;
            if (m.target != null)
            {
                if (Targetable(map, m.target))
                {
                    ChaseStep(map, m, m.target, speed, now, dt);
                    return;
                }
                m.target = null;   // chết / rời map: đứng chờ lệnh
            }
            if (m.hasOrder && MoveMonster(map, m, m.orderTo, 0f, speed, now, dt)) m.hasOrder = false;
        }

        static void TakeControl(ServerMonster m)
        {
            m.controlled = true;
            m.target = null;
            m.hasOrder = false;
            m.wandering = false;
            m.swingAt = -1f;
            m.holdUntil = 0f;
            m.nextSend = 0f;
        }

        // các con còn sống được chọn (một con / cả map), trả về số con
        int Each(short mapId, short monsterId, Action<ServerMap, ServerMonster> act)
        {
            if (!maps.TryGetValue(mapId, out ServerMap map)) return 0;
            int n = 0;
            foreach (ServerMonster m in map.monsters)
            {
                if (m.dead || (monsterId != AllMonsters && m.id != monsterId)) continue;
                act(map, m);
                n++;
            }
            return n;
        }
    }
}
