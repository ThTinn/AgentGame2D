using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;
using Game.Enemies;
using Game.Items;
using Game.Npcs;
using Game.Players;
using static Game.Story.StoryText;

namespace Game.Story
{
    /// <summary>Dấu trên đầu NPC: có việc mới để nhận (!), có việc đã xong chờ trả / cần gặp (?), hay là kẻ thù.</summary>
    public enum NpcMark : byte { None, Available, Ready, Enemy }

    /// <summary>Một mục tiêu của một nhiệm vụ (vd mục tiêu Talk đang chờ ở một NPC).</summary>
    public struct QuestStep
    {
        public QuestTemplate quest;
        public int objective;

        public QuestObjective Objective => quest.objectives[objective];
    }

    /// <summary>Chỉ đường tới đâu: gặp một NPC, tới một chỗ, hay tìm một loại quái.</summary>
    public enum GoalKind : byte { None, Npc, Place, Monster }

    /// <summary>
    /// Bước tiếp theo của một nhiệm vụ, cho phần chỉ đường (bên scene đổi ra toạ độ thật: NPC đang đứng đâu, bãi quái
    /// nào, cổng nào dẫn sang map khác).
    /// </summary>
    public struct QuestGoal
    {
        public GoalKind kind;
        public string npc;            // Npc: NpcTemplate.name
        public int monsterId;         // Monster: id loại quái (0 = không rõ)
        public QuestLocation where;   // Place: chỗ cần tới. Monster: bãi nên tới (null / chưa đặt = tự tìm). None: null
        public string label;          // dòng hiện cạnh mũi tên: "Gặp Bà Diệp", "Hạ Slime xanh 2/5"
    }

    /// <summary>Kết quả trả một nhiệm vụ, để hộp thoại viết ra người chơi nhận được gì.</summary>
    public struct QuestReward
    {
        public long gold, exp;
        public AffinityTier tier;        // bậc thân thiện với giver lúc trả (quyết định hệ số thưởng)
        public string items;             // "Chìa nhỏ x1, Chìa sắt x1"; null = không có
        public bool missedBonus;         // có quà thêm nhưng chưa đủ thân thiện
        public string affinity;          // "Lão Thạch +10, Thầy Uyên -5"
        public string ending;            // tên kết cục chương (QuestOutcome.ending); null = không phải kết cục
        public string enemies;           // những NPC vừa thành kẻ thù vì lựa chọn này; null = không ai
    }

    /// <summary>
    /// Luật của cốt truyện: độ thân thiện với NPC, nhiệm vụ, lựa chọn và kết cục. Dữ liệu nằm ở <see cref="StoryState"/> của
    /// người chơi mình (Player.Story), danh mục ở <see cref="QuestDatabase"/> và <see cref="NpcDatabase"/>.
    ///  - Thân thiện -100..100, 7 bậc (<see cref="Affinity"/>). Tụt tới -50 là kẻ thù vĩnh viễn: không nói chuyện, việc đang
    ///    làm với người đó tự hủy. Ác cảm (âm) thì vẫn nói chuyện nhưng không giao việc.
    ///  - Nhiệm vụ chỉ được giao khi đủ minTier với giver và đủ điều kiện (cờ cốt truyện...). Thưởng vàng + EXP nhân hệ số
    ///    theo bậc; nhiệm vụ có nhiều cách giải quyết (outcomes) thì người chơi chọn lúc trả, mỗi cách một hậu quả.
    ///  - Lựa chọn trong hội thoại (StoryChoice) đổi thân thiện, đặt cờ, biến người thành kẻ thù.
    ///  - Việc hằng ngày (QuestCategory.Daily) làm được một lần mỗi ngày theo giờ máy.
    ///  - Mục tiêu Kill đếm qua <see cref="OnMonsterKilled"/> (CoreSystems.GrantKillRewards gọi khi hạ quái); Collect đếm
    ///    thẳng trong túi; Reach qua <see cref="OnPlayerAt"/> (chỉ đường bên scene báo vị trí); Talk khi hộp thoại NPC mở
    ///    (<see cref="CompleteTalk"/>).
    ///  - Theo dõi: một nhiệm vụ được theo (<see cref="Tracked"/>), <see cref="GoalOf"/> cho biết bước tiếp theo để chỉ đường.
    /// Online: client chạy trước cho UI phản hồi ngay, server chạy bản Java (ServerJava/src/game/story) rồi gửi bản chuẩn ghi đè.
    /// </summary>
    public static class QuestSystem
    {
        /// <summary>
        /// Thân thiện / nhiệm vụ / cờ / túi đồ vừa đổi: NPC vẽ lại dấu trên đầu, nhật ký vẽ lại. Khi đang chơi, mọi thay đổi
        /// trong cùng một khung hình gom thành một lần bắn (cuối khung, LateUpdate).
        /// </summary>
        public static event Action Changed;

        static bool changePending;
        static ChangeDispatcher dispatcher;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            changePending = false;
            dispatcher = null;
        }

        /// <summary>
        /// CoreSystems nối StoryState.Changed và Inventory.Changed của người chơi mình vào đây. Một thao tác thường đổi nhiều
        /// thứ liền nhau (trả việc: túi, vàng, tiến độ, thân thiện, việc theo dõi...): đang chơi thì chỉ đánh dấu, cuối khung
        /// mới bắn <see cref="Changed"/> một lần. Ngoài Play mode (Editor, test) thì bắn ngay.
        /// </summary>
        public static void NotifyChanged()
        {
            if (!Application.isPlaying)
            {
                FlushChanged();
                return;
            }
            changePending = true;
            if (dispatcher == null)
            {
                var go = new GameObject("[QuestSystem]") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                dispatcher = go.AddComponent<ChangeDispatcher>();
            }
        }

        /// <summary>Bắn <see cref="Changed"/> ngay (bỏ lượt đang chờ cuối khung).</summary>
        public static void FlushChanged()
        {
            changePending = false;
            Changed?.Invoke();
        }

        sealed class ChangeDispatcher : MonoBehaviour
        {
            void LateUpdate()
            {
                if (changePending) FlushChanged();
            }
        }

        static readonly StringBuilder line = new StringBuilder(128);
        static readonly List<QuestRecord> recordBuffer = new List<QuestRecord>(8);
        static readonly List<QuestStep> stepBuffer = new List<QuestStep>(4);

        static PlayerProfile Me => CoreSystems.Me;
        static StoryState State => Me != null ? Me.Story : null;

        // ---------------- thân thiện ----------------

        public static int AffinityOf(NpcTemplate npc)
        {
            StoryState s = State;
            return npc == null ? 0 : s != null ? s.GetAffinity(npc.name, npc.startAffinity) : npc.startAffinity;
        }

        public static AffinityTier TierOf(NpcTemplate npc) =>
            npc != null && IsEnemy(npc.name) ? AffinityTier.Enemy : Affinity.TierOf(AffinityOf(npc));

        public static bool IsEnemy(string npcName) => State != null && !string.IsNullOrEmpty(npcName) && State.IsEnemy(npcName);

        /// <summary>Cộng thân thiện với NPC theo tên (NPC lạ trong bảng thì bỏ qua). Trả lượng thực sự đổi.</summary>
        public static int AddAffinity(string npcName, int amount)
        {
            StoryState s = State;
            NpcTemplate npc = Npc(npcName);
            if (s == null || npc == null || amount == 0) return 0;
            return s.AddAffinity(npc.name, npc.startAffinity, amount);
        }

        /// <summary>Bắt chuyện: lần đầu trong phiên thì thân thêm một chút (kẻ thù thì không).</summary>
        public static void Greet(NpcTemplate npc)
        {
            StoryState s = State;
            if (s == null || npc == null || !NetSync.CanAct || s.IsEnemy(npc.name)) return;
            if (s.MarkGreeted(npc.name)) s.AddAffinity(npc.name, npc.startAffinity, Affinity.GreetGain);
        }

        /// <summary>Câu chào theo bậc thân thiện.</summary>
        public static string GreetingOf(NpcTemplate npc)
        {
            AffinityTier tier = TierOf(npc);
            if (tier <= AffinityTier.Disliked)
                return T(!string.IsNullOrEmpty(npc.greetingDisliked) ? npc.greetingDisliked
                       : !string.IsNullOrEmpty(npc.greetingStranger) ? npc.greetingStranger : npc.greeting);
            if (tier == AffinityTier.Stranger && !string.IsNullOrEmpty(npc.greetingStranger)) return T(npc.greetingStranger);
            if (tier >= AffinityTier.Trusted && !string.IsNullOrEmpty(npc.greetingTrusted)) return T(npc.greetingTrusted);
            return T(npc.greeting);
        }

        /// <summary>Câu kẻ thù nói khi người chơi bắt chuyện (rồi quay lưng).</summary>
        public static string ShunLineOf(NpcTemplate npc) =>
            !string.IsNullOrEmpty(npc.shunLine) ? T(npc.shunLine)
                : L("{0} quay lưng, không thèm nói chuyện với ngươi.|{0} turns away and won't talk to you.", T(npc.Label));

        /// <summary>Giá dịch vụ của NPC sau khi tính bậc thân thiện.</summary>
        public static long PriceAt(NpcTemplate npc, long basePrice) => Affinity.Price(basePrice, TierOf(npc));

        /// <summary>Món NPC thích mà người chơi đang có trong túi.</summary>
        public static void GiftsInBag(NpcTemplate npc, List<NpcGift> into)
        {
            into.Clear();
            PlayerProfile me = Me;
            ItemDatabase items = ItemDatabase.Current;
            if (me == null || items == null || npc?.gifts == null) return;
            foreach (NpcGift g in npc.gifts)
            {
                ItemTemplate it = items.Find(g.item);
                if (it != null && me.Inventory.Has(it.id)) into.Add(g);
            }
        }

        /// <summary>Tặng một món NPC thích: mất 1 món trong túi, tăng thân thiện. Trả lượng thân thiện tăng (0 = không tặng được).</summary>
        public static int GiveGift(NpcTemplate npc, NpcGift gift)
        {
            if (!Guard() || npc == null || gift == null || IsEnemy(npc.name)) return 0;
            if (AffinityOf(npc) >= Affinity.Max) return 0;   // đã Tri kỷ tối đa: đừng lấy mất món đồ mà không được gì
            ItemTemplate it = ItemDatabase.Current != null ? ItemDatabase.Current.Find(gift.item) : null;
            PlayerProfile me = Me;
            if (it == null || me == null || !me.Inventory.Remove(it.id)) return 0;
            NetSync.Send?.Gift(npc.name, gift.item);
            return State.AddAffinity(npc.name, npc.startAffinity, gift.affinity);
        }

        // ---------------- điều kiện và hậu quả ----------------

        /// <summary>Điều kiện có đúng lúc này không. null / trống = đúng.</summary>
        public static bool Check(StoryCondition c)
        {
            if (c == null || c.IsEmpty) return true;
            StoryState s = State;
            if (s == null) return false;
            if (!string.IsNullOrEmpty(c.requiresFlag) && !s.HasFlag(c.requiresFlag)) return false;
            if (!string.IsNullOrEmpty(c.forbidsFlag) && s.HasFlag(c.forbidsFlag)) return false;
            if (!string.IsNullOrEmpty(c.tierNpc) && TierOf(Npc(c.tierNpc)) < c.minTier) return false;
            if (c.minAllies > 0 && AlliesAt(c.allyTier) < c.minAllies) return false;
            if (!string.IsNullOrEmpty(c.item) && CountInBag(c.item) < c.itemCount) return false;
            return true;
        }

        /// <summary>Vì sao điều kiện chưa đúng, một dòng cho người chơi đọc ("cần 4 người Tin cậy (đang có 2)"). Đúng rồi thì null.</summary>
        public static string Explain(StoryCondition c)
        {
            if (Check(c)) return null;
            line.Clear();
            void Part(string s) { if (line.Length > 0) line.Append("; "); line.Append(s); }
            if (!string.IsNullOrEmpty(c.tierNpc) && TierOf(Npc(c.tierNpc)) < c.minTier)
            {
                NpcTemplate n = Npc(c.tierNpc);
                Part(L("cần {0} với {1}|needs {0} with {1}", Affinity.NameOf(c.minTier), n != null ? T(n.Label) : c.tierNpc));
            }
            if (c.minAllies > 0)
            {
                int have = AlliesAt(c.allyTier);
                if (have < c.minAllies) Part(L("cần {0} người {1} (đang có {2})|needs {0} people at {1} (have {2})", c.minAllies, Affinity.NameOf(c.allyTier), have));
            }
            if (!string.IsNullOrEmpty(c.item) && CountInBag(c.item) < c.itemCount)
                Part(L("cần {0} {1} (đang có {2})|needs {0} {1} (have {2})", c.itemCount, ItemLabel(c.item), CountInBag(c.item)));
            if (line.Length == 0) Part(L("chưa đủ điều kiện|requirements not met"));   // cờ cốt truyện: không nói trước
            return line.ToString();
        }

        /// <summary>Số NPC (không tính kẻ thù) thân với người chơi từ bậc tier trở lên.</summary>
        public static int AlliesAt(AffinityTier tier)
        {
            NpcDatabase db = NpcDatabase.Current;
            if (db == null) return 0;
            int n = 0;
            foreach (NpcTemplate npc in db.npcs)
                if (npc != null && TierOf(npc) >= tier) n++;
            return n;
        }

        /// <summary>
        /// Áp các hậu quả. Ghi tóm tắt đổi thân thiện vào `affinity`, ai vừa thành kẻ thù vào `enemies` (để hiện ra cho người chơi).
        /// </summary>
        static void Apply(List<StoryEffect> effects, StringBuilder affinity, StringBuilder enemies)
        {
            if (effects == null) return;
            StoryState s = State;
            foreach (StoryEffect e in effects)
            {
                if (e == null) continue;
                switch (e.kind)
                {
                    case EffectKind.Affinity:
                        bool wasEnemy = IsEnemy(e.target);
                        int gained = AddAffinity(e.target, e.amount);
                        if (gained != 0 && affinity != null)
                        {
                            if (affinity.Length > 0) affinity.Append(", ");
                            affinity.Append(LabelOf(e.target)).Append(gained > 0 ? " +" : " ").Append(gained);
                        }
                        if (!wasEnemy && IsEnemy(e.target)) Append(enemies, LabelOf(e.target));
                        break;
                    case EffectKind.SetFlag:
                        s.SetFlag(e.target, true);
                        break;
                    case EffectKind.ClearFlag:
                        s.SetFlag(e.target, false);
                        break;
                    case EffectKind.MakeEnemy:
                        if (Npc(e.target) == null || s.IsEnemy(e.target)) break;
                        s.MakeEnemy(e.target);
                        Append(enemies, LabelOf(e.target));
                        break;
                    case EffectKind.Gold:
                        if (Me != null) Me.AddGold(e.amount);
                        break;
                }
            }
        }

        static void Append(StringBuilder sb, string s)
        {
            if (sb == null) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(s);
        }

        /// <summary>Lấy vật phẩm điều kiện đòi (chọn xong thì nộp).</summary>
        static void Consume(StoryCondition c)
        {
            if (c == null || string.IsNullOrEmpty(c.item) || Me == null) return;
            ItemTemplate it = ItemDatabase.Current != null ? ItemDatabase.Current.Find(c.item) : null;
            if (it != null) Me.Inventory.Remove(it.id, c.itemCount);
        }

        // ---------------- lựa chọn trong hội thoại ----------------

        /// <summary>Lựa chọn còn hiện: đã chọn rồi (có cờ flag) thì ẩn. Điều kiện chưa đủ vẫn hiện (nút mờ).</summary>
        public static bool IsShown(StoryChoice c) => c != null && (string.IsNullOrEmpty(c.flag) || State == null || !State.HasFlag(c.flag));

        /// <summary>
        /// Người chơi chọn một câu đáp: nộp vật phẩm (nếu điều kiện đòi), áp hậu quả, đặt cờ chọn-một-lần.
        /// Trả tóm tắt để hiện ("Lão Thạch -10"), null khi không có gì đáng nói; false khi không chọn được.
        /// </summary>
        public static bool Choose(StoryChoice c, out string summary)
        {
            summary = null;
            if (!Guard() || c == null || !IsShown(c) || !Check(c.condition)) return false;
            NetSync.Send?.Choice(OwnerOf(c), c.name);
            Consume(c.condition);
            var affinity = new StringBuilder();
            var enemies = new StringBuilder();
            Apply(c.effects, affinity, enemies);
            if (!string.IsNullOrEmpty(c.flag)) State.SetFlag(c.flag, true);
            summary = Summarize(affinity, enemies);
            return true;
        }

        /// <summary>
        /// Câu đáp này nằm ở đâu, để gói tin gửi mã thay cho object: "npc:&lt;npc&gt;/&lt;lựa chọn&gt;" (câu đáp sau lời kể
        /// của NPC) hoặc "quest:&lt;nhiệm vụ&gt;" (câu đáp lúc được giao việc). null khi không thuộc bảng nào.
        /// </summary>
        public static string OwnerOf(StoryChoice c)
        {
            if (c == null) return null;
            QuestDatabase quests = QuestDatabase.Current;
            if (quests != null)
                foreach (QuestTemplate q in quests.quests)
                    if (q?.offerChoices != null && q.offerChoices.Contains(c)) return "quest:" + q.name;
            NpcDatabase npcs = NpcDatabase.Current;
            if (npcs != null)
                foreach (NpcTemplate n in npcs.npcs)
                {
                    if (n?.options == null) continue;
                    foreach (NpcOption o in n.options)
                        if (o?.choices != null && o.choices.Contains(c)) return "npc:" + n.name + "/" + o.name;
                }
            return null;
        }

        /// <summary>Ngược với <see cref="OwnerOf"/>: tìm câu đáp theo chỗ + mã (server tra gói tin). null khi không có.</summary>
        public static StoryChoice FindChoice(string owner, string choice)
        {
            if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(choice)) return null;
            List<StoryChoice> list = null;
            if (owner.StartsWith("quest:", StringComparison.Ordinal))
                list = QuestDatabase.Current?.Find(owner.Substring(6))?.offerChoices;
            else if (owner.StartsWith("npc:", StringComparison.Ordinal))
            {
                int slash = owner.IndexOf('/');
                NpcTemplate n = slash > 4 ? Npc(owner.Substring(4, slash - 4)) : null;
                string option = slash > 0 ? owner.Substring(slash + 1) : null;
                if (n?.options != null)
                    foreach (NpcOption o in n.options)
                        if (o != null && o.name == option) list = o.choices;
            }
            if (list == null) return null;
            foreach (StoryChoice c in list) if (c != null && c.name == choice) return c;
            return null;
        }

        static string Summarize(StringBuilder affinity, StringBuilder enemies)
        {
            if (affinity.Length == 0 && enemies.Length == 0) return null;
            var sb = new StringBuilder();
            if (affinity.Length > 0) sb.Append(L("Thân thiện: {0}|Affinity: {0}", affinity));
            if (enemies.Length > 0) sb.Append(sb.Length > 0 ? "\n" : "").Append(L("Thành kẻ thù: {0}|Now enemies: {0}", enemies));
            return sb.ToString();
        }

        // ---------------- nhiệm vụ: trạng thái ----------------

        public static QuestStatus StatusOf(QuestTemplate q)
        {
            StoryState s = State;
            if (q == null || s == null) return QuestStatus.Locked;
            // người giao / nhận là kẻ thù: việc này không còn tồn tại với người chơi
            if (s.IsEnemy(q.giver) || s.IsEnemy(q.TurnInNpc)) return QuestStatus.Locked;

            QuestRecord r = s.Get(q.name);
            if (r != null && r.active) return ObjectivesMet(q, r) ? QuestStatus.Ready : QuestStatus.Active;
            if (r != null && r.timesDone > 0)
            {
                if (q.IsDaily) { if (r.lastDay == StoryState.Today) return QuestStatus.Done; }
                else if (!q.repeatable) return QuestStatus.Done;
            }
            // phải gặp một kẻ thù (mục tiêu Talk): kẻ thù không nói chuyện, nhận vào cũng không làm xong được
            if (TalksToEnemy(q, s)) return QuestStatus.Locked;
            if (!string.IsNullOrEmpty(q.requires))
            {
                QuestRecord pre = s.Get(q.requires);
                if (pre == null || pre.timesDone == 0) return QuestStatus.Locked;
            }
            if (!Check(q.condition)) return QuestStatus.Locked;
            return QuestStatus.Available;
        }

        static bool TalksToEnemy(QuestTemplate q, StoryState s)
        {
            foreach (QuestObjective o in q.objectives)
                if (o != null && o.kind == ObjectiveKind.Talk && s.IsEnemy(o.target)) return true;
            return false;
        }

        /// <summary>Người chơi đủ thân thiện với giver để được giao nhiệm vụ này chưa.</summary>
        public static bool TierAllows(QuestTemplate q) => q != null && TierOf(Npc(q.giver)) >= q.minTier;

        /// <summary>
        /// Nhiệm vụ cần hiện ở NPC này: việc nó giao (nhận được, đang làm, chờ trả) và việc người khác giao mà trả ở đây
        /// (chờ trả). Bị khóa vì chưa xong việc trước thì ẩn; chưa đủ thân thiện thì vẫn hiện để NPC từ chối.
        /// </summary>
        public static void QuestsAt(NpcTemplate npc, List<QuestTemplate> into)
        {
            into.Clear();
            QuestDatabase db = QuestDatabase.Current;
            if (db == null || npc == null) return;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null) continue;
                QuestStatus st = StatusOf(q);
                bool giver = q.giver == npc.name, turnIn = q.TurnInNpc == npc.name;
                if (st == QuestStatus.Ready && turnIn) into.Add(q);
                else if (giver && (st == QuestStatus.Available || st == QuestStatus.Active || st == QuestStatus.Ready)) into.Add(q);
            }
        }

        /// <summary>
        /// Dấu trên đầu NPC: kẻ thù; ? khi có việc chờ trả ở đây hoặc việc đang làm cần gặp người này; ! khi có việc mới mà
        /// người chơi đủ thân để nhận.
        /// </summary>
        public static NpcMark MarkOf(NpcTemplate npc)
        {
            QuestDatabase db = QuestDatabase.Current;
            if (npc == null || State == null) return NpcMark.None;
            if (IsEnemy(npc.name)) return NpcMark.Enemy;
            if (db == null) return NpcMark.None;
            bool available = false;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null) continue;
                QuestStatus st = StatusOf(q);
                if (st == QuestStatus.Ready && q.TurnInNpc == npc.name) return NpcMark.Ready;
                if (st == QuestStatus.Active && PendingTalk(q, npc.name) >= 0) return NpcMark.Ready;
                if (st == QuestStatus.Available && q.giver == npc.name && TierAllows(q)) available = true;
            }
            return available ? NpcMark.Available : NpcMark.None;
        }

        // ---------------- nhiệm vụ: mục tiêu ----------------

        /// <summary>
        /// Tiến độ một mục tiêu (kẹp ở count): Kill theo số đã hạ, Reach / Talk 1 khi đã tới / đã gặp, Collect theo số đang
        /// có trong túi.
        /// </summary>
        public static int ProgressOf(QuestTemplate q, int objective)
        {
            QuestObjective o = q.objectives[objective];
            int n;
            if (o.Tracked)
            {
                QuestRecord r = State?.Get(q.name);
                n = r != null && r.active ? r.Progress(objective) : 0;
            }
            else n = CountInBag(o.target);
            return Mathf.Min(n, o.count);
        }

        public static string LabelOf(QuestObjective o)
        {
            if (!string.IsNullOrEmpty(o.label)) return T(o.label);
            switch (o.kind)
            {
                case ObjectiveKind.Kill:
                    MonsterTemplate m = MonsterOf(o.target);
                    return L("Hạ {0}|Defeat {0}", m != null && !string.IsNullOrEmpty(m.displayName) ? T(m.displayName) : o.target);
                case ObjectiveKind.Reach:
                    return string.IsNullOrEmpty(o.target) ? L("Đến nơi đánh dấu|Reach the marked place") : L("Đến {0}|Reach {0}", T(o.target));
                case ObjectiveKind.Talk:
                    return L("Gặp {0}|Talk to {0}", LabelOf(o.target));
                default:
                    return L("Mang {0}|Bring {0}", ItemLabel(o.target));
            }
        }

        /// <summary>
        /// Id loại quái của target mục tiêu Kill: viết bằng số thì là id luôn (Monster_Template.idTemplate), không thì tra
        /// MonsterTemplate.name. 0 = không rõ.
        /// </summary>
        public static int MonsterIdOf(string target)
        {
            if (string.IsNullOrEmpty(target)) return 0;
            if (int.TryParse(target, out int id)) return id;
            MonsterTemplate m = MonsterDatabase.Current != null ? MonsterDatabase.Current.Find(target) : null;
            return m != null ? m.id : 0;
        }

        static MonsterTemplate MonsterOf(string target)
        {
            MonsterDatabase db = MonsterDatabase.Current;
            if (db == null || string.IsNullOrEmpty(target)) return null;
            return int.TryParse(target, out int id) ? db.Get(id) : db.Find(target);
        }

        /// <summary>Mục tiêu Kill này có tính con quái loại templateId (tên trong MonsterDatabase, hoặc id viết bằng số) không.</summary>
        static bool Matches(QuestObjective o, int templateId, MonsterTemplate t)
        {
            if (o.kind != ObjectiveKind.Kill || string.IsNullOrEmpty(o.target)) return false;
            if (int.TryParse(o.target, out int id)) return id == templateId;
            return t != null && o.target == t.name;
        }

        /// <summary>Mỗi mục tiêu một dòng "Hạ Slime: 3/5". Không có mục tiêu thì là việc đưa tin / quyết định.</summary>
        public static string ProgressText(QuestTemplate q)
        {
            line.Clear();
            if (q.objectives.Count == 0) return L("Đến gặp {0}|Go see {0}", LabelOf(q.TurnInNpc));
            for (int i = 0; i < q.objectives.Count; i++)
            {
                if (i > 0) line.Append('\n');
                line.Append(LabelOf(q.objectives[i])).Append(": ").Append(ProgressOf(q, i)).Append('/').Append(q.objectives[i].count);
            }
            return line.ToString();
        }

        static bool ObjectivesMet(QuestTemplate q, QuestRecord r)
        {
            for (int i = 0; i < q.objectives.Count; i++)
            {
                QuestObjective o = q.objectives[i];
                int n = o.Tracked ? r.Progress(i) : CountInBag(o.target);
                if (n < o.count) return false;
            }
            return true;
        }

        // ghi tiến độ một mục tiêu Kill / Reach / Talk và báo người chơi; vừa đủ hết mục tiêu thì báo quay lại trả
        static void Advance(QuestTemplate q, QuestRecord r, int objective, int value)
        {
            QuestObjective o = q.objectives[objective];
            bool wasReady = ObjectivesMet(q, r);
            r.SetProgress(objective, Mathf.Min(value, o.count));
            if (o.count > 1) Notify.Toast(T(q.title) + ": " + LabelOf(o) + " " + r.Progress(objective) + "/" + o.count, 1.5f);
            else Notify.Toast(L("{0}: {1} - xong|{0}: {1} - done", T(q.title), LabelOf(o)), 2f);
            if (!wasReady && ObjectivesMet(q, r))
                Notify.Toast(L("Xong việc \"{0}\": quay lại gặp {1}|\"{0}\" done: return to {1}", T(q.title), LabelOf(q.TurnInNpc)), 3f);
        }

        /// <summary>Chỉ số mục tiêu Talk còn chờ gặp NPC này trong nhiệm vụ đang làm; -1 = không có.</summary>
        static int PendingTalk(QuestTemplate q, string npcName)
        {
            QuestRecord r = State?.Get(q.name);
            if (r == null || !r.active) return -1;
            for (int i = 0; i < q.objectives.Count; i++)
            {
                QuestObjective o = q.objectives[i];
                if (o.kind == ObjectiveKind.Talk && o.target == npcName && r.Progress(i) < o.count) return i;
            }
            return -1;
        }

        // ---------------- nhiệm vụ: nhận / bỏ / trả ----------------

        /// <summary>
        /// Nhận nhiệm vụ. False khi chưa mở, đã nhận, hoặc chưa đủ thân thiện. Việc chính tuyến (hoặc khi chưa theo dõi gì)
        /// được theo dõi luôn để chỉ đường.
        /// </summary>
        public static bool Accept(QuestTemplate q)
        {
            if (!Guard() || StatusOf(q) != QuestStatus.Available || !TierAllows(q)) return false;
            QuestTemplate tracked = Tracked;
            State.Start(q.name);
            NetSync.Send?.Accept(q.name);
            if (tracked == null || tracked == q || q.category == QuestCategory.Main) State.Tracked = q.name;
            Notify.Toast(L("Nhận nhiệm vụ: {0}|Quest accepted: {0}", T(q.title)), 2.5f);
            return true;
        }

        /// <summary>Bỏ ngang: NPC giao việc phật lòng (trừ <see cref="Affinity.AbandonPenalty"/>).</summary>
        public static void Abandon(QuestTemplate q)
        {
            if (!Guard() || q == null) return;
            QuestStatus st = StatusOf(q);
            if (st != QuestStatus.Active && st != QuestStatus.Ready) return;
            NetSync.Send?.Abandon(q.name);
            State.Stop(q.name, false);
            AddAffinity(q.giver, -Affinity.AbandonPenalty);
            Notify.Toast(L("Đã bỏ nhiệm vụ: {0}|Quest abandoned: {0}", T(q.title)));
            if (State.Tracked == q.name) State.Tracked = null;
            Retrack();
        }

        /// <summary>Các cách giải quyết người chơi được thấy lúc trả (cả cách chưa đủ điều kiện, để nút mờ kèm lý do).</summary>
        public static bool HasOutcomes(QuestTemplate q) => q != null && q.outcomes != null && q.outcomes.Count > 0;

        /// <summary>
        /// Trả nhiệm vụ đã đủ mục tiêu: lấy vật phẩm cần nộp, phát thưởng theo bậc thân thiện, cộng thân thiện. Nhiệm vụ có
        /// nhiều cách giải quyết thì phải truyền outcome (đủ điều kiện): thêm thưởng, hậu quả và có thể là kết cục chương.
        /// </summary>
        public static bool TurnIn(QuestTemplate q, QuestOutcome outcome, out QuestReward reward)
        {
            reward = default;
            PlayerProfile me = Me;
            if (!Guard() || me == null || StatusOf(q) != QuestStatus.Ready) return false;
            if (HasOutcomes(q) && (outcome == null || !q.outcomes.Contains(outcome) || !Check(outcome.condition))) return false;
            if (outcome != null && !CanPayBoth(q, outcome.condition)) return false;
            NetSync.Send?.TurnIn(q.name, outcome?.name);

            ItemDatabase items = ItemDatabase.Current;
            foreach (QuestObjective o in q.objectives)
            {
                if (o.kind != ObjectiveKind.Collect) continue;
                ItemTemplate it = items != null ? items.Find(o.target) : null;
                if (it != null) me.Inventory.Remove(it.id, o.count);
            }
            if (outcome != null) Consume(outcome.condition);

            // thưởng tính theo bậc lúc trả, trước khi cộng thân thiện của chính nhiệm vụ này
            AffinityTier tier = TierOf(Npc(q.giver));
            float scale = Affinity.RewardScaleOf(tier);
            long gold = q.gold + (outcome != null ? outcome.gold : 0);
            long exp = q.exp + (outcome != null ? outcome.exp : 0);
            reward.tier = tier;
            reward.gold = gold > 0 ? Math.Max(1, (long)Math.Round(gold * scale)) : 0;
            reward.exp = exp > 0 ? Math.Max(1, (long)Math.Round(exp * scale)) : 0;
            if (reward.gold > 0) me.AddGold(reward.gold);
            if (reward.exp > 0) me.Progress.AddExp(reward.exp);

            line.Clear();
            Give(me, items, q.item, q.itemCount);
            if (outcome != null) Give(me, items, outcome.item, outcome.itemCount);
            if (!string.IsNullOrEmpty(q.bonusItem))
            {
                if (tier >= q.bonusTier) Give(me, items, q.bonusItem, 1);
                else reward.missedBonus = true;
            }
            reward.items = line.Length > 0 ? line.ToString() : null;

            State.Stop(q.name, true, outcome != null ? outcome.name : null);

            var affinity = new StringBuilder();
            var enemies = new StringBuilder();
            foreach (AffinityGain g in q.affinity)
            {
                int gained = AddAffinity(g.npc, g.amount);
                if (gained == 0) continue;
                if (affinity.Length > 0) affinity.Append(", ");
                affinity.Append(LabelOf(g.npc)).Append(gained > 0 ? " +" : " ").Append(gained);
            }
            if (outcome != null) Apply(outcome.effects, affinity, enemies);
            reward.affinity = affinity.Length > 0 ? affinity.ToString() : null;
            reward.enemies = enemies.Length > 0 ? enemies.ToString() : null;
            reward.ending = outcome != null && !string.IsNullOrEmpty(outcome.ending) ? T(outcome.ending) : null;

            // việc vừa trả đang được theo dõi: chuyển sang việc nên làm tiếp (thường là việc kế của cốt truyện)
            if (State.Tracked == q.name) State.Tracked = null;
            Retrack();
            return true;
        }

        // cách giải quyết đòi nộp cùng loại vật phẩm với mục tiêu Collect: Check chỉ so từng phần, ở đây phải đủ cả hai cộng lại
        static bool CanPayBoth(QuestTemplate q, StoryCondition c)
        {
            if (c == null || string.IsNullOrEmpty(c.item)) return true;
            int need = c.itemCount;
            foreach (QuestObjective o in q.objectives)
                if (o != null && o.kind == ObjectiveKind.Collect && o.target == c.item) need += o.count;
            return CountInBag(c.item) >= need;
        }

        // ghi vào line: "Chìa nhỏ x1"
        static void Give(PlayerProfile me, ItemDatabase items, string item, int count)
        {
            if (string.IsNullOrEmpty(item) || count <= 0) return;
            ItemTemplate it = items != null ? items.Find(item) : null;
            if (it == null)
            {
                Debug.LogWarning("QuestSystem: không có vật phẩm '" + item + "' trong ItemDatabase.");
                return;
            }
            me.Loot(it.id, count);
            if (line.Length > 0) line.Append(", ");
            line.Append(T(it.Label)).Append(" x").Append(count);
        }

        // ---------------- sự kiện game ----------------

        /// <summary>Người chơi mình vừa hạ một con quái loại templateId (id trong MonsterDatabase). CoreSystems.GrantKillRewards gọi.</summary>
        /// <remarks>Game: bản GAME_2D nhận Enemy (entity 2D); ở đây chỉ cần id loại quái.</remarks>
        public static void OnMonsterKilled(int templateId)
        {
            StoryState s = State;
            QuestDatabase db = QuestDatabase.Current;
            if (s == null || db == null || templateId <= 0) return;

            // không có MonsterDatabase (chơi local dùng Monster_Template của Game): mục tiêu ghi id bằng số vẫn đếm được
            MonsterTemplate t = MonsterDatabase.Current != null ? MonsterDatabase.Current.Get(templateId) : null;

            bool changed = false;
            foreach (QuestRecord r in s.Quests)
            {
                if (!r.active) continue;
                QuestTemplate q = db.Find(r.quest);
                if (q == null) continue;

                for (int i = 0; i < q.objectives.Count; i++)
                {
                    QuestObjective o = q.objectives[i];
                    if (!Matches(o, templateId, t) || r.Progress(i) >= o.count) continue;
                    Advance(q, r, i, r.Progress(i) + 1);
                    changed = true;
                }
            }
            if (changed) s.NotifyChanged();
        }

        /// <summary>
        /// Người chơi mình đang đứng ở (x, z) của map mapId. Phần chỉ đường bên scene gọi vài lần mỗi giây: mục tiêu Reach
        /// của việc đang làm mà chỗ đó nằm trong bán kính thì xong.
        /// </summary>
        public static void OnPlayerAt(short mapId, Vector2 xz)
        {
            StoryState s = State;
            QuestDatabase db = QuestDatabase.Current;
            if (s == null || db == null || !NetSync.InServer) return;   // online: server tự tính theo vị trí nhận được

            bool changed = false;
            foreach (QuestRecord r in s.Quests)
            {
                if (!r.active) continue;
                QuestTemplate q = db.Find(r.quest);
                if (q == null) continue;
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    QuestObjective o = q.objectives[i];
                    if (o.kind != ObjectiveKind.Reach || o.where == null || o.where.mapId != mapId || r.Progress(i) >= o.count) continue;
                    if ((xz - o.where.point).sqrMagnitude > o.where.radius * o.where.radius) continue;
                    Advance(q, r, i, o.count);
                    changed = true;
                }
            }
            if (changed) s.NotifyChanged();
        }

        // ---------------- mục tiêu Talk ----------------

        /// <summary>Các mục tiêu Talk đang chờ ở NPC này (việc đang làm, chưa gặp). Hộp thoại NPC cho NPC nói trước menu.</summary>
        public static void TalksAt(NpcTemplate npc, List<QuestStep> into)
        {
            into.Clear();
            QuestDatabase db = QuestDatabase.Current;
            if (db == null || npc == null || State == null || IsEnemy(npc.name)) return;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null || StatusOf(q) != QuestStatus.Active) continue;
                QuestRecord r = State.Get(q.name);
                for (int i = 0; i < q.objectives.Count; i++)
                {
                    QuestObjective o = q.objectives[i];
                    if (o.kind == ObjectiveKind.Talk && o.target == npc.name && r.Progress(i) < o.count)
                        into.Add(new QuestStep { quest = q, objective = i });
                }
            }
        }

        /// <summary>Người chơi đã nghe NPC nói xong cho mục tiêu Talk này: đánh dấu xong. False khi không còn chờ.</summary>
        public static bool CompleteTalk(QuestStep step)
        {
            StoryState s = State;
            if (s == null || !NetSync.CanAct || step.quest == null) return false;
            QuestRecord r = s.Get(step.quest.name);
            if (r == null || !r.active || step.objective < 0 || step.objective >= step.quest.objectives.Count) return false;
            QuestObjective o = step.Objective;
            if (o.kind != ObjectiveKind.Talk || r.Progress(step.objective) >= o.count) return false;
            NetSync.Send?.TalkDone(step.quest.name, step.objective);
            Advance(step.quest, r, step.objective, o.count);
            s.NotifyChanged();
            return true;
        }

        /// <summary>Bắt chuyện mà scene không có hộp thoại: coi như đã nghe hết, mọi mục tiêu Talk ở NPC này xong luôn.</summary>
        public static void OnTalkedTo(NpcTemplate npc)
        {
            TalksAt(npc, stepBuffer);
            foreach (QuestStep step in stepBuffer) CompleteTalk(step);
            stepBuffer.Clear();
        }

        /// <summary>
        /// Một NPC vừa thành kẻ thù (CoreSystems nối StoryState.BecameEnemy vào đây): việc đang làm mà người đó giao, nhận
        /// trả, hoặc còn chờ gặp người đó (mục tiêu Talk chưa xong: kẻ thù không nói chuyện nữa, việc sẽ kẹt mãi) tự hủy,
        /// không trừ thêm thân thiện.
        /// </summary>
        public static void OnBecameEnemy(string npcName)
        {
            StoryState s = State;
            QuestDatabase db = QuestDatabase.Current;
            if (s == null) return;

            Notify.Toast(L("{0} đã coi ngươi là kẻ thù.|{0} now sees you as an enemy.", LabelOf(npcName)), 3f);
            if (db == null) return;

            recordBuffer.Clear();
            foreach (QuestRecord r in s.Quests) if (r.active) recordBuffer.Add(r);   // Stop xóa dòng khỏi bảng: chép ra trước
            foreach (QuestRecord r in recordBuffer)
            {
                QuestTemplate q = db.Find(r.quest);
                if (q != null && (q.giver == npcName || q.TurnInNpc == npcName || PendingTalk(q, npcName) >= 0)) s.Stop(q.name, false);
            }
            recordBuffer.Clear();
            Retrack();
        }

        // ---------------- theo dõi + chỉ đường ----------------

        static bool Trackable(QuestStatus st) => st == QuestStatus.Available || st == QuestStatus.Active || st == QuestStatus.Ready;

        /// <summary>Nhiệm vụ đang theo dõi; null khi không theo gì hoặc việc đó đã trả / bị khóa.</summary>
        public static QuestTemplate Tracked
        {
            get
            {
                StoryState s = State;
                QuestDatabase db = QuestDatabase.Current;
                if (s == null || db == null || s.Tracked == null) return null;
                QuestTemplate q = db.Find(s.Tracked);
                return q != null && Trackable(StatusOf(q)) ? q : null;
            }
        }

        public static bool IsTracked(QuestTemplate q) => q != null && State != null && State.Tracked == q.name && Trackable(StatusOf(q));

        /// <summary>Theo dõi (chỉ đường tới) nhiệm vụ này; null = thôi theo. Việc đã trả / bị khóa thì không theo được.</summary>
        public static bool Track(QuestTemplate q)
        {
            StoryState s = State;
            if (s == null) return false;
            if (q != null && !Trackable(StatusOf(q))) return false;
            s.Tracked = q?.name;
            NetSync.Send?.Track(q?.name);
            return true;
        }

        /// <summary>Đang không theo việc nào (hoặc việc đang theo đã xong): tự theo việc nên làm tiếp (<see cref="Suggest"/>).</summary>
        public static void Retrack()
        {
            StoryState s = State;
            if (s == null || Tracked != null) return;
            s.Tracked = Suggest()?.name;
        }

        /// <summary>
        /// Việc nên làm tiếp: cốt truyện chính trước (chờ trả, đang làm, rồi việc mới nhận được), sau đó việc phụ / hằng ngày
        /// (chờ trả, đang làm, việc mới). Việc chưa đủ thân thiện để nhận thì bỏ qua. Cùng mức thì theo thứ tự trong bảng.
        /// </summary>
        public static QuestTemplate Suggest()
        {
            QuestDatabase db = QuestDatabase.Current;
            if (db == null || State == null) return null;
            QuestTemplate best = null;
            int bestScore = 0;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null) continue;
                QuestStatus st = StatusOf(q);
                int score;
                if (st == QuestStatus.Ready) score = 3;
                else if (st == QuestStatus.Active) score = 2;
                else if (st == QuestStatus.Available && TierAllows(q)) score = 1;
                else continue;
                if (q.category == QuestCategory.Main) score += 10;
                if (score > bestScore) { best = q; bestScore = score; }
            }
            return best;
        }

        /// <summary>
        /// Bước tiếp theo của nhiệm vụ để chỉ đường: việc mới thì tới gặp người giao, xong rồi thì tới người nhận trả, đang làm
        /// thì mục tiêu đầu tiên chưa xong (gặp NPC / tới chỗ / tìm bãi quái). kind None khi không có chỗ nào để dẫn tới.
        /// </summary>
        public static QuestGoal GoalOf(QuestTemplate q)
        {
            var g = new QuestGoal();
            if (q == null) return g;
            switch (StatusOf(q))
            {
                case QuestStatus.Available:
                    g.kind = GoalKind.Npc;
                    g.npc = q.giver;
                    g.label = L("Gặp {0} để nhận việc|See {0} to take the quest", LabelOf(q.giver));
                    return g;
                case QuestStatus.Ready:
                    g.kind = GoalKind.Npc;
                    g.npc = q.TurnInNpc;
                    g.label = L("Trả việc cho {0}|Turn in to {0}", LabelOf(q.TurnInNpc));
                    return g;
                case QuestStatus.Active:
                    break;
                default:
                    return g;
            }

            for (int i = 0; i < q.objectives.Count; i++)
            {
                QuestObjective o = q.objectives[i];
                int have = ProgressOf(q, i);
                if (have >= o.count) continue;
                g.label = o.count > 1 ? LabelOf(o) + " " + have + "/" + o.count : LabelOf(o);
                switch (o.kind)
                {
                    case ObjectiveKind.Talk:
                        g.kind = GoalKind.Npc;
                        g.npc = o.target;
                        break;
                    case ObjectiveKind.Reach:
                        g.kind = GoalKind.Place;
                        g.where = o.where;
                        break;
                    case ObjectiveKind.Kill:
                        g.kind = GoalKind.Monster;
                        g.monsterId = MonsterIdOf(o.target);
                        g.where = o.where;
                        break;
                    default:
                        g.kind = o.where != null && o.where.IsSet ? GoalKind.Place : GoalKind.None;
                        g.where = o.where;
                        break;
                }
                return g;
            }
            return g;
        }

        // ---------------- tra cứu cho nhật ký ----------------

        /// <summary>Số lần đã trả nhiệm vụ này (việc lặp lại / hằng ngày cộng dồn).</summary>
        public static int TimesDone(QuestTemplate q)
        {
            QuestRecord r = q != null ? State?.Get(q.name) : null;
            return r != null ? r.timesDone : 0;
        }

        /// <summary>Cách giải quyết đã chọn lần trả gần nhất; null khi chưa trả hoặc việc không có nhánh.</summary>
        public static QuestOutcome OutcomeOf(QuestTemplate q)
        {
            QuestRecord r = q != null ? State?.Get(q.name) : null;
            if (r == null || string.IsNullOrEmpty(r.outcome) || q.outcomes == null) return null;
            foreach (QuestOutcome o in q.outcomes) if (o != null && o.name == r.outcome) return o;
            return null;
        }

        /// <summary>
        /// Mọi nhiệm vụ đang ở một trong các trạng thái cho trước, theo thứ tự trong QuestDatabase.
        /// Done gồm cả việc lặp lại / hằng ngày đã trả ít nhất một lần (dù đang Available lại).
        /// </summary>
        public static void QuestsWith(List<QuestTemplate> into, params QuestStatus[] statuses)
        {
            into.Clear();
            QuestDatabase db = QuestDatabase.Current;
            if (db == null || State == null) return;
            foreach (QuestTemplate q in db.quests)
            {
                if (q == null) continue;
                QuestStatus st = StatusOf(q);
                foreach (QuestStatus want in statuses)
                {
                    bool match = want == st || (want == QuestStatus.Done && q.Recurring && TimesDone(q) > 0);
                    if (!match) continue;
                    into.Add(q);
                    break;
                }
            }
        }

        /// <summary>
        /// Thưởng dự kiến nếu trả lúc này (vàng + EXP đã nhân hệ số theo bậc thân thiện hiện tại với giver), một dòng;
        /// thêm dòng quà thêm nếu có. Ví dụ "138 vàng, 184 EXP (x1.15 Thân thiện)\nQuà thêm khi Tin cậy: Chìa sắt".
        /// </summary>
        public static string RewardPreview(QuestTemplate q)
        {
            AffinityTier tier = TierOf(Npc(q.giver));
            float scale = Affinity.RewardScaleOf(tier);
            line.Clear();
            if (q.gold > 0) line.Append(L("{0} vàng|{0} gold", Math.Max(1, (long)Math.Round(q.gold * scale))));
            if (q.exp > 0) line.Append(line.Length > 0 ? ", " : "").Append(Math.Max(1, (long)Math.Round(q.exp * scale))).Append(" EXP");
            if (!string.IsNullOrEmpty(q.item)) line.Append(line.Length > 0 ? ", " : "").Append(ItemLabel(q.item)).Append(" x").Append(q.itemCount);
            if (line.Length == 0) line.Append(L("không có|none"));
            else if (q.gold > 0 || q.exp > 0) line.Append(" (x").Append(scale.ToString("0.##")).Append(' ').Append(Affinity.NameOf(tier)).Append(')');
            if (!string.IsNullOrEmpty(q.bonusItem))
                line.Append('\n').Append(L("Quà thêm khi {0}: {1}|Bonus at {0}: {1}", Affinity.NameOf(q.bonusTier), ItemLabel(q.bonusItem)));
            if (HasOutcomes(q)) line.Append('\n').Append(L("Thưởng thêm tùy cách giải quyết lúc trả.|Extra reward depends on how you resolve it."));
            return line.ToString();
        }

        /// <summary>Tên hiện của vật phẩm theo ItemTemplate.name; không có trong bảng thì trả lại tên khóa.</summary>
        public static string ItemLabel(string item)
        {
            ItemTemplate it = ItemDatabase.Current != null ? ItemDatabase.Current.Find(item) : null;
            return it != null ? T(it.Label) : item;
        }

        /// <summary>NPC theo NpcTemplate.name (giver / turnIn của nhiệm vụ); null khi không có trong bảng.</summary>
        public static NpcTemplate NpcOf(string name) => Npc(name);

        /// <summary>Tên hiện của NPC theo NpcTemplate.name; không có trong bảng thì trả lại tên khóa.</summary>
        public static string LabelOf(string npcName)
        {
            NpcTemplate n = Npc(npcName);
            return n != null ? T(n.Label) : npcName;
        }

        // ---------------- tiện ích ----------------

        static NpcTemplate Npc(string name) => NpcDatabase.Current != null ? NpcDatabase.Current.Find(name) : null;

        static int CountInBag(string item)
        {
            PlayerProfile me = Me;
            ItemTemplate it = ItemDatabase.Current != null ? ItemDatabase.Current.Find(item) : null;
            return me != null && it != null ? me.Inventory.Count(it.id) : 0;
        }

        // client online (chạy trước để UI phản hồi ngay, gửi lên server, server gửi bản chuẩn về ghi đè) hoặc
        // server giả lập đang xử lý thì chạy được; online mà chưa có kênh tới server thì chưa làm được
        static bool Guard()
        {
            if (State == null) return false;
            if (NetSync.CanAct) return true;
            Notify.Toast(L("Chưa hỗ trợ khi chơi online|Not available online yet"));
            return false;
        }
    }
}
