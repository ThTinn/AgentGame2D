using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>Âm dùng vào việc gì, để bộ chọn trong Editor chỉ liệt kê đúng loại.</summary>
    public enum SoundType : byte { Skill, Ui, Music }

    [Serializable]
    public class SoundRecord
    {
        public int id;
        [Tooltip("Tên khóa, không đổi (vd energy_bolt_hit). Tool sinh âm tìm lại bản ghi theo tên nên chạy lại vẫn giữ id.")]
        public string name;
        public SoundType type;
        [Tooltip("Tăng khi đổi clip: bản online so số này để tải lại bundle.")]
        public byte version = 1;
        [Tooltip("File gốc trong project. Chỉ Editor dùng để đóng bundle (Tools/Core/Audio/Build Sound Bundles); game nạp clip "
                 + "từ bundle Data/<nền tảng>/sound/12_<id>.bytes.")]
        public AudioClip clip;
    }

    /// <summary>
    /// Danh mục âm thanh của game, một asset ở Assets/_Project/Core/Data/Sounds/SoundData.asset. Dữ liệu khác (skill...) chỉ giữ
    /// id âm; clip nằm trong asset bundle chứ không đi theo bản build. Id không bao giờ dùng lại cho âm khác.
    /// </summary>
    /// <remarks>
    /// Asset này chỉ để soạn trong Editor: không gì lúc chạy tham chiếu tới nó (tham chiếu thì mọi clip bị kéo vào bản build).
    /// </remarks>
    [CreateAssetMenu(fileName = "SoundData", menuName = "Core/Sound Data")]
    public class SoundData : ScriptableObject
    {
        public const string AssetPath = "Assets/_Project/Core/Data/Sounds/SoundData.asset";

        public List<SoundRecord> sounds = new List<SoundRecord>();

        [NonSerialized] Dictionary<int, SoundRecord> byId;
        [NonSerialized] Dictionary<string, SoundRecord> byName;

        void Index()
        {
            if (byId != null && byId.Count == sounds.Count) return;
            byId = new Dictionary<int, SoundRecord>(sounds.Count);
            byName = new Dictionary<string, SoundRecord>(sounds.Count);
            foreach (var s in sounds) { byId[s.id] = s; byName[s.name] = s; }
        }

        public void ClearCache() { byId = null; byName = null; }

        public SoundRecord Get(int id) { Index(); byId.TryGetValue(id, out var r); return r; }

        public int FindId(string name) { Index(); return name != null && byName.TryGetValue(name, out var r) ? r.id : 0; }

        /// <summary>
        /// Bản ghi tên name (tạo mới với id kế tiếp nếu chưa có), gán clip; clip đổi thì tăng version để bản online tải lại.
        /// Trả id.
        /// </summary>
        public int Register(string name, SoundType type, AudioClip clip)
        {
            SoundRecord r = Get(FindId(name));
            if (r == null)
            {
                int max = 0;
                foreach (var s in sounds) max = Mathf.Max(max, s.id);
                r = new SoundRecord { id = max + 1, name = name };
                sounds.Add(r);
                ClearCache();
            }
            else if (r.clip != clip) r.version++;
            r.type = type;
            r.clip = clip;
            return r.id;
        }
    }
}
