using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Thiết lập import mặc định cho file âm thanh, tối ưu cho mobile. Chỉ áp lần đầu file được nhập (chưa có .meta),
    /// sửa tay trong Inspector sau đó thì giữ nguyên. Gói mua (Assets/ThirdParty) không bị đụng.
    /// </summary>
    /// <remarks>
    /// Nhạc nền (đường dẫn có thư mục Music / BGM, hoặc tên bắt đầu bằng bgm / music): Streaming + Vorbis, không preload,
    /// giữ stereo — không chiếm RAM, chỉ tốn một luồng giải mã.
    /// SFX: ép mono. File nhỏ (tiếng ngắn, phát liên tục: click, trúng đòn) giải nén sẵn khi nạp + ADPCM, giải mã gần như
    /// không tốn CPU; file lớn hơn thì Compressed In Memory + Vorbis cho đỡ RAM.
    /// Tools/Core/Audio/Apply Mobile Import Settings áp lại cho mọi clip trong Assets/_Project.
    /// </remarks>
    public class AudioImportDefaults : AssetPostprocessor
    {
        const string ProjectRoot = "Assets/_Project";
        const long SmallSfxBytes = 200 * 1024; // khoảng dưới ~2 giây với wav 44.1kHz stereo

        void OnPreprocessAudio()
        {
            if (!assetImporter.importSettingsMissing) return;
            if (!assetPath.StartsWith(ProjectRoot)) return;
            Apply((AudioImporter)assetImporter);
        }

        public static bool IsMusic(string path)
        {
            string p = path.Replace('\\', '/').ToLowerInvariant();
            string file = Path.GetFileName(p);
            return p.Contains("/music/") || p.Contains("/bgm/") || file.StartsWith("bgm") || file.StartsWith("music");
        }

        public static void Apply(AudioImporter importer)
        {
            string path = importer.assetPath;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;

            if (IsMusic(path))
            {
                importer.forceToMono = false;
                importer.loadInBackground = true;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.5f;
                s.preloadAudioData = false;
            }
            else
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                s.preloadAudioData = true;
                long size = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (size <= SmallSfxBytes)
                {
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.ADPCM;
                }
                else
                {
                    s.loadType = AudioClipLoadType.CompressedInMemory;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.5f;
                }
            }

            importer.defaultSampleSettings = s;
            // Bỏ override riêng của Android / iOS (nếu có) để dùng đúng thiết lập trên.
            importer.ClearSampleSettingOverride("Android");
            importer.ClearSampleSettingOverride("iOS");
        }

        [MenuItem("Tools/Core/Audio/Apply Mobile Import Settings")]
        static void ApplyAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectRoot });
            int done = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is AudioImporter importer)
                    {
                        Apply(importer);
                        importer.SaveAndReimport();
                        done++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Debug.Log($"AudioImportDefaults: áp thiết lập mobile cho {done} clip trong {ProjectRoot}.");
        }
    }
}
