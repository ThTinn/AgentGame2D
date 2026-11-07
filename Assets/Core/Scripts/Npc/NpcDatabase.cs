using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Story;

namespace Game.Npcs
{
    /// <summary>Việc một lựa chọn trong hộp thoại NPC làm. Giá trị cố định vì nằm trong asset.</summary>
    public enum NpcAction : byte
    {
        Talk = 0,       // đọc các trang trong pages
        Heal = 1,       // hồi đầy máu + mana, trả price vàng
        BuyItem = 2,    // mua một vật phẩm (item = ItemTemplate.name), trả price vàng
        OpenPanel = 3,  // mở một panel của UI: param = UIID (WorldMap = 2, PlayerUpgrade = 9, Inventory = 10...)
    }

    /// <summary>Một nút trong hộp thoại của NPC.</summary>
    [Serializable]
    public class NpcOption
    {
        [Tooltip("Mã cố định, duy nhất trong NPC này: gói tin gửi mã này cho server (mua / chữa / chọn câu đáp), không gửi chữ.")]
        public string name;
        public string label;
        public NpcAction action;
        [Tooltip("Talk: các trang hội thoại, bấm >> để sang trang.")]
        [TextArea(2, 5)] public string[] pages;
        [Tooltip("Heal / BuyItem: số vàng phải trả. 0 = miễn phí.")]
        public long price;
        [Tooltip("BuyItem: tên vật phẩm trong ItemDatabase (ItemTemplate.name), vd key_small.")]
        public string item;
        [Tooltip("OpenPanel: UIID của panel cần mở.")]
        public int param;
        [Tooltip("Talk: các câu người chơi đáp sau khi nghe hết pages (đổi thân thiện, đặt cờ...). Trống = nghe xong về menu.")]
        public List<Game.Story.StoryChoice> choices = new List<Game.Story.StoryChoice>();
    }

    /// <summary>Một món NPC thích nhận làm quà, và độ thân thiện tăng thêm mỗi lần tặng.</summary>
    [Serializable]
    public class NpcGift
    {
        [Tooltip("ItemTemplate.name, vd gem_red.")]
        public string item;
        public int affinity = 3;
    }

    /// <summary>Một NPC. Trên map, object loại Npc trỏ tới NPC này bằng MapObject.param = id.</summary>
    [Serializable]
    public class NpcTemplate
    {
        public int id;
        public string name;          // khóa duy nhất, không dấu: độ thân thiện và nhiệm vụ lưu theo tên này
        public string displayName;   // tên hiện trên đầu và trên tiêu đề hộp thoại
        [Tooltip("Nghề / vai trò, hiện cạnh tên trong hộp thoại (Thợ rèn, Canh cổng...).")]
        public string role;
        [TextArea(2, 5)] public string greeting;
        [Tooltip("Câu chào khi còn Xa lạ. Trống = greeting.")]
        [TextArea(2, 4)] public string greetingStranger;
        [Tooltip("Câu chào khi đã Tin cậy trở lên. Trống = greeting.")]
        [TextArea(2, 4)] public string greetingTrusted;
        [Tooltip("Câu chào khi đang Ác cảm (thân thiện âm). Trống = greetingStranger.")]
        [TextArea(2, 4)] public string greetingDisliked;
        [Tooltip("Câu từ chối khi người chơi chưa đủ thân thiện để nhận một nhiệm vụ.")]
        [TextArea(2, 4)] public string refuseLine;
        [Tooltip("Đã là kẻ thù: bấm nói chuyện thì NPC quay lưng, chỉ hiện câu này (thông báo nổi).")]
        public string shunLine;
        [Tooltip("Tối đa NpcDatabase.MaxOptions nút; mọi BuyItem gom thành một nút Cửa hàng có chia trang, hộp thoại tự thêm Nhiệm vụ, Tặng quà và Tạm biệt.")]
        public List<NpcOption> options = new List<NpcOption>();

        [Header("Thân thiện")]
        [Tooltip("Độ thân thiện (-100..100) lúc mới gặp.")]
        [Range(-100, 100)] public int startAffinity = 10;
        [Tooltip("Món NPC thích: tặng thì tăng thân thiện.")]
        public List<NpcGift> gifts = new List<NpcGift>();

        [Header("Trên map")]
        [Tooltip("Prefab nhân vật (Animator với idle / move / faceX / faceY / moveSpeed). Trống: NpcDatabase.defaultPrefab, "
                 + "không có nốt thì dựng tạm từ ảnh của MapObjectTemplate npc.")]
        // Game: bản GAME_2D là "Npc prefab" (component 2D chưa chép sang); tạm giữ GameObject, ref cũ trong asset thành Missing.
        public GameObject prefab;
        [Tooltip("Bán kính đi dạo quanh chỗ đứng, tính bằng ô map. 0 = đứng yên.")]
        public float wanderRadius;

        public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
    }

    /// <summary>
    /// Danh mục NPC (Assets/Data/Npcs/NpcDatabase.asset). GameController giữ tham chiếu và đăng ký lúc Awake,
    /// cả game tra qua <see cref="Current"/>. Tools/Map/Build Npcs dựng bảng mẫu và đặt NPC lên các map.
    /// </summary>
    [CreateAssetMenu(fileName = "NpcDatabase", menuName = "Core/Npc Database")]
    public class NpcDatabase : ScriptableObject
    {
        /// <summary>
        /// Số lựa chọn riêng tối đa của một NPC. Cột nút của NpcDialogPopup còn thêm Nhiệm vụ, Tặng quà, Tạm biệt:
        /// 6 nút cao ~350 trên màn 720, vừa phía trên khung chữ.
        /// </summary>
        public const int MaxOptions = 3;
        public const int CurrentSchema = 1;

        public int schemaVersion = CurrentSchema;

        [Tooltip("Prefab nhân vật cho NPC không gán prefab riêng. Trống: dựng tạm từ ảnh của MapObjectTemplate npc.")]
        // Game: bản GAME_2D là "Npc defaultPrefab", xem ghi chú ở NpcTemplate.prefab.
        public GameObject defaultPrefab;
        public List<NpcTemplate> npcs = new List<NpcTemplate>();

        [NonSerialized] Dictionary<int, NpcTemplate> byId;
        [NonSerialized] int byIdSource = -1;   // npcs.Count lúc dựng byId (trùng id thì byId ít dòng hơn, đừng so với byId.Count)

        static NpcDatabase current;

        /// <summary>Bảng NPC của game; null khi scene chưa có ai giữ bảng.</summary>
        public static NpcDatabase Current => current;

        /// <summary>Đăng ký bảng NPC cho cả game.</summary>
        public static void Use(NpcDatabase table)
        {
            if (table == null || table == current) return;
            if (current != null) Debug.LogWarning("NpcDatabase: đang dùng " + current.name + ", bị thay bằng " + table.name + ".", table);
            current = table;
        }

        // Static sống qua cả lần bấm Play sau nếu không xóa (Enter Play Mode Options tắt Reload Domain).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        public void ClearCache() => byId = null;

        public NpcTemplate Get(int id)
        {
            if (byId == null || byIdSource != npcs.Count)
            {
                byId = new Dictionary<int, NpcTemplate>(npcs.Count);
                // trùng id: giữ NPC đứng trước (Validate báo lỗi này)
                foreach (var n in npcs) if (n != null && !byId.ContainsKey(n.id)) byId[n.id] = n;
                byIdSource = npcs.Count;
            }
            return byId.TryGetValue(id, out NpcTemplate npc) ? npc : null;
        }

        public NpcTemplate Find(string name)
        {
            foreach (var n in npcs) if (n != null && n.name == name) return n;
            return null;
        }

        /// <summary>Thêm NPC khi tên còn mới (trả bản cũ nếu tên đã có); id không bao giờ dùng lại.</summary>
        public NpcTemplate Add(NpcTemplate npc)
        {
            NpcTemplate old = Find(npc.name);
            if (old != null) return old;

            int max = 0;
            foreach (var n in npcs) if (n.id > max) max = n.id;
            npc.id = max + 1;
            npcs.Add(npc);
            ClearCache();
            return npc;
        }

        void OnValidate()
        {
            ClearCache();
            foreach (var n in npcs)
            {
                if (n.options == null) continue;
                // mọi hàng bán chỉ chiếm một nút Cửa hàng
                int buttons = n.options.FindAll(o => o != null && o.action != NpcAction.BuyItem).Count;
                if (n.options.Exists(o => o != null && o.action == NpcAction.BuyItem)) buttons++;
                if (buttons > MaxOptions)
                    Debug.LogWarning($"NpcDatabase: NPC '{n.name}' có {buttons} nút lựa chọn, hộp thoại chỉ hiện {MaxOptions}.", this);
            }
        }

        // ---------------- file cho server ----------------

        // Binary (MyWriter/MyReader, big-endian), file npcs.bin gửi cho server. Chỉ phần logic: KHÔNG có displayName, role,
        // câu chào / từ chối / quay lưng, nhãn và lời kể của lựa chọn, nhãn / lời đáp của câu đáp, prefab, wanderRadius
        // (những thứ đó chỉ client dùng). Server cần: thân thiện ban đầu, giá + vật phẩm của dịch vụ để kiểm tra mua / chữa,
        // câu đáp (điều kiện + hậu quả), món thích nhận.
        //   short schema, short count {
        //     short id, UTF name, sbyte startAffinity,
        //     byte optionCount { UTF name, byte action, int price, UTF item, short param, choices },
        //     byte giftCount { UTF item, short affinity } }
        // choices: xem Game.Story.StoryBinary.
        public byte[] ToBytes() => ToBytes(npcs, schemaVersion);

        /// <summary>Ghi một danh sách bất kỳ (server tự dựng danh sách cũng ghi được bằng hàm này).</summary>
        public static byte[] ToBytes(List<NpcTemplate> npcs, int schemaVersion)
        {
            var w = new Game.Net.MyWriter(4 * 1024);
            w.WriteShort(checked((short)schemaVersion));
            w.WriteShort(checked((short)npcs.Count));
            foreach (NpcTemplate n in npcs)
            {
                w.WriteShort(checked((short)n.id));
                w.WriteUTF(n.name ?? "");
                w.WriteSByte(checked((sbyte)n.startAffinity));

                int count = n.options != null ? n.options.Count : 0;
                w.WriteByte(checked((byte)count));
                for (int i = 0; i < count; i++)
                {
                    NpcOption o = n.options[i];
                    w.WriteUTF(o.name ?? "");
                    w.WriteByte((byte)o.action);
                    w.WriteInt(checked((int)o.price));
                    w.WriteUTF(o.item ?? "");
                    w.WriteShort(checked((short)o.param));
                    StoryBinary.Write(w, o.choices);
                }

                count = n.gifts != null ? n.gifts.Count : 0;
                w.WriteByte(checked((byte)count));
                for (int i = 0; i < count; i++)
                {
                    w.WriteUTF(n.gifts[i].item ?? "");
                    w.WriteShort(checked((short)n.gifts[i].affinity));
                }
            }
            return w.ToArray();
        }

        /// <summary>
        /// Đọc bảng từ <see cref="ToBytes"/> vào danh sách (không tạo ScriptableObject, server C# dùng lại được). Template đọc
        /// ra chỉ có phần logic (chữ hiển thị trống). False và lý do khi dữ liệu hỏng, schema mới hơn, id / name / mã lựa chọn
        /// / mã câu đáp trống hoặc trùng.
        /// </summary>
        public static bool Read(byte[] bytes, List<NpcTemplate> into, out int schema, out string error)
        {
            schema = 0;
            error = null;
            if (bytes == null || bytes.Length == 0) { error = "empty npc data"; return false; }
            try
            {
                var r = new Game.Net.MyReader(bytes);
                schema = r.ReadShort();
                if (schema > CurrentSchema) { error = "npc schema too new (" + schema + ")"; return false; }
                int count = r.ReadShort();
                if (count < 0 || count > 30000) { error = "bad npc count"; return false; }
                var ids = new HashSet<int>();
                var names = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var n = new NpcTemplate { id = r.ReadShort(), name = r.ReadUTF(), startAffinity = r.ReadSByte() };

                    int optionCount = r.ReadByte();
                    var optionNames = new HashSet<string>();
                    for (int k = 0; k < optionCount; k++)
                    {
                        var o = new NpcOption
                        {
                            name = r.ReadUTF(), action = (NpcAction)r.ReadByte(), price = r.ReadInt(), item = r.ReadUTF(), param = r.ReadShort(),
                        };
                        if (!StoryBinary.ReadChoices(r, o.choices, out error)) { error = "npc " + n.name + "/" + o.name + ": " + error; return false; }
                        if (string.IsNullOrEmpty(o.name) || !optionNames.Add(o.name))
                        {
                            error = "npc " + n.name + ": option name empty or duplicate '" + o.name + "'";
                            return false;
                        }
                        n.options.Add(o);
                    }

                    int giftCount = r.ReadByte();
                    for (int k = 0; k < giftCount; k++) n.gifts.Add(new NpcGift { item = r.ReadUTF(), affinity = r.ReadShort() });

                    if (n.id <= 0 || !ids.Add(n.id)) { error = "bad or duplicate npc id " + n.id; return false; }
                    if (string.IsNullOrEmpty(n.name) || !names.Add(n.name)) { error = "empty or duplicate npc name '" + n.name + "'"; return false; }
                    into.Add(n);
                }
                if (r.Available != 0) { error = "unexpected bytes at the end"; return false; }
            }
            catch (Exception e) { error = "bad npc data: " + e.Message; return false; }
            return true;
        }

        /// <summary>Kiểm tra trước khi xuất: tên NPC, mã lựa chọn, mã câu đáp có và duy nhất. Trả danh sách lỗi (rỗng = ổn).</summary>
        public List<string> Validate() => Validate(npcs);

        public static List<string> Validate(List<NpcTemplate> npcs)
        {
            var problems = new List<string>();
            var names = new List<string>();
            foreach (NpcTemplate n in npcs) if (n != null) names.Add(n.name);
            string bad = StoryBinary.FindBadName(names);
            if (bad != null) problems.Add("Tên NPC trống / trùng: " + bad);

            // id là thứ map lưu (MapObject.param) và file server đọc: phải dương, duy nhất, vừa short
            var ids = new HashSet<int>();
            foreach (NpcTemplate n in npcs)
            {
                if (n == null) continue;
                if (n.id <= 0 || n.id > short.MaxValue) problems.Add(n.name + ": id " + n.id + " ngoài 1.." + short.MaxValue);
                else if (!ids.Add(n.id)) problems.Add(n.name + ": id " + n.id + " trùng với NPC khác");
            }

            foreach (NpcTemplate n in npcs)
            {
                if (n == null || n.options == null) continue;
                var optionNames = new List<string>();
                foreach (NpcOption o in n.options)
                {
                    if (o == null) continue;
                    optionNames.Add(o.name);
                    var choiceNames = new List<string>();
                    if (o.choices != null) foreach (StoryChoice c in o.choices) if (c != null) choiceNames.Add(c.name);
                    bad = StoryBinary.FindBadName(choiceNames);
                    if (bad != null) problems.Add(n.name + "/" + o.name + ": mã câu đáp trống / trùng: " + bad);
                }
                bad = StoryBinary.FindBadName(optionNames);
                if (bad != null) problems.Add(n.name + ": mã lựa chọn trống / trùng: " + bad);
                if (n.startAffinity < Affinity.Min || n.startAffinity > Affinity.Max) problems.Add(n.name + ": startAffinity ngoài -100..100");
            }
            return problems;
        }
    }
}
