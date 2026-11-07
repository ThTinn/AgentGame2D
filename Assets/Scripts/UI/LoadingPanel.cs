using System;
using System.Collections.Generic;
using DungeonOnline.Net;
using Game.UI;
using TMPro;
using UnityEngine;

namespace DungeonOnline.UI
{
    /// <summary>
    /// First screen: pick a server (or type an IP), press Start, then watch the data download. Display only: it reports
    /// the choice through <see cref="StartPressed"/> and shows what <see cref="GameFlow"/> tells it. Mirrors GameB's
    /// LoadingScene (server list, custom IP row, progress bar with MB).
    /// </summary>
    /// <example>
    /// <code>
    /// var loading = LoadingPanel.Create(canvas.transform, serverList);
    /// loading.StartPressed += (host, port) => net.Connect(host, port);
    /// loading.SetProgress(data.Progress, data.ReceivedBytes, data.TotalBytes);
    /// </code>
    /// </example>
    public sealed class LoadingPanel : MonoBehaviour
    {
        private const string PREF_SERVER = "loading.server";   // index of the chosen server, last one = typed IP
        private const string PREF_ADDRESS = "loading.address";
        private const float MegaByte = 1048576f;

        private readonly List<TextBtn> _serverButtons = new List<TextBtn>();
        private ServerList _servers;
        private TMP_InputField _address;
        private TextBtn _start;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _size;
        private RectTransform _bar;
        private int _selected;

        /// <summary>Start was pressed with this host and port.</summary>
        public event Action<string, int> StartPressed;

        /// <summary>Builds the panel over everything under <paramref name="parent"/> (usually the GameCanvas).</summary>
        public static LoadingPanel Create(Transform parent, ServerList servers)
        {
            RectTransform root = PanelBuilder.FullScreen(parent, "LoadingPanel", PanelBuilder.Backdrop);
            var panel = root.gameObject.AddComponent<LoadingPanel>();
            panel._servers = servers;
            panel.Build(root);
            return panel;
        }

        private void Build(RectTransform root)
        {
            RectTransform box = PanelBuilder.Box(root, new Vector2(640f, 600f));
            PanelBuilder.Label(box, "DungeonOnline", 44f, 60f);
            PanelBuilder.Label(box, Lang.GetSplitText("Chọn máy chủ|Choose a server"), 26f, 36f);

            if (_servers != null)
                for (int i = 0; i < _servers.Servers.Count; i++)
                {
                    int index = i;
                    ServerList.Entry entry = _servers.Servers[i];
                    _serverButtons.Add(PanelBuilder.Button(box, $"{entry.Name}  ({entry.Host}:{entry.Port})", 52f,
                        () => Select(index)));
                }

            _address = PanelBuilder.Input(box, Lang.GetSplitText("Hoặc nhập IP (192.168.x.x:18889)|Or type an IP (192.168.x.x:18889)"),
                false);
            _address.text = PlayerPrefsGet(PREF_ADDRESS, "");
            _address.onSelect.AddListener(_ => Select(ServerCount));

            _start = PanelBuilder.Button(box, Lang.GetSplitText("Bắt đầu|Start"), 64f, PressStart);
            _status = PanelBuilder.Label(box, "", 24f, 34f);
            _bar = PanelBuilder.Bar(box, 18f);
            _size = PanelBuilder.Label(box, "", 22f, 28f);
            ShowProgress(false);

            int saved = PlayerPrefsGetInt(PREF_SERVER, 0);
            Select(saved >= 0 && saved <= ServerCount ? saved : 0);
        }

        private int ServerCount => _serverButtons.Count;

        private void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _serverButtons.Count; i++)
                _serverButtons[i].SetColorText(i == index ? UIColors.TextSelected : UIColors.TextNormal);
        }

        private void PressStart()
        {
            string host;
            int port;
            if (_selected < ServerCount)
            {
                ServerList.Entry entry = _servers.Servers[_selected];
                host = entry.Host;
                port = entry.Port;
            }
            else
            {
                int defaultPort = ServerCount > 0 ? _servers.Servers[0].Port : 18889;
                if (!ServerList.TryParseAddress(_address.text, defaultPort, out host, out port))
                {
                    SetStatus(Lang.GetSplitText("IP sai định dạng (ip hoặc ip:cổng)|Bad address (ip or ip:port)"));
                    return;
                }
                PlayerPrefsSet(PREF_ADDRESS, _address.text.Trim());
            }
            PlayerPrefsSetInt(PREF_SERVER, _selected);
            StartPressed?.Invoke(host, port);
        }

        /// <summary>Shows a status line, e.g. "Connecting..." or why it failed.</summary>
        public void SetStatus(string text) => _status.text = text;

        /// <summary>Locks the Start button while connecting or downloading.</summary>
        public void SetBusy(bool busy) => _start.SetInteractable(!busy);

        /// <summary>Shows download progress (0-1) and the size done / total. Hidden when there is nothing to download.</summary>
        public void SetProgress(float progress, long received, long total)
        {
            ShowProgress(total > 0);
            if (total <= 0) return;
            PanelBuilder.SetBar(_bar, progress);
            _size.text = $"{received / MegaByte:F1}/{total / MegaByte:F1} MB";
        }

        /// <summary>Shows or hides the whole panel.</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        private void ShowProgress(bool show)
        {
            _bar.gameObject.SetActive(show);
            _size.gameObject.SetActive(show);
        }

        // PlayerPrefs can throw in some sandboxed players; a missing preference is never fatal
        private static string PlayerPrefsGet(string key, string fallback)
        {
            try { return PlayerPrefs.GetString(key, fallback); }
            catch (Exception) { return fallback; }
        }

        private static int PlayerPrefsGetInt(string key, int fallback)
        {
            try { return PlayerPrefs.GetInt(key, fallback); }
            catch (Exception) { return fallback; }
        }

        private static void PlayerPrefsSet(string key, string value)
        {
            try { PlayerPrefs.SetString(key, value); }
            catch (Exception) { }
        }

        private static void PlayerPrefsSetInt(string key, int value)
        {
            try { PlayerPrefs.SetInt(key, value); }
            catch (Exception) { }
        }
    }
}
