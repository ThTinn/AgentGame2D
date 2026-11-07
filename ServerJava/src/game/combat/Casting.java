package game.combat;

import java.io.*;
import game.build.Build;
import game.build.WeaponStats;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;
import game.world.GameMap;
import game.world.World;
import static game.combat.CombatConfig.*;

/**
 * Tung đòn: dựng skill như người chơi đang có, kiểm hồi chiêu và mana, tạo đòn đang bay / đang chém ở {@link Hits}. Port
 * từ reference/LocalServer (ServerCombat). Mốc ra đòn là khung vung tay của skill (hitDelay), cùng mốc client hiện hiệu
 * ứng (Skill_Controller.ImpactAt): đạn rời tay / nhát chém trúng ở đó.
 */
public class Casting {
    static final int BASIC = 0, FIRST_SLOT = 1000;     // id đòn: 0 là đòn thường, 1000 + ô là skill ô 1-4
    static final double SKILL_TICK = 0.05;              // Skill_MN.TICK
    static final double COOLDOWN_SLACK = 0.08, FAN_STEP = 12;

    enum Kind { BULLET, MELEE, AREA, OTHER }

    /** Skill như người chơi đang có, cùng cách dựng với LocalSkillBridge.Build. Tầm / bán kính tính bằng mét. */
    static class Cast {
        int id, template, level, percent, mana, maxTarget;
        Kind kind;
        boolean aroundSelf;     // phép vùng quanh người tung (tầm 0)
        double range, rangeDame, cooldown;
        double hitDelay;        // giây từ lúc tung tới khung vung tay: đạn rời tay, cận chiến / vùng trúng
    }

    static int lastHitId;

    /**
     * ATTACK: id đòn, điểm xuất phát, điểm nhắm (x100). Server tin vị trí client gửi. Dựng đòn, kiểm hồi chiêu / mana, rồi
     * tạo một đòn cho mỗi viên đạn (nâng cấp thêm đạn xòe thành quạt FAN_STEP độ) và báo cả khu BULLET_ADD.
     */
    public static void attack(Player p, DataInputStream r) throws IOException {
        int id = r.readShort();
        int sx = r.readInt(), sy = r.readInt(), sz = r.readInt(), tx = r.readInt(), ty = r.readInt(), tz = r.readInt();
        GameMap map = p.room;
        if (!Tables.ready || map == null || p.dead) return;
        Cast c = castOf(p, id);
        double now = World.now();
        if (c == null || !canCast(p, c, now)) return;

        double x = sx / 100.0, z = sz / 100.0, aimX = tx / 100.0, aimZ = tz / 100.0;
        double dx = aimX - x, dz = aimZ - z, len = Math.hypot(dx, dz);
        if (len < 0.01) {       // nhắm vào chính chỗ đứng: đánh thẳng về phía trước (+z)
            dx = 0;
            dz = 1;
        } else {
            dx /= len;
            dz /= len;
        }
        WeaponStats weapon = id == BASIC ? p.profile.stats.boosted() : new WeaponStats();
        int limit = id == BASIC && c.maxTarget > 0 ? c.maxTarget + weapon.targets : 0;
        double reach = Math.max(0.5, c.range + cells(weapon.range));

        if (c.aroundSelf) {
            aimX = p.x / 100.0;
            aimZ = p.z / 100.0;
        } else if (c.kind == Kind.AREA) {   // phép vùng: rơi ở điểm nhắm, xa quá tầm thì kéo về mép tầm
            double d = Math.min(len, c.range);
            aimX = x + dx * d;
            aimZ = z + dz * d;
        }

        int shots = c.kind == Kind.BULLET ? 1 + weapon.projectiles : 1;
        for (int i = 0; i < shots; i++) {
            double a = Math.toRadians(FAN_STEP * (i - (shots - 1) / 2.0)), cos = Math.cos(a), sin = Math.sin(a);
            Hits.Hit h = new Hits.Hit();
            h.map = map;
            h.attacker = p;
            h.cast = c;
            h.weapon = weapon;
            h.id = nextHitId();
            h.limit = limit;
            h.x = x;
            h.z = z;
            h.y = sy;
            h.dirX = dx * cos + dz * sin;
            h.dirZ = dz * cos - dx * sin;
            h.reach = reach;
            h.targetX = c.kind == Kind.BULLET ? x + h.dirX * reach : aimX;
            h.targetZ = c.kind == Kind.BULLET ? z + h.dirZ * reach : aimZ;
            h.hitAt = now + c.hitDelay;
            map.broadcast(Cmd.BULLET, addPacket(h, sx, sy, sz, ty, i > 0));
            Hits.hits.add(h);
        }
        p.flush(0);
    }

    /**
     * BULLET_ADD: id đòn, người tung, id skill của client, điểm đầu, điểm cuối, tốc đạn mỗi nhịp skill (x100), thời gian
     * tồn tại (-1 = theo skill), template, cấp, cờ đạn phụ (client không chạy lại anim tung).
     */
    static Packet addPacket(Hits.Hit h, int sx, int sy, int sz, int ty, boolean extraShot) throws IOException {
        Cast c = h.cast;
        Packet out = new Packet();
        out.w.writeByte(Hits.BULLET_ADD);
        out.w.writeShort(h.id);
        out.w.writeInt(h.attacker.id);
        out.w.writeShort(c.id);
        out.vec(sx, sy, sz);
        out.vec(Packet.cm(h.targetX), c.kind == Kind.BULLET ? sy : ty, Packet.cm(h.targetZ));
        out.w.writeInt(Math.max(1, (int) Math.round(Math.max(1, bulletSpeed) * 100 * SKILL_TICK)));
        out.w.writeByte(-1);        // timeMaxExist: theo skill
        out.w.writeShort(c.template);
        out.w.writeShort(c.level);
        out.w.writeBoolean(extraShot);
        return out;
    }

    /**
     * Dựng đòn id của p. Đòn thường theo vũ khí đang cầm; skill ô lấy theo ô (null khi ô trống). Skill loại Basic: gốc là
     * option của item vũ khí, cấp chỉ ghi phần tăng (SkillTemplate.Stats). Không cầm vũ khí thì đòn thường gốc cận chiến.
     */
    static Cast castOf(Player p, int id) {
        Profile pr = p.profile;
        int template, level;
        if (id == BASIC) {
            template = pr.build.basicId;
            level = pr.build.basicLevel;
        } else {
            int slot = id - FIRST_SLOT;
            if (slot < 0 || slot >= Build.SKILL_SLOTS || pr.build.skillId[slot] <= 0) return null;
            template = pr.build.skillId[slot];
            level = pr.build.skillLevel[slot];
        }
        Cast c = new Cast();
        c.id = id;
        c.template = template;
        c.level = level;
        Tables.Skill s = Tables.skills.get(template);
        Tables.Level lv = s != null ? s.level(level) : null;
        if (lv == null) {
            if (id != BASIC) return null;
            c.template = -1;
            c.kind = Kind.MELEE;
            c.range = c.rangeDame = 1.5;
            c.cooldown = basicCooldown;
            c.hitDelay = hitDelay;
            return c;
        }

        double damage = lv.damage, cooldown = lv.cooldown, range = lv.range;
        int maxTarget = lv.maxTarget;
        if (s.kind == Tables.SKILL_BASIC) {
            Tables.Weapon w = Tables.weaponOfSkill(template);
            Tables.Item it = w != null ? Tables.weaponItem(w.id) : null;
            if (it != null) {
                damage += it.option(Tables.ATTACK_DAMAGE);
                cooldown += it.option(Tables.ATTACK_COOLDOWN);
                range += it.option(Tables.ATTACK_RANGE);
                maxTarget += (int) Math.rint(it.option(Tables.ATTACK_TARGETS));
            }
        }
        c.aroundSelf = s.type == Tables.TYPE_AREA && range <= 0;
        c.kind = kindOf(s.type);
        c.range = Math.max(0.5, cells(range));
        double radius = cells(lv.radius);
        c.rangeDame = c.kind == Kind.MELEE ? Math.max(c.range, radius) : radius;
        c.cooldown = Math.max(0.05, cooldown);
        c.percent = Math.max(1, (int) Math.rint(damage));
        c.mana = id == BASIC ? 0 : Math.max(0, lv.manaUse);
        c.maxTarget = maxTarget;
        c.hitDelay = s.hitDelay > 0 ? s.hitDelay : hitDelay;   // skill chưa đo khung vung tay: số chung của world.bin
        return c;
    }

    /** Loại đòn theo SkillType: đạn, vùng, cận chiến (cả lướt), còn lại OTHER. */
    static Kind kindOf(int skillType) {
        if (skillType == Tables.TYPE_PROJECTILE) return Kind.BULLET;
        if (skillType == Tables.TYPE_AREA) return Kind.AREA;
        if (skillType == Tables.TYPE_MELEE || skillType == Tables.TYPE_DASH) return Kind.MELEE;
        return Kind.OTHER;
    }

    /**
     * Tung được không: hết hồi chiêu (nới COOLDOWN_SLACK giây cho trễ mạng) và đủ mana. Được thì trừ mana, đặt hồi chiêu
     * mới (đòn thường giảm theo tốc đánh, skill theo giảm hồi chiêu). Thiếu mana thì báo, tối đa mỗi giây một lần.
     */
    static boolean canCast(Player p, Cast c, double now) throws IOException {
        if (now < p.readyAt.getOrDefault(c.id, 0.0) - COOLDOWN_SLACK) return false;
        Profile pr = p.profile;
        if (c.mana > 0) {
            if (pr.mp < c.mana) {
                if (now >= p.nextDeny) {
                    p.nextDeny = now + 1;
                    p.conn.toast("Không đủ mana (" + c.mana + " MP)");
                }
                p.flush(Profile.PART_STATS);
                return false;
            }
            pr.mp -= c.mana;
            p.dirty |= Profile.PART_STATS;
        }
        double cd = c.id == BASIC ? c.cooldown * (1 - pr.stats.attackSpeedCut()) : c.cooldown * pr.stats.skillCooldownScale();
        p.readyAt.put(c.id, now + cd);
        return true;
    }

    /** Id đòn kế tiếp, quay vòng 1..31999 (vừa short của gói). */
    static int nextHitId() {
        lastHitId = lastHitId % 31999 + 1;
        return lastHitId;
    }
}
