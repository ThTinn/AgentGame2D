using System.Collections.Generic;
using UnityEngine;

namespace Game.ServerSim
{
    /// <summary>
    /// Người chơi ảo (bot) của server giả lập: <see cref="ServerPlayer"/> không có kết nối (conn null, gói gửi tới thì bỏ),
    /// đứng trong map như người thật nên client thấy qua MAP_INFO / ADD_PLAYER / MOVE / BULLET như mọi người chơi khác.
    /// Server lái hết: đi dạo quanh chỗ xuất hiện, tìm quái gần, chạy tới, đánh bằng skill 0 (chung luật ServerCombat với người
    /// thật: sát thương, thưởng, đồ rơi), hạ xong thì nhặt đồ của mình. Chỉ nghĩ khi map có người thật (không ai xem thì đứng yên).
    /// Server không biết độ cao địa hình: toạ độ gửi đi mang y = CoreNet.GroundY, client tự đặt xuống đất / ngang tầm tay.
    /// Số lượng, tốc độ...: LocalPlaySettings (mục "Local Server: người chơi ảo"). Bot không lưu hồ sơ.
    /// </summary>
    public sealed partial class ServerWorld
    {
        const float BotSendInterval = 0.12f;   // = GameController.UDPcount (6) x FixedUpdate 0.02s: nhịp client nội suy một gói MOVE
        const sbyte BotSendTicks = 6;
        const float BotArrive = 0.3f;
        const float BotLootRange = 8f;
        const float BotPickRange = 0.8f;
        const float BotAttackPose = 0.45f;      // giữ dáng đánh chừng này giây rồi về đứng yên

        static readonly string[] BotNames = { "An", "Bình", "Châu", "Dũng", "Giang", "Hà", "Khoa", "Linh", "Minh", "Nga", "Phong", "Quân" };

        enum BotState { Idle, Walk, Chase, Attack, Loot }

        sealed class ServerBot
        {
            public ServerPlayer player;
            public BotState state;
            public ServerMonster target;
            public GroundItem item;
            public Vector2 home, dest;
            public float nextThink, nextAttack, nextSend, poseUntil;
            public int combo;
            public bool moved;                  // đã đi từ gói MOVE trước, chưa báo
            public PlayerStateEnum sentState = PlayerStateEnum.IDLE;

            public Vector2 Pos
            {
                get => new Vector2(player.pos.x, player.pos.z);
                set => player.pos = new Vector3(value.x, CoreNet.GroundY, value.y);
            }
        }

        readonly List<ServerBot> bots = new List<ServerBot>();

        void SpawnBots()
        {
            for (int i = 0; i < settings.botCount; i++)
            {
                string name = BotNames[i % BotNames.Length] + (i < BotNames.Length ? "" : " " + (i / BotNames.Length + 1));
                var p = new ServerPlayer(nextPlayerId++, "Bot " + name);
                p.Load(settings.playerData);
                Vector2 at = settings.spawn + Random.insideUnitCircle * 4f;
                EnterMap(p, settings.mapId, at, 0f);
                bots.Add(new ServerBot { player = p, home = at, nextThink = Now + Random.Range(0.5f, 2f) });
            }
            if (bots.Count > 0) Debug.Log($"LocalServer: tạo {bots.Count} bot ở map {settings.mapId}.");
        }

        void ReleaseBots()
        {
            foreach (ServerBot b in bots)
            {
                if (maps.TryGetValue(b.player.mapId, out ServerMap map)) map.players.Remove(b.player);
                b.player.Release();
            }
            bots.Clear();
        }

        /// <summary>Bot vừa hồi sinh ở at (ServerMonsters.RevivePlayer): quên việc đang làm, đi dạo quanh chỗ mới.</summary>
        void OnBotRevived(ServerPlayer p, Vector2 at)
        {
            foreach (ServerBot b in bots)
            {
                if (b.player != p) continue;
                b.home = at;
                b.moved = false;
                b.poseUntil = 0f;
                b.sentState = PlayerStateEnum.IDLE;
                Rest(b, Now, 1f, 2f);
                return;
            }
        }

        ServerPlayer FindPlayer(int id)
        {
            foreach (ServerPlayer o in players.Values) if (o.id == id) return o;
            foreach (ServerBot b in bots) if (b.player.id == id) return b.player;
            return null;
        }

        static bool HasHuman(ServerMap map)
        {
            foreach (ServerPlayer o in map.players) if (o.conn != null) return true;
            return false;
        }

        // ================================================================ mỗi frame

        void TickBots(float now, float dt)
        {
            foreach (ServerBot b in bots)
            {
                if (b.player.dead) continue;   // nằm chờ hồi sinh (ServerMonsters)
                if (!maps.TryGetValue(b.player.mapId, out ServerMap map) || !HasHuman(map)) continue;
                Skill_Template basic = SkillOf(b.player, BasicSkill);   // đòn thường theo vũ khí của bot; chưa có thì chưa đánh
                Think(b, map, basic, now, dt);
                SendBotMove(b, map, now, false);
            }
        }

        void Think(ServerBot b, ServerMap map, Skill_Template basic, float now, float dt)
        {
            switch (b.state)
            {
                case BotState.Idle:
                    if (now < b.nextThink) return;
                    ChooseTask(b, map, basic, now);
                    return;

                case BotState.Walk:
                    if (MoveToward(b, b.dest, 0f, dt)) Rest(b, now, 1f, 3f);
                    return;

                case BotState.Chase:
                case BotState.Attack:
                {
                    ServerMonster m = b.target;
                    if (m == null || m.dead || basic == null)
                    {
                        Rest(b, now, 0.4f, 1f);
                        return;
                    }
                    float reach = ReachOf(basic);
                    float dist = Vector2.Distance(b.Pos, m.pos);
                    if (b.state == BotState.Chase)
                    {
                        if (dist <= reach || MoveToward(b, m.pos, reach * 0.9f, dt)) b.state = BotState.Attack;
                        return;
                    }
                    if (dist > reach * 1.3f)   // quái đi dạo ra xa: đuổi tiếp
                    {
                        b.state = BotState.Chase;
                        return;
                    }
                    if (now >= b.nextAttack) BotAttack(b, map, m, now);
                    return;
                }

                case BotState.Loot:
                {
                    GroundItem it = b.item;
                    if (it == null || !map.items.ContainsKey(it.id))
                    {
                        Rest(b, now, 0.2f, 0.5f);
                        return;
                    }
                    if (MoveToward(b, it.pos, BotPickRange * 0.5f, dt))
                    {
                        BotPick(b, map, it);
                        Rest(b, now, 0.2f, 0.5f);
                    }
                    return;
                }
            }
        }

        // nhặt đồ của mình trước, rồi tới quái gần nhất (ưu tiên con chưa bot nào nhắm), không có gì thì đi dạo
        void ChooseTask(ServerBot b, ServerMap map, Skill_Template basic, float now)
        {
            Vector2 pos = b.Pos;
            GroundItem loot = null;
            float best = BotLootRange * BotLootRange;
            foreach (GroundItem it in map.items.Values)
            {
                if (it.owner != b.player.id) continue;
                float d = (it.pos - pos).sqrMagnitude;
                if (d < best) { best = d; loot = it; }
            }
            if (loot != null)
            {
                b.item = loot;
                b.state = BotState.Loot;
                return;
            }

            if (basic != null)
            {
                ServerMonster target = null;
                float bestScore = settings.botSearchRange * settings.botSearchRange;
                foreach (ServerMonster m in map.monsters)
                {
                    if (m.dead) continue;
                    float d = (m.pos - pos).sqrMagnitude;
                    if (d > settings.botSearchRange * settings.botSearchRange) continue;
                    float score = TargetedByOtherBot(b, m) ? d * 4f + 25f : d;   // tản ra, đừng cả đám xúm một con
                    if (score < bestScore) { bestScore = score; target = m; }
                }
                if (target != null)
                {
                    b.target = target;
                    b.state = BotState.Chase;
                    return;
                }
            }

            b.dest = b.home + Random.insideUnitCircle * settings.botWanderRadius;
            b.state = BotState.Walk;
        }

        bool TargetedByOtherBot(ServerBot self, ServerMonster m)
        {
            foreach (ServerBot o in bots)
                if (o != self && o.target == m && (o.state == BotState.Chase || o.state == BotState.Attack)) return true;
            return false;
        }

        void Rest(ServerBot b, float now, float min, float max)
        {
            b.state = BotState.Idle;
            b.target = null;
            b.item = null;
            b.nextThink = now + Random.Range(min, max);
        }

        /// <summary>Đi về phía to, dừng cách stopAt mét. true khi đã tới.</summary>
        bool MoveToward(ServerBot b, Vector2 to, float stopAt, float dt)
        {
            Vector2 pos = b.Pos;
            Vector2 delta = to - pos;
            float dist = delta.magnitude;
            float left = dist - stopAt;
            if (left <= BotArrive) return true;
            float step = Mathf.Min(left, Mathf.Max(0.5f, settings.botMoveSpeed) * dt);
            b.Pos = pos + delta / dist * step;
            b.player.dir = Yaw(delta);
            b.moved = true;
            return left - step <= BotArrive;
        }

        float ReachOf(Skill_Template t)
        {
            switch (KindOf(t.type))
            {
                case HitKind.Melee: return Mathf.Max(1f, t.rangeDame + 0.5f) * 0.7f;
                case HitKind.Bullet: return Mathf.Clamp(t.rangeSkill * 0.7f, 1.5f, 8f);
                default: return Mathf.Clamp(t.rangeSkill * 0.7f, 1f, 6f);
            }
        }

        void BotAttack(ServerBot b, ServerMap map, ServerMonster m, float now)
        {
            // hồi chiêu đòn thường (ServerCombat.CanCast) dài hơn nhịp đánh của bot: chờ, đừng vung dáng không ra đòn
            if (b.player.readyAt.TryGetValue(BasicSkill, out float ready) && now < ready) return;
            b.nextAttack = now + Mathf.Max(0.2f, settings.botAttackInterval);
            b.player.dir = Yaw(m.pos - b.Pos);
            // dáng đánh trước (MOVE state ATTACK) rồi mới tới BULLET, như client người thật gửi
            b.player.action = (byte)PlayerStateEnum.ATTACK;
            b.player.subAct = (byte)((int)PlayerAction.ATTACK1 + b.combo);   // ATTACK1 -> 2 -> 3 xoay vòng
            b.combo = (b.combo + 1) % 3;
            b.poseUntil = now + BotAttackPose;
            SendBotMove(b, map, now, true);
            Attack(b.player, BasicSkill, b.player.pos, new Vector3(m.pos.x, CoreNet.GroundY, m.pos.y));
        }

        void BotPick(ServerBot b, ServerMap map, GroundItem it)
        {
            using (CoreSystems.AsServer(b.player.profile, null)) b.player.profile.Loot(it.itemId, it.count);   // túi đầy thì thôi, đồ vẫn mất
            map.items.Remove(it.id);
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.GROUND_ITEM, w =>
                {
                    w.writeByte(GroundLoot.SubRemove);
                    w.writeInt(it.id);
                    w.writeInt(b.player.id);
                });
        }

        /// <summary>
        /// Báo vị trí / dáng của bot cho người thật trong map. Đang chạy: mỗi BotSendInterval một gói (client nội suy BotSendTicks
        /// FixedUpdate tới điểm mới). Đổi dáng (đứng / chạy / đánh) thì gửi ngay. force: gửi luôn với dáng đang đặt sẵn.
        /// </summary>
        void SendBotMove(ServerBot b, ServerMap map, float now, bool force)
        {
            ServerPlayer p = b.player;
            if (!force)
            {
                if (now < b.poseUntil) return;   // đang giữ dáng đánh
                PlayerStateEnum want = b.moved ? PlayerStateEnum.MOVE : PlayerStateEnum.IDLE;
                if (want == b.sentState && (!b.moved || now < b.nextSend)) return;
                p.action = (byte)want;
                p.subAct = (byte)(want == PlayerStateEnum.MOVE ? PlayerAction.RUN : PlayerAction.IDLE);
            }
            b.sentState = (PlayerStateEnum)p.action;
            b.moved = false;
            b.nextSend = now + BotSendInterval;
            Vector3 pos = p.pos;
            int dir = p.dir;
            byte action = p.action, subAct = p.subAct;
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.MOVE, w =>
                {
                    w.writeInt(p.id);
                    WriteVec(w, pos);
                    w.writeInt(dir);
                    w.writeByte(action);
                    w.writeByte(subAct);
                    w.writeByte(BotSendTicks);
                    w.writeByte(0);            // Player.TypeSentMove.MOVE
                });
        }

        // góc quay quanh trục y (độ, 0..360) nhìn theo hướng d trên mặt phẳng xz, như transform.localEulerAngles.y của client
        static int Yaw(Vector2 d) =>
            d.sqrMagnitude < 1e-6f ? 0 : Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 360f));
    }
}
