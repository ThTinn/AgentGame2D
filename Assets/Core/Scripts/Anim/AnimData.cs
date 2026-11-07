using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Net;

namespace Game.Anim
{
    // Animator states the player base controller knows. Values are stable: they are written to the exported file.
    public enum AnimSlot : byte { Idle = 0, Move = 1, Dash = 2, Attack = 3, Hurt = 4, Die = 5, Jump = 6, Sit = 7 }

    /// <summary>
    /// Bố cục cố định của base controller: mỗi hành động có bao nhiêu clip (motion) và tên state / clip gốc của từng clip,
    /// cộng 4 state skill. Idle: 0 là idle chính, 1-2 là idle phụ chạy ngẫu nhiên. Move: một clip chạy (không blend đi bộ
    /// sang chạy). Attack: 4 bước combo đánh thường. Jump: 0 bay lên, 1 rơi xuống (blend theo yVelocity).
    /// Sit: ngồi lên ghế / ghế dài (PlayerSitState): 0 ngồi xuống, 1 đang ngồi (lặp), 2 đứng dậy.
    /// </summary>
    /// <remarks>Hành động mới luôn thêm vào CUỐI: chỉ số phẳng của clip cũ giữ nguyên, mảng tư thế đã lưu vẫn đúng chỗ.</remarks>
    /// <remarks>
    /// Game: bản GAME_2D mỗi clip có ba hướng (_down/_side/_up) và lớp sprite vũ khí. Nhân vật 3D xoay cả model nên mỗi
    /// clip chỉ một bản; clip gốc trong controller tên đúng bằng tên state (PlayerAnimSetup dựng).
    /// </remarks>
    public static class AnimLayout
    {
        public const int SlotCount = 8;
        public const int SkillCount = 4;

        // độ dài mảng tư thế của các bố cục cũ, để PerMotion nhận ra mà chuyển: theo hành động (7 hành động, trước khi
        // lưu theo clip) và theo clip lúc Move còn hai clip walk + run (14 clip, trước khi có Sit)
        const int LegacySlotCount = 7;
        const int LegacyWalkTotal = 14;

        static readonly string[][] Motions =
        {
            new[] { "idle0", "idle1", "idle2" },
            new[] { "run" },
            new[] { "dash" },
            new[] { "attack1", "attack2", "attack3", "attack4" },
            new[] { "hurt" },
            new[] { "die" },
            new[] { "jump_up", "jump_fall" },
            new[] { "sit_down", "sit_idle", "sit_up" },
        };

        /// <summary>Tên state (cũng là tên clip gốc trong base controller) của các clip trong hành động.</summary>
        public static IReadOnlyList<string> MotionNames(AnimSlot slot) => Motions[(int)slot];

        public static int MotionCount(AnimSlot slot) => Motions[(int)slot].Length;

        /// <summary>Tên state của ô skill, index từ 0.</summary>
        public static string SkillName(int index) => "skill" + (index + 1);

        // Lựa chọn tư thế được lưu theo từng clip (idle0-2, run, dash, ...): chỉ số phẳng = Offset(slot) + motion.
        static readonly int[] Offsets = BuildOffsets();

        /// <summary>Tổng số clip của mọi hành động, cũng là độ dài mảng id tư thế theo clip.</summary>
        public static readonly int MotionTotal = Offsets[Offsets.Length - 1] + Motions[Motions.Length - 1].Length;

        static int[] BuildOffsets()
        {
            var offsets = new int[Motions.Length];
            for (int i = 1; i < Motions.Length; i++) offsets[i] = offsets[i - 1] + Motions[i - 1].Length;
            return offsets;
        }

        /// <summary>Chỉ số phẳng của clip motion trong hành động slot.</summary>
        public static int MotionIndex(AnimSlot slot, int motion) => Offsets[(int)slot] + motion;

        /// <summary>Tên state của clip theo chỉ số phẳng (idle0, run, attack3...).</summary>
        public static string MotionNameAt(int index)
        {
            for (int s = Motions.Length - 1; s >= 0; s--)
                if (index >= Offsets[s]) return index - Offsets[s] < Motions[s].Length ? Motions[s][index - Offsets[s]] : null;
            return null;
        }

        /// <summary>
        /// Mảng id tư thế theo clip (<see cref="MotionTotal"/> ô). Nhận cả mảng kiểu cũ theo hành động
        /// (7 ô): mọi clip của hành động lấy id của hành động đó. Thiếu ô (mảng lưu trước khi có hành động mới) thì 0.
        /// </summary>
        public static int[] PerMotion(int[] ids)
        {
            var result = new int[MotionTotal];
            if (ids == null) return result;
            // bố cục cũ: Move có hai clip walk + run (walk ở ngay sau ba idle). Bỏ ô walk.
            if (ids.Length == LegacyWalkTotal)
            {
                int walk = Offsets[(int)AnimSlot.Move];
                var trimmed = new int[LegacyWalkTotal - 1];
                for (int i = 0, j = 0; i < ids.Length; i++)
                    if (i != walk) trimmed[j++] = ids[i];
                ids = trimmed;
            }
            bool perSlot = ids.Length == LegacySlotCount;
            for (int s = 0; s < Motions.Length; s++)
                for (int m = 0; m < Motions[s].Length; m++)
                {
                    int src = perSlot ? s : Offsets[s] + m;
                    if (src < ids.Length) result[Offsets[s] + m] = ids[src];
                }
            return result;
        }
    }

    /// <summary>
    /// Một clip: tải từ asset bundle theo AnimTemplate (<see cref="animId"/>, đường chính: server / StreamingAssets gửi
    /// bundle anim/0_{id}) hoặc clip có sẵn trong build (<see cref="clip"/>, để test). Icon do công cụ Editor render sẵn
    /// từ model; khung xem trước (AnimPreview) chạy model thật lúc chơi.
    /// </summary>
    [Serializable]
    public class AnimMotion
    {
        [Tooltip("Id anim trong AnimTemplate (bundle anim/0_{id}). -1 = không dùng bundle, chạy clip bên dưới.")]
        public short animId = -1;
        [Tooltip("Clip có sẵn trong build, dùng khi animId = -1.")]
        public AnimationClip clip;
        [Tooltip("Tên anim (AnimTemplate.name hoặc tên clip) hiện trên UI.")]
        public string name;
        [Tooltip("Icon của clip (frame đầu), do công cụ Editor render sẵn (Tools/Anim/Render Anim Icons). Giữ dạng mảng cho dữ liệu cũ.")]
        public Sprite[] preview;

        public bool HasClip => animId >= 0 || clip != null;

        /// <summary>Cùng một anim (cùng bundle, hoặc cùng clip trong build).</summary>
        public bool SameAs(AnimMotion other) =>
            other != null && (animId >= 0 ? animId == other.animId : other.animId < 0 && clip == other.clip);

        public Sprite Icon => preview != null && preview.Length > 0 ? preview[0] : null;

        /// <summary>Tên cho UI: in_air_up thành "In air up".</summary>
        public string Label
        {
            get
            {
                string n = !string.IsNullOrEmpty(name) ? name : clip != null ? clip.name : animId >= 0 ? "anim " + animId : "?";
                n = n.Replace('_', ' ');
                return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : "?";
            }
        }
    }

    /// <summary>
    /// Một cách thể hiện của cùng một hành động, gồm đủ các clip mà hành động đó cần (<see cref="AnimLayout.MotionCount"/>):
    /// idle 3 clip, attack 4 bước, jump lên + rơi. Variant thiếu clip nào thì clip đó mượn clip đầu, nên variant một
    /// clip gắn được vào bất kỳ ô nào của hành động (người chơi chọn tư thế theo từng clip).
    /// </summary>
    [Serializable]
    public class AnimVariant
    {
        [Tooltip("Id ổn định trong hành động, 0 là bộ mặc định. PlayerData lưu id này chứ không lưu thứ tự trong danh sách.")]
        public int id;
        [Tooltip("Tên hiện trên UI trang bị.")]
        public string name;
        [Tooltip("Theo thứ tự của AnimLayout: idle chính/phụ, chạy, các bước combo, bay lên/rơi xuống.")]
        public List<AnimMotion> motions = new List<AnimMotion>();

        public AnimMotion Motion(int index)
        {
            if (motions.Count == 0) return null;
            return motions[index >= 0 && index < motions.Count ? index : 0];
        }

        public Sprite Icon => Motion(0)?.Icon;

        public string Label => !string.IsNullOrEmpty(name) ? name : Motion(0)?.Label ?? "?";
    }

    // One action of one character. variants[0] là bộ mặc định, cũng là bộ dựng nên base controller.
    [Serializable]
    public class AnimEntry
    {
        public AnimSlot slot;
        public List<AnimVariant> variants = new List<AnimVariant>();

        public int Count => variants.Count;

        /// <summary>Variant theo id, không có id đó thì trả bộ mặc định.</summary>
        public AnimVariant Get(int variantId)
        {
            if (variants.Count == 0) return null;
            foreach (var v in variants) if (v.id == variantId) return v;
            return variants[0];
        }
    }

    /// <summary>Đặt lên mảng int chỉ số theo AnimLayout.MotionIndex: Inspector ghi tên clip (idle0, run...) thay cho "Element N".</summary>
    public class AnimSlotLabelsAttribute : PropertyAttribute { }

    [Serializable]
    public class AnimSet
    {
        public int id;
        public string name;
        public List<AnimEntry> entries = new List<AnimEntry>();

        public AnimEntry Get(AnimSlot slot)
        {
            foreach (var e in entries) if (e.slot == slot) return e;
            return null;
        }
    }

    /// <summary>
    /// Nhân vật đang mang anim, nhìn từ phía dữ liệu / UI (Core.Data không thấy được CharacterAnimator ở Assembly-CSharp).
    /// CharacterAnimator cài interface này; PlayerProfile.Anim trỏ tới nó.
    /// </summary>
    public interface IAnimView
    {
        AnimData Data { get; }
        /// <summary>Id bộ anim đang dùng (AnimSet.id).</summary>
        int AnimId { get; }
        /// <summary>Id tư thế đang mặc ở clip motion của hành động.</summary>
        int GetVariant(AnimSlot slot, int motion);
    }

    // Authoring asset: animation sets by character id. Characters whose id has no set (or a set missing an action) use defaultId.
    [CreateAssetMenu(fileName = "AnimData", menuName = "Anim/Anim Data")]
    public class AnimData : ScriptableObject
    {
        /// <summary>
        /// 2: mỗi slot mang danh sách variant thay vì đúng một bộ clip. 3: variant có id ổn định.
        /// 4: variant là bộ nhiều clip (idle 3, move walk/run, attack 4 bước).
        /// 5: ảnh xem trước nằm ở từng clip (AnimMotion) thay vì ở variant.
        /// 6: (2D) clip cầm vũ khí có thêm lớp vũ khí / tay.
        /// 7: bản 3D: mỗi clip một bản (không ba hướng), có thể tải từ bundle (AnimMotion.animId); Jump hai clip.
        /// 8: Move chỉ một clip chạy (bỏ blend đi bộ sang chạy).
        /// </summary>
        public const int CurrentSchema = 8;

        public int schemaVersion = CurrentSchema;
        public int defaultId = 0;
        public List<AnimSet> sets = new List<AnimSet>();

        [NonSerialized] Dictionary<int, AnimSet> byId;

        public void ClearCache() => byId = null;

        void OnValidate() => byId = null;

        void Index()
        {
            if (byId != null && byId.Count == sets.Count) return;
            byId = new Dictionary<int, AnimSet>(sets.Count);
            foreach (var s in sets) byId[s.id] = s;
        }

        public AnimSet GetSet(int id) { Index(); byId.TryGetValue(id, out var s); return s; }
        public AnimSet DefaultSet => GetSet(defaultId);

        /// <summary>Entry của (nhân vật, hành động): bộ riêng trước, thiếu thì lấy của bộ mặc định.</summary>
        public AnimEntry GetEntry(int id, AnimSlot slot)
        {
            AnimEntry e = GetSet(id)?.Get(slot);
            if (e != null && e.Count > 0) return e;
            return id == defaultId ? e : DefaultSet?.Get(slot);
        }

        /// <summary>Danh sách variant để UI liệt kê. Không bao giờ null.</summary>
        public IReadOnlyList<AnimVariant> Variants(int id, AnimSlot slot)
        {
            AnimEntry e = GetEntry(id, slot);
            return e != null ? (IReadOnlyList<AnimVariant>)e.variants : Array.Empty<AnimVariant>();
        }

        // The clip for (character id, action, variant id, motion): own set first, then the default set.
        public AnimMotion Resolve(int id, AnimSlot slot, int variantId, int motion)
        {
            AnimMotion m = GetSet(id)?.Get(slot)?.Get(variantId)?.Motion(motion);
            if ((m != null && m.HasClip) || id == defaultId) return m;
            return DefaultSet?.Get(slot)?.Get(variantId)?.Motion(motion);
        }

        // Binary (MyWriter): short schema, short defaultId, short count
        //   { short id, UTF name, byte n { byte slot, byte variants { short id, UTF name,
        //     byte motions { short animId, UTF clip } } } }
        public byte[] ToBytes()
        {
            var w = new MyWriter(4 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)defaultId));
            w.WriteShort(checked((short)sets.Count));
            foreach (var s in sets)
            {
                w.WriteShort(checked((short)s.id));
                w.WriteUTF(s.name ?? "");
                w.WriteByte(checked((byte)s.entries.Count));
                foreach (var e in s.entries)
                {
                    w.WriteByte((byte)e.slot);
                    w.WriteByte(checked((byte)e.variants.Count));
                    foreach (var v in e.variants)
                    {
                        w.WriteShort(checked((short)v.id));
                        w.WriteUTF(v.name ?? "");
                        w.WriteByte(checked((byte)v.motions.Count));
                        foreach (var m in v.motions)
                        {
                            w.WriteShort(m.animId);
                            w.WriteUTF(m.clip ? m.clip.name : "");
                        }
                    }
                }
            }
            return w.ToArray();
        }
    }
}
