using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Game.ServerSim
{
    /// <summary>
    /// Server giả lập chạy ngay trong Editor (cùng process với client), để thử luồng online mà không cần server thật:
    /// client kết nối thật qua TCP tới 127.0.0.1:<see cref="Port"/>, gói tin đi đúng khung và định dạng như với server thật.
    /// <list type="bullet">
    /// <item>Bật: menu Tools/Local Server/Enable (cờ <see cref="Enabled"/> trong PlayerPrefs, tắt Local Play). Lần Play sau
    /// LoadingScene tự chọn server 127.0.0.1, GameController.DoStartIE mở server rồi kết nối.</item>
    /// <item>Thread nhận kết nối + mỗi client một thread đọc; mọi luật chạy trên main thread (<see cref="Update"/>) trong
    /// <see cref="ServerWorld"/>, nên dùng chung được ScriptableObject và code Core của client.</item>
    /// <item>Hồ sơ người chơi lưu ở persistentDataPath/local_server/&lt;tài khoản&gt;.json (không kiểm mật khẩu).</item>
    /// <item>Nghe trên mọi địa chỉ: máy khác cùng mạng LAN (bản build, kể cả release) nối vào bằng IP LAN của máy chạy server
    /// (in ra Console khi mở, <see cref="LanAddresses"/>), nhập ở ô "IP server" của LoadingScene.</item>
    /// </list>
    /// Bản build vẫn có lớp này nhưng không tự bật (chỉ Editor đọc cờ).
    /// </summary>
    public class LocalServer : MonoBehaviour
    {
        /// <summary>Cổng nghe: trùng mục "local_127" (127.0.0.1:18888) có sẵn trong ServerInfo.</summary>
        public const int Port = 18888;
        public const string Host = "127.0.0.1";
        const string PrefKey = "LocalServer";
        const int MaxPerFrame = 500;

        public static LocalServer Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            Selected = false;
        }

        /// <summary>Lần chọn này dùng server giả lập (màn chọn server ở LoadingScene). Chỉ giữ trong phiên chạy, chạy được cả bản build.</summary>
        public static bool Selected { get; set; }

        /// <summary>Phiên này kết nối tới server giả lập: bật từ menu Editor (<see cref="Enabled"/>) hoặc chọn ở màn chọn server.</summary>
        public static bool Active => Enabled || Selected;

        /// <summary>Lần chạy sau dùng server giả lập (chỉ có tác dụng trong Editor). Lưu trong PlayerPrefs.</summary>
        public static bool Enabled
        {
#if UNITY_EDITOR
            get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
#else
            get => false;
#endif
            set
            {
                if (value) PlayerPrefs.SetInt(PrefKey, 1);
                else PlayerPrefs.DeleteKey(PrefKey);
                PlayerPrefs.Save();
            }
        }

        TcpListener listener;
        Thread acceptThread;
        volatile bool stopping;
        int nextConnId = 1;
        readonly ConcurrentQueue<Incoming> inbox = new ConcurrentQueue<Incoming>();
        readonly List<ServerConnection> connections = new List<ServerConnection>();
        readonly object connLock = new object();
        ServerWorld world;

        public ServerWorld World => world;

        /// <summary>Mở server nếu chưa mở. false khi không mở được cổng (đã có server khác chiếm).</summary>
        public static bool EnsureRunning()
        {
            if (Instance != null) return true;
            var go = new GameObject("[LocalServer]");
            DontDestroyOnLoad(go);
            var server = go.AddComponent<LocalServer>();
            if (server.StartListening()) return true;
            Destroy(go);
            return false;
        }

        /// <summary>Chọn 127.0.0.1:Port trong danh sách server cho lần kết nối này (không ghi lựa chọn vào máy).</summary>
        public static void SelectServer()
        {
            int index = ServerInfo.ipInfo.FindIndex(s => s != null && s.ip == Host && s.port == Port);
            if (index < 0)
            {
                ServerInfo.ipInfo.Add(new ServerInfo { id = (short)ServerInfo.ipInfo.Count, name = "LocalServer", ip = Host, port = Port });
                index = ServerInfo.ipInfo.Count - 1;
            }
            ServerInfo.ipInfo[index].id = (short)index;   // GetIpInfo tra theo id, Connect theo vị trí: cho trùng nhau
            ServerInfo.SetIpSelect(index, false);
        }

        bool StartListening()
        {
            var settings = Resources.Load<LocalPlaySettings>("LocalPlaySettings");
            if (settings == null)
            {
                Debug.LogWarning("LocalServer: chưa có Resources/LocalPlaySettings, dùng cấu hình mặc định (không có NPC / nhiệm vụ).");
                settings = ScriptableObject.CreateInstance<LocalPlaySettings>();
            }
            try
            {
                listener = new TcpListener(IPAddress.Any, Port);   // máy khác trong LAN nối vào được (Windows có thể hỏi Firewall)
                NoInherit(listener.Server);
                listener.Start();
            }
            catch (Exception e)
            {
                Debug.LogError($"LocalServer: không mở được cổng {Port} ({e.Message}). Có server khác đang chạy?");
                listener = null;
                return false;
            }
            // cùng bảng với client (đăng ký trùng thì bỏ qua): server tra NPC, nhiệm vụ, vật phẩm... qua X.Current
            CoreSystems.Use(settings.items, settings.skills, settings.weapons, settings.monsterData, settings.upgradeTree,
                              settings.options, settings.npcs, settings.quests, settings.drops, settings.gearUpgrades);
            Game.Map.MapObjectDatabase.Use(settings.mapObjects);
            Instance = this;
            world = new ServerWorld(settings);
            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "LocalServer Accept" };
            acceptThread.Start();
            string lan = string.Join(", ", LanAddresses());
            Debug.Log($"LocalServer: đang nghe cổng {Port}. Máy này: {Host}:{Port}; máy khác trong LAN nhập IP: "
                      + (lan.Length > 0 ? lan : "(không tìm thấy IP LAN)") + $". Hồ sơ lưu ở {ServerPlayer.Folder}");
            return true;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

        const uint HandleFlagInherit = 1;
#endif

        /// <summary>
        /// Không cho tiến trình con kế thừa socket. Trên Windows socket mặc định kế thừa được: Editor sinh tiến trình con trong
        /// lúc server chạy (đã gặp: relay_win.exe của unity-mcp) thì con giữ một bản handle, Stop xong cổng vẫn LISTENING tới khi
        /// con thoát; lần Play sau không mở được cổng, client nối vào socket không ai xử lý nên đăng nhập treo.
        /// </summary>
        static void NoInherit(Socket socket)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!SetHandleInformation(socket.Handle, HandleFlagInherit, 0))
                Debug.LogWarning($"LocalServer: không tắt được kế thừa socket (lỗi {Marshal.GetLastWin32Error()}).");
#endif
        }

        /// <summary>IPv4 LAN của máy này (card mạng đang chạy, bỏ loopback), dạng "ip:cổng" để máy khác nhập.</summary>
        public static List<string> LanAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation addr in nic.GetIPProperties().UnicastAddresses)
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                            list.Add($"{addr.Address}:{Port}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LocalServer: không đọc được card mạng ({e.Message}).");
            }
            return list;
        }

        void AcceptLoop()
        {
            while (!stopping)
            {
                try
                {
                    // hỏi Pending thay vì nằm chờ trong AcceptTcpClient, để lúc tắt không còn thread kẹt trong socket nghe
                    if (!listener.Pending())
                    {
                        Thread.Sleep(20);
                        continue;
                    }
                    TcpClient client = listener.AcceptTcpClient();
                    NoInherit(client.Client);
                    ServerConnection conn;
                    lock (connLock)
                    {
                        conn = new ServerConnection(nextConnId++, client, inbox);
                        connections.Add(conn);
                    }
                    conn.Start();
                }
                catch (Exception)
                {
                    if (stopping) return;
                    Thread.Sleep(100);   // lỗi lặp lại (cổng hỏng...): đừng quay vòng ăn hết một nhân CPU
                }
            }
        }

        void Update()
        {
            if (world == null) return;
            for (int i = 0; i < MaxPerFrame && inbox.TryDequeue(out Incoming msg); i++)
            {
                world.Handle(msg);
                if (msg.disconnect)
                    lock (connLock) connections.Remove(msg.conn);
            }
            world.Tick();
        }

        void OnDestroy() => Stop();

        void OnApplicationQuit() => Stop();

        void Stop()
        {
            if (stopping) return;
            stopping = true;
            // đóng cổng trước: lưu hồ sơ (Shutdown) có lỗi cũng không để cổng bị giữ
            acceptThread?.Join(500);   // chờ vòng nhận thoát rồi mới đóng socket nghe
            try { listener?.Stop(); }
            catch (Exception e) { Debug.LogWarning($"LocalServer: đóng cổng {Port} lỗi ({e.Message})."); }
            try { world?.Shutdown(); }
            catch (Exception e) { Debug.LogException(e); }
            lock (connLock)
            {
                foreach (ServerConnection c in connections) c.Close();
                connections.Clear();
            }
            if (Instance == this) Instance = null;
            Debug.Log("LocalServer: đã tắt.");
        }
    }
}
