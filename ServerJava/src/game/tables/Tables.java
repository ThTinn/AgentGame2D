package game.tables;

import java.util.*;

/**
 * Bảng luật đọc từ các table tpl_* lúc khởi động ({@link TableLoader}), chỉ đọc sau đó. Mọi chỉ số là cặp (id trong
 * tpl_options, giá trị) nên luật cộng chỉ số viết một lần ở Stats. Chưa đẩy template thì {@link #ready} là false: server
 * vẫn chạy, không có chiến đấu.
 */
public class Tables {
    // id trong tpl_options, trùng enum PlayerStat của client
    public static final int MAX_HP = 0, MAX_MP = 1, DAMAGE = 2, CRIT = 3, DEFENSE = 4, HP_REGEN = 6, EXP_BONUS = 7;
    public static final int GOLD_FIND = 8, ITEM_FIND = 9, WEAPON_POWER = 10, ATTACK_SPEED = 11, SKILL_HASTE = 12;
    public static final int CRIT_DAMAGE = 14, HIT_EXP = 15, UPGRADE_BOOST = 16, RANGE = 17, RESTORE_HP = 18, RESTORE_MP = 19;
    // gốc đòn đánh thường, nằm ở item vũ khí
    public static final int ATTACK_DAMAGE = 20, ATTACK_COOLDOWN = 21, ATTACK_RANGE = 22, ATTACK_TARGETS = 23;
    // nâng cấp vũ khí
    public static final int WEAPON_DAMAGE = 24, PROJECTILES = 25, EXPLOSION_RADIUS = 26, EXPLOSION_DAMAGE = 27, ARC = 28;
    public static final int OPTION_COUNT = 29;

    // ItemKind của client
    public static final int KIND_GEM = 0, KIND_WEAPON = 2, KIND_ARMOR = 3, KIND_SKIN = 4, KIND_HP_POTION = 5, KIND_MP_POTION = 6;
    public static final int KIND_BUFF = 7, KIND_STONE = 8, KIND_JEWEL = 9;
    // SkillKind, SkillType
    public static final int SKILL_BASIC = 1, TYPE_MELEE = 0, TYPE_PROJECTILE = 1, TYPE_AREA = 2, TYPE_DASH = 3;
    // EquipSlot
    public static final int SLOT_WEAPON = 5, SLOT_RING = 8, SLOT_SKIN = 9, SLOT_COUNT = 9;
    // GearStone
    public static final int STONE_UPGRADE = 1, STONE_LUCKY = 2, STONE_PROTECT = 3, STONE_DRILL = 4;
    // NpcAction
    public static final int NPC_HEAL = 1, NPC_BUY = 2;

    /** Một chỉ số: id trong tpl_options và giá trị. */
    public static class Effect {
        public int stat;
        public double value;

        public Effect(int stat, double value) {
            this.stat = stat;
            this.value = value;
        }
    }

    /** Vật phẩm (tpl_items). value: ô đồ mặc với giáp, các loại khác tùy kind. maxStack 0 là không giới hạn. */
    public static class Item {
        public int id, kind, rarity, value, maxStack;
        String name;
        public final List<Effect> options = new ArrayList<>();

        /** Bình máu, bình mana, thuốc tăng chỉ số: dùng được từ túi. */
        public boolean usable() {
            return kind == KIND_HP_POTION || kind == KIND_MP_POTION || kind == KIND_BUFF;
        }

        /** Ô đồ mặc của vật phẩm, 0 khi không mặc được. */
        public int slot() {
            if (kind == KIND_WEAPON) return SLOT_WEAPON;
            if (kind == KIND_SKIN) return SLOT_SKIN;
            if (kind == KIND_ARMOR && value > 0 && value <= SLOT_RING && value != SLOT_WEAPON) return value;
            return 0;
        }

        /** Cường hóa / khoét lỗ được: vũ khí hoặc giáp mặc được, không chồng. */
        public boolean upgradable() {
            return (kind == KIND_WEAPON || kind == KIND_ARMOR) && maxStack == 1 && slot() != 0;
        }

        /** Số tối đa trong một ô túi. */
        public int stackLimit() {
            return maxStack <= 0 ? Integer.MAX_VALUE : maxStack;
        }

        /** Giá trị của chỉ số stat trên vật phẩm, 0 khi không có. */
        public double option(int stat) {
            for (Effect e : options) if (e.stat == stat) return e.value;
            return 0;
        }

        /** Hai viên đá khảm cùng loại (cùng chỉ số đầu tiên): một món không khảm hai viên cùng loại. */
        public boolean sameJewel(Item o) {
            return kind == KIND_JEWEL && o != null && o.kind == KIND_JEWEL && !options.isEmpty() && !o.options.isEmpty()
                    && options.get(0).stat == o.options.get(0).stat;
        }
    }

    /** Một cấp của skill. */
    public static class Level {
        public int damage, manaUse, maxTarget;
        public double cooldown, range, radius;
    }

    /**
     * Skill (tpl_skills). kind: Basic hay skill ô; type: cận chiến, đạn, vùng, lướt. hitDelay: giây từ lúc tung tới khung vung
     * tay của anim (SkillTemplate.hitDelay), 0 = không đo.
     */
    public static class Skill {
        public int id, kind, type;
        public double hitDelay;
        final List<Level> levels = new ArrayList<>();

        /** Cấp n (đếm từ 1), kẹp vào khoảng có trong bảng. null khi skill không có cấp nào. */
        public Level level(int n) {
            if (levels.isEmpty()) return null;
            return levels.get(Math.max(1, Math.min(n, levels.size())) - 1);
        }
    }

    /** Một nút nâng cấp của vũ khí. parent 0 là nút gốc. */
    public static class Upgrade {
        public int id, parent, maxRank;
        public List<Effect> effects;
    }

    /** Vũ khí (tpl_weapons): skill đánh thường và cây nâng cấp. */
    public static class Weapon {
        public int id, basicSkill;
        public final Map<Integer, Upgrade> upgrades = new LinkedHashMap<>();
    }

    /** Một nút của cây nhân vật. */
    public static class Node {
        public int id, parent, maxRank, cost, requiredLevel, branch, ring;
        public List<Effect> effects;
    }

    /** Thưởng Chuyên tinh: đủ điểm trong một nhánh thì được thêm chỉ số. */
    public static class Mastery {
        public int branch, points;
        public List<Effect> effects;
    }

    /** Một mốc cường hóa hoặc khoét lỗ: tỉ lệ, số đá, vàng, rớt cấp khi hỏng. */
    public static class Step {
        public double chance;
        public int stones, protectStones;
        public long gold;
        public boolean dropOnFail;
    }

    /** Một dòng đồ rơi của quái: vật phẩm, tỉ lệ, số lượng min..max. */
    public static class Drop {
        public String item;
        public double chance;
        public int min, max;
    }

    /** Một lựa chọn của NPC: chữa, bán đồ, hoặc câu đáp. */
    public static class NpcOption {
        public String name, item;
        public int action;
        public long price;
        public List<Choice> choices;
    }

    /** NPC (tpl_npcs). */
    public static class Npc {
        public String name;
        public int startAffinity;
        public final Map<String, NpcOption> options = new HashMap<>();
        public final Map<String, Integer> gifts = new HashMap<>();     // vật phẩm -> thân thiện tăng khi tặng
    }

    /** Điều kiện của câu đáp / kết cục / nhiệm vụ. Trường trống là không đòi. */
    public static class Condition {
        public String requiresFlag, forbidsFlag, tierNpc, item;
        public int minTier, minAllies, allyTier, itemCount;
    }

    /** Một tác động của cốt truyện: đổi thân thiện, đặt cờ, ... (kind theo StoryEffectKind của client). */
    public static class StoryEffect {
        public int kind, amount;
        public String target;
    }

    /** Một câu đáp: điều kiện, cờ đặt khi chọn, các tác động. */
    public static class Choice {
        public String name, flag;
        public Condition condition;
        public List<StoryEffect> effects;
    }

    /** Một mục tiêu của nhiệm vụ: hạ quái, tới nơi, nộp đồ... */
    public static class Objective {
        public int kind, count, mapId;
        public String target, label;
        public double x, z, radius;
    }

    /** Một kết cục của nhiệm vụ: phần thưởng và tác động, chọn theo điều kiện. */
    public static class Outcome {
        public String name, item;
        public long gold, exp;
        public int itemCount;
        public Condition condition;
        public List<StoryEffect> effects;
    }

    /** Nhiệm vụ (tpl_quests). */
    public static class Quest {
        public String name, title, giver, turnIn, requires, item, bonusItem;
        public int category, minTier, itemCount, bonusTier;
        public boolean repeatable;
        public long gold, exp;
        public Condition condition;
        public final List<Objective> objectives = new ArrayList<>();
        public List<Choice> offerChoices;
        public final List<Outcome> outcomes = new ArrayList<>();
        public final Map<String, Integer> affinity = new LinkedHashMap<>();

        /** NPC nhận trả nhiệm vụ: turnIn, trống thì là người giao. */
        public String turnInNpc() {
            return turnIn.isEmpty() ? giver : turnIn;
        }
    }

    public static boolean ready;
    public static final double[] caps = new double[OPTION_COUNT];              // 0 = không có trần
    public static final Map<Integer, Item> items = new HashMap<>();
    public static final Map<String, Item> itemsByName = new HashMap<>();
    public static final Map<Integer, Weapon> weapons = new HashMap<>();
    public static final Map<Integer, Skill> skills = new HashMap<>();
    public static final Map<Integer, Node> nodes = new LinkedHashMap<>();
    public static final List<Mastery> masteries = new ArrayList<>();
    public static final Map<String, List<Drop>> drops = new HashMap<>();
    public static final List<Step> gearSteps = new ArrayList<>();
    public static final List<Step> socketSteps = new ArrayList<>();
    public static final Map<String, Npc> npcs = new LinkedHashMap<>();
    public static final Map<String, Quest> quests = new LinkedHashMap<>();    // giữ thứ tự bảng: việc gợi ý theo dõi lấy theo thứ tự này
    // tra theo khóa, dựng lúc nạp: đòn đánh nào cũng cần vũ khí của skill và item của vũ khí
    static final Map<Integer, Item> weaponItems = new HashMap<>();
    static final Map<Integer, Item> stones = new HashMap<>();
    static final Map<Integer, Weapon> basicWeapons = new HashMap<>();
    // trường đơn của các bảng (tpl_settings)
    public static double expBase, expGrowth, optionPerLevel, weaponPowerPerLevel, luckyBonus;
    public static int maxLevel, pointsPerLevel, milestoneEvery, milestoneBonusPoints, branchCount;
    public static long removeGemGold;
    public static boolean refillOnLevelUp;

    /** Vật phẩm đá cường hóa loại kind (STONE_*), null khi bảng không có. */
    public static Item stone(int kind) {
        return stones.get(kind);
    }

    /** Vũ khí có skill đánh thường là skillId, null khi skill đó không thuộc vũ khí nào. */
    public static Weapon weaponOfSkill(int skillId) {
        return basicWeapons.get(skillId);
    }

    /** Item của vũ khí (giữ option gốc của đòn đánh thường). */
    public static Item weaponItem(int weaponId) {
        return weaponItems.get(weaponId);
    }

    /** EXP cần để lên từ cấp level. */
    public static long expToNext(int level) {
        return Math.round(expBase * Math.pow(Math.max(1, level), expGrowth));
    }

    /** Điểm cây nhân vật nhận khi lên tới cấp level: cấp mốc được thêm điểm. */
    public static int pointsForLevel(int level) {
        boolean milestone = milestoneEvery > 0 && level % milestoneEvery == 0;
        return pointsPerLevel + (milestone ? milestoneBonusPoints : 0);
    }
}
