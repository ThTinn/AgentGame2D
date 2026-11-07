package game.tables;

import java.util.*;
import game.db.Database;
import game.db.Json;
import game.db.TemplateStore;
import static game.tables.Tables.*;

/** Parse các hàng tpl_* thành {@link Tables} lúc khởi động. Mỗi bảng một hàm load*, tên trường trùng CoreTemplates của client. */
public class TableLoader {
    /** Đọc mọi table tpl_* từ MySQL rồi điền Tables. Chưa đẩy template lần nào thì để nguyên, {@link Tables#ready} vẫn false. */
    public static void load() throws Database.Failure {
        Map<String, List<Map<String, Object>>> t = TemplateStore.loadRules();
        if (t == null) return;
        loadOptions(t.get("tpl_options"));
        loadItems(t.get("tpl_items"));
        loadSkills(t.get("tpl_skills"));
        loadWeapons(t.get("tpl_weapons"));
        loadTree(t.get("tpl_upgrade_nodes"), t.get("tpl_masteries"));
        loadDrops(t.get("tpl_drops"));
        loadGear(t.get("tpl_gear_steps"), t.get("tpl_gear_sockets"));
        loadNpcs(t.get("tpl_npcs"));
        loadQuests(t.get("tpl_quests"));
        buildIndexes();
        loadSettings(TemplateStore.setting(t, "upgrade_tree"), TemplateStore.setting(t, "gear"));
        ready = true;
    }

    // ---------------- từng bảng ----------------

    /** Trần của từng chỉ số (option có capKind khác 0). */
    static void loadOptions(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            int id = integer(r, "id");
            if (id < OPTION_COUNT && Json.num(r, "capKind") != 0) caps[id] = Json.real(r, "cap");
        }
    }

    /** Vật phẩm, tra được theo id và theo tên. */
    static void loadItems(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            Item it = new Item();
            it.id = integer(r, "id");
            it.name = Json.str(r, "name");
            it.kind = integer(r, "kind");
            it.rarity = integer(r, "rarity");
            it.value = integer(r, "value");
            it.maxStack = integer(r, "maxStack");
            it.options.addAll(effects(r, "options"));
            items.put(it.id, it);
            itemsByName.put(it.name, it);
        }
    }

    /** Skill và các cấp của nó. */
    static void loadSkills(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            Skill s = new Skill();
            s.id = integer(r, "id");
            s.kind = integer(r, "kind");
            s.type = integer(r, "type");
            s.hitDelay = Json.real(r, "hitDelay");
            for (Map<String, Object> l : Json.rows(r, "levels")) {
                Level lv = new Level();
                lv.damage = integer(l, "damage");
                lv.manaUse = integer(l, "manaUse");
                lv.maxTarget = integer(l, "maxTarget");
                lv.cooldown = Json.real(l, "cooldown");
                lv.range = Json.real(l, "range");
                lv.radius = Json.real(l, "radius");
                s.levels.add(lv);
            }
            skills.put(s.id, s);
        }
    }

    /** Vũ khí và cây nâng cấp của nó. */
    static void loadWeapons(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            Weapon w = new Weapon();
            w.id = integer(r, "id");
            w.basicSkill = integer(r, "basicSkill");
            for (Map<String, Object> u : Json.rows(r, "upgrades")) {
                Upgrade up = new Upgrade();
                up.id = integer(u, "id");
                up.parent = integer(u, "parent");
                up.maxRank = integer(u, "maxRank");
                up.effects = effects(u, "effects");
                w.upgrades.put(up.id, up);
            }
            weapons.put(w.id, w);
        }
    }

    /** Cây nhân vật và thưởng Chuyên tinh theo nhánh. */
    static void loadTree(List<Map<String, Object>> nodeRows, List<Map<String, Object>> masteryRows) {
        for (Map<String, Object> r : nodeRows) {
            Node n = new Node();
            n.id = integer(r, "id");
            n.parent = integer(r, "parent");
            n.maxRank = integer(r, "maxRank");
            n.cost = integer(r, "cost");
            n.requiredLevel = integer(r, "requiredLevel");
            n.branch = integer(r, "branch");
            n.ring = integer(r, "ring");
            n.effects = effects(r, "effects");
            nodes.put(n.id, n);
        }
        for (Map<String, Object> r : masteryRows) {
            Mastery m = new Mastery();
            m.branch = integer(r, "branch");
            m.points = integer(r, "points");
            m.effects = effects(r, "effects");
            masteries.add(m);
        }
    }

    /** Bảng đồ rơi theo tên (quái trỏ tới tên bảng). */
    static void loadDrops(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            List<Drop> list = new ArrayList<>();
            for (Map<String, Object> e : Json.rows(r, "entries")) {
                Drop d = new Drop();
                d.item = Json.str(e, "item");
                d.chance = Json.real(e, "chance");
                d.min = integer(e, "min");
                d.max = integer(e, "max");
                list.add(d);
            }
            drops.put(Json.str(r, "name"), list);
        }
    }

    /** Các mốc cường hóa và khoét lỗ. */
    static void loadGear(List<Map<String, Object>> stepRows, List<Map<String, Object>> socketRows) {
        for (Map<String, Object> r : stepRows) {
            Step s = step(r);
            s.dropOnFail = flag(r, "dropOnFail");
            s.protectStones = integer(r, "protectStones");
            gearSteps.add(s);
        }
        for (Map<String, Object> r : socketRows) socketSteps.add(step(r));
    }

    /** Phần chung của một mốc cường hóa / khoét lỗ. */
    static Step step(Map<String, Object> r) {
        Step s = new Step();
        s.chance = Json.real(r, "chance");
        s.stones = integer(r, "stones");
        s.gold = Json.num(r, "gold");
        return s;
    }

    /** NPC: lựa chọn và quà tặng. */
    static void loadNpcs(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            Npc npc = new Npc();
            npc.name = Json.str(r, "name");
            npc.startAffinity = integer(r, "startAffinity");
            for (Map<String, Object> o : Json.rows(r, "options")) {
                NpcOption n = new NpcOption();
                n.name = Json.str(o, "name");
                n.item = Json.str(o, "item");
                n.action = integer(o, "action");
                n.price = Json.num(o, "price");
                n.choices = choices(o, "choices");
                npc.options.put(n.name, n);
            }
            for (Map<String, Object> g : Json.rows(r, "gifts")) npc.gifts.put(Json.str(g, "item"), integer(g, "affinity"));
            npcs.put(npc.name, npc);
        }
    }

    /** Nhiệm vụ, giữ thứ tự bảng. */
    static void loadQuests(List<Map<String, Object>> rows) {
        for (Map<String, Object> r : rows) {
            Quest q = new Quest();
            q.name = Json.str(r, "name");
            q.title = Json.str(r, "title");
            q.giver = Json.str(r, "giver");
            q.turnIn = Json.str(r, "turnIn");
            q.requires = Json.str(r, "requires");
            q.item = Json.str(r, "item");
            q.itemCount = integer(r, "itemCount");
            q.bonusItem = Json.str(r, "bonusItem");
            q.bonusTier = integer(r, "bonusTier");
            q.category = integer(r, "category");
            q.minTier = integer(r, "minTier");
            q.repeatable = flag(r, "repeatable");
            q.gold = Json.num(r, "gold");
            q.exp = Json.num(r, "exp");
            q.condition = condition(r);
            q.offerChoices = choices(r, "offerChoices");
            for (Map<String, Object> o : Json.rows(r, "objectives")) q.objectives.add(objective(o));
            for (Map<String, Object> o : Json.rows(r, "outcomes")) q.outcomes.add(outcome(o));
            for (Map<String, Object> a : Json.rows(r, "affinity")) q.affinity.put(Json.str(a, "npc"), integer(a, "amount"));
            quests.put(q.name, q);
        }
    }

    /** Một mục tiêu nhiệm vụ. "where" trống thì mapId = -1 (không giới hạn nơi); điểm lưu dạng (x, y) với y là trục z. */
    static Objective objective(Map<String, Object> o) {
        Objective x = new Objective();
        x.kind = integer(o, "kind");
        x.count = integer(o, "count");
        x.target = Json.str(o, "target");
        x.label = Json.str(o, "label");
        Map<String, Object> where = object(o, "where");
        x.mapId = where.isEmpty() ? -1 : integer(where, "mapId");
        x.radius = Json.real(where, "radius");
        Map<String, Object> point = object(where, "point");
        x.x = Json.real(point, "x");
        x.z = Json.real(point, "y");
        return x;
    }

    /** Một kết cục nhiệm vụ. */
    static Outcome outcome(Map<String, Object> o) {
        Outcome x = new Outcome();
        x.name = Json.str(o, "name");
        x.item = Json.str(o, "item");
        x.itemCount = integer(o, "itemCount");
        x.gold = Json.num(o, "gold");
        x.exp = Json.num(o, "exp");
        x.condition = condition(o);
        x.effects = storyEffects(o);
        return x;
    }

    /** Bảng tra nhanh: item của vũ khí, item của từng loại đá, vũ khí theo skill đánh thường. Gặp trùng thì giữ cái đầu. */
    static void buildIndexes() {
        for (Item it : items.values()) {
            if (it.kind == KIND_WEAPON) weaponItems.putIfAbsent(it.value, it);
            if (it.kind == KIND_STONE) stones.putIfAbsent(it.value, it);
        }
        for (Weapon w : weapons.values()) basicWeapons.putIfAbsent(w.basicSkill, w);
    }

    /** Trường đơn của cây nhân vật (EXP, điểm theo cấp) và của cường hóa (tpl_settings). */
    static void loadSettings(Map<String, Object> tree, Map<String, Object> gear) {
        expBase = Json.real(tree, "expBase");
        expGrowth = Json.real(tree, "expGrowth");
        maxLevel = integer(tree, "maxLevel");
        pointsPerLevel = integer(tree, "pointsPerLevel");
        milestoneEvery = integer(tree, "milestoneEvery");
        milestoneBonusPoints = integer(tree, "milestoneBonusPoints");
        refillOnLevelUp = Json.bool(tree, "refillOnLevelUp");
        Object names = tree.get("branchNames");
        branchCount = names instanceof List ? ((List<?>) names).size() : 0;

        optionPerLevel = Json.real(gear, "optionPerLevel");
        weaponPowerPerLevel = Json.real(gear, "weaponPowerPerLevel");
        luckyBonus = Json.real(gear, "luckyBonus");
        removeGemGold = Json.num(gear, "removeGemGold");
    }

    // ---------------- phần dùng chung ----------------

    /** Trường số nguyên kiểu int, thiếu thì 0. */
    static int integer(Map<String, Object> m, String key) {
        return (int) Json.num(m, key);
    }

    /** Cờ đúng / sai: MySQL có thể trả BOOLEAN hoặc số 0 / 1. */
    static boolean flag(Map<String, Object> m, String key) {
        return Json.bool(m, key) || Json.num(m, key) != 0;
    }

    /** Trường object lồng, thiếu thì map rỗng. */
    @SuppressWarnings("unchecked")
    static Map<String, Object> object(Map<String, Object> row, String key) {
        Object v = row.get(key);
        return v instanceof Map ? (Map<String, Object>) v : new HashMap<>();
    }

    /** Mảng chỉ số {stat, value}. */
    static List<Effect> effects(Map<String, Object> row, String key) {
        List<Effect> list = new ArrayList<>();
        for (Map<String, Object> e : Json.rows(row, key)) list.add(new Effect(integer(e, "stat"), Json.real(e, "value")));
        return list;
    }

    /** Trường "condition" của hàng, thiếu thì điều kiện rỗng (không đòi gì). */
    static Condition condition(Map<String, Object> row) {
        Map<String, Object> m = object(row, "condition");
        Condition c = new Condition();
        c.requiresFlag = Json.str(m, "requiresFlag");
        c.forbidsFlag = Json.str(m, "forbidsFlag");
        c.tierNpc = Json.str(m, "tierNpc");
        c.item = Json.str(m, "item");
        c.minTier = integer(m, "minTier");
        c.minAllies = integer(m, "minAllies");
        c.allyTier = integer(m, "allyTier");
        c.itemCount = integer(m, "itemCount");
        return c;
    }

    /** Mảng "effects" của câu đáp / kết cục. */
    static List<StoryEffect> storyEffects(Map<String, Object> row) {
        List<StoryEffect> list = new ArrayList<>();
        for (Map<String, Object> e : Json.rows(row, "effects")) {
            StoryEffect s = new StoryEffect();
            s.kind = integer(e, "kind");
            s.amount = integer(e, "amount");
            s.target = Json.str(e, "target");
            list.add(s);
        }
        return list;
    }

    /** Mảng câu đáp ở trường key. */
    static List<Choice> choices(Map<String, Object> row, String key) {
        List<Choice> list = new ArrayList<>();
        for (Map<String, Object> c : Json.rows(row, key)) {
            Choice x = new Choice();
            x.name = Json.str(c, "name");
            x.flag = Json.str(c, "flag");
            x.condition = condition(c);
            x.effects = storyEffects(c);
            list.add(x);
        }
        return list;
    }
}
