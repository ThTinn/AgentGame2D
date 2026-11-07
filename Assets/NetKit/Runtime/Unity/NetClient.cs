using System.IO;
using UnityEngine;

namespace NetKit
{
    /// <summary>
    /// Cầu nối với Unity: giữ một <see cref="NetConnection"/> và một <see cref="DataDownloader"/> sống qua mọi scene, gọi Pump mỗi
    /// frame ở main thread, đóng socket khi thoát. Lấy bằng <see cref="Get"/>, không cần đặt sẵn trong scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class NetClient : MonoBehaviour
    {
        public static NetClient Instance { get; private set; }

        public NetConnection Net { get; private set; }
        public DataDownloader Data { get; private set; }

        /// <summary>Mã thiết bị gửi trong CLIENT_INFO, server chọn thư mục file theo mã này (1 Android, 2 iOS, 3 PC).</summary>
        public static int DeviceType
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.Android: return 1;
                    case RuntimePlatform.IPhonePlayer: return 2;
                    default: return 3;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;   // Enter Play Mode không reload domain

        /// <summary>NetClient đang chạy, chưa có thì tạo GameObject [NetKit] (DontDestroyOnLoad).</summary>
        public static NetClient Get()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[NetKit]");
            DontDestroyOnLoad(go);
            return go.AddComponent<NetClient>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            NetLog.Info = Debug.Log;
            NetLog.Error = Debug.LogError;
            Net = new NetConnection();
            Data = new DataDownloader(Net, new DataStore(Path.Combine(Application.persistentDataPath, "netkit")));
        }

        void Update() => Net.Pump(Time.unscaledDeltaTime);

        void OnDestroy()
        {
            if (Instance != this) return;
            Net.Close();
            Instance = null;
        }
    }
}
