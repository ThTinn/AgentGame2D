package game.player;

import java.io.*;
import java.util.*;
import game.build.Build;
import game.build.Stats;
import game.db.Json;
import game.item.Consumables;
import game.item.Equipment;
import game.item.GearForge;
import game.item.Inventory;
import game.story.Story;
import game.tables.Tables;

/**
 * Hồ sơ một người chơi ở server. Gốc là JSON PlayerData trong players.profile, trường server không quản giữ nguyên trong
 * {@link #root}. Lớp này giữ cấp, vàng, máu, mana và ghép các phần có luật riêng: hành trang, trang bị, đồ dùng, cường hóa
 * (game.item), skill và nâng cấp, chỉ số (game.build), cốt truyện (game.story). Chỉ gọi khi giữ World.LOCK.
 */
public class Profile {
    // các phần của PROFILE SYNC (ProfileSync của client), cộng bit để gửi nhiều phần
    public static final int PART_STORY = 1, PART_BAG = 2, PART_STATS = 4, PART_BUILD = 8, PART_ALL = 15;

    public final Map<String, Object> root;
    public final Inventory bag = new Inventory();
    public final Equipment gear = new Equipment(this);
    public final Consumables consumables = new Consumables(this);
    public final GearForge forge = new GearForge(this);
    public final Build build = new Build(this);
    public final Stats stats = new Stats(this);
    public int level, points;
    public long exp, gold, baseHp, baseMp, hp, maxHp, mp, maxMp;
    public boolean dead;
    public Story story;     // null khi chưa có bảng tpl_*

    /**
     * Dựng hồ sơ từ JSON. Chưa có bảng tpl_* thì chỉ đọc cấp, vàng, máu, mana gốc. Có bảng thì đọc thêm skill, đồ, nhiệm
     * vụ, tính lại chỉ số rồi hồi đầy máu và mana.
     */
    public Profile(String json) {
        root = Json.object(json);
        level = Math.max(1, (int) Json.num(root, "level"));
        exp = Json.num(root, "exp");
        points = (int) Json.num(root, "potentialPoints");
        gold = Json.num(root, "gold");
        baseHp = maxHp = hp = Json.num(root, "maxHp");
        baseMp = maxMp = mp = Json.num(root, "maxMp");
        build.weapon = (int) Json.num(root, "weaponId");
        if (!Tables.ready) return;

        build.load(root);
        consumables.load(root);
        gear.load(root, bag.load(root));
        stats.recompute();
        hp = maxHp;
        mp = maxMp;
        story = new Story(this);
        story.quests.retrack();    // bản lưu chưa theo dõi việc nào: theo việc nên làm tiếp, như CoreSystems lúc vào game
    }

    /** Hồ sơ hiện tại dạng JSON, cùng định dạng PlayerData của client. Ghi các phần server quản vào root rồi ghi cả root. */
    public String json() {
        if (!Tables.ready) return Json.write(root);
        root.put("level", (long) level);
        root.put("exp", exp);
        root.put("potentialPoints", (long) points);
        root.put("gold", gold);
        root.put("weaponId", (long) build.weapon);
        build.saveUpgrades(root);
        bag.save(root);
        gear.save(root);
        consumables.save(root);
        build.saveSkills(root);
        story.save(root);
        return Json.write(root);
    }

    /**
     * Cộng EXP (kèm bonusPercent % thưởng), lên cấp khi đủ, mỗi cấp được thêm điểm cây nhân vật. Tới cấp tối đa thì EXP
     * về 0. Lên cấp có thể hồi đầy máu, mana (refillOnLevelUp) nếu chưa chết.
     */
    public void addExp(long amount, double bonusPercent) {
        if (amount <= 0 || level >= Tables.maxLevel) return;
        exp += amount + (long) (amount * Math.max(0, bonusPercent) / 100);
        boolean leveled = false;
        while (level < Tables.maxLevel && exp >= Tables.expToNext(level)) {
            exp -= Tables.expToNext(level);
            level++;
            points += Tables.pointsForLevel(level);
            leveled = true;
        }
        if (level >= Tables.maxLevel) exp = 0;
        if (leveled && Tables.refillOnLevelUp && !dead) {
            hp = maxHp;
            mp = maxMp;
        }
    }

    /** Thân gói PROFILE SYNC cho các phần trong parts (client chỉ đọc phần có bit). Định dạng ở ProfileSync.cs. */
    byte[] sync(int parts) throws IOException {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        DataOutputStream w = new DataOutputStream(bytes);
        w.writeByte(parts);
        if ((parts & PART_STORY) != 0) story.write(w);
        if ((parts & PART_BAG) != 0) {
            bag.write(w);
            gear.write(w);
            consumables.write(w);
        }
        if ((parts & PART_STATS) != 0) writeStats(w);
        if ((parts & PART_BUILD) != 0) build.write(w);
        return bytes.toByteArray();
    }

    /** Phần STATS của SYNC: cấp, EXP, điểm, vàng, máu, mana. */
    void writeStats(DataOutputStream w) throws IOException {
        w.writeShort(level);
        w.writeLong(exp);
        w.writeInt(points);
        w.writeLong(gold);
        w.writeLong(hp);
        w.writeLong(maxHp);
        w.writeLong(mp);
        w.writeLong(maxMp);
    }
}
