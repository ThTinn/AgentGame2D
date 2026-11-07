using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Map;
using Game.Npcs;
using Game.Players;

namespace Game.ServerSim
{
    /// <summary>Một map đang có người: người chơi, quái (theo LocalPlaySettings.CampsOf), đồ trên đất, ghế đang có người ngồi.</summary>
    public sealed class ServerMap
    {
        public short id;
        public readonly List<ServerPlayer> players = new List<ServerPlayer>();
        public readonly List<ServerMonster> monsters = new List<ServerMonster>();
        public readonly Dictionary<int, GroundItem> items = new Dictionary<int, GroundItem>();
        public readonly Dictionary<int, Dictionary<int, int>> seats = new Dictionary<int, Dictionary<int, int>>();   // uid -> point -> người ngồi

        public ServerMonster Monster(int id)
        {
            foreach (ServerMonster m in monsters) if (m.id == id) return m;
            return null;
        }
    }

    public sealed class ServerMonster
    {
        public short id, templateId, level;
        public long hp, maxHp;
        public Vector2 home, pos;
        public float radius;
        public bool dead;
        public float respawnAt, nextWander;

        // AI (ServerMonsters): bãi, sức đánh, đang làm gì
        public Vector2 campCenter;
        public long damage;
        public MonsterMode mode;
        public ServerPlayer target;
        public Vector2 wanderTo;
        public bool wandering;
        public float attackReadyAt, swingAt = -1f, holdUntil, nextSend;
        public bool moveSent;          // đã gửi MOB.MOVE, chưa STOP / ATTACK: client đang chạy

        // server điều khiển tay (ServerMonsterControl): AI tắt, chỉ làm theo lệnh tới khi MonsterRelease / hồi sinh
        public bool controlled;
        public bool hasOrder;          // đang đi tới orderTo
        public Vector2 orderTo;
        public float orderSpeed;
    }

    public enum MonsterMode { Wander, Chase, Return }

    public sealed class GroundItem
    {
        public int id, itemId, count, owner;
        public Vector2 pos;
        public Vector3 from;
        public float freeAt, expireAt;
    }

    /// <summary>
    /// Luật của server giả lập, chạy trên main thread (LocalServer.Update gọi <see cref="Handle"/> cho từng gói và
    /// <see cref="Tick"/> mỗi frame). Trả lời đúng các gói client đang đọc (GlobalMessageHandler) theo thứ tự của server thật:
    /// CLIENT_INFO -> CHECK_DATA_VERSION, UPDATE_DATA, REQUEST_DATA (ServerWorld.Data); LOGIN -> LOGIN + MENU + ME_INFO + PLAYER_DATA + MAP_INFO; MOVE / chat / PORTAL /
    /// MAP_OBJECT / GROUND_ITEM / ATTACK (ServerWorld.Combat) / NPC_* QUEST_* ITEM_* (ServerWorld.Core) / MAP_READY.
    /// Gói chưa làm thì ghi log một lần.
    /// </summary>
    public sealed partial class ServerWorld
    {
        const float ReachInterval = 0.25f;     // giây giữa hai lần xét mục tiêu Reach của một người chơi (theo gói MOVE)
        const float SaveInterval = 10f;
        const float LootOwnerSeconds = 10f;    // đồ rơi chỉ người hạ quái nhặt được trong chừng này giây
        const float LootLifetime = 60f;

        readonly LocalPlaySettings settings;
        readonly Dictionary<short, ServerMap> maps = new Dictionary<short, ServerMap>();
        readonly Dictionary<ServerConnection, ServerPlayer> players = new Dictionary<ServerConnection, ServerPlayer>();
        readonly HashSet<short> unknownCmds = new HashSet<short>();
        readonly List<string> toasts = new List<string>();
        int nextPlayerId = 1001;
        int nextItemId = 1;
        float nextSave;

        public int PlayerCount => players.Count;

        public ServerWorld(LocalPlaySettings settings)
        {
            this.settings = settings;
            SpawnBots();
        }

        // đồng hồ thật, không theo pause / timeScale của game (server không dừng khi client dừng)
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        public static float Now => (float)clock.Elapsed.TotalSeconds;
        float lastTick = -1f;

        // ================================================================ gói vào

        public void Handle(Incoming msg)
        {
            ServerConnection conn = msg.conn;
            if (msg.disconnect)
            {
                DropUploads(conn);
                Disconnect(conn);
                return;
            }
            players.TryGetValue(conn, out ServerPlayer p);
            var r = new MyReader(msg.data);
            try
            {
                switch (msg.cmd)
                {
                    case Cmd_Message.CLIENT_INFO:
                        ClientInfo(conn, r);
                        break;
                    case Cmd_Message.UPDATE_DATA:
                        SendTemplate(conn, r.ReadByte());
                        break;
                    case Cmd_Message.REQUEST_DATA:
                        RequestAsset(conn, r);
                        break;
                    case Cmd_Message.PING:
                        long sent = r.ReadLong();
                        Send(conn, Cmd_Message.PING, w => w.WriteLong(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                        break;
                    case Cmd_Message.LOGIN:
                        Login(conn, r);
                        break;
                    case Cmd_Message.MOVE:
                        if (p != null && !p.dead) Move(p, r);   // đang nằm chờ hồi sinh: đứng yên
                        break;
                    case Cmd_Message.ROTATE:
                        break;
                    case Cmd_Message.ATTACK:
                        if (p != null) Attack(p, r);
                        break;
                    case Cmd_Message.CHAT_MAP:
                        if (p != null) ChatMap(p, r.ReadUTF());
                        break;
                    case Cmd_Message.CHAT_WORLD:
                        if (p != null) ChatWorld(p, r.ReadUTF());
                        break;
                    case Cmd_Message.CHAT_PM:
                        if (p != null) ChatPrivate(p, r);
                        break;
                    case Cmd_Message.CHAT_CLAN:
                        if (p != null) ChatClan(p, r);
                        break;
                    case Cmd_Message.CHAT_PARTY:
                        if (p != null) ChatParty(p, r);
                        break;
                    // tổ đội / bang (ServerWorld.Social)
                    case Cmd_Message.PARTY:
                        if (p != null) HandleParty(p, r.ReadByte(), r);
                        break;
                    case Cmd_Message.CLAN:
                        if (p != null) HandleClan(p, r.ReadByte(), r);
                        break;
                    case Cmd_Message.PLAYER_INFO:
                        PlayerInfo(conn, r.ReadInt());
                        break;
                    case Cmd_Message.MAP_OBJECT:
                        if (p != null) MapObject(p, r);
                        break;
                    case Cmd_Message.GROUND_ITEM:
                        if (p != null) GroundItemMsg(p, r);
                        break;
                    // Core online: NPC / nhiệm vụ / túi đồ / skill-nâng cấp (ServerWorld.Core), byte đầu là sub-command
                    case Cmd_Message.NPC:
                    case Cmd_Message.QUEST:
                    case Cmd_Message.ITEM:
                    case Cmd_Message.BUILD:
                        if (p != null) HandleCore(p, msg.cmd, r.ReadByte(), r);
                        break;
                    case Cmd_Message.MAP:
                        if (p != null) HandleMap(p, r.ReadByte(), r);
                        break;
                    case Cmd_Message.PLAYER:
                        if (p != null) HandlePlayer(p, r.ReadByte(), r);
                        break;
                    default:
                        if (unknownCmds.Add(msg.cmd)) Debug.LogWarning("LocalServer: chưa giả lập gói " + msg.cmd + " (" + msg.data.Length + " byte).");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("LocalServer: lỗi xử lý gói " + msg.cmd + ": " + e);
            }
            if (p != null) Flush(p, 0);
        }

        // ================================================================ mỗi frame

        public void Tick()
        {
            float now = Now;
            float dt = lastTick < 0f ? 0f : now - lastTick;
            lastTick = now;
            TickUploads();
            foreach (ServerMap map in maps.Values)
            {
                if (!HasHuman(map)) continue;   // bot thôi thì không ai xem: để yên tới khi có người vào
                TickMonsters(map, now, Mathf.Min(dt, 0.25f));
                TickItems(map, now);
            }
            TickBots(now, Mathf.Min(dt, 0.25f));   // Editor dừng lâu: đừng cho bot nhảy cóc
            TickLife(now, dt);
            TickHits(now);
            TickSocial(now);
            TickPlayerState(now);
            foreach (ServerPlayer p in players.Values)
            {
                using (CoreSystems.AsServer(p.profile)) p.profile.TickBuffs(dt);   // thuốc tăng chỉ số hết hạn
                Flush(p, 0);   // đổi ngoài gói tin (thuốc hết hạn): gửi luôn, đừng chờ tới gói kế của người chơi
            }
            if (now >= nextSave)
            {
                nextSave = now + SaveInterval;
                foreach (ServerPlayer p in players.Values) p.Save();
            }
        }

        public void SaveAll()
        {
            foreach (ServerPlayer p in players.Values) p.Save(true);
        }

        /// <summary>Tắt server: ghi hồ sơ người chơi, hủy hồ sơ dựng tạm của bot.</summary>
        public void Shutdown()
        {
            SaveAll();
            ReleaseBots();
        }

        // ================================================================ đăng nhập / rời

        void Login(ServerConnection conn, MyReader r)
        {
            string user = r.Available() > 0 ? r.ReadUTF() : "";
            if (r.Available() > 0) r.ReadUTF();   // mật khẩu: server giả lập nhận mọi mật khẩu
            if (string.IsNullOrWhiteSpace(user)) user = "player" + conn.id;
            user = user.Trim();

            if (players.TryGetValue(conn, out ServerPlayer old)) Disconnect(conn, keepConnection: true);
            foreach (ServerPlayer other in players.Values)
                if (other.account == user)
                {
                    SendToast(conn, "[LocalServer] Tài khoản đang được dùng ở kết nối khác.");
                    return;
                }

            var p = new ServerPlayer(nextPlayerId++, user) { conn = conn };
            p.Load(settings.playerData);
            conn.player = p;
            players[conn] = p;
            Debug.Log($"LocalServer: {user} (id {p.id}) đăng nhập từ {conn.Remote}.");

            // như server thật: LOGIN 1 byte (TypeLogin, 1), popup chào (MENU: icon, nội dung, nút) rồi ME_INFO, MAP_INFO
            Send(conn, Cmd_Message.LOGIN, w => w.writeByte((sbyte)TypeLogin.Team));
            Send(conn, Cmd_Message.MENU, w =>
            {
                w.writeShort(12652);
                w.writeUTF($"Chào mừng {p.Name} vào game.");
                w.writeByte(1);
                w.writeUTF("OK");
            });
            PlayerProfile pr = p.profile;
            Send(conn, Cmd_Message.ME_INFO, w =>
            {
                w.writeInt(p.id);
                w.writeUTF(p.Name);
                w.WriteLong(pr.data.curHp);
                w.WriteLong(pr.data.maxHp);
                w.WriteLong(pr.data.curMp);
                w.WriteLong(pr.data.maxMp);
                w.WriteLong(0);           // coin
                w.WriteLong(pr.Gold);
            });
            p.CopyToData();
            byte[] json = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(p.data));
            Send(conn, Cmd_Message.PROFILE, Cmd_Message.ProfileSub.DATA, w => WriteBlock(w, json));
            p.dirty = 0;
            EnterMap(p, settings.mapId, settings.spawn, 0f);
            SocialOnLogin(p);   // bang của người này (tổ đội chỉ trong phiên: vừa vào là chưa có)
        }

        void Disconnect(ServerConnection conn, bool keepConnection = false)
        {
            if (!players.TryGetValue(conn, out ServerPlayer p)) return;
            LeaveMap(p);
            SocialOnDisconnect(p);   // rời tổ đội, bang thấy người này offline
            p.Save(true);
            p.Release();
            players.Remove(conn);
            Debug.Log($"LocalServer: {p.account} rời server.");
            if (!keepConnection) conn.Close();
        }

        // ================================================================ map

        ServerMap MapOf(short id)
        {
            if (maps.TryGetValue(id, out ServerMap map)) return map;
            map = new ServerMap { id = id };
            short monsterId = 1;
            foreach (LocalPlaySettings.MonsterCamp camp in settings.CampsOf(id))
            {
                if (camp == null) continue;
                for (int i = 0; i < camp.count; i++)
                {
                    // như LocalPlay.SpawnCamps: con đầu ở giữa, còn lại vòng 0.45 bán kính
                    float angle = i * Mathf.PI * 2f / Mathf.Max(1, camp.count - 1);
                    float rr = i == 0 ? 0f : camp.radius * 0.45f;
                    Vector2 at = camp.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rr;
                    map.monsters.Add(new ServerMonster
                    {
                        id = monsterId++, templateId = camp.templateId, hp = camp.maxHp, maxHp = camp.maxHp,
                        home = at, pos = at, radius = camp.radius, nextWander = Now + UnityEngine.Random.Range(3f, 8f),
                        campCenter = camp.center, damage = camp.damage, level = (short)Mathf.Max(1, camp.level),
                    });
                }
            }
            maps[id] = map;
            return map;
        }

        /// <summary>Đưa người chơi vào map (đăng nhập, qua cổng): rời map cũ, gửi MAP_INFO cho người đó, ADD_PLAYER cho người khác.</summary>
        void EnterMap(ServerPlayer p, short mapId, Vector2 xz, float facing)
        {
            LeaveMap(p);
            ServerMap map = MapOf(mapId);
            p.mapId = mapId;
            p.pos = new Vector3(xz.x, CoreNet.GroundY, xz.y);
            p.mapReady = false;

            Send(p.conn, Cmd_Message.MAP_INFO, w =>
            {
                w.writeShort(mapId);
                w.writeByte(0);                // zone
                WriteVec(w, p.pos);
                w.writeByte(0);                // portal_type
                w.writeByte(map.players.Count);
                foreach (ServerPlayer o in map.players) WritePlayer(w, o);
                w.writeShort(map.monsters.Count);
                foreach (ServerMonster m in map.monsters)
                {
                    w.writeShort(m.id);
                    w.writeShort(m.templateId);
                    w.WriteLong(m.dead ? 0 : m.hp);
                    w.WriteLong(m.maxHp);
                    w.writeShort(m.level);
                    WriteVec(w, new Vector3(m.pos.x, CoreNet.GroundY, m.pos.y));
                }
            });

            foreach (ServerPlayer o in map.players)
            {
                Send(o.conn, Cmd_Message.ADD_PLAYER, w =>
                {
                    WritePlayer(w, p);
                    w.writeByte(0);            // portal_type
                });
                SendPlayerAnim(o.conn, p);     // ADD_PLAYER dựng nhân vật ngay nên tư thế theo sau được
            }
            AnnounceState(map, p);             // cấp / bang (ServerWorld.PlayerState)
            map.players.Add(p);
        }

        void LeaveMap(ServerPlayer p)
        {
            if (p.mapId < 0 || !maps.TryGetValue(p.mapId, out ServerMap map)) return;
            map.players.Remove(p);
            ReleaseSeat(map, p, broadcast: true);
            foreach (ServerMonster m in map.monsters) if (m.target == p) StartReturn(m);
            foreach (ServerPlayer o in map.players) Send(o.conn, Cmd_Message.LEAVE_MAP, w => w.writeInt(p.id));
            p.mapId = -1;
        }

        // khi client dựng xong map: NPC, bàn ghế / cổng (kèm chỗ đang có người ngồi), đồ đang nằm trên đất
        void MapReady(ServerPlayer p)
        {
            if (p.mapId < 0) return;
            ServerMap map = MapOf(p.mapId);
            p.mapReady = true;

            NpcDatabase npcs = settings.npcs;
            if (npcs != null)
                Send(p.conn, Cmd_Message.NPC, Cmd_Message.NpcSub.LIST, w =>
                {
                    w.writeShort(map.id);
                    int n = settings.HasNpcs(map.id) ? Mathf.Min(255, npcs.npcs.Count) : 0;   // NPC chỉ ở làng (npcMapId)
                    w.writeByte(n);
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 at = settings.NpcSpot(i, npcs.npcs.Count);
                        w.writeShort(npcs.npcs[i].id);
                        WriteFloat(w, at.x);
                        WriteFloat(w, at.y);
                        w.writeShort(settings.NpcFacing(at));
                    }
                });

            MapObjectDatabase objects = settings.mapObjects;
            if (objects != null)
            {
                var list = new List<MapObjectPlacement>();
                objects.PlacementsOf(map.id, list);
                Send(p.conn, Cmd_Message.MAP_OBJECT, w =>
                {
                    w.writeByte(MapObjectSystem.SubList);
                    w.writeShort(map.id);
                    w.writeShort(list.Count);
                    foreach (MapObjectPlacement o in list)
                    {
                        // như MapObjectDatabase.WritePlacements (cùng định dạng, khác lớp writer)
                        w.writeInt(o.uid);
                        w.writeShort(o.mapId);
                        w.writeShort(o.objectId);
                        WriteFloat(w, o.position.x);
                        WriteFloat(w, o.position.y);
                        w.writeShort(Mathf.RoundToInt(Mathf.Repeat(o.rotationY, 360f)));
                        w.writeShort(o.targetMapId);
                        w.writeInt(o.targetUid);
                    }
                });
                foreach (var seat in map.seats)
                    foreach (var point in seat.Value)
                        Send(p.conn, Cmd_Message.MAP_OBJECT, w =>
                        {
                            w.writeByte(MapObjectSystem.SubSit);
                            w.writeInt(point.Value);
                            w.writeInt(seat.Key);
                            w.writeByte(point.Key);
                        });
            }

            Send(p.conn, Cmd_Message.GROUND_ITEM, w =>
            {
                w.writeByte(GroundLoot.SubList);
                w.writeShort(map.items.Count);
                foreach (GroundItem it in map.items.Values) WriteItem(w, it);
            });

            // tư thế của những người đã ở sẵn trong map (MAP_INFO dựng họ sau khi nạp scene: gửi lúc này mới chắc có nhân vật)
            foreach (ServerPlayer o in map.players)
                if (o != p)
                {
                    SendPlayerAnim(p.conn, o);
                    SendPlayerState(p.conn, o);
                }
        }

        static void SendPlayerAnim(ServerConnection to, ServerPlayer o)
        {
            PlayerData d = o.data;
            if (d == null) return;
            int[] v = d.animVariants;
            Send(to, Cmd_Message.PLAYER, Cmd_Message.PlayerSub.ANIM, w =>
            {
                w.writeInt(o.id);
                w.writeShort(d.animSetId);
                w.writeShort(v != null ? v.Length : 0);
                if (v != null) foreach (int id in v) w.writeShort(id);
            });
        }

        // ================================================================ di chuyển / chat

        void Move(ServerPlayer p, MyReader r)
        {
            Vector3 pos = r.ReadVector3();
            int dir = r.ReadInt();
            byte action = r.ReadByte();
            byte subAct = r.ReadByte();
            sbyte count = r.ReadSByte();
            sbyte type = r.ReadSByte();
            p.pos = pos;
            p.dir = dir;
            p.action = action;
            p.subAct = subAct;

            if (maps.TryGetValue(p.mapId, out ServerMap map))
                foreach (ServerPlayer o in map.players)
                {
                    if (o == p) continue;
                    Send(o.conn, Cmd_Message.MOVE, w =>
                    {
                        w.writeInt(p.id);
                        WriteVec(w, pos);
                        w.writeInt(dir);
                        w.writeByte(action);
                        w.writeByte(subAct);
                        w.writeByte(count);
                        w.writeByte(type);
                    });
                }

            // mục tiêu "đến địa điểm" do server xét theo vị trí nhận được
            if (Now >= p.nextReachCheck)
            {
                p.nextReachCheck = Now + ReachInterval;
                toasts.Clear();
                using (CoreSystems.AsServer(p.profile, toasts))
                    Game.Story.QuestSystem.OnPlayerAt(p.mapId, new Vector2(pos.x, pos.z));
                ForwardToasts(p);
            }
        }

        void ChatMap(ServerPlayer p, string content)
        {
            if (!maps.TryGetValue(p.mapId, out ServerMap map)) return;
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.CHAT_MAP, w =>
                {
                    w.writeByte(1);            // cat 1: người chơi
                    w.writeInt(p.id);
                    w.writeUTF(content);
                });
        }

        void ChatWorld(ServerPlayer p, string content)
        {
            int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (ServerPlayer o in players.Values)
                Send(o.conn, Cmd_Message.CHAT_WORLD, w =>
                {
                    w.writeInt(p.id);
                    w.writeUTF(p.Name);
                    w.writeShort(0);           // avatar
                    w.writeUTF(content);
                    w.writeInt(time);
                });
        }

        // Chat_PM_To_ID ghi byte 0 + int id, Chat_PM_To_Name ghi byte 0 + UTF tên: phân biệt theo số byte còn lại
        void ChatPrivate(ServerPlayer p, MyReader r)
        {
            string content = r.ReadUTF();
            r.ReadByte();
            ServerPlayer to = null;
            if (r.Available() == 4)
                to = FindPlayer(r.ReadInt());   // cả bot (không đọc tin, người gửi vẫn thấy tin của mình)
            else
            {
                string name = r.ReadUTF();
                foreach (ServerPlayer o in players.Values) if (o.Name == name || o.account == name) to = o;
            }
            if (to == null)
            {
                SendToast(p.conn, "[LocalServer] Người nhận không online.");
                return;
            }
            int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (ServerPlayer target in new[] { to, p })
            {
                Send(target.conn, Cmd_Message.CHAT_PM, w =>
                {
                    w.writeInt(p.id);
                    w.writeUTF(content);
                    w.writeInt(time);
                });
                if (to == p) break;
            }
        }

        void PlayerInfo(ServerConnection conn, int id)
        {
            ServerPlayer o = FindPlayer(id);   // cả bot
            if (o == null) return;
            PlayerProfile pr = o.profile;
            Send(conn, Cmd_Message.PLAYER_INFO, w =>
            {
                w.writeInt(o.id);
                w.writeUTF(o.Name);
                w.writeShort(0);               // avatar
                // phần thêm cho popup thông tin người chơi (client đọc khi còn byte, nên khớp cả gói ngắn của server cũ)
                w.writeByte(o.conn == null ? 1 : 0);   // bot
                w.writeShort(pr.Progress.Level);
                w.WriteLong(pr.data.curHp);
                w.WriteLong(pr.data.maxHp);
                w.WriteLong(pr.data.curMp);
                w.WriteLong(pr.data.maxMp);
                w.writeShort(o.mapId);
                w.writeShort(pr.WeaponId);
                int count = 0;
                for (int s = 1; s <= Game.Items.Equipment.SlotCount; s++) if (pr.Equipment[(Game.Items.EquipSlot)s] > 0) count++;
                w.writeByte(count);
                for (int s = 1; s <= Game.Items.Equipment.SlotCount; s++)
                {
                    int item = pr.Equipment[(Game.Items.EquipSlot)s];
                    if (item <= 0) continue;
                    w.writeByte(s);
                    w.writeShort(item);
                }
            });
        }

        // ================================================================ cổng / ghế / đồ trên đất

        void Portal(ServerPlayer p, int uid)
        {
            MapObjectPlacement portal = settings.mapObjects != null ? settings.mapObjects.FindPlacement(p.mapId, uid) : null;
            if (portal == null || !portal.IsLinked)
            {
                SendToast(p.conn, "[LocalServer] Cổng chưa nối tới đâu.");
                return;
            }
            Vector2? arrive = MapObjectSystem.ArrivalAt(portal.targetMapId, portal.targetUid, out float facing);
            EnterMap(p, portal.targetMapId, arrive ?? settings.spawn, facing);
        }

        // bảng chọn map: map phải có template (client cũng kiểm bundle), đứng ở chỗ xuất hiện chung
        void Travel(ServerPlayer p, short mapId)
        {
            if (mapId == p.mapId) return;
            if (!Map_Template.List_Map_Template.ContainsKey(mapId))
            {
                SendToast(p.conn, "[LocalServer] Không có map " + mapId + ".");
                return;
            }
            EnterMap(p, mapId, settings.spawn, 0f);
        }

        // MapSub: dựng xong map, chọn map, qua cổng
        void HandleMap(ServerPlayer p, byte sub, MyReader r)
        {
            switch (sub)
            {
                case Cmd_Message.MapSub.READY:
                    MapReady(p);
                    break;
                case Cmd_Message.MapSub.TRAVEL:
                    short mapId = r.ReadShort();
                    if (!p.dead) Travel(p, mapId);
                    break;
                case Cmd_Message.MapSub.PORTAL:
                    Portal(p, r.ReadInt());
                    break;
                case Cmd_Message.MapSub.ZONE_LIST:
                    SendZones(p);
                    break;
                case Cmd_Message.MapSub.ZONE_CHANGE:
                    r.ReadByte();
                    SendZones(p);
                    break;
                default:
                    Debug.LogWarning("LocalServer: MAP sub " + sub + " chưa có.");
                    break;
            }
        }

        // server giả lập không chia khu: mỗi map đúng một khu
        void SendZones(ServerPlayer p)
        {
            int players = maps.TryGetValue(p.mapId, out ServerMap map) ? map.players.Count : 1;
            Send(p.conn, Cmd_Message.MAP, w =>
            {
                w.writeByte(Cmd_Message.MapSub.ZONE_LIST);
                w.writeByte(0);
                w.writeByte(50);
                w.writeByte(1);
                w.writeByte((byte)Mathf.Min(players, 255));
            });
        }

        // PlayerSub: bảng tư thế (người cùng map thấy tư thế mới)
        void HandlePlayer(ServerPlayer p, byte sub, MyReader r)
        {
            if (sub != Cmd_Message.PlayerSub.ANIM_SET)
            {
                Debug.LogWarning("LocalServer: PLAYER sub " + sub + " chưa có.");
                return;
            }
            if (p.SetAnimVariant(r.ReadByte(), r.ReadByte(), r.ReadShort()) && maps.TryGetValue(p.mapId, out ServerMap animMap))
                foreach (ServerPlayer o in animMap.players)
                    if (o != p) SendPlayerAnim(o.conn, p);
        }

        void MapObject(ServerPlayer p, MyReader r)
        {
            if (!maps.TryGetValue(p.mapId, out ServerMap map)) return;
            byte sub = r.ReadByte();
            if (sub == MapObjectSystem.SubSit)
            {
                int uid = r.ReadInt();
                byte point = r.ReadByte();
                MapObjectPlacement place = settings.mapObjects != null ? settings.mapObjects.FindPlacement(p.mapId, uid) : null;
                MapObjectTemplate t = place != null ? settings.mapObjects.Get(place.objectId) : null;
                bool free = t != null && point < t.seats
                            && (!map.seats.TryGetValue(uid, out var used) || !used.ContainsKey(point) || used[point] == p.id);
                if (!free)
                {
                    Send(p.conn, Cmd_Message.MAP_OBJECT, w =>
                    {
                        w.writeByte(MapObjectSystem.SubStand);
                        w.writeInt(p.id);
                    });
                    return;
                }
                ReleaseSeat(map, p, broadcast: false);
                if (!map.seats.TryGetValue(uid, out var points)) map.seats[uid] = points = new Dictionary<int, int>();
                points[point] = p.id;
                foreach (ServerPlayer o in map.players)
                    Send(o.conn, Cmd_Message.MAP_OBJECT, w =>
                    {
                        w.writeByte(MapObjectSystem.SubSit);
                        w.writeInt(p.id);
                        w.writeInt(uid);
                        w.writeByte(point);
                    });
            }
            else if (sub == MapObjectSystem.SubStand)
            {
                ReleaseSeat(map, p, broadcast: true);
            }
        }

        void ReleaseSeat(ServerMap map, ServerPlayer p, bool broadcast)
        {
            bool had = false;
            foreach (var seat in map.seats.Values)
            {
                int found = -1;
                foreach (var point in seat) if (point.Value == p.id) found = point.Key;
                if (found < 0) continue;
                seat.Remove(found);
                had = true;
            }
            if (!had || !broadcast) return;
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.MAP_OBJECT, w =>
                {
                    w.writeByte(MapObjectSystem.SubStand);
                    w.writeInt(p.id);
                });
            if (!map.players.Contains(p))
                Send(p.conn, Cmd_Message.MAP_OBJECT, w =>
                {
                    w.writeByte(MapObjectSystem.SubStand);
                    w.writeInt(p.id);
                });
        }

        void GroundItemMsg(ServerPlayer p, MyReader r)
        {
            byte sub = r.ReadByte();
            if (sub != GroundLoot.SubPick || !maps.TryGetValue(p.mapId, out ServerMap map)) return;
            int id = r.ReadInt();
            if (!map.items.TryGetValue(id, out GroundItem it)) return;
            if (it.owner != 0 && it.owner != p.id && Now < it.freeAt) return;   // chưa tới lượt: client tự thử lại sau

            int lost;
            using (CoreSystems.AsServer(p.profile, null)) lost = p.profile.Loot(it.itemId, it.count);
            if (lost >= it.count) return;   // túi đầy: để nguyên trên đất
            if (lost > 0)
            {
                // nhặt được một phần: phần còn lại vẫn nằm đó. Gói GROUND_ITEM không có "đổi số lượng": bỏ hình cũ rồi thêm lại
                // món đó (rơi tại chỗ, không bay từ chỗ quái chết) để mọi người thấy đúng số còn lại
                it.count = lost;
                it.from = new Vector3(it.pos.x, CoreNet.GroundY, it.pos.y);
                foreach (ServerPlayer o in map.players)
                {
                    Send(o.conn, Cmd_Message.GROUND_ITEM, w =>
                    {
                        w.writeByte(GroundLoot.SubRemove);
                        w.writeInt(id);
                        w.writeInt(0);
                    });
                    Send(o.conn, Cmd_Message.GROUND_ITEM, w =>
                    {
                        w.writeByte(GroundLoot.SubAdd);
                        WriteItem(w, it);
                    });
                }
                return;
            }
            map.items.Remove(id);
            foreach (ServerPlayer o in map.players)
                Send(o.conn, Cmd_Message.GROUND_ITEM, w =>
                {
                    w.writeByte(GroundLoot.SubRemove);
                    w.writeInt(id);
                    w.writeInt(p.id);
                });
        }

        void DropItems(ServerMap map, Vector2 at, List<Game.Items.DropRoll> rolls, int owner)
        {
            foreach (Game.Items.DropRoll roll in rolls)
            {
                Game.Items.ItemTemplate t = Game.Items.ItemDatabase.Current != null ? Game.Items.ItemDatabase.Current.Find(roll.item) : null;
                if (t == null) continue;
                Vector2 drop = at + UnityEngine.Random.insideUnitCircle * 1.2f;
                var it = new GroundItem
                {
                    id = nextItemId++, itemId = t.id, count = roll.count, owner = owner, pos = drop,
                    from = new Vector3(at.x, CoreNet.GroundY, at.y), freeAt = Now + LootOwnerSeconds, expireAt = Now + LootLifetime,
                };
                map.items[it.id] = it;
                foreach (ServerPlayer o in map.players)
                    Send(o.conn, Cmd_Message.GROUND_ITEM, w =>
                    {
                        w.writeByte(GroundLoot.SubAdd);
                        WriteItem(w, it);
                    });
            }
        }

        void WriteItem(MyWriter w, GroundItem it)
        {
            w.writeInt(it.id);
            w.writeShort(it.itemId);
            w.writeShort(it.count);
            WriteVec(w, it.from);
            WriteFloat(w, it.pos.x);
            WriteFloat(w, it.pos.y);
            w.writeInt(it.owner);
            w.writeShort(Mathf.Max(0, Mathf.RoundToInt(it.freeAt - Now)));
            w.writeShort(Mathf.Max(1, Mathf.RoundToInt(it.expireAt - Now)));
        }

        void TickItems(ServerMap map, float now)
        {
            List<int> expired = null;
            foreach (GroundItem it in map.items.Values)
                if (now >= it.expireAt) (expired ??= new List<int>()).Add(it.id);
            if (expired == null) return;
            foreach (int id in expired)
            {
                map.items.Remove(id);
                foreach (ServerPlayer o in map.players)
                    Send(o.conn, Cmd_Message.GROUND_ITEM, w =>
                    {
                        w.writeByte(GroundLoot.SubRemove);
                        w.writeInt(id);
                        w.writeInt(0);
                    });
            }
        }

        // ================================================================ gửi

        static void WritePlayer(MyWriter w, ServerPlayer o)
        {
            Game.Core.EntityData d = o.profile.data;
            w.writeInt(o.id);
            w.writeUTF(o.Name);
            w.WriteLong(d.curHp);
            w.WriteLong(d.maxHp);
            w.WriteLong(d.curMp);
            w.WriteLong(d.maxMp);
            WriteVec(w, o.pos);
        }

        static void WriteFloat(MyWriter w, float v) => w.writeInt(Mathf.RoundToInt(v * 100f));

        static void WriteVec(MyWriter w, Vector3 v)
        {
            WriteFloat(w, v.x);
            WriteFloat(w, v.y);
            WriteFloat(w, v.z);
        }

        static void WriteBlock(MyWriter w, byte[] bytes)
        {
            w.writeInt(bytes.Length);
            w.WriteBytes(bytes);
        }

        // lệnh mới: cmd cha + sub-command (Cmd_Message.*Sub), dữ liệu ghi tiếp theo sau
        static void Send(ServerConnection conn, short cmd, byte sub, Action<MyWriter> body) =>
            Send(conn, cmd, w =>
            {
                w.writeByte(sub);
                body(w);
            });

        static void Send(ServerConnection conn, short cmd, Action<MyWriter> body)
        {
            if (conn == null || conn.closed) return;
            var m = new Message(cmd);
            body(m.w);
            conn.Send(m);
        }

        static void SendToast(ServerConnection conn, string text, float seconds = 2.5f) =>
            Send(conn, Cmd_Message.NOTIFY, Cmd_Message.NotifySub.TOAST, w =>
            {
                w.writeUTF(text);
                w.writeShort(Mathf.RoundToInt(seconds * 10f));
            });

        // thông báo server sinh ra trong lúc xử lý (hạ quái, tới nơi...) chuyển về đúng người chơi
        void ForwardToasts(ServerPlayer p)
        {
            foreach (string t in toasts) SendToast(p.conn, t);
            toasts.Clear();
        }

        /// <summary>Gửi phần hồ sơ vừa đổi (và <paramref name="force"/>) về client. Gọi sau mỗi gói / sự kiện của người chơi.</summary>
        void Flush(ServerPlayer p, byte force)
        {
            byte parts = (byte)(p.dirty | force);
            if (parts == 0 || p.profile == null) return;
            p.dirty = 0;
            if (p.conn == null) return;   // bot: không có client nào cần hồ sơ
            byte[] block = ProfileSync.Write(p.profile, parts);
            Send(p.conn, Cmd_Message.PROFILE, Cmd_Message.ProfileSub.SYNC, w => WriteBlock(w, block));
        }
    }
}
