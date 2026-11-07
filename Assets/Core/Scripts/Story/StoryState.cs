using System;
using System.Collections.Generic;

namespace Game.Story
{
    /// <summary>
    /// Bậc thân thiện của một NPC với người chơi. Giá trị dùng để so sánh (cao hơn = thân hơn). Stranger = 0 để trường
    /// minTier mặc định của nhiệm vụ là "ai cũng nhận được, trừ người đang ghét mình".
    /// </summary>
    public enum AffinityTier : sbyte
    {
        Enemy = -2,     // Kẻ thù     -100..-50: vĩnh viễn, không thèm nói chuyện
        Disliked = -1,  // Ác cảm     -49..-1: nói chuyện lạnh nhạt, không giao việc
        Stranger = 0,   // Xa lạ      0-19
        Known = 1,      // Quen biết  20-39
        Friendly = 2,   // Thân thiện 40-59
        Trusted = 3,    // Tin cậy    60-79
        Bonded = 4,     // Tri kỷ     80-100
    }

    /// <summary>Luật của độ thân thiện: mốc từng bậc, tên, hệ số thưởng và hệ số giá. Chỉ có một bảng cho mọi NPC.</summary>
    public static class Affinity
    {
        public const int Min = -100, Max = 100;
        /// <summary>Tụt tới mức này là thành kẻ thù, và không bao giờ gỡ lại được.</summary>
        public const int EnemyAt = -50;
        /// <summary>Bỏ nhiệm vụ đang làm: NPC giao việc giảm chừng này.</summary>
        public const int AbandonPenalty = 5;
        /// <summary>Lần đầu bắt chuyện trong một phiên chơi: tăng chừng này.</summary>
        public const int GreetGain = 1;

        // theo thứ tự AffinityTier từ Enemy (chỉ số 0) tới Bonded (chỉ số 6)
        static readonly int[] Floors = { Min, EnemyAt + 1, 0, 20, 40, 60, 80 };
        // vàng + EXP của nhiệm vụ nhân hệ số này: thân thì NPC hào phóng, lạ thì trả ít
        static readonly float[] RewardScale = { 0f, 0.6f, 0.8f, 1f, 1.15f, 1.3f, 1.5f };
        // giá dịch vụ (chữa thương, mua đồ) nhân hệ số này
        static readonly float[] PriceScale = { 2f, 1.3f, 1.1f, 1f, 0.95f, 0.9f, 0.8f };

        static int Index(AffinityTier tier) => (int)tier - (int)AffinityTier.Enemy;

        public static AffinityTier TierOf(int value)
        {
            for (int i = Floors.Length - 1; i > 0; i--)
                if (value >= Floors[i]) return (AffinityTier)(i + (int)AffinityTier.Enemy);
            return AffinityTier.Enemy;
        }

        public static int FloorOf(AffinityTier tier) => Floors[Index(tier)];
        /// <summary>Tên bậc ở ngôn ngữ đang chọn (StoryText.L: chuỗi song ngữ, Lang_Data.xlsx gom được).</summary>
        public static string NameOf(AffinityTier tier)
        {
            switch (tier)
            {
                case AffinityTier.Enemy: return StoryText.L("Kẻ thù|Enemy");
                case AffinityTier.Disliked: return StoryText.L("Ác cảm|Disliked");
                case AffinityTier.Stranger: return StoryText.L("Xa lạ|Stranger");
                case AffinityTier.Known: return StoryText.L("Quen biết|Acquainted");
                case AffinityTier.Friendly: return StoryText.L("Thân thiện|Friendly");
                case AffinityTier.Trusted: return StoryText.L("Tin cậy|Trusted");
                default: return StoryText.L("Tri kỷ|Soulmate");
            }
        }
        public static float RewardScaleOf(AffinityTier tier) => RewardScale[Index(tier)];
        public static float PriceScaleOf(AffinityTier tier) => PriceScale[Index(tier)];

        /// <summary>Giá sau khi tính bậc thân thiện, làm tròn, giá gốc dương thì ít nhất 1.</summary>
        public static long Price(long basePrice, AffinityTier tier) =>
            basePrice <= 0 ? 0 : Math.Max(1, (long)Math.Round(basePrice * PriceScaleOf(tier)));
    }

    public enum QuestStatus : byte
    {
        Locked = 0,      // chưa đủ điều kiện (việc trước, cờ cốt truyện) hoặc người giao / nhận là kẻ thù: ẩn
        Available = 1,   // nhận được (nếu đủ thân thiện)
        Active = 2,      // đang làm
        Ready = 3,       // đủ mục tiêu, chờ trả
        Done = 4,        // đã trả (việc lặp lại về Available ngay; việc hằng ngày về Available sang ngày sau)
    }

    /// <summary>Tiến độ một nhiệm vụ của người chơi. Ghi xuống PlayerData nên chỉ dùng kiểu Unity serialize được.</summary>
    [Serializable]
    public class QuestRecord
    {
        public string quest;            // QuestTemplate.name
        public bool active;             // đang làm (false = đã trả, chưa nhận lại)
        public int timesDone;           // số lần đã trả (việc lặp lại / hằng ngày cộng dồn)
        public string outcome;          // QuestOutcome.name đã chọn lần trả gần nhất; trống = việc không có nhánh
        public int lastDay;             // ngày trả gần nhất (StoryState.Today), cho việc hằng ngày
        public List<int> progress = new List<int>();   // số quái đã hạ cho từng mục tiêu Kill (mục tiêu Collect đếm thẳng trong túi)

        public int Progress(int objective) => objective < progress.Count ? progress[objective] : 0;

        public void SetProgress(int objective, int value)
        {
            while (progress.Count <= objective) progress.Add(0);
            progress[objective] = value;
        }
    }

    /// <summary>Độ thân thiện đã lưu của một NPC (theo NpcTemplate.name).</summary>
    [Serializable]
    public class AffinityRecord
    {
        public string npc;
        public int value;
    }

    /// <summary>
    /// Cốt truyện của người chơi: độ thân thiện với từng NPC, tiến độ nhiệm vụ và cờ cốt truyện (những gì người chơi đã
    /// chọn, ai đã thành kẻ thù, kết cục nào). Thuần dữ liệu, luật nằm ở <see cref="QuestSystem"/>. Người chơi giữ một bản
    /// (Player.Story); GameController nạp từ PlayerData và ghi lại mỗi khi <see cref="Changed"/> bắn.
    /// </summary>
    public class StoryState
    {
        /// <summary>Cờ "NPC này là kẻ thù": EnemyFlag + NpcTemplate.name.</summary>
        public const string EnemyFlag = "enemy:";
        /// <summary>Cờ tự đặt khi trả xong một nhiệm vụ: DoneFlag + QuestTemplate.name (điều kiện có thể hỏi cờ này).</summary>
        public const string DoneFlag = "done:";

        readonly Dictionary<string, int> affinity = new Dictionary<string, int>();
        readonly Dictionary<string, QuestRecord> quests = new Dictionary<string, QuestRecord>();
        readonly HashSet<string> flags = new HashSet<string>();
        // tên NPC đang là kẻ thù, song song với cờ EnemyFlag + tên: IsEnemy chạy mỗi khung (Npc.Update) nên tra thẳng theo tên,
        // không ghép chuỗi "enemy:" + tên mỗi lần (rác GC)
        readonly HashSet<string> enemies = new HashSet<string>();
        // NPC đã chào trong phiên này: chào hỏi chỉ cộng thân thiện một lần mỗi phiên
        readonly HashSet<string> greeted = new HashSet<string>();

        /// <summary>Thân thiện, nhiệm vụ hoặc cờ vừa đổi. Lưu và cập nhật UI (dấu ! / ? trên đầu NPC) nghe cái này.</summary>
        public event Action Changed;

        /// <summary>Một NPC vừa thành kẻ thù (QuestSystem dọn các việc dính tới NPC đó).</summary>
        public event Action<string> BecameEnemy;

        public IEnumerable<QuestRecord> Quests => quests.Values;
        public IEnumerable<string> Flags => flags;

        string tracked;

        /// <summary>
        /// Nhiệm vụ người chơi đang theo dõi (QuestTemplate.name; null = không theo): khung theo dõi đưa lên đầu, chỉ đường
        /// dẫn tới chỗ cần đến. Luật chọn tự động nằm ở QuestSystem.
        /// </summary>
        public string Tracked
        {
            get => tracked;
            set
            {
                if (string.IsNullOrEmpty(value)) value = null;
                if (tracked == value) return;
                tracked = value;
                Changed?.Invoke();
            }
        }

        /// <summary>Số ngày tính theo giờ máy, đổi lúc nửa đêm: việc hằng ngày làm lại được khi số này tăng.</summary>
        public static int Today => (int)(DateTime.Now.Date - new DateTime(2000, 1, 1)).TotalDays;

        public void Load(List<AffinityRecord> savedAffinity, List<QuestRecord> savedQuests, List<string> savedFlags, string savedTracked = null)
        {
            affinity.Clear();
            quests.Clear();
            flags.Clear();
            enemies.Clear();
            // greeted giữ nguyên: là chuyện của phiên chơi, không nằm trong bản lưu. Client online nạp lại cốt truyện mỗi lần
            // server gửi bản chuẩn (ProfileSync); xóa ở đây thì lần bắt chuyện sau lại cộng thân thiện như lần đầu.
            tracked = string.IsNullOrEmpty(savedTracked) ? null : savedTracked;
            if (savedAffinity != null)
                foreach (AffinityRecord a in savedAffinity)
                    if (a != null && !string.IsNullOrEmpty(a.npc)) affinity[a.npc] = Clamp(a.value);
            if (savedQuests != null)
                foreach (QuestRecord q in savedQuests)
                    if (q != null && !string.IsNullOrEmpty(q.quest)) quests[q.quest] = Copy(q);
            if (savedFlags != null)
                foreach (string f in savedFlags)
                    if (!string.IsNullOrEmpty(f)) AddFlag(f);
            Changed?.Invoke();
        }

        public void Save(List<AffinityRecord> intoAffinity, List<QuestRecord> intoQuests, List<string> intoFlags)
        {
            intoAffinity.Clear();
            foreach (var pair in affinity) intoAffinity.Add(new AffinityRecord { npc = pair.Key, value = pair.Value });
            intoQuests.Clear();
            foreach (QuestRecord q in quests.Values) intoQuests.Add(Copy(q));
            intoFlags.Clear();
            intoFlags.AddRange(flags);
        }

        // ---------------- cờ ----------------

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void SetFlag(string flag, bool on)
        {
            if (string.IsNullOrEmpty(flag)) return;
            bool changed = on ? AddFlag(flag) : RemoveFlag(flag);
            if (changed) Changed?.Invoke();
        }

        // mọi chỗ thêm / bỏ cờ đi qua đây để tập enemies luôn khớp cờ EnemyFlag
        bool AddFlag(string flag)
        {
            if (!flags.Add(flag)) return false;
            if (flag.StartsWith(EnemyFlag, StringComparison.Ordinal)) enemies.Add(flag.Substring(EnemyFlag.Length));
            return true;
        }

        bool RemoveFlag(string flag)
        {
            if (!flags.Remove(flag)) return false;
            if (flag.StartsWith(EnemyFlag, StringComparison.Ordinal)) enemies.Remove(flag.Substring(EnemyFlag.Length));
            return true;
        }

        // ---------------- thân thiện ----------------

        public bool IsEnemy(string npc) => npc != null && enemies.Contains(npc);

        /// <summary>Độ thân thiện với NPC; chưa gặp bao giờ thì là startValue (NpcTemplate.startAffinity).</summary>
        public int GetAffinity(string npc, int startValue) => affinity.TryGetValue(npc, out int v) ? v : Clamp(startValue);

        /// <summary>
        /// Cộng (âm là trừ) thân thiện; trả về lượng thực sự đổi sau khi kẹp trong [-100, 100]. Tụt tới
        /// <see cref="Affinity.EnemyAt"/> thì thành kẻ thù vĩnh viễn; kẻ thù thì không đổi được nữa.
        /// </summary>
        public int AddAffinity(string npc, int startValue, int amount)
        {
            if (IsEnemy(npc)) return 0;
            int before = GetAffinity(npc, startValue);
            int after = Clamp(before + amount);
            affinity[npc] = after;
            if (after <= Affinity.EnemyAt) MakeEnemy(npc, false);
            if (after != before) Changed?.Invoke();
            return after - before;
        }

        /// <summary>Biến NPC thành kẻ thù vĩnh viễn (lựa chọn cốt truyện, hoặc thân thiện tụt quá sâu).</summary>
        public void MakeEnemy(string npc, bool notify = true)
        {
            if (!AddFlag(EnemyFlag + npc)) return;
            if (!affinity.TryGetValue(npc, out int v) || v > Affinity.EnemyAt) affinity[npc] = Affinity.EnemyAt;
            BecameEnemy?.Invoke(npc);
            if (notify) Changed?.Invoke();
        }

        /// <summary>true nếu đây là lần chào đầu tiên trong phiên với NPC này.</summary>
        public bool MarkGreeted(string npc) => greeted.Add(npc);

        // ---------------- nhiệm vụ ----------------

        public QuestRecord Get(string quest) => quests.TryGetValue(quest, out QuestRecord r) ? r : null;

        public QuestRecord Start(string quest)
        {
            QuestRecord r = Get(quest) ?? (quests[quest] = new QuestRecord { quest = quest });
            r.active = true;
            r.progress.Clear();
            Changed?.Invoke();
            return r;
        }

        /// <summary>Thôi làm. finished: đã trả (ghi kết cục, ngày, đặt cờ done:quest); không thì là bỏ ngang.</summary>
        public void Stop(string quest, bool finished, string outcome = null)
        {
            QuestRecord r = Get(quest);
            if (r == null) return;
            r.active = false;
            r.progress.Clear();
            if (finished)
            {
                r.timesDone++;
                r.outcome = outcome;
                r.lastDay = Today;
                flags.Add(DoneFlag + quest);
            }
            else if (r.timesDone == 0) quests.Remove(quest);   // bỏ ngang lần đầu: như chưa nhận bao giờ
            Changed?.Invoke();
        }

        public void NotifyChanged() => Changed?.Invoke();

        static int Clamp(int v) => Math.Max(Affinity.Min, Math.Min(Affinity.Max, v));

        static QuestRecord Copy(QuestRecord q) => new QuestRecord
        {
            quest = q.quest, active = q.active, timesDone = q.timesDone, outcome = q.outcome, lastDay = q.lastDay,
            progress = q.progress != null ? new List<int>(q.progress) : new List<int>(),
        };
    }
}
