using System;
using System.Collections.Generic;
using System.Text;
using DungeonOnline.Entities;
using Game;
using Game.Core;
using Game.Players;
using NetKit;
using UnityEngine;

namespace DungeonOnline.Net
{
    /// <summary>
    /// The game half of the connection to ServerJava, after the data download: sends LOGIN and the local player's MOVE,
    /// and turns what the server sends into the scene (map, other players, monsters, the Core profile).
    /// Modelled on GameB's Service / GlobalMessageHandler / OnlineCore.
    /// </summary>
    /// <example>
    /// <code>
    /// session.LoggedIn += () => loginPanel.Hide();
    /// session.MapEntered += map => Debug.Log("in map " + map.id);
    /// session.Login("hero", "secret");
    /// </code>
    /// </example>
    /// <remarks>Single-thread only: NetKit runs every handler on Unity's main thread (NetConnection.Pump).</remarks>
    public sealed class OnlineSession : MonoBehaviour
    {
        [SerializeField] private Player _player;
        [SerializeField] private MapView _mapView;
        [SerializeField] private OtherPlayer _otherPlayerPrefab;
        [SerializeField] private Monster _monsterPrefab;
        [Tooltip("Seconds between MOVE packets while the local player moves.")]
        [SerializeField] private float _moveSendInterval = 0.1f;
        [Tooltip("Seconds between MOVE packets while standing still, so others see the player after joining.")]
        [SerializeField] private float _idleSendInterval = 1f;

        private readonly Dictionary<int, OtherPlayer> _others = new Dictionary<int, OtherPlayer>();
        private readonly Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        private NetConnection _net;
        private DataDownloader _data;
        private MapTable _maps = new MapTable();
        private float _sinceSend;
        private Vector2 _sentPos;
        private bool _sentFacingLeft;
        private EntityState _sentState;

        /// <summary>Server id of the local player; 0 before ME_INFO.</summary>
        public int MyId { get; private set; }

        /// <summary>The map the player is in, or null.</summary>
        public MapInfo CurrentMap { get; private set; }

        /// <summary>Maps from the downloaded template.</summary>
        public MapTable Maps => _maps;

        /// <summary>The server accepted LOGIN.</summary>
        public event Action LoggedIn;

        /// <summary>A message box text from the server (MENU), e.g. why a login was refused.</summary>
        public event Action<string> Message;

        /// <summary>The map is built and the player stands in it.</summary>
        public event Action<MapInfo> MapEntered;

        // ---------------- connection ----------------

        /// <summary>Starts listening on <paramref name="net"/>; call once before connecting.</summary>
        public void Attach(NetConnection net, DataDownloader data)
        {
            if (_net != null) return;
            _net = net;
            _data = data;
            data.TemplateReady += OnTemplate;
            net.On(GameCmd.LOGIN, OnLogin);
            net.On(GameCmd.MENU, OnMenu);
            net.On(GameCmd.NOTIFY, OnNotify);
            net.On(GameCmd.ME_INFO, OnMeInfo);
            net.On(GameCmd.PROFILE, OnProfile);
            net.On(GameCmd.MAP_INFO, OnMapInfo);
            net.On(GameCmd.ADD_PLAYER, OnAddPlayer);
            net.On(GameCmd.LEAVE_MAP, OnLeaveMap);
            net.On(GameCmd.MOVE, OnMove);
            net.On(GameCmd.MOB, OnMob);
        }

        /// <summary>Sends LOGIN. The server creates the account on a first login (no MySQL: kept in RAM only).</summary>
        public void Login(string name, string password)
        {
            if (_net == null || !_net.IsConnected) return;
            _net.Send(GameCmd.LOGIN, w =>
            {
                w.WriteUTF(name);
                w.WriteUTF(password);
            });
        }

        /// <summary>Forgets the map and everyone in it, e.g. after a disconnect.</summary>
        public void ResetWorld()
        {
            ClearEntities();
            CurrentMap = null;
            MyId = 0;
        }

        /// <summary>Builds <paramref name="map"/> and puts the player at <paramref name="position"/>; also used offline by tests.</summary>
        public void EnterMap(MapInfo map, Vector2 position)
        {
            ClearEntities();
            CurrentMap = map;
            if (_mapView != null) _mapView.Build(map);
            if (_player != null)
            {
                _player.EnterMap(position, map != null ? map.fallLimitY : position.y - 10f);
                _player.SetControllable(true);
                _sentPos = position;
            }
            _sinceSend = 0f;
            MapEntered?.Invoke(map);
        }

        private void Update()
        {
            if (CurrentMap == null || _player == null || _net == null || !_net.IsConnected) return;
            _sinceSend += Time.deltaTime;
            Vector2 pos = _player.transform.position;
            bool changed = (pos - _sentPos).sqrMagnitude > 0.0001f || _player.FacingLeft != _sentFacingLeft
                           || _player.State != _sentState;
            if (_sinceSend < (changed ? _moveSendInterval : _idleSendInterval)) return;
            _sinceSend = 0f;
            _sentPos = pos;
            _sentFacingLeft = _player.FacingLeft;
            _sentState = _player.State;
            _net.Send(GameCmd.MOVE, w => ServerCodec.WriteMove(w, pos, _sentFacingLeft, _sentState));
        }

        // NetClient outlives the scene: drop every hook into it
        private void OnDestroy()
        {
            if (_data != null) _data.TemplateReady -= OnTemplate;
            if (_net == null) return;
            foreach (int cmd in new[] { GameCmd.LOGIN, GameCmd.MENU, GameCmd.NOTIFY, GameCmd.ME_INFO, GameCmd.PROFILE,
                                        GameCmd.MAP_INFO, GameCmd.ADD_PLAYER, GameCmd.LEAVE_MAP, GameCmd.MOVE, GameCmd.MOB })
                _net.Off(cmd);
        }

        // ---------------- server -> client ----------------

        private void OnTemplate(int type, byte[] data)
        {
            if (type == MapTable.TemplateType) _maps = MapTable.Parse(data);
        }

        // LOGIN: byte (1 = accepted). Refusals come as MENU.
        private void OnLogin(PacketReader r) => LoggedIn?.Invoke();

        // MENU: short icon, UTF text, byte n { UTF button }
        private void OnMenu(PacketReader r)
        {
            r.ReadShort();
            Message?.Invoke(r.ReadUTF());
        }

        // NOTIFY TOAST: UTF text, short tenths of a second
        private void OnNotify(PacketReader r)
        {
            if (r.ReadByte() == GameCmd.NOTIFY_TOAST) Message?.Invoke(r.ReadUTF());
        }

        // ME_INFO: int id, UTF name, long hp, maxHp, mp, maxMp, coin, gold
        private void OnMeInfo(PacketReader r)
        {
            MyId = r.ReadInt();
        }

        // PROFILE: byte sub, int length, UTF-8 JSON in PlayerData's format
        private void OnProfile(PacketReader r)
        {
            int sub = r.ReadByte();
            if (sub != GameCmd.PROFILE_DATA && sub != GameCmd.PROFILE_SYNC) return;
            string json = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt()));
            var data = ScriptableObject.CreateInstance<PlayerData>();
            data.hideFlags = HideFlags.DontSave;
            JsonUtility.FromJsonOverwrite(json, data);
            CoreSystems.StartOnline(data);
            Destroy(data);   // copied into the PlayerProfile
        }

        // MAP_INFO: short map, byte zone, vec me, byte portal, byte n { player }, short m { mob }
        private void OnMapInfo(PacketReader r)
        {
            int mapId = r.ReadShort();
            r.ReadByte();   // zone
            Vector2 me = ServerCodec.ReadPos(r);
            r.ReadByte();   // portal type
            MapInfo map = _maps.Find(mapId);
            if (map == null) Debug.LogWarning($"OnlineSession: map {mapId} is not in the downloaded maps template", this);
            EnterMap(map, me);

            int players = r.ReadByte();
            for (int i = 0; i < players; i++) SpawnOther(r);
            int mobs = r.ReadShort();
            for (int i = 0; i < mobs; i++) SpawnMonster(r);
        }

        // ADD_PLAYER: player, byte portal
        private void OnAddPlayer(PacketReader r) => SpawnOther(r);

        // LEAVE_MAP: int id
        private void OnLeaveMap(PacketReader r)
        {
            int id = r.ReadInt();
            if (_others.TryGetValue(id, out OtherPlayer other))
            {
                _others.Remove(id);
                if (other != null) Destroy(other.gameObject);
            }
        }

        // MOVE: int id, vec, int dir, byte state, byte action, byte count, byte type
        private void OnMove(PacketReader r)
        {
            int id = r.ReadInt();
            Vector2 pos = ServerCodec.ReadPos(r);
            int dir = r.ReadInt();
            int state = r.ReadByte(), action = r.ReadByte();
            if (_others.TryGetValue(id, out OtherPlayer other) && other != null)
                other.PushMove(pos, ServerCodec.IsFacingLeft(dir), ServerCodec.ToEntityState(state, action));
        }

        // MOB: byte sub, short id, then by sub
        private void OnMob(PacketReader r)
        {
            int sub = r.ReadByte();
            int id = r.ReadShort();
            if (!_monsters.TryGetValue(id, out Monster mob) || mob == null) return;
            switch (sub)
            {
                case GameCmd.MOB_MOVE:
                case GameCmd.MOB_STOP:
                {
                    Vector2 to = ServerCodec.ReadPos(r);
                    bool left = to.x < mob.transform.position.x;
                    mob.PushMove(to, left, sub == GameCmd.MOB_MOVE ? EntityState.Run : EntityState.Idle);
                    break;
                }
                case GameCmd.MOB_HP:
                    mob.UpdateStats(r.ReadLong(), r.ReadLong(), 0, 0);
                    break;
                case GameCmd.MOB_DIE:
                    mob.Die();
                    break;
                case GameCmd.MOB_RESPAWN:
                {
                    long hp = r.ReadLong(), maxHp = r.ReadLong();
                    mob.Respawn(ServerCodec.ReadPos(r));
                    mob.UpdateStats(hp, maxHp, 0, 0);
                    break;
                }
            }
        }

        // player = int id, UTF name, long hp, maxHp, mp, maxMp, vec
        private void SpawnOther(PacketReader r)
        {
            int id = r.ReadInt();
            string name = r.ReadUTF();
            long hp = r.ReadLong(), maxHp = r.ReadLong(), mp = r.ReadLong(), maxMp = r.ReadLong();
            Vector2 pos = ServerCodec.ReadPos(r);
            if (id == MyId || _otherPlayerPrefab == null) return;
            if (_others.TryGetValue(id, out OtherPlayer old) && old != null) Destroy(old.gameObject);
            OtherPlayer other = Instantiate(_otherPlayerPrefab, pos, Quaternion.identity);
            other.SetInfo(new EntityData(id, name, hp, maxHp, mp, maxMp));
            _others[id] = other;
        }

        // mob = short id, short template, long hp, long maxHp, short level, vec
        private void SpawnMonster(PacketReader r)
        {
            int id = r.ReadShort(), template = r.ReadShort();
            long hp = r.ReadLong(), maxHp = r.ReadLong();
            r.ReadShort();  // level
            Vector2 pos = ServerCodec.ReadPos(r);
            if (_monsterPrefab == null) return;
            Monster mob = Instantiate(_monsterPrefab, pos, Quaternion.identity);
            mob.SetTemplate(template);
            mob.SetInfo(new EntityData(id, "Monster " + template, hp, maxHp, 0, 0));
            _monsters[id] = mob;
        }

        private void ClearEntities()
        {
            foreach (OtherPlayer o in _others.Values) if (o != null) Destroy(o.gameObject);
            foreach (Monster m in _monsters.Values) if (m != null) Destroy(m.gameObject);
            _others.Clear();
            _monsters.Clear();
        }
    }
}
