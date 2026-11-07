using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    public enum ObjectiveKind : byte
    {
        Kill = 0,      // hạ `count` con quái loại `target` (MonsterTemplate.name, hoặc id loại quái viết bằng số)
        Collect = 1,   // có `count` vật phẩm `target` (ItemTemplate.name) trong túi lúc trả; trả xong bị lấy đi
        Reach = 2,     // tới chỗ `where` (map + điểm + bán kính); target = tên chỗ, chỉ để hiện
        Talk = 3,      // bắt chuyện với NPC `target` (NpcTemplate.name); NPC nói `lines`
    }

    /// <summary>Loại nhiệm vụ: cốt truyện chính (theo chương), việc phụ, việc hằng ngày (làm lại mỗi ngày theo giờ máy).</summary>
    public enum QuestCategory : byte
    {
        Main = 0,
        Side = 1,
        Daily = 2,
    }

    /// <summary>
    /// Một chỗ trên thế giới: map + điểm (x, z) + bán kính. Mục tiêu Reach bắt buộc có; Kill / Collect có thì chỉ đường tới
    /// đây (bãi quái nên tới), không có thì chỉ đường tự tìm bãi quái gần nhất.
    /// </summary>
    [Serializable]
    public class QuestLocation
    {
        [Tooltip("Id map (Map_Template.idTemplate). -1 = không có chỗ cố định.")]
        public short mapId = -1;
        [Tooltip("Điểm (x, z) trên map; độ cao tự dò theo mặt đất.")]
        public Vector2 point;
        [Tooltip("Bán kính (m): người chơi vào trong vòng này là tới nơi.")]
        [Min(0.5f)] public float radius = 5f;

        public bool IsSet => mapId >= 0;
    }

    [Serializable]
    public class QuestObjective
    {
        public ObjectiveKind kind;
        [Tooltip("Kill: MonsterTemplate.name hoặc id loại quái (\"1\"). Collect: ItemTemplate.name (gem_red...). "
                 + "Talk: NpcTemplate.name. Reach: tên chỗ (chỉ để hiện, có thể trống).")]
        public string target;
        [Min(1)] public int count = 1;
        [Tooltip("Mô tả ngắn hiện trong hộp thoại và thông báo, vd \"Hạ Slime\". Trống thì tự ghép từ target.")]
        public string label;
        [Tooltip("Reach: chỗ phải tới. Kill / Collect: chỗ nên tới (chỉ đường). Talk: bỏ trống (chỉ đường tới NPC).")]
        public QuestLocation where = new QuestLocation();
        [Tooltip("Talk: các trang NPC nói khi người chơi tới gặp vì mục tiêu này.")]
        [TextArea(2, 4)] public string[] lines;

        /// <summary>Tiến độ ghi trong QuestRecord (Kill / Reach / Talk); Collect đếm thẳng trong túi.</summary>
        public bool Tracked => kind != ObjectiveKind.Collect;
    }

    /// <summary>Độ thân thiện được cộng khi trả nhiệm vụ (một nhiệm vụ có thể làm nhiều NPC quý mình hơn).</summary>
    [Serializable]
    public class AffinityGain
    {
        public string npc;    // NpcTemplate.name
        public int amount;
    }

    // ================================================================ rẽ nhánh

    public enum EffectKind : byte
    {
        Affinity = 0,    // target = NpcTemplate.name, amount = điểm cộng (âm là trừ)
        SetFlag = 1,     // target = tên cờ
        ClearFlag = 2,   // target = tên cờ
        MakeEnemy = 3,   // target = NpcTemplate.name: thành kẻ thù vĩnh viễn
        Gold = 4,        // amount = vàng (âm là mất)
    }

    /// <summary>Một hậu quả của lựa chọn: đổi thân thiện, đặt / xóa cờ cốt truyện, biến ai đó thành kẻ thù, cho / lấy vàng.</summary>
    [Serializable]
    public class StoryEffect
    {
        public EffectKind kind;
        public string target;
        public int amount;
    }

    /// <summary>
    /// Điều kiện của một lựa chọn / nhiệm vụ. Trống hết = luôn đúng. Cờ có sẵn: "done:&lt;nhiệm vụ&gt;" (đã trả),
    /// "enemy:&lt;npc&gt;" (đã thành kẻ thù), cùng mọi cờ do lựa chọn đặt.
    /// </summary>
    [Serializable]
    public class StoryCondition
    {
        [Tooltip("Phải có cờ này.")]
        public string requiresFlag;
        [Tooltip("Không được có cờ này.")]
        public string forbidsFlag;
        [Tooltip("Phải thân với NPC này (NpcTemplate.name) từ minTier trở lên. Trống = bỏ qua.")]
        public string tierNpc;
        public AffinityTier minTier;
        [Tooltip("Phải có ít nhất chừng này NPC (không tính kẻ thù) thân từ allyTier trở lên. 0 = bỏ qua.")]
        public int minAllies;
        public AffinityTier allyTier = AffinityTier.Trusted;
        [Tooltip("Phải có vật phẩm này trong túi (ItemTemplate.name); chọn xong bị lấy đi itemCount cái. Trống = bỏ qua.")]
        public string item;
        [Min(1)] public int itemCount = 1;

        public bool IsEmpty =>
            string.IsNullOrEmpty(requiresFlag) && string.IsNullOrEmpty(forbidsFlag) && string.IsNullOrEmpty(tierNpc)
            && minAllies <= 0 && string.IsNullOrEmpty(item);
    }

    /// <summary>
    /// Một câu người chơi đáp trong hội thoại (sau khi NPC kể chuyện, hoặc lúc NPC giao việc). Không đủ điều kiện thì nút
    /// mờ, ghi lý do. Có <see cref="flag"/> thì chỉ chọn được một lần (chọn xong đặt cờ, lần sau ẩn).
    /// </summary>
    [Serializable]
    public class StoryChoice
    {
        [Tooltip("Mã cố định, duy nhất trong cùng danh sách: gói tin gửi mã này cho server (\"chọn câu nào\"), không gửi chữ.")]
        public string name;
        public string label;
        public StoryCondition condition = new StoryCondition();
        [Tooltip("NPC đáp lại (các trang).")]
        [TextArea(2, 4)] public string[] reply;
        public List<StoryEffect> effects = new List<StoryEffect>();
        [Tooltip("Cờ đặt khi chọn; đã có cờ thì lựa chọn này ẩn đi (chỉ chọn được một lần). Trống = chọn bao nhiêu lần cũng được.")]
        public string flag;
    }

    /// <summary>
    /// Một cách giải quyết nhiệm vụ, chọn lúc trả (ví dụ bênh người này hay người kia, hay kết cục chương). Thưởng gốc
    /// của nhiệm vụ vẫn nhận; cách giải quyết cộng thêm thưởng riêng, lời kết riêng và hậu quả riêng.
    /// </summary>
    [Serializable]
    public class QuestOutcome
    {
        public string name;                 // khóa, lưu vào QuestRecord.outcome
        public string label;                // nút người chơi bấm
        [Tooltip("Tên kết cục (nhiệm vụ kết thúc chương). Trống = cách giải quyết bình thường.")]
        public string ending;
        public StoryCondition condition = new StoryCondition();
        [Tooltip("Lời kết riêng; trống thì dùng QuestTemplate.complete.")]
        [TextArea(2, 5)] public string[] complete;
        public long gold, exp;
        public string item;
        [Min(1)] public int itemCount = 1;
        public List<StoryEffect> effects = new List<StoryEffect>();
    }

    /// <summary>
    /// Một nhiệm vụ. NPC giao (giver) chỉ giao khi người chơi đủ <see cref="minTier"/> và đã xong <see cref="requires"/>.
    /// Trả ở <see cref="turnIn"/> (trống = chính giver). Không có mục tiêu = việc đưa tin: đến gặp turnIn là xong.
    /// </summary>
    [Serializable]
    public class QuestTemplate
    {
        public int id;
        public string name;                 // khóa duy nhất, không dấu: lưu vào PlayerData theo tên này
        public string title;
        [Tooltip("Cốt truyện chính / việc phụ / hằng ngày. Daily: mỗi ngày (theo giờ máy) nhận và trả được một lần.")]
        public QuestCategory category = QuestCategory.Side;
        [Tooltip("Chương của cốt truyện chính (hiện \"Chương n\"). 0 = không ghi.")]
        [Min(0)] public int chapter;
        [Tooltip("NpcTemplate.name của NPC giao nhiệm vụ.")]
        public string giver;
        [Tooltip("NpcTemplate.name của NPC nhận trả. Trống = giver.")]
        public string turnIn;
        [Tooltip("Bậc thân thiện tối thiểu với giver để được giao. Thấp hơn thì NPC từ chối (NpcTemplate.refuseLine).")]
        public AffinityTier minTier;
        [Tooltip("QuestTemplate.name phải trả xong trước. Trống = không cần.")]
        public string requires;
        [Tooltip("Trả xong nhận lại được ngay (tuần tra, việc vặt). Việc hằng ngày thì chọn category Daily.")]
        public bool repeatable;
        [Tooltip("Điều kiện thêm để việc này hiện ra (cờ cốt truyện, thân thiện...). Không đủ thì ẩn như chưa mở.")]
        public StoryCondition condition = new StoryCondition();

        [Header("Lời thoại")]
        [Tooltip("Các trang NPC kể khi giao việc.")]
        [TextArea(2, 5)] public string[] offer;
        [Tooltip("Câu khi đang làm dở mà quay lại hỏi.")]
        [TextArea(2, 4)] public string inProgress;
        [Tooltip("Các trang khi trả nhiệm vụ (cốt truyện đi tiếp ở đây).")]
        [TextArea(2, 5)] public string[] complete;
        [Tooltip("Cách người chơi đáp lúc được giao việc (thái độ). Chọn xong vẫn tới bước Nhận / Để sau.")]
        public List<StoryChoice> offerChoices = new List<StoryChoice>();
        [Tooltip("Câu NPC hỏi trước khi người chơi chọn cách giải quyết (khi có outcomes).")]
        [TextArea(2, 4)] public string turnInPrompt;
        [Tooltip("Các cách giải quyết chọn lúc trả. Trống = trả thẳng, một kết quả.")]
        public List<QuestOutcome> outcomes = new List<QuestOutcome>();

        [Header("Mục tiêu")]
        public List<QuestObjective> objectives = new List<QuestObjective>();

        [Header("Thưởng (vàng + EXP nhân hệ số theo bậc thân thiện với giver)")]
        public long gold;
        public long exp;
        [Tooltip("Vật phẩm luôn nhận (ItemTemplate.name). Trống = không có.")]
        public string item;
        [Min(1)] public int itemCount = 1;
        [Tooltip("Vật phẩm thêm chỉ khi thân thiện với giver từ bonusTier trở lên.")]
        public string bonusItem;
        public AffinityTier bonusTier = AffinityTier.Trusted;
        public List<AffinityGain> affinity = new List<AffinityGain>();

        public string TurnInNpc => string.IsNullOrEmpty(turnIn) ? giver : turnIn;

        /// <summary>Việc hằng ngày: làm lại được sang ngày sau.</summary>
        public bool IsDaily => category == QuestCategory.Daily;

        /// <summary>Làm lại được (lặp lại ngay, hoặc hằng ngày).</summary>
        public bool Recurring => repeatable || IsDaily;
    }

    /// <summary>
    /// Danh mục nhiệm vụ (Assets/_Project/Core/Data/Npcs/QuestDatabase.asset). LocalPlaySettings giữ tham chiếu và
    /// đăng ký lúc vào game, cả game tra qua <see cref="Current"/>. Tools/Core/Quests/Build Story Quests dựng cốt truyện
    /// mẫu theo 5 NPC của NpcDatabase; Tools/Core/Quests/Export Server Data ghi quests.bin + npcs.bin cho server.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "Core/Quest Database")]
    public class QuestDatabase : ScriptableObject
    {
        // 2: thêm category + chapter, chỗ (location) của từng mục tiêu, mục tiêu Reach / Talk; cờ daily suy từ category
        public const int CurrentSchema = 2;

        public int schemaVersion = CurrentSchema;
        public List<QuestTemplate> quests = new List<QuestTemplate>();

        [NonSerialized] Dictionary<string, QuestTemplate> byName;
        [NonSerialized] int byNameSource;   // quests.Count lúc dựng byName: danh sách đổi số dòng thì dựng lại

        static QuestDatabase current;

        public static QuestDatabase Current => current;

        public static void Use(QuestDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("QuestDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public void ClearCache() => byName = null;

        public QuestTemplate Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // so với số dòng lúc dựng, không so với byName.Count: bảng có dòng trống / tên trùng thì hai số luôn lệch, dựng lại mỗi lần tra
            if (byName == null || byNameSource != quests.Count)
            {
                byNameSource = quests.Count;
                byName = new Dictionary<string, QuestTemplate>(quests.Count);
                foreach (var q in quests) if (q != null && !string.IsNullOrEmpty(q.name)) byName[q.name] = q;
            }
            return byName.TryGetValue(name, out QuestTemplate t) ? t : null;
        }

        /// <summary>Thêm khi tên còn mới; tên đã có thì trả bản cũ. Id không bao giờ dùng lại.</summary>
        public QuestTemplate Add(QuestTemplate quest)
        {
            QuestTemplate old = Find(quest.name);
            if (old != null) return old;

            int max = 0;
            foreach (var q in quests) if (q.id > max) max = q.id;
            quest.id = max + 1;
            quests.Add(quest);
            ClearCache();
            return quest;
        }

        void OnValidate() => ClearCache();

        // ---------------- file cho server ----------------

        // Binary (MyWriter/MyReader, big-endian), file quests.bin gửi cho server. Chỉ phần logic: KHÔNG có title, offer,
        // inProgress, complete, turnInPrompt, nhãn mục tiêu, lời NPC của mục tiêu Talk, nhãn / lời đáp của lựa chọn,
        // nhãn / lời kết / tên kết cục.
        //   short schema, short count {
        //     short id, UTF name, byte category, byte chapter, UTF giver, UTF turnIn, sbyte minTier, UTF requires,
        //     byte flags (1 = repeatable, 2 = daily), condition,
        //     byte objectiveCount { byte kind, UTF target, short count, location },
        //     int gold, int exp, UTF item, short itemCount, UTF bonusItem, sbyte bonusTier,
        //     byte affinityCount { UTF npc, short amount },
        //     choices (offerChoices),
        //     byte outcomeCount { UTF name, bool isEnding, condition, int gold, int exp, UTF item, short itemCount, effects } }
        //   location: short mapId (-1 = không có), float x, float z, float radius (float: MyWriter.WriteFloat, x100)
        // condition / effects / choices: xem StoryBinary.
        // Luôn ghi schema mới nhất (CurrentSchema), dù asset còn ghi schemaVersion cũ.
        public byte[] ToBytes() => ToBytes(quests);

        /// <summary>Ghi một danh sách bất kỳ (server tự dựng danh sách cũng ghi được bằng hàm này).</summary>
        public static byte[] ToBytes(List<QuestTemplate> quests)
        {
            var w = new Game.Net.MyWriter(8 * 1024);
            w.WriteShort(CurrentSchema);
            w.WriteShort(checked((short)quests.Count));
            foreach (QuestTemplate q in quests)
            {
                w.WriteShort(checked((short)q.id));
                w.WriteUTF(q.name ?? "");
                w.WriteByte((byte)q.category);
                w.WriteByte(checked((byte)q.chapter));
                w.WriteUTF(q.giver ?? "");
                w.WriteUTF(q.turnIn ?? "");
                w.WriteSByte((sbyte)q.minTier);
                w.WriteUTF(q.requires ?? "");
                w.WriteByte((byte)((q.repeatable ? 1 : 0) | (q.IsDaily ? 2 : 0)));
                StoryBinary.Write(w, q.condition);

                int n = q.objectives != null ? q.objectives.Count : 0;
                w.WriteByte(checked((byte)n));
                for (int i = 0; i < n; i++)
                {
                    QuestObjective o = q.objectives[i];
                    w.WriteByte((byte)o.kind);
                    w.WriteUTF(o.target ?? "");
                    w.WriteShort(checked((short)o.count));
                    QuestLocation at = o.where ?? new QuestLocation();
                    w.WriteShort(at.mapId);
                    w.WriteFloat(at.point.x);
                    w.WriteFloat(at.point.y);
                    w.WriteFloat(at.radius);
                }

                w.WriteInt(checked((int)q.gold));
                w.WriteInt(checked((int)q.exp));
                w.WriteUTF(q.item ?? "");
                w.WriteShort(checked((short)q.itemCount));
                w.WriteUTF(q.bonusItem ?? "");
                w.WriteSByte((sbyte)q.bonusTier);

                n = q.affinity != null ? q.affinity.Count : 0;
                w.WriteByte(checked((byte)n));
                for (int i = 0; i < n; i++)
                {
                    w.WriteUTF(q.affinity[i].npc ?? "");
                    w.WriteShort(checked((short)q.affinity[i].amount));
                }

                StoryBinary.Write(w, q.offerChoices);

                n = q.outcomes != null ? q.outcomes.Count : 0;
                w.WriteByte(checked((byte)n));
                for (int i = 0; i < n; i++)
                {
                    QuestOutcome o = q.outcomes[i];
                    w.WriteUTF(o.name ?? "");
                    w.WriteBool(!string.IsNullOrEmpty(o.ending));
                    StoryBinary.Write(w, o.condition);
                    w.WriteInt(checked((int)o.gold));
                    w.WriteInt(checked((int)o.exp));
                    w.WriteUTF(o.item ?? "");
                    w.WriteShort(checked((short)o.itemCount));
                    StoryBinary.Write(w, o.effects);
                }
            }
            return w.ToArray();
        }

        /// <summary>
        /// Đọc bảng từ <see cref="ToBytes"/> vào danh sách (không tạo ScriptableObject, server C# dùng lại được). Template đọc
        /// ra chỉ có phần logic (chữ hiển thị trống). Kết cục đọc ra có ending = "1" thay cho tên kết cục. False và lý do khi
        /// dữ liệu hỏng, schema mới hơn, id / name / mã lựa chọn / mã cách giải quyết trống hoặc trùng.
        /// </summary>
        public static bool Read(byte[] bytes, List<QuestTemplate> into, out int schema, out string error)
        {
            schema = 0;
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty quest data"; return false; }
            try
            {
                var r = new Game.Net.MyReader(bytes);
                schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "quest schema too new (" + schema + ")"; return false; }
                int count = r.ReadShort();
                if (count < 0 || count > 30000) { error = "bad quest count"; return false; }
                var ids = new HashSet<int>();
                var names = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var q = new QuestTemplate { id = r.ReadShort(), name = r.ReadUTF() };
                    if (schema >= 2)
                    {
                        q.category = (QuestCategory)r.ReadByte();
                        q.chapter = r.ReadByte();
                    }
                    q.giver = r.ReadUTF();
                    q.turnIn = r.ReadUTF();
                    q.minTier = (AffinityTier)r.ReadSByte();
                    q.requires = r.ReadUTF();
                    int flags = r.ReadByte();
                    q.repeatable = (flags & 1) != 0;
                    if (schema < 2) q.category = (flags & 2) != 0 ? QuestCategory.Daily : QuestCategory.Side;
                    q.condition = StoryBinary.ReadCondition(r);

                    int n = r.ReadByte();
                    for (int k = 0; k < n; k++)
                    {
                        var o = new QuestObjective { kind = (ObjectiveKind)r.ReadByte(), target = r.ReadUTF(), count = r.ReadShort() };
                        if (schema >= 2)
                        {
                            o.where.mapId = r.ReadShort();
                            o.where.point = new Vector2(r.ReadFloat(), r.ReadFloat());
                            o.where.radius = r.ReadFloat();
                        }
                        q.objectives.Add(o);
                    }

                    q.gold = r.ReadInt();
                    q.exp = r.ReadInt();
                    q.item = r.ReadUTF();
                    q.itemCount = r.ReadShort();
                    q.bonusItem = r.ReadUTF();
                    q.bonusTier = (AffinityTier)r.ReadSByte();

                    n = r.ReadByte();
                    for (int k = 0; k < n; k++) q.affinity.Add(new AffinityGain { npc = r.ReadUTF(), amount = r.ReadShort() });

                    if (!StoryBinary.ReadChoices(r, q.offerChoices, out error)) { error = "quest " + q.name + ": " + error; return false; }

                    n = r.ReadByte();
                    var outcomeNames = new HashSet<string>();
                    for (int k = 0; k < n; k++)
                    {
                        var o = new QuestOutcome { name = r.ReadUTF() };
                        o.ending = r.ReadBool() ? "1" : null;
                        o.condition = StoryBinary.ReadCondition(r);
                        o.gold = r.ReadInt();
                        o.exp = r.ReadInt();
                        o.item = r.ReadUTF();
                        o.itemCount = r.ReadShort();
                        o.effects = StoryBinary.ReadEffects(r);
                        if (string.IsNullOrEmpty(o.name) || !outcomeNames.Add(o.name))
                        {
                            error = "quest " + q.name + ": outcome name empty or duplicate '" + o.name + "'";
                            return false;
                        }
                        q.outcomes.Add(o);
                    }

                    if (q.id <= 0 || !ids.Add(q.id)) { error = "bad or duplicate quest id " + q.id; return false; }
                    if (string.IsNullOrEmpty(q.name) || !names.Add(q.name)) { error = "empty or duplicate quest name '" + q.name + "'"; return false; }
                    into.Add(q);
                }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad quest data: " + e.Message; return false; }
            return true;
        }

        /// <summary>
        /// Kiểm tra bảng trước khi xuất: id / name, mã lựa chọn và mã cách giải quyết phải có và duy nhất; NPC / nhiệm vụ được
        /// nhắc tới phải tồn tại. Trả danh sách lỗi (rỗng = ổn).
        /// </summary>
        public List<string> Validate(ICollection<string> npcNames) => Validate(quests, npcNames);

        public static List<string> Validate(List<QuestTemplate> quests, ICollection<string> npcNames)
        {
            var problems = new List<string>();
            var names = new HashSet<string>();
            foreach (QuestTemplate q in quests) if (q != null && !string.IsNullOrEmpty(q.name)) names.Add(q.name);
            string bad = StoryBinary.FindBadName(QuestNames(quests));
            if (bad != null) problems.Add("Tên nhiệm vụ trống / trùng: " + bad);

            foreach (QuestTemplate q in quests)
            {
                if (q == null) continue;
                if (q.id <= 0 || q.id > short.MaxValue) problems.Add(q.name + ": id " + q.id + " ngoài 1.." + short.MaxValue);
                if (q.chapter > byte.MaxValue) problems.Add(q.name + ": chapter quá lớn");
                if (q.IsDaily && q.repeatable) problems.Add(q.name + ": việc hằng ngày không được bật repeatable");
                if (npcNames != null && !npcNames.Contains(q.giver)) problems.Add(q.name + ": không có NPC giao '" + q.giver + "'");
                if (q.objectives != null)
                    for (int i = 0; i < q.objectives.Count; i++)
                    {
                        QuestObjective o = q.objectives[i];
                        string at = q.name + "/mục tiêu " + (i + 1);
                        if (o == null) { problems.Add(at + ": trống"); continue; }
                        if (o.kind == ObjectiveKind.Reach && (o.where == null || !o.where.IsSet)) problems.Add(at + ": Reach phải có where (mapId >= 0)");
                        if (o.kind == ObjectiveKind.Talk && npcNames != null && !npcNames.Contains(o.target)) problems.Add(at + ": không có NPC '" + o.target + "'");
                        if ((o.kind == ObjectiveKind.Kill || o.kind == ObjectiveKind.Collect) && string.IsNullOrEmpty(o.target)) problems.Add(at + ": thiếu target");
                        if (o.kind != ObjectiveKind.Kill && o.kind != ObjectiveKind.Collect && o.count != 1) problems.Add(at + ": Reach / Talk chỉ cần count = 1");
                    }
                if (npcNames != null && !string.IsNullOrEmpty(q.turnIn) && !npcNames.Contains(q.turnIn))
                    problems.Add(q.name + ": không có NPC nhận trả '" + q.turnIn + "'");
                if (!string.IsNullOrEmpty(q.requires) && !names.Contains(q.requires))
                    problems.Add(q.name + ": việc cần trước '" + q.requires + "' không có trong bảng");
                bad = StoryBinary.FindBadName(ChoiceNames(q.offerChoices));
                if (bad != null) problems.Add(q.name + ": mã câu đáp trống / trùng: " + bad);
                bad = StoryBinary.FindBadName(OutcomeNames(q));
                if (bad != null) problems.Add(q.name + ": mã cách giải quyết trống / trùng: " + bad);
            }
            return problems;
        }

        static IEnumerable<string> QuestNames(List<QuestTemplate> quests) { foreach (QuestTemplate q in quests) if (q != null) yield return q.name; }

        static IEnumerable<string> OutcomeNames(QuestTemplate q)
        {
            if (q.outcomes == null) yield break;
            foreach (QuestOutcome o in q.outcomes) if (o != null) yield return o.name;
        }

        internal static IEnumerable<string> ChoiceNames(List<StoryChoice> choices)
        {
            if (choices == null) yield break;
            foreach (StoryChoice c in choices) if (c != null) yield return c.name;
        }
    }
}
