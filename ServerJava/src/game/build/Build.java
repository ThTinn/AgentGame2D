package game.build;

import java.io.*;
import java.util.*;
import game.db.Json;
import game.player.Profile;
import game.tables.Tables;

/**
 * Skill trên nút, vũ khí đang cầm, nâng cấp vũ khí, cây nhân vật: port các hàm luật của PlayerProgress,
 * WeaponUpgradeState. Chỉ gọi khi giữ World.LOCK.
 */
public class Build {
    public static final int SKILL_SLOTS = 4;

    final Profile me;
    public final int[] skillId = new int[SKILL_SLOTS], skillLevel = new int[SKILL_SLOTS];     // skill ô 1-4, 0 là trống
    final Map<Integer, Integer> upgrades = new LinkedHashMap<>();  // key(vũ khí, nút) -> cấp
    final Map<Integer, Integer> nodes = new LinkedHashMap<>();     // nút cây nhân vật -> cấp
    public int weapon, basicId, basicLevel;                         // vũ khí đang cầm, skill đánh thường của nó

    public Build(Profile me) {
        this.me = me;
    }

    // ---------------- lưu, gửi ----------------

    /** Nạp cây nhân vật, nâng cấp vũ khí, skill đánh thường và skill ô từ hồ sơ. Nút cấp 0 bỏ qua. */
    public void load(Map<String, Object> root) {
        for (Map<String, Object> r : Json.rows(root, "playerUpgrades"))
            if (Json.num(r, "rank") > 0) nodes.put((int) Json.num(r, "id"), (int) Json.num(r, "rank"));
        for (Map<String, Object> r : Json.rows(root, "weaponUpgrades"))
            if (Json.num(r, "rank") > 0) {
                int k = key((int) Json.num(r, "weaponId"), (int) Json.num(r, "upgradeId"));
                upgrades.put(k, (int) Json.num(r, "rank"));
            }

        Map<String, Object> basic = object(root, "basicSkill");
        basicId = (int) Json.num(basic, "templateId");
        basicLevel = Math.max(1, (int) Json.num(basic, "level"));
        List<Map<String, Object>> slots = Json.rows(root, "skillSlots");
        for (int i = 0; i < SKILL_SLOTS && i < slots.size(); i++) {
            skillId[i] = (int) Json.num(slots.get(i), "templateId");
            skillLevel[i] = skillId[i] > 0 ? Math.max(1, (int) Json.num(slots.get(i), "level")) : 0;
        }
    }

    /** Trường object lồng của hồ sơ, thiếu thì map rỗng. */
    @SuppressWarnings("unchecked")
    static Map<String, Object> object(Map<String, Object> root, String key) {
        Object v = root.get(key);
        return v instanceof Map ? (Map<String, Object>) v : new HashMap<>();
    }

    /** Ghi cây nhân vật ("playerUpgrades") và nâng cấp vũ khí ("weaponUpgrades") vào hồ sơ. */
    public void saveUpgrades(Map<String, Object> root) {
        List<Object> nodeList = new ArrayList<>();
        for (Map.Entry<Integer, Integer> e : nodes.entrySet()) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("id", (long) e.getKey());
            m.put("rank", (long) e.getValue());
            nodeList.add(m);
        }
        root.put("playerUpgrades", nodeList);

        List<Object> upgradeList = new ArrayList<>();
        for (Map.Entry<Integer, Integer> e : upgrades.entrySet()) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("weaponId", (long) (e.getKey() >> 16));
            m.put("upgradeId", (long) (e.getKey() & 0xFFFF));
            m.put("rank", (long) e.getValue());
            upgradeList.add(m);
        }
        root.put("weaponUpgrades", upgradeList);
    }

    /** Ghi skill đánh thường và skill ô vào hồ sơ. Ô trống ghi cấp 1 như client. */
    public void saveSkills(Map<String, Object> root) {
        Map<String, Object> basic = new LinkedHashMap<>();
        basic.put("templateId", (long) basicId);
        basic.put("level", (long) basicLevel);
        root.put("basicSkill", basic);
        List<Object> list = new ArrayList<>();
        for (int i = 0; i < SKILL_SLOTS; i++) {
            Map<String, Object> m = new LinkedHashMap<>();
            m.put("templateId", (long) skillId[i]);
            m.put("level", (long) Math.max(1, skillLevel[i]));
            list.add(m);
        }
        root.put("skillSlots", list);
    }

    /** Phần BUILD của gói SYNC: skill ô, nâng cấp vũ khí, cây nhân vật. */
    public void write(DataOutputStream w) throws IOException {
        w.writeByte(SKILL_SLOTS);
        for (int i = 0; i < SKILL_SLOTS; i++) {
            w.writeShort(skillId[i]);
            w.writeByte(skillLevel[i]);
        }
        w.writeShort(upgrades.size());
        for (Map.Entry<Integer, Integer> e : upgrades.entrySet()) {
            w.writeShort(e.getKey() >> 16);
            w.writeShort(e.getKey() & 0xFFFF);
            w.writeByte(e.getValue());
        }
        w.writeShort(nodes.size());
        for (Map.Entry<Integer, Integer> e : nodes.entrySet()) {
            w.writeShort(e.getKey());
            w.writeByte(e.getValue());
        }
    }

    // ---------------- luật ----------------

    /** Khóa của một nút nâng cấp vũ khí: id vũ khí ở 16 bit cao, id nút ở 16 bit thấp. */
    static int key(int weaponId, int upgradeId) {
        return weaponId << 16 | upgradeId & 0xFFFF;
    }

    /** Số điểm đã tiêu vào nhánh branch của cây (không tính vòng gốc ring 0), để xét thưởng Chuyên tinh. */
    int spent(int branch) {
        int n = 0;
        for (Map.Entry<Integer, Integer> e : nodes.entrySet()) {
            Tables.Node node = Tables.nodes.get(e.getKey());
            if (node != null && node.ring > 0 && node.branch == branch) n += node.cost * Math.min(e.getValue(), node.maxRank);
        }
        return n;
    }

    /** Cầm vũ khí id (0 hoặc id lạ là tay không), đổi skill đánh thường theo vũ khí. */
    public void setWeapon(int id) {
        Tables.Weapon w = Tables.weapons.get(id);
        weapon = w != null ? w.id : 0;
        basicId = w != null ? w.basicSkill : 0;
    }

    /** Gắn skill id vào ô slot (0 là gỡ). Skill đang ở ô khác thì chuyển sang, giữ cấp. */
    public boolean setSkill(int slot, int id) {
        if (slot < 0 || slot >= SKILL_SLOTS || (id != 0 && !Tables.skills.containsKey(id))) return false;
        int lv = 1;
        for (int i = 0; i < SKILL_SLOTS; i++) {
            if (id == 0 || i == slot || skillId[i] != id) continue;
            lv = skillLevel[i];
            skillId[i] = 0;
            skillLevel[i] = 0;
        }
        skillId[slot] = id;
        skillLevel[slot] = id > 0 ? lv : 0;
        return true;
    }

    /** Mở thêm một cấp nút upgradeId của vũ khí. Cần chưa tối đa và nút cha đã mở. */
    public boolean upgradeWeapon(int weaponId, int upgradeId) {
        Tables.Weapon w = Tables.weapons.get(weaponId);
        Tables.Upgrade u = w != null ? w.upgrades.get(upgradeId) : null;
        if (u == null) return false;
        int rank = upgrades.getOrDefault(key(weaponId, upgradeId), 0);
        boolean parentOpen = u.parent == 0 || upgrades.getOrDefault(key(weaponId, u.parent), 0) > 0;
        if (rank >= u.maxRank || !parentOpen) return false;
        upgrades.put(key(weaponId, upgradeId), rank + 1);
        me.stats.recompute();
        return true;
    }

    /** Xóa mọi nâng cấp của một vũ khí. */
    public boolean resetWeapon(int weaponId) {
        if (!Tables.weapons.containsKey(weaponId)) return false;
        upgrades.keySet().removeIf(k -> k >> 16 == weaponId);
        me.stats.recompute();
        return true;
    }

    /** Mở thêm một cấp nút cây nhân vật: cần chưa tối đa, nút cha đã mở, đủ cấp và đủ điểm. */
    public boolean upgradeNode(int id) {
        Tables.Node n = Tables.nodes.get(id);
        if (n == null) return false;
        int rank = nodes.getOrDefault(id, 0);
        boolean parentOpen = n.parent == 0 || nodes.getOrDefault(n.parent, 0) > 0;
        if (rank >= n.maxRank || !parentOpen || me.level < n.requiredLevel || me.points < n.cost) return false;
        me.points -= n.cost;
        nodes.put(id, rank + 1);
        me.stats.recompute();
        return true;
    }

    /** Tẩy cây nhân vật: trả lại toàn bộ điểm đã tiêu. */
    public void resetTree() {
        for (Map.Entry<Integer, Integer> e : nodes.entrySet()) {
            Tables.Node n = Tables.nodes.get(e.getKey());
            if (n != null) me.points += n.cost * Math.min(e.getValue(), n.maxRank);
        }
        nodes.clear();
        me.stats.recompute();
    }
}
