using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NetKit.Samples
{
    /// <summary>
    /// Demo chạy trên một scene trống: thêm component này vào một GameObject rồi Play (server NetKit đang chạy). Đi qua đủ
    /// luồng: kết nối → tải dữ liệu → đăng nhập → vào map, thấy người khác, đi lại (WASD), chat, đổi khu / map, mua đồ theo
    /// kiểu client làm trước. UI vẽ bằng OnGUI để không cần prefab; game thật thay bằng UI của mình, giữ phần mạng.
    /// </summary>
    public class DemoGame : MonoBehaviour, IShopRemote
    {
        public string host = "127.0.0.1";
        public int port = 19000;
        public int clientVersion = 1;
        public float moveSpeed = 5f;
        public float sendEvery = 0.1f;      // giây giữa hai gói MOVE khi đang đi

        const int TemplateNews = 0, TemplateShop = 1;

        enum Phase { Offline, Connecting, Downloading, Login, LoggingIn, Playing }

        class Avatar
        {
            public Transform body;
            public Vector3 target;
            public string name;
        }

        Phase phase;
        NetClient client;
        NetConnection net;
        string user = "", pass = "", chatInput = "", status = "", news = "", zoneText = "";
        bool worldChat;
        ShopTable shop = new ShopTable();
        readonly DemoProfile profile = new DemoProfile();

        int myId = -1, mapId = -1, zone;
        string myName = "";
        Avatar me;
        Vector3 lastSent;
        float sendTimer;
        readonly Dictionary<int, Avatar> others = new Dictionary<int, Avatar>();
        readonly List<string> chat = new List<string>();
        readonly List<KeyValuePair<string, float>> toasts = new List<KeyValuePair<string, float>>();
        readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        Camera cam;

        // ---------------- vòng đời ----------------

        void Start()
        {
            client = NetClient.Get();
            net = client.Net;
            net.Connected += OnConnected;
            net.ConnectFailed += OnConnectFailed;
            net.Disconnected += OnDisconnected;
            client.Data.TemplateReady += OnTemplate;
            client.Data.Done += OnDataDone;

            net.On(Cmd.LOGIN, OnLogin);
            net.On(Cmd.ME_INFO, OnMeInfo);
            net.On(Cmd.PROFILE, OnProfile);
            net.On(Cmd.MAP_INFO, OnMapInfo);
            net.On(Cmd.ADD_PLAYER, r => AddOther(r));
            net.On(Cmd.LEAVE_MAP, OnLeave);
            net.On(Cmd.MOVE, OnMove);
            net.On(Cmd.CHAT_MAP, r => OnChat(r, "Map"));
            net.On(Cmd.CHAT_WORLD, r => OnChat(r, "Thế giới"));
            net.On(Cmd.MAP, OnMapSub);
            net.On(Cmd.NOTIFY, OnNotify);

            cam = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(5, 1, 5);
        }

        void OnDestroy()
        {
            if (client == null) return;
            net.Connected -= OnConnected;
            net.ConnectFailed -= OnConnectFailed;
            net.Disconnected -= OnDisconnected;
            client.Data.TemplateReady -= OnTemplate;
            client.Data.Done -= OnDataDone;
            foreach (int cmd in new[] { Cmd.LOGIN, Cmd.ME_INFO, Cmd.PROFILE, Cmd.MAP_INFO, Cmd.ADD_PLAYER, Cmd.LEAVE_MAP, Cmd.MOVE,
                         Cmd.CHAT_MAP, Cmd.CHAT_WORLD, Cmd.MAP, Cmd.NOTIFY })
                net.Off(cmd);
            if (ReferenceEquals(NetSync<IShopRemote>.Remote, this)) NetSync<IShopRemote>.Remote = null;
        }

        void Update()
        {
            foreach (Avatar a in others.Values)
                a.body.position = Vector3.Lerp(a.body.position, a.target, 1 - Mathf.Exp(-10 * Time.deltaTime));
            toasts.RemoveAll(t => t.Value < Time.time);
            if (me == null) return;
            Move();
            cam.transform.position = me.body.position + new Vector3(0, 12, -10);
            cam.transform.LookAt(me.body.position);
        }

        // đi bằng WASD, gửi MOVE tối đa mỗi sendEvery giây khi vị trí đổi
        void Move()
        {
            Vector3 dir = Vector3.zero;
            if (held.Contains(KeyCode.W)) dir.z += 1;
            if (held.Contains(KeyCode.S)) dir.z -= 1;
            if (held.Contains(KeyCode.A)) dir.x -= 1;
            if (held.Contains(KeyCode.D)) dir.x += 1;
            if (dir != Vector3.zero)
            {
                me.body.position += dir.normalized * moveSpeed * Time.deltaTime;
                me.body.rotation = Quaternion.LookRotation(dir);
            }
            sendTimer -= Time.deltaTime;
            if (sendTimer > 0 || (me.body.position - lastSent).sqrMagnitude < 0.0001f) return;
            sendTimer = sendEvery;
            lastSent = me.body.position;
            net.Send(Cmd.MOVE, w =>
            {
                w.WriteVec3(lastSent);
                w.WriteInt(Mathf.RoundToInt(me.body.eulerAngles.y * 100));
            });
        }

        // ---------------- kết nối, tải dữ liệu ----------------

        void OnConnected()
        {
            phase = Phase.Downloading;
            client.Data.Begin(NetClient.DeviceType, clientVersion);
        }

        void OnConnectFailed() => SetOffline("Không kết nối được " + host + ":" + port);

        void OnDisconnected() => SetOffline("Mất kết nối");

        void SetOffline(string why)
        {
            phase = Phase.Offline;
            status = why;
            client.Data.Reset();
            NetSync<IShopRemote>.Remote = null;
            ClearMap();
            if (me != null) Destroy(me.body.gameObject);
            me = null;
        }

        void OnTemplate(int type, byte[] data)
        {
            if (type == TemplateNews) news = Encoding.UTF8.GetString(data);
            else if (type == TemplateShop) shop = JsonUtility.FromJson<ShopTable>(Encoding.UTF8.GetString(data));
        }

        void OnDataDone()
        {
            phase = Phase.Login;
            status = "Dữ liệu đủ: " + client.Data.ServerFiles.Count + " file, " + client.Data.Store.Root;
        }

        // ---------------- đăng nhập, hồ sơ ----------------

        void OnLogin(PacketReader r)
        {
            bool ok = r.ReadBool();
            string why = r.ReadUTF();
            if (ok)
            {
                phase = Phase.Playing;
                NetSync<IShopRemote>.Remote = this;
            }
            else
            {
                phase = Phase.Login;
                status = why;
            }
        }

        void OnMeInfo(PacketReader r)
        {
            myId = r.ReadInt();
            myName = r.ReadUTF();
        }

        // DATA lúc vào game và SYNC sau mỗi lệnh hồ sơ đều ghi đè cả hồ sơ
        void OnProfile(PacketReader r)
        {
            r.ReadByte();   // ProfileSub: DATA hoặc SYNC, demo xử lý như nhau
            string json = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt()));
            using (NetSync.ApplyingServer())
                JsonUtility.FromJsonOverwrite(json, profile);
        }

        void IShopRemote.Buy(string item, int count) =>
            net.Send(Cmd.SHOP, Cmd.ShopSub.BUY, w =>
            {
                w.WriteUTF(item);
                w.WriteShort(count);
            });

        // ---------------- map ----------------

        void OnMapInfo(PacketReader r)
        {
            ClearMap();
            mapId = r.ReadShort();
            zone = r.ReadByte();
            Vector3 pos = r.ReadVec3();
            if (me == null) me = NewAvatar(myName, Color.green);
            me.body.position = me.target = lastSent = pos;
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) AddOther(r);
            zoneText = "";
        }

        void AddOther(PacketReader r)
        {
            int id = r.ReadInt();
            string name = r.ReadUTF();
            Vector3 pos = r.ReadVec3();
            if (id == myId) return;
            if (!others.TryGetValue(id, out Avatar a)) others[id] = a = NewAvatar(name, Color.white);
            a.body.position = a.target = pos;
        }

        void OnLeave(PacketReader r)
        {
            int id = r.ReadInt();
            if (!others.TryGetValue(id, out Avatar a)) return;
            Destroy(a.body.gameObject);
            others.Remove(id);
        }

        void OnMove(PacketReader r)
        {
            int id = r.ReadInt();
            Vector3 pos = r.ReadVec3();
            float dir = r.ReadInt() / 100f;
            if (!others.TryGetValue(id, out Avatar a)) return;
            a.target = pos;
            a.body.rotation = Quaternion.Euler(0, dir, 0);
        }

        void OnMapSub(PacketReader r)
        {
            if (r.ReadByte() != Cmd.MapSub.ZONE_LIST) return;
            int current = r.ReadByte(), capacity = r.ReadByte(), n = r.ReadByte();
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++) sb.Append(i == current ? "[" : " ").Append(i).Append(':').Append(r.ReadByte()).Append('/')
                .Append(capacity).Append(i == current ? "]" : " ");
            zoneText = sb.ToString();
        }

        void ClearMap()
        {
            foreach (Avatar a in others.Values) Destroy(a.body.gameObject);
            others.Clear();
        }

        Avatar NewAvatar(string name, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(0.8f, 1.6f, 0.8f);
            go.GetComponent<Renderer>().material.color = color;
            return new Avatar { body = go.transform, name = name };
        }

        // ---------------- chat, thông báo ----------------

        void OnChat(PacketReader r, string channel)
        {
            r.ReadInt();
            string name = r.ReadUTF(), text = r.ReadUTF();
            chat.Add("[" + channel + "] " + name + ": " + text);
            if (chat.Count > 8) chat.RemoveAt(0);
        }

        void OnNotify(PacketReader r)
        {
            if (r.ReadByte() != Cmd.NotifySub.TOAST) return;
            string text = r.ReadUTF();
            toasts.Add(new KeyValuePair<string, float>(text, Time.time + r.ReadShort() / 10f));
        }

        // ---------------- UI ----------------

        void OnGUI()
        {
            TrackKeys();
            GUILayout.BeginArea(new Rect(10, 10, 360, Screen.height - 20), GUI.skin.box);
            switch (phase)
            {
                case Phase.Offline: DrawOffline(); break;
                case Phase.Connecting: GUILayout.Label("Đang kết nối " + host + ":" + port + "..."); break;
                case Phase.Downloading:
                    GUILayout.Label("Đang tải dữ liệu " + Mathf.RoundToInt(client.Data.Progress * 100) + "%");
                    break;
                case Phase.Login: DrawLogin(); break;
                case Phase.LoggingIn: GUILayout.Label("Đang đăng nhập..."); break;
                case Phase.Playing: DrawPlaying(); break;
            }
            foreach (var t in toasts) GUILayout.Label("» " + t.Key);
            GUILayout.EndArea();
            if (phase == Phase.Playing) DrawNames();
        }

        // phím giữ, đọc từ sự kiện IMGUI: chạy được cả với Input System lẫn Input cũ. Ô nhập đang focus thì không đi
        void TrackKeys()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0) held.Add(e.keyCode);
            else if (e.type == EventType.KeyUp) held.Remove(e.keyCode);
        }

        void DrawOffline()
        {
            GUILayout.Label("Máy chủ");
            host = GUILayout.TextField(host);
            int.TryParse(GUILayout.TextField(port.ToString()), out port);
            if (GUILayout.Button("Kết nối"))
            {
                phase = Phase.Connecting;
                status = "";
                net.Connect(host, port);
            }
            GUILayout.Label(status);
        }

        void DrawLogin()
        {
            GUILayout.Label(news);
            GUILayout.Label("Tên (chưa có thì server tự tạo tài khoản)");
            user = GUILayout.TextField(user, 24);
            GUILayout.Label("Mật khẩu");
            pass = GUILayout.PasswordField(pass, '*', 64);
            if (GUILayout.Button("Đăng nhập"))
            {
                phase = Phase.LoggingIn;
                net.Send(Cmd.LOGIN, w =>
                {
                    w.WriteUTF(user);
                    w.WriteUTF(pass);
                });
            }
            GUILayout.Label(status);
        }

        void DrawPlaying()
        {
            GUILayout.Label(myName + " (id " + myId + ")  map " + mapId + "  khu " + zone + "  ping " + net.PingMs + " ms");
            GUILayout.Label("Vàng " + profile.gold + "   potion " + profile.CountOf("potion") + "   elixir " + profile.CountOf("elixir"));
            GUILayout.BeginHorizontal();
            foreach (ShopItem item in shop.items)
                if (GUILayout.Button("Mua " + item.id + " (" + item.price + ")"))
                {
                    string why = profile.Buy(shop, item.id, 1);
                    if (why != null) toasts.Add(new KeyValuePair<string, float>(why, Time.time + 2.5f));
                }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Danh sách khu")) net.Send(Cmd.MAP, Cmd.MapSub.ZONE_LIST);
            if (GUILayout.Button("Khu kế")) net.Send(Cmd.MAP, Cmd.MapSub.ZONE_CHANGE, w => w.WriteByte(zone + 1));
            if (GUILayout.Button("Sang map " + (mapId == 0 ? 1 : 0)))
                net.Send(Cmd.MAP, Cmd.MapSub.TRAVEL, w => w.WriteShort(mapId == 0 ? 1 : 0));
            GUILayout.EndHorizontal();
            if (zoneText.Length > 0) GUILayout.Label(zoneText);

            GUILayout.Space(8);
            foreach (string line in chat) GUILayout.Label(line);
            GUILayout.BeginHorizontal();
            worldChat = GUILayout.Toggle(worldChat, "Thế giới", GUILayout.Width(80));
            chatInput = GUILayout.TextField(chatInput, 300);
            if (GUILayout.Button("Gửi", GUILayout.Width(50)) && chatInput.Length > 0)
            {
                string text = chatInput;
                net.Send(worldChat ? Cmd.CHAT_WORLD : Cmd.CHAT_MAP, w => w.WriteUTF(text));
                chatInput = "";
                GUIUtility.keyboardControl = 0;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("WASD để đi (bấm ra ngoài ô chat trước)");
        }

        void DrawNames()
        {
            if (me != null) DrawName(me);
            foreach (Avatar a in others.Values) DrawName(a);
        }

        void DrawName(Avatar a)
        {
            Vector3 p = cam.WorldToScreenPoint(a.body.position + Vector3.up * 1.2f);
            if (p.z <= 0) return;
            GUI.Label(new Rect(p.x - 60, Screen.height - p.y - 10, 120, 20), a.name);
        }
    }
}
