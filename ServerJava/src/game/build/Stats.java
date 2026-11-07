package game.build;

import game.item.ItemStack;
import game.player.Profile;
import game.tables.Tables;

/**
 * Chỉ số của một người chơi: mảng theo id trong tpl_options, cộng từ cây, Chuyên tinh, đồ, đá khảm, thuốc. Thêm option
 * mới chỉ cần sửa Tables (hằng số) và chỗ đọc chỉ số đó. Chỉ gọi khi giữ World.LOCK.
 */
public class Stats {
    final Profile me;
    public double[] bonus = new double[Tables.OPTION_COUNT];   // tổng chỉ số theo id option
    WeaponStats weaponStats = new WeaponStats();               // nâng cấp của vũ khí đang cầm, chưa nhân hiệu quả cây

    public Stats(Profile me) {
        this.me = me;
    }

    // ---------------- tính lại ----------------

    /** Gộp lại mọi nguồn chỉ số rồi áp máu / mana tối đa như PlayerProfile.ApplyBonus. Gọi sau mọi thay đổi ảnh hưởng chỉ số. */
    public void recompute() {
        bonus = sumBonus();
        weaponStats = sumWeapon();
        applyMaxHpMp();
    }

    /** Cộng chỉ số từ cây nhân vật (theo cấp nút), Chuyên tinh (nhánh đủ điểm), đồ đang mặc và thuốc đang có tác dụng. */
    double[] sumBonus() {
        double[] s = new double[Tables.OPTION_COUNT];
        for (Tables.Node node : Tables.nodes.values()) {
            int rank = Math.min(me.build.nodes.getOrDefault(node.id, 0), node.maxRank);
            if (rank > 0) for (Tables.Effect e : node.effects) add(s, e, rank);
        }
        for (int b = 0; b < Tables.branchCount; b++) {
            int spent = me.build.spent(b);
            for (Tables.Mastery m : Tables.masteries)
                if (m.branch == b && spent >= m.points) for (Tables.Effect e : m.effects) add(s, e, 1);
        }
        for (int i = 1; i <= Tables.SLOT_COUNT; i++) if (me.gear.slots[i] != null) addGear(s, me.gear.slots[i]);
        for (int id : me.consumables.buffs.keySet()) {
            Tables.Item it = Tables.items.get(id);
            if (it != null) for (Tables.Effect e : it.options) add(s, e, 1);
        }
        return s;
    }

    /** Cộng các nút nâng cấp đã mở của vũ khí đang cầm. */
    WeaponStats sumWeapon() {
        WeaponStats w = new WeaponStats();
        Tables.Weapon held = Tables.weapons.get(me.build.weapon);
        if (held == null) return w;
        for (Tables.Upgrade u : held.upgrades.values()) {
            int rank = Math.min(me.build.upgrades.getOrDefault(Build.key(held.id, u.id), 0), u.maxRank);
            if (rank > 0) for (Tables.Effect e : u.effects) w.add(e, rank);
        }
        return w;
    }

    /** Máu / mana tối đa = gốc + chỉ số. Tối đa tăng thì máu / mana hiện tại tăng theo (khi còn sống), giảm thì cắt bớt. */
    void applyMaxHpMp() {
        long newHp = me.baseHp + (long) bonus[Tables.MAX_HP], newMp = me.baseMp + (long) bonus[Tables.MAX_MP];
        if (!me.dead && newHp > me.maxHp) me.hp += newHp - me.maxHp;
        if (!me.dead && newMp > me.maxMp) me.mp += newMp - me.maxMp;
        me.maxHp = newHp;
        me.maxMp = newMp;
        me.hp = Math.min(me.hp, me.maxHp);
        me.mp = Math.min(me.mp, me.maxMp);
    }

    /**
     * Cộng một chỉ số times lần, như PlayerStats.Add: máu, mana làm tròn theo từng dòng. Option sau RANGE (của vũ khí, của
     * bình) không cộng vào nhân vật.
     */
    static void add(double[] stats, Tables.Effect e, int times) {
        if (e.stat > Tables.RANGE) return;
        double v = e.value * times;
        stats[e.stat] += e.stat <= Tables.MAX_MP ? Math.rint(v) : v;
    }

    /**
     * Chỉ số của một món đang mặc, như GearUpgradeTable.EffectsOf + AddGemEffects: option của món nhân theo cấp cường hóa,
     * vũ khí cường hóa cộng sức mạnh vũ khí, cộng thêm option của đá khảm.
     */
    static void addGear(double[] stats, ItemStack s) {
        Tables.Item it = Tables.items.get(s.itemId);
        if (it == null) return;
        double scale = s.level > 0 ? 1 + Tables.optionPerLevel * s.level / 100 : 1;
        for (Tables.Effect e : it.options)
            if (e.stat < Tables.ATTACK_DAMAGE) add(stats, new Tables.Effect(e.stat, e.value * scale), 1);
        if (s.level > 0 && it.kind == Tables.KIND_WEAPON) stats[Tables.WEAPON_POWER] += Tables.weaponPowerPerLevel * s.level;
        for (int i = 0; i < s.sockets; i++) {
            Tables.Item gem = Tables.items.get(s.gems[i]);
            if (gem != null) for (Tables.Effect e : gem.options) add(stats, e, 1);
        }
    }

    // ---------------- đọc chỉ số đã kẹp trần ----------------

    /** value kẹp trong [0, trần của option]; option không khai trần thì dùng fallback. */
    static double capped(double value, int option, double fallback) {
        double cap = Tables.caps[option] > 0 ? Tables.caps[option] : fallback;
        return Math.max(0, Math.min(value, cap));
    }

    /** Phần sát thương nhận vào được giáp giảm (0..1), trần mặc định 50%. */
    public double defenseCut() {
        return capped(bonus[Tables.DEFENSE], Tables.DEFENSE, 50) / 100;
    }

    /** Hệ số nhân hồi chiêu skill ô (1 - giảm hồi chiêu), trần giảm mặc định 40%. */
    public double skillCooldownScale() {
        return 1 - capped(bonus[Tables.SKILL_HASTE], Tables.SKILL_HASTE, 40) / 100;
    }

    /** Phần hồi chiêu đòn thường được giảm (0..1) từ nâng cấp vũ khí cộng chỉ số tốc đánh, trần mặc định 50%. */
    public double attackSpeedCut() {
        double cap = Tables.caps[Tables.ATTACK_SPEED] > 0 ? Tables.caps[Tables.ATTACK_SPEED] : 50;
        return Math.min(cap / 100, weaponStats.attackSpeed + bonus[Tables.ATTACK_SPEED] / 100);
    }

    /** Tỉ lệ chí mạng (0..1) = base + chỉ số, trần mặc định 60%. */
    public double critChance(double base) {
        double cap = Tables.caps[Tables.CRIT] > 0 ? Tables.caps[Tables.CRIT] : 60;
        return Math.min(cap / 100, base + bonus[Tables.CRIT] / 100);
    }

    /** Nâng cấp vũ khí sau khi nhân "hiệu quả nâng cấp" của cây (PlayerProfile.BoostedWeaponStats). */
    public WeaponStats boosted() {
        double k = 1 + Math.max(0, bonus[Tables.UPGRADE_BOOST]) / 100;
        WeaponStats b = new WeaponStats();
        WeaponStats w = weaponStats;
        b.projectiles = w.projectiles;
        b.targets = w.targets;
        b.attackSpeed = w.attackSpeed;
        b.damage = (int) Math.rint(w.damage * k);
        b.explosionDamage = (int) Math.rint(w.explosionDamage * k);
        b.explosionRadius = w.explosionRadius * k;
        b.range = w.range * k;
        b.arc = w.arc * k;
        return b;
    }
}
