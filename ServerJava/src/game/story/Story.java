package game.story;

import java.io.*;
import java.time.LocalDate;
import java.time.temporal.ChronoUnit;
import java.util.*;
import game.db.Json;
import game.net.Packet;
import game.player.Profile;
import game.tables.Tables;

/**
 * Cốt truyện của một người chơi ở server: port QuestSystem, StoryState, Affinity và NpcSystem của client. Thân thiện,
 * nhiệm vụ, cờ nằm trong {@link Profile#root} như PlayerData. Server là bản chuẩn: client làm trước rồi nhận PROFILE.SYNC
 * ghi đè. Lớp này giữ cờ, thân thiện, điều kiện, hậu quả, dịch vụ NPC; nhiệm vụ ở {@link QuestLog}. Chỉ gọi khi giữ
 * World.LOCK.
 */
public class Story {
    static final int AFFINITY_MIN = -100, AFFINITY_MAX = 100;
    static final int ENEMY_AT = -50;            // thân thiện từ mức này trở xuống thì thành kẻ thù
    static final int ABANDON_PENALTY = 5;       // thân thiện mất khi bỏ nhiệm vụ của NPC
    static final int GREET_GAIN = 1;            // thân thiện được khi chào lần đầu trong phiên
    static final int TIER_ENEMY = -2;           // bậc thấp nhất (Enemy); bậc = chỉ số trong FLOORS + TIER_ENEMY
    // StoryEffectKind
    static final int EFFECT_AFFINITY = 0, EFFECT_SET_FLAG = 1, EFFECT_CLEAR_FLAG = 2, EFFECT_ENEMY = 3, EFFECT_GOLD = 4;
    static final String ENEMY_FLAG = "enemy:";  // cờ "enemy:<npc>" đánh dấu NPC đã thành kẻ thù

    // theo thứ tự bậc từ Enemy (chỉ số 0) tới Bonded (chỉ số 6), như Affinity của client
    static final int[] FLOORS = { AFFINITY_MIN, ENEMY_AT + 1, 0, 20, 40, 60, 80 };
    static final float[] REWARD_SCALE = { 0f, 0.6f, 0.8f, 1f, 1.15f, 1.3f, 1.5f };
    static final float[] PRICE_SCALE = { 2f, 1.3f, 1.1f, 1f, 0.95f, 0.9f, 0.8f };

    final Profile me;
    final Map<String, Integer> affinity = new LinkedHashMap<>();    // NPC -> thân thiện (chưa có thì theo startAffinity)
    public final QuestLog quests;
    final Set<String> flags = new LinkedHashSet<>();
    final Set<String> enemies = new HashSet<>();                    // NPC đã thành kẻ thù, dựng từ cờ ENEMY_FLAG
    final Set<String> greeted = new HashSet<>();                    // chào hỏi chỉ cộng thân thiện một lần mỗi phiên
    public final List<String> toasts = new ArrayList<>();          // thông báo sinh ở server (hạ quái, tới nơi), gửi sau mỗi việc

    /** Đọc thân thiện, nhiệm vụ, cờ và việc đang theo dõi từ hồ sơ. */
    public Story(Profile me) {
        this.me = me;
        quests = new QuestLog(this);
        for (Map<String, Object> a : Json.rows(me.root, "npcAffinity")) {
            String npc = Json.str(a, "npc");
            if (!npc.isEmpty()) affinity.put(npc, clamp((int) Json.num(a, "value")));
        }
        for (Map<String, Object> q : Json.rows(me.root, "quests")) {
            QuestLog.Record r = readRecord(q);
            if (r != null) quests.records.put(r.quest, r);
        }
        Object saved = me.root.get("storyFlags");
        if (saved instanceof List)
            for (Object f : (List<?>) saved) if (f instanceof String && !((String) f).isEmpty()) addFlag((String) f);
        String t = Json.str(me.root, "trackedQuest");
        quests.tracked = t.isEmpty() ? null : t;
    }

    /** Một bản ghi nhiệm vụ trong hồ sơ, null khi thiếu tên. */
    static QuestLog.Record readRecord(Map<String, Object> q) {
        String name = Json.str(q, "quest");
        if (name.isEmpty()) return null;
        QuestLog.Record r = new QuestLog.Record();
        r.quest = name;
        r.active = Json.bool(q, "active");
        r.timesDone = (int) Json.num(q, "timesDone");
        r.outcome = Json.str(q, "outcome");
        r.lastDay = (int) Json.num(q, "lastDay");
        for (int n : Json.ints(q, "progress")) r.progress.add(n);
        return r;
    }

    /** Ghi thân thiện, nhiệm vụ, cờ, việc đang theo dõi vào hồ sơ. */
    public void save(Map<String, Object> root) {
        List<Object> affinityList = new ArrayList<>();
        for (Map.Entry<String, Integer> e : affinity.entrySet()) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("npc", e.getKey());
            m.put("value", (long) e.getValue());
            affinityList.add(m);
        }
        root.put("npcAffinity", affinityList);

        List<Object> questList = new ArrayList<>();
        for (QuestLog.Record r : quests.records.values()) questList.add(recordJson(r));
        root.put("quests", questList);
        root.put("storyFlags", new ArrayList<Object>(flags));
        root.put("trackedQuest", quests.tracked != null ? quests.tracked : "");
    }

    /** Một bản ghi nhiệm vụ dạng JSON của hồ sơ. */
    static Map<String, Object> recordJson(QuestLog.Record r) {
        Map<String, Object> m = new LinkedHashMap<>();
        m.put("quest", r.quest);
        m.put("active", r.active);
        m.put("timesDone", (long) r.timesDone);
        m.put("outcome", r.outcome);
        m.put("lastDay", (long) r.lastDay);
        List<Object> progress = new ArrayList<>();
        for (int n : r.progress) progress.add((long) n);
        m.put("progress", progress);
        return m;
    }

    /** Phần Story của PROFILE.SYNC, định dạng ở ProfileSync.cs. */
    public void write(DataOutputStream w) throws IOException {
        w.writeShort(affinity.size());
        for (Map.Entry<String, Integer> e : affinity.entrySet()) {
            Packet.utf(w, e.getKey());
            w.writeShort(e.getValue());
        }
        w.writeShort(quests.records.size());
        for (QuestLog.Record r : quests.records.values()) {
            Packet.utf(w, r.quest);
            w.writeBoolean(r.active);
            w.writeShort(r.timesDone);
            Packet.utf(w, r.outcome);
            w.writeInt(r.lastDay);
            w.writeByte(r.progress.size());
            for (int n : r.progress) w.writeShort(n);
        }
        w.writeShort(flags.size());
        for (String f : flags) Packet.utf(w, f);
        Packet.utf(w, quests.tracked != null ? quests.tracked : "");
    }

    // ---------------- tiện ích ----------------

    /** Số ngày kể từ 1/1/2000 theo giờ máy server, dùng cho việc hằng ngày. */
    static int today() {
        return (int) ChronoUnit.DAYS.between(LocalDate.of(2000, 1, 1), LocalDate.now());
    }

    /** Kẹp thân thiện vào [AFFINITY_MIN, AFFINITY_MAX]. */
    static int clamp(int v) {
        return Math.max(AFFINITY_MIN, Math.min(AFFINITY_MAX, v));
    }

    /** Chuỗi null hoặc rỗng. */
    static boolean empty(String s) {
        return s == null || s.isEmpty();
    }

    /** Phần tiếng Việt của tên dạng "Tiếng Việt|English": thông báo của server viết tiếng Việt như các thông báo khác. */
    static String vi(String s) {
        int bar = s.indexOf('|');
        return bar >= 0 ? s.substring(0, bar) : s;
    }

    // ---------------- cờ, thân thiện ----------------

    /** Thêm cờ; cờ kẻ thù thì ghi nhận NPC đó là kẻ thù. false khi đã có. */
    boolean addFlag(String flag) {
        if (!flags.add(flag)) return false;
        if (flag.startsWith(ENEMY_FLAG)) enemies.add(flag.substring(ENEMY_FLAG.length()));
        return true;
    }

    /** Bật / tắt cờ. Tên rỗng thì bỏ qua. */
    void setFlag(String flag, boolean on) {
        if (empty(flag)) return;
        if (on) addFlag(flag);
        else if (flags.remove(flag) && flag.startsWith(ENEMY_FLAG)) enemies.remove(flag.substring(ENEMY_FLAG.length()));
    }

    /** NPC này đã thành kẻ thù chưa. */
    boolean isEnemy(String npc) {
        return npc != null && enemies.contains(npc);
    }

    /** Thân thiện hiện tại với NPC, chưa gặp thì theo startAffinity. */
    int getAffinity(Tables.Npc npc) {
        Integer v = affinity.get(npc.name);
        return v != null ? v : clamp(npc.startAffinity);
    }

    /** Bậc ứng với điểm thân thiện value (TIER_ENEMY..4). */
    static int tierOf(int value) {
        for (int i = FLOORS.length - 1; i > 0; i--) if (value >= FLOORS[i]) return i + TIER_ENEMY;
        return TIER_ENEMY;
    }

    /** Bậc với NPC: kẻ thù luôn TIER_ENEMY; npc null thì như thân thiện 0. */
    int tierOf(Tables.Npc npc) {
        if (npc == null) return tierOf(0);
        return isEnemy(npc.name) ? TIER_ENEMY : tierOf(getAffinity(npc));
    }

    /** Hệ số thưởng nhiệm vụ theo bậc với người giao. */
    static float rewardScale(int tier) {
        return REWARD_SCALE[tier - TIER_ENEMY];
    }

    /** Giá dịch vụ theo bậc, ít nhất 1 (giá gốc 0 thì miễn phí). */
    static long price(long base, int tier) {
        return base <= 0 ? 0 : Math.max(1, (long) Math.rint(base * PRICE_SCALE[tier - TIER_ENEMY]));
    }

    /** Cộng thân thiện với NPC theo tên. Trả mức thay đổi thật (0 khi NPC lạ, đã là kẻ thù, hoặc amount = 0). */
    int addAffinity(String name, int amount) {
        Tables.Npc npc = Tables.npcs.get(name);
        if (npc == null || amount == 0 || isEnemy(name)) return 0;
        return addAffinity(npc, amount);
    }

    /** Cộng thân thiện, kẹp trong khoảng; xuống tới ENEMY_AT thì NPC thành kẻ thù. Trả mức thay đổi thật. */
    int addAffinity(Tables.Npc npc, int amount) {
        if (isEnemy(npc.name)) return 0;
        int before = getAffinity(npc), after = clamp(before + amount);
        affinity.put(npc.name, after);
        if (after <= ENEMY_AT) makeEnemy(npc.name);
        return after - before;
    }

    /** NPC thành kẻ thù: đặt cờ, hạ thân thiện về ENEMY_AT, hủy các nhiệm vụ đang làm của NPC đó. */
    void makeEnemy(String npc) {
        if (!addFlag(ENEMY_FLAG + npc)) return;
        Integer v = affinity.get(npc);
        if (v == null || v > ENEMY_AT) affinity.put(npc, ENEMY_AT);
        quests.onBecameEnemy(npc);
    }

    /** Bắt chuyện: lần đầu trong phiên được cộng GREET_GAIN thân thiện. */
    public void greet(Tables.Npc npc) {
        if (!isEnemy(npc.name) && greeted.add(npc.name)) addAffinity(npc, GREET_GAIN);
    }

    /** Số cái vật phẩm tên item trong túi. */
    int countInBag(String item) {
        Tables.Item it = Tables.itemsByName.get(item);
        return it != null ? me.bag.count(it.id) : 0;
    }

    // ---------------- điều kiện, hậu quả ----------------

    /** Điều kiện đạt chưa: cờ cần có / cấm, bậc với một NPC, số đồng minh ở bậc nào đó, đủ vật phẩm. null luôn đạt. */
    boolean check(Tables.Condition c) {
        if (c == null) return true;
        if (!empty(c.requiresFlag) && !flags.contains(c.requiresFlag)) return false;
        if (!empty(c.forbidsFlag) && flags.contains(c.forbidsFlag)) return false;
        if (!empty(c.tierNpc) && tierOf(Tables.npcs.get(c.tierNpc)) < c.minTier) return false;
        if (c.minAllies > 0 && alliesAt(c.allyTier) < c.minAllies) return false;
        return empty(c.item) || countInBag(c.item) >= c.itemCount;
    }

    /** Số NPC đang ở bậc tier trở lên. */
    int alliesAt(int tier) {
        int n = 0;
        for (Tables.Npc npc : Tables.npcs.values()) if (tierOf(npc) >= tier) n++;
        return n;
    }

    /** Áp các tác động: đổi thân thiện, bật / tắt cờ, biến NPC thành kẻ thù, cộng / trừ vàng. */
    void apply(List<Tables.StoryEffect> effects) {
        for (Tables.StoryEffect e : effects)
            switch (e.kind) {
                case EFFECT_AFFINITY:
                    addAffinity(e.target, e.amount);
                    break;
                case EFFECT_SET_FLAG:
                    setFlag(e.target, true);
                    break;
                case EFFECT_CLEAR_FLAG:
                    setFlag(e.target, false);
                    break;
                case EFFECT_ENEMY:
                    if (Tables.npcs.containsKey(e.target)) makeEnemy(e.target);
                    break;
                case EFFECT_GOLD:
                    me.gold = Math.max(0, me.gold + e.amount);
                    break;
                default:
                    break;
            }
    }

    /** Lấy đi vật phẩm mà điều kiện đòi (nếu có). */
    void consume(Tables.Condition c) {
        if (c == null || empty(c.item)) return;
        Tables.Item it = Tables.itemsByName.get(c.item);
        if (it != null) me.bag.remove(it.id, c.itemCount);
    }

    /** Câu đáp tên choice của owner ("quest:nhiệm vụ" hoặc "npc:npc/lựa chọn"). null khi không có. */
    public static Tables.Choice findChoice(String owner, String choice) {
        if (empty(owner) || empty(choice)) return null;
        List<Tables.Choice> list = null;
        if (owner.startsWith("quest:")) {
            Tables.Quest q = Tables.quests.get(owner.substring(6));
            if (q != null) list = q.offerChoices;
        } else if (owner.startsWith("npc:")) {
            int slash = owner.indexOf('/');
            Tables.Npc n = slash > 4 ? Tables.npcs.get(owner.substring(4, slash)) : null;
            Tables.NpcOption o = n != null ? n.options.get(owner.substring(slash + 1)) : null;
            if (o != null) list = o.choices;
        }
        if (list != null) for (Tables.Choice c : list) if (choice.equals(c.name)) return c;
        return null;
    }

    /** Chọn câu đáp: chưa chọn lần nào (cờ của nó chưa bật) và đạt điều kiện thì lấy vật phẩm, áp tác động, bật cờ. */
    public boolean choose(Tables.Choice c) {
        if (c == null || (!empty(c.flag) && flags.contains(c.flag)) || !check(c.condition)) return false;
        consume(c.condition);
        apply(c.effects);
        setFlag(c.flag, true);
        return true;
    }

    // ---------------- dịch vụ NPC ----------------

    /** Dùng dịch vụ o của NPC (chữa, mua đồ) với giá theo bậc. Kẻ thù không phục vụ. */
    public boolean useOption(Tables.Npc npc, Tables.NpcOption o) {
        if (isEnemy(npc.name)) return false;
        long cost = price(o.price, tierOf(npc));
        if (me.gold < cost) return false;
        if (o.action == Tables.NPC_HEAL) return heal(cost);
        if (o.action == Tables.NPC_BUY) return buy(o.item, cost);
        return false;
    }

    /** Chữa đầy máu và mana. Không chữa khi đã chết hoặc đang đầy. */
    boolean heal(long cost) {
        if (me.dead || (me.hp >= me.maxHp && me.mp >= me.maxMp)) return false;
        me.gold -= cost;
        me.hp = me.maxHp;
        me.mp = me.maxMp;
        return true;
    }

    /** Mua một cái vật phẩm tên item. Túi đầy thì không mua. */
    boolean buy(String item, long cost) {
        Tables.Item it = Tables.itemsByName.get(item);
        if (it == null || me.bag.add(it.id, 1) > 0) return false;
        me.gold -= cost;
        return true;
    }

    /** Tặng NPC một vật phẩm nó thích: cộng thân thiện theo bảng quà. Kẻ thù hoặc đã tối đa thì không nhận. */
    public boolean giveGift(Tables.Npc npc, String item) {
        Integer gain = npc.gifts.get(item);
        Tables.Item it = Tables.itemsByName.get(item);
        if (gain == null || it == null || isEnemy(npc.name) || getAffinity(npc) >= AFFINITY_MAX) return false;
        if (!me.bag.remove(it.id, 1)) return false;
        addAffinity(npc, gain);
        return true;
    }
}
