using DungeonOnline.Entities;
using DungeonOnline.UI;
using Game.UI;
using NetKit;
using UnityEngine;

namespace DungeonOnline.Net
{
    /// <summary>
    /// The game's start-up in the single Main scene: Loading (pick server, download data) → Login → Playing.
    /// Shows one panel at a time over the GameCanvas and keeps the player frozen until the server puts it in a map.
    /// Follows GameB's LoadingScene → LoginScene → GameScene, without separate scenes.
    /// </summary>
    /// <example>
    /// Tests and tools skip the server:
    /// <code>
    /// FindFirstObjectByType&lt;GameFlow&gt;().PlayOffline(map, map.spawn.ToVector());
    /// </code>
    /// </example>
    public sealed class GameFlow : MonoBehaviour
    {
        /// <summary>Where the start-up is.</summary>
        public enum Step
        {
            Loading,
            Login,
            Playing,
        }

        [SerializeField] private OnlineSession _session;
        [SerializeField] private ServerList _servers;
        [SerializeField] private Player _player;
        [Tooltip("Client version sent in CLIENT_INFO.")]
        [SerializeField] private int _clientVersion = 1;

        private NetClient _client;
        private LoadingPanel _loading;
        private LoginPanel _login;
        private bool _downloading;

        /// <summary>Current step.</summary>
        public Step Current { get; private set; } = Step.Loading;

        private void Start()
        {
            if (_player != null) _player.SetControllable(false);
            Transform parent = PanelParent();
            _loading = LoadingPanel.Create(parent, _servers);
            _login = LoginPanel.Create(parent);
            _loading.StartPressed += Connect;
            _login.LoginPressed += _session.Login;

            _client = NetClient.Get();
            _session.Attach(_client.Net, _client.Data);
            _client.Net.Connected += OnConnected;
            _client.Net.ConnectFailed += OnConnectFailed;
            _client.Net.Disconnected += OnDisconnected;
            _client.Data.Done += OnDataDone;
            _session.LoggedIn += OnLoggedIn;
            _session.Message += OnServerMessage;
            _session.MapEntered += OnMapEntered;
            ShowStep(Step.Loading);
            _loading.SetStatus(Lang.GetSplitText("Chọn máy chủ rồi bấm Bắt đầu.|Choose a server, then press Start."));
        }

        private void OnDestroy()
        {
            if (_client == null) return;   // NetClient outlives the scene: drop every hook into it
            _client.Net.Connected -= OnConnected;
            _client.Net.ConnectFailed -= OnConnectFailed;
            _client.Net.Disconnected -= OnDisconnected;
            _client.Data.Done -= OnDataDone;
        }

        private void Update()
        {
            if (_downloading) _loading.SetProgress(_client.Data.Progress, _client.Data.ReceivedBytes, _client.Data.TotalBytes);
        }

        /// <summary>Enters <paramref name="map"/> at <paramref name="position"/> without a server, hiding the panels.</summary>
        public void PlayOffline(MapInfo map, Vector2 position)
        {
            _session.EnterMap(map, position);
        }

        // ---------------- steps ----------------

        private void Connect(string host, int port)
        {
            _loading.SetBusy(true);
            _loading.SetStatus(Lang.GetSplitText($"Đang kết nối {host}:{port}...|Connecting to {host}:{port}..."));
            _client.Net.Connect(host, port);
        }

        private void OnConnected()
        {
            _loading.SetStatus(Lang.GetSplitText("Đang kiểm tra dữ liệu...|Checking data..."));
            _downloading = true;
            _client.Data.Begin(NetClient.DeviceType, _clientVersion);
        }

        private void OnConnectFailed()
        {
            _loading.SetBusy(false);
            _loading.SetStatus(Lang.GetSplitText("Không kết nối được máy chủ. Thử lại?|Cannot reach the server. Try again?"));
        }

        private void OnDataDone()
        {
            _downloading = false;
            _loading.SetProgress(1f, _client.Data.ReceivedBytes, _client.Data.TotalBytes);
            if (_session.Maps.maps.Length == 0)
            {
                _loading.SetBusy(false);
                _loading.SetStatus(Lang.GetSplitText("Máy chủ không gửi bản đồ.|The server sent no maps."));
                return;
            }
            ShowStep(Step.Login);
        }

        private void OnLoggedIn()
        {
            _login.SetMessage(Lang.GetSplitText("Đang vào bản đồ...|Entering the map..."));
        }

        private void OnServerMessage(string text)
        {
            if (Current != Step.Login) return;
            _login.SetMessage(text);
            _login.SetBusy(false);
        }

        private void OnMapEntered(MapInfo map) => ShowStep(Step.Playing);

        // lost the server: back to the first screen, like GameB restarting from LoadingScene
        private void OnDisconnected()
        {
            _downloading = false;
            _client.Data.Reset();
            _session.ResetWorld();
            if (_player != null) _player.SetControllable(false);
            ShowStep(Step.Loading);
            _loading.SetBusy(false);
            _loading.SetStatus(Lang.GetSplitText("Mất kết nối máy chủ.|Disconnected from the server."));
        }

        private void ShowStep(Step step)
        {
            Current = step;
            if (_loading != null) _loading.SetVisible(step == Step.Loading);
            if (_login != null)
            {
                _login.SetVisible(step == Step.Login);
                if (step == Step.Login) _login.SetBusy(false);
            }
        }

        // the panels go over the Core GameCanvas when the scene has one, otherwise on their own canvas
        private Transform PanelParent()
        {
            if (GameUIManager.Instance != null) return GameUIManager.Instance.Canvas.transform;
            return UIFactory.CreateCanvas("FlowCanvas", 300).transform;
        }
    }
}
