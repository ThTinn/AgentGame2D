package game.story;

import java.util.*;
import game.player.Profile;
import game.tables.Tables;

/**
 * Nhiệm vụ của một người chơi: nhận, bỏ, trả, tiến độ, việc hằng ngày, theo dõi. Luật chung với QuestSystem.cs của
 * client. Mục tiêu hạ quái server tự đếm, mục tiêu tới nơi server tính theo MOVE. Chỉ gọi khi giữ World.LOCK.
 */
public class QuestLog {
    static final int LOCKED = 0, AVAILABLE = 1, ACTIVE = 2, READY = 3, DONE = 4;        // QuestStatus
    static final int KIND_KILL = 0, KIND_COLLECT = 1, KIND_REACH = 2, KIND_TALK = 3;    // ObjectiveKind
    static final int CATEGORY_MAIN = 0, CATEGORY_DAILY = 2;
    static final String DONE_FLAG = "done:";    // cờ "done:<nhiệm vụ>" bật khi trả xong lần đầu

    /** Bản ghi một nhiệm vụ của người chơi: đang làm không, số lần đã xong, kết cục lần cuối, ngày xong, tiến độ từng mục tiêu. */
    static class Record {
        String quest, outcome = "";
        boolean active;
        int timesDone, lastDay;
        final List<Integer> progress = new ArrayList<>();

        /** Tiến độ của mục tiêu objective, chưa có thì 0. */
        int progress(int objective) {
            return objective < progress.size() ? progress.get(objective) : 0;
        }

        /** Đặt tiến độ của mục tiêu objective, nới danh sách nếu cần. */
        void setProgress(int objective, int value) {
            while (progress.size() <= objective) progress.add(0);
            progress.set(objective, value);
        }
    }

    final Story story;
    final Profile me;
    final Map<String, Record> records = new LinkedHashMap<>();
    String tracked;     // nhiệm vụ đang theo dõi, null khi không theo việc nào

    QuestLog(Story story) {
        this.story = story;
        me = story.me;
    }

    // ---------------- trạng thái ----------------

    /**
     * Trạng thái nhiệm vụ với người chơi, như QuestSystem.StatusOf: khóa nếu người giao / nhận trả là kẻ thù; đang làm thì
     * READY khi đủ mục tiêu; đã xong thì DONE (việc hằng ngày chỉ DONE trong ngày); còn lại xét nhiệm vụ trước và điều
     * kiện.
     */
    int statusOf(Tables.Quest q) {
        if (q == null || story.isEnemy(q.giver) || story.isEnemy(q.turnInNpc())) return LOCKED;
        Record r = records.get(q.name);
        if (r != null && r.active) return objectivesMet(q, r) ? READY : ACTIVE;
        if (r != null && r.timesDone > 0) {
            if (q.category == CATEGORY_DAILY) {
                if (r.lastDay == Story.today()) return DONE;
            } else if (!q.repeatable) {
                return DONE;
            }
        }
        for (Tables.Objective o : q.objectives) if (o.kind == KIND_TALK && story.isEnemy(o.target)) return LOCKED;
        if (!Story.empty(q.requires)) {
            Record pre = records.get(q.requires);
            if (pre == null || pre.timesDone == 0) return LOCKED;
        }
        return story.check(q.condition) ? AVAILABLE : LOCKED;
    }

    /** Bậc với người giao đủ để nhận việc. */
    boolean tierAllows(Tables.Quest q) {
        return story.tierOf(Tables.npcs.get(q.giver)) >= q.minTier;
    }

    /** Đủ mọi mục tiêu chưa. Mục tiêu thu thập đếm theo đồ trong túi, các loại khác theo tiến độ đã ghi. */
    boolean objectivesMet(Tables.Quest q, Record r) {
        for (int i = 0; i < q.objectives.size(); i++) {
            Tables.Objective o = q.objectives.get(i);
            int have = o.kind == KIND_COLLECT ? story.countInBag(o.target) : r.progress(i);
            if (have < o.count) return false;
        }
        return true;
    }

    /** Mục tiêu "nói chuyện với npc" còn dở của nhiệm vụ q đang làm, -1 khi không có. */
    int pendingTalk(Tables.Quest q, String npc) {
        Record r = records.get(q.name);
        if (r == null || !r.active) return -1;
        for (int i = 0; i < q.objectives.size(); i++) {
            Tables.Objective o = q.objectives.get(i);
            if (o.kind == KIND_TALK && npc.equals(o.target) && r.progress(i) < o.count) return i;
        }
        return -1;
    }

    // ---------------- nhận, bỏ, trả ----------------

    /** Nhận việc. Việc chính, hoặc khi chưa theo việc nào, thì chuyển sang theo dõi việc vừa nhận. */
    public boolean accept(Tables.Quest q) {
        if (statusOf(q) != AVAILABLE || !tierAllows(q)) return false;
        Tables.Quest current = trackedQuest();
        Record r = records.computeIfAbsent(q.name, k -> {
            Record n = new Record();
            n.quest = k;
            return n;
        });
        r.active = true;
        r.progress.clear();
        if (current == null || current == q || q.category == CATEGORY_MAIN) tracked = q.name;
        return true;
    }

    /** Bỏ việc đang làm: mất ABANDON_PENALTY thân thiện với người giao. */
    public boolean abandon(Tables.Quest q) {
        int st = statusOf(q);
        if (st != ACTIVE && st != READY) return false;
        stop(q.name, false, "");
        story.addAffinity(q.giver, -Story.ABANDON_PENALTY);
        if (q.name.equals(tracked)) tracked = null;
        retrack();
        return true;
    }

    /**
     * Trả việc với kết cục outcomeName (nhiệm vụ không có kết cục thì bỏ qua tên). Lấy đồ thu thập và đồ kết cục đòi, trao
     * thưởng theo bậc với người giao, ghi xong, cộng thân thiện, áp tác động của kết cục.
     */
    public boolean turnIn(Tables.Quest q, String outcomeName) {
        if (statusOf(q) != READY) return false;
        Tables.Outcome outcome = findOutcome(q, outcomeName);
        if (!q.outcomes.isEmpty() && (outcome == null || !story.check(outcome.condition))) return false;
        if (outcome != null && !canPayBoth(q, outcome.condition)) return false;

        takeCollected(q);
        if (outcome != null) story.consume(outcome.condition);
        payRewards(q, outcome);

        stop(q.name, true, outcome != null ? outcome.name : "");
        for (Map.Entry<String, Integer> a : q.affinity.entrySet()) story.addAffinity(a.getKey(), a.getValue());
        if (outcome != null) story.apply(outcome.effects);
        if (q.name.equals(tracked)) tracked = null;
        retrack();
        return true;
    }

    /** Kết cục tên name của q (trùng tên thì lấy cái sau cùng), null khi không có. */
    static Tables.Outcome findOutcome(Tables.Quest q, String name) {
        Tables.Outcome found = null;
        for (Tables.Outcome o : q.outcomes) if (o.name.equals(name)) found = o;
        return found;
    }

    /** Đủ vật phẩm cho cả mục tiêu thu thập lẫn điều kiện c của kết cục khi hai bên đòi cùng một món. */
    boolean canPayBoth(Tables.Quest q, Tables.Condition c) {
        if (c == null || Story.empty(c.item)) return true;
        int need = c.itemCount;
        for (Tables.Objective o : q.objectives) if (o.kind == KIND_COLLECT && c.item.equals(o.target)) need += o.count;
        return story.countInBag(c.item) >= need;
    }

    /** Lấy đi đồ của các mục tiêu thu thập. */
    void takeCollected(Tables.Quest q) {
        for (Tables.Objective o : q.objectives) {
            if (o.kind != KIND_COLLECT) continue;
            Tables.Item it = Tables.itemsByName.get(o.target);
            if (it != null) me.bag.remove(it.id, o.count);
        }
    }

    /**
     * Thưởng vàng, EXP (của nhiệm vụ cộng kết cục) nhân theo bậc với người giao lúc trả, trước khi cộng thân thiện của
     * chính nhiệm vụ này. Thêm vật phẩm thưởng, và vật phẩm thưởng thêm khi bậc đủ bonusTier.
     */
    void payRewards(Tables.Quest q, Tables.Outcome outcome) {
        int tier = story.tierOf(Tables.npcs.get(q.giver));
        float scale = Story.rewardScale(tier);
        long gold = q.gold + (outcome != null ? outcome.gold : 0), exp = q.exp + (outcome != null ? outcome.exp : 0);
        if (gold > 0) me.gold += Math.max(1, (long) Math.rint(gold * scale));
        if (exp > 0) me.addExp(Math.max(1, (long) Math.rint(exp * scale)), 0);

        give(q.item, q.itemCount);
        if (outcome != null) give(outcome.item, outcome.itemCount);
        if (!Story.empty(q.bonusItem) && tier >= q.bonusTier) give(q.bonusItem, 1);
    }

    /** Thêm count cái vật phẩm tên item vào túi (túi đầy thì phần thừa mất). */
    void give(String item, int count) {
        Tables.Item it = Tables.itemsByName.get(item);
        if (it != null && count > 0) me.bag.add(it.id, count);
    }

    /**
     * Dừng nhiệm vụ. finished: ghi một lần xong, kết cục, ngày, bật cờ DONE_FLAG. Bỏ dở mà chưa xong lần nào thì xóa
     * luôn bản ghi.
     */
    void stop(String quest, boolean finished, String outcome) {
        Record r = records.get(quest);
        if (r == null) return;
        r.active = false;
        r.progress.clear();
        if (finished) {
            r.timesDone++;
            r.outcome = outcome;
            r.lastDay = Story.today();
            story.flags.add(DONE_FLAG + quest);
        } else if (r.timesDone == 0) {
            records.remove(quest);
        }
    }

    /** NPC vừa thành kẻ thù: việc đang làm mà NPC đó giao, nhận trả, hoặc còn chờ gặp thì tự hủy, không trừ thêm thân thiện. */
    void onBecameEnemy(String npc) {
        for (Record r : new ArrayList<>(records.values())) {
            if (!r.active) continue;
            Tables.Quest q = Tables.quests.get(r.quest);
            if (q == null) continue;
            if (npc.equals(q.giver) || npc.equals(q.turnInNpc()) || pendingTalk(q, npc) >= 0) stop(q.name, false, "");
        }
        retrack();
    }

    // ---------------- tiến độ ----------------

    /** Hạ một con quái loại template: cộng một cho mọi mục tiêu hạ quái khớp. true khi có tiến độ đổi. */
    public boolean onMonsterKilled(int template) {
        boolean changed = false;
        String id = Integer.toString(template);
        for (Record r : records.values()) {
            Tables.Quest q = r.active ? Tables.quests.get(r.quest) : null;
            if (q == null) continue;
            for (int i = 0; i < q.objectives.size(); i++) {
                Tables.Objective o = q.objectives.get(i);
                if (o.kind != KIND_KILL || !id.equals(o.target) || r.progress(i) >= o.count) continue;
                advance(q, r, i, r.progress(i) + 1);
                changed = true;
            }
        }
        return changed;
    }

    /** Người chơi đứng ở (x, z) mét trên map: xong mọi mục tiêu tới nơi có vòng tròn chứa điểm đó. true khi có tiến độ đổi. */
    public boolean onPlayerAt(int map, double x, double z) {
        boolean changed = false;
        for (Record r : records.values()) {
            Tables.Quest q = r.active ? Tables.quests.get(r.quest) : null;
            if (q == null) continue;
            for (int i = 0; i < q.objectives.size(); i++) {
                Tables.Objective o = q.objectives.get(i);
                if (o.kind != KIND_REACH || o.mapId != map || r.progress(i) >= o.count) continue;
                if (Math.hypot(x - o.x, z - o.z) > o.radius) continue;
                advance(q, r, i, o.count);
                changed = true;
            }
        }
        return changed;
    }

    /** Client báo đã nói xong mục tiêu "nói chuyện" objective. Server tin client ở bước này. */
    public boolean completeTalk(Tables.Quest q, int objective) {
        Record r = q != null ? records.get(q.name) : null;
        if (r == null || !r.active || objective < 0 || objective >= q.objectives.size()) return false;
        Tables.Objective o = q.objectives.get(objective);
        if (o.kind != KIND_TALK || r.progress(objective) >= o.count) return false;
        r.setProgress(objective, o.count);
        return true;
    }

    /** Ghi tiến độ và thêm thông báo cho người chơi, như QuestSystem.Advance. Vừa đủ mọi mục tiêu thì nhắc quay lại trả việc. */
    void advance(Tables.Quest q, Record r, int objective, int value) {
        Tables.Objective o = q.objectives.get(objective);
        boolean wasReady = objectivesMet(q, r);
        r.setProgress(objective, Math.min(value, o.count));
        String title = Story.vi(q.title), label = objectiveLabel(o);
        if (o.count > 1) story.toasts.add(title + ": " + label + " " + r.progress(objective) + "/" + o.count);
        else story.toasts.add(title + ": " + label + " - xong");
        if (!wasReady && objectivesMet(q, r)) story.toasts.add("Xong việc \"" + title + "\": quay lại trả việc");
    }

    /** Tên mục tiêu để hiện trong thông báo: label nếu có, không thì tự đặt theo loại. */
    static String objectiveLabel(Tables.Objective o) {
        if (!Story.empty(o.label)) return Story.vi(o.label);
        if (o.kind == KIND_KILL) return "Hạ quái";
        if (!Story.empty(o.target)) return "Đến " + Story.vi(o.target);
        return "Đến nơi đánh dấu";
    }

    // ---------------- theo dõi ----------------

    /** Theo dõi được: có thể nhận, đang làm, hoặc chờ trả. */
    static boolean trackable(int st) {
        return st == AVAILABLE || st == ACTIVE || st == READY;
    }

    /** Nhiệm vụ đang theo dõi nếu nó vẫn theo dõi được, không thì null. */
    Tables.Quest trackedQuest() {
        Tables.Quest q = tracked != null ? Tables.quests.get(tracked) : null;
        return q != null && trackable(statusOf(q)) ? q : null;
    }

    /** Theo dõi nhiệm vụ name (rỗng là bỏ theo dõi). false khi nhiệm vụ không theo dõi được. */
    public boolean track(String name) {
        if (Story.empty(name)) {
            tracked = null;
            return true;
        }
        Tables.Quest q = Tables.quests.get(name);
        if (q == null || !trackable(statusOf(q))) return false;
        tracked = name;
        return true;
    }

    /** Việc đang theo dõi không còn hợp lệ thì chuyển sang việc nên làm tiếp ({@link #suggest}). */
    public void retrack() {
        if (trackedQuest() != null) return;
        Tables.Quest s = suggest();
        tracked = s != null ? s.name : null;
    }

    /**
     * Việc nên làm tiếp: chờ trả (3) hơn đang làm (2) hơn nhận được (1), việc chính được cộng 10. Cùng điểm thì lấy việc
     * đứng trước trong bảng.
     */
    Tables.Quest suggest() {
        Tables.Quest best = null;
        int bestScore = 0;
        for (Tables.Quest q : Tables.quests.values()) {
            int score = suggestScore(q);
            if (score > bestScore) {
                best = q;
                bestScore = score;
            }
        }
        return best;
    }

    /** Điểm của một nhiệm vụ cho {@link #suggest}, 0 khi không gợi ý. */
    int suggestScore(Tables.Quest q) {
        int st = statusOf(q), score;
        if (st == READY) score = 3;
        else if (st == ACTIVE) score = 2;
        else if (st == AVAILABLE && tierAllows(q)) score = 1;
        else return 0;
        return q.category == CATEGORY_MAIN ? score + 10 : score;
    }
}
