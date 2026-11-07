using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Net
{
    /// <summary>Servers the Loading panel offers. The asset lives in Assets/Data.</summary>
    [CreateAssetMenu(fileName = "ServerList", menuName = "DungeonOnline/Server List")]
    public sealed class ServerList : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string Name;
            public string Host;
            /// <summary>GameServer port: it also serves the data download.</summary>
            public int Port;
        }

        public List<Entry> Servers = new List<Entry>();

        /// <summary>
        /// Parses "host" or "host:port" typed by the player; a missing port uses <paramref name="defaultPort"/>.
        /// Returns false for an empty host or a port outside 1-65535.
        /// </summary>
        public static bool TryParseAddress(string text, int defaultPort, out string host, out int port)
        {
            host = null;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();
            int colon = text.LastIndexOf(':');
            if (colon >= 0)
            {
                if (!int.TryParse(text.Substring(colon + 1), out port)) return false;
                text = text.Substring(0, colon);
            }
            host = text;
            return host.Length > 0 && port > 0 && port <= 65535;
        }
    }
}
