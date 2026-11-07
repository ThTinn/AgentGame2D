package game.combat;

import java.io.*;
import java.util.*;
import game.build.WeaponStats;
import game.mob.Mob;
import game.mob.MobAI;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;
import game.world.GameMap;
import game.world.World;
import static game.combat.CombatConfig.*;

/**
 * Đòn đang bay / đang chém: đạn, cận chiến, phép vùng, nổ, tính sát thương và gói BULLET. Mọi đòn chờ tới mốc vung tay
 * ({@link Hit#hitAt}) như client, nên gói trúng / quái chết tới client đúng lúc client cho đạn tới nơi / nhát chém ra.
 */
public class Hits {
    // sub của lệnh BULLET
    static final int BULLET_ADD = 0, BULLET_END = 1, BULLET_HIT = 2, BULLET_BLAST = 3;
    static final double AFTER_HIT = 0.8;    // cận chiến / vùng: giữ đòn thêm chừng này giây sau mốc trúng rồi mới báo xong

    /** Một đòn đang chạy. Tọa độ mét, riêng y giữ x100 như gói. */
    static class Hit {
        GameMap map;
        Player attacker;
        Casting.Cast cast;
        WeaponStats weapon;
        int id, limit, contacts, y;         // limit: số con tối đa (0 = không giới hạn), contacts: số lần đạn đã chạm
        double x, z, dirX, dirZ;            // điểm xuất phát, hướng đã chuẩn hóa
        double targetX, targetZ, reach;     // điểm đích, tầm bay của đạn
        double flown, hitAt, endAt;         // quãng đạn đã bay, mốc vung tay (đạn rời tay / cận chiến, vùng trúng), lúc báo xong
        boolean hitDone;
        final List<Mob> struck = new ArrayList<>();     // quái đã trúng đòn này, không trúng lần hai
    }

    /** Sát thương lên một con. */
    static class Damage {
        Mob mob;
        long amount;

        Damage(Mob mob, long amount) {
            this.mob = mob;
            this.amount = amount;
        }
    }

    static final List<Hit> hits = new ArrayList<>();

    /** Một nhịp của mọi đòn đang chạy (WorldLoop gọi). Đòn xong thì bỏ khỏi danh sách. */
    public static void tickHits(double now, double dt) throws IOException {
        for (int i = hits.size() - 1; i >= 0; i--) {
            Hit h = hits.get(i);
            boolean done = h.cast.kind == Casting.Kind.BULLET ? tickBullet(h, now, dt) : tickTimed(h, now);
            if (done) hits.remove(i);
        }
    }

    /**
     * Một nhịp của đạn: chưa tới mốc vung tay thì đạn còn trên tay; sau đó xét cả đoạn bay trong nhịp này (nhịp đầu chỉ tính
     * phần sau mốc). Chạm quái hoặc hết tầm thì nổ ở điểm chạm; còn được chạm thêm (limit) thì bay tiếp. Trả true khi đạn
     * kết thúc.
     */
    static boolean tickBullet(Hit h, double now, double dt) throws IOException {
        if (now < h.hitAt) return false;
        double from = h.flown, to = from + Math.max(1, bulletSpeed) * Math.min(dt, now - h.hitAt);
        double ax = h.x + h.dirX * from, az = h.z + h.dirZ * from, bx = h.x + h.dirX * to, bz = h.z + h.dirZ * to;
        Mob hit = Targeting.firstOnSegment(h.map, ax, az, bx, bz, bulletHitRadius, h.struck);
        h.flown = to;
        if (hit == null && to < h.reach) return false;

        double ix = bx, iz = bz;    // điểm chạm: chỗ gần quái nhất trên đoạn bay, hết tầm thì cuối đoạn
        if (hit != null) {
            double t = Targeting.closestT(ax, az, bx, bz, hit.x, hit.z);
            ix = ax + (bx - ax) * t;
            iz = az + (bz - az) * t;
        }
        List<Mob> victims = bulletVictims(h, hit, ix, iz);
        boolean last = hit == null || ++h.contacts >= h.limit;
        List<Damage> results = damage(h, victims, 1);
        if (!victims.isEmpty()) blast(h, ix, iz, results);
        sendBullet(h, last ? BULLET_END : BULLET_HIT, results, ix, iz);
        finish(h, results);
        return last;
    }

    /** Quái trúng ở điểm chạm: mọi con trong vùng rangeDame (nếu có) cộng con bị chạm, trừ các con đã trúng đòn này. */
    static List<Mob> bulletVictims(Hit h, Mob hit, double ix, double iz) {
        List<Mob> victims = new ArrayList<>();
        if (h.cast.rangeDame > 0.01) Targeting.collectCircle(h.map, ix, iz, h.cast.rangeDame, victims);
        if (hit != null && !victims.contains(hit)) victims.add(hit);
        victims.removeAll(h.struck);
        h.struck.addAll(victims);
        return victims;
    }

    /**
     * Một nhịp của cận chiến / phép vùng: tới mốc chém thì tính trúng một lần, giữ thêm AFTER_HIT giây rồi báo xong để
     * skill bên client tự dọn. Trả true khi đã báo xong.
     */
    static boolean tickTimed(Hit h, double now) throws IOException {
        if (h.hitDone) {
            if (now < h.endAt) return false;
            sendBullet(h, BULLET_END, new ArrayList<>(), h.targetX, h.targetZ);
            return true;
        }
        if (now < h.hitAt) return false;
        h.hitDone = true;
        h.endAt = now + AFTER_HIT;

        List<Mob> victims = new ArrayList<>();
        double cx = h.targetX, cz = h.targetZ;      // chỗ hiện hiệu ứng trúng
        if (h.cast.kind == Casting.Kind.MELEE) {
            double ox = h.attacker.x / 100.0, oz = h.attacker.z / 100.0;
            meleeVictims(h, ox, oz, victims);
            cx = ox + h.dirX;
            cz = oz + h.dirZ;
        } else if (h.cast.kind == Casting.Kind.AREA) {
            Targeting.collectCircle(h.map, cx, cz, Math.max(0.5, h.cast.rangeDame), victims);
        }
        if (victims.isEmpty()) return false;

        double nearX = victims.get(0).x, nearZ = victims.get(0).z;
        h.struck.addAll(victims);
        List<Damage> results = damage(h, victims, 1);
        blast(h, nearX, nearZ, results);
        sendBullet(h, BULLET_HIT, results, cx, cz);
        finish(h, results);
        return false;
    }

    /**
     * Quái trong hình quạt trước mặt người đánh (đứng ở ox, oz): bán kính tầm đòn + 0.5 (ít nhất 1) + tầm cộng của vũ khí,
     * góc meleeHalfAngle + góc cộng của vũ khí. Xếp gần trước, cắt còn limit con.
     */
    static void meleeVictims(Hit h, double ox, double oz, List<Mob> victims) {
        double radius = Math.max(1, h.cast.rangeDame + 0.5) + cells(h.weapon.range);
        Targeting.collectCone(h.map, ox, oz, h.dirX, h.dirZ, radius, meleeHalfAngle + h.weapon.arc, victims);
        victims.sort(Comparator.comparingDouble(m -> Math.hypot(m.x - ox, m.z - oz)));
        if (h.limit > 0 && victims.size() > h.limit) victims.subList(h.limit, victims.size()).clear();
    }

    /**
     * Nâng cấp nổ: vụ nổ ở (x, z) gây explosionPercent % sát thương lên những con đòn chưa chạm
     * tới. Kết quả cộng vào into.
     */
    static void blast(Hit h, double x, double z, List<Damage> into) throws IOException {
        if (h.weapon.explosionRadius <= 0) return;
        Packet out = new Packet();
        out.w.writeByte(BULLET_BLAST);
        out.vec(Packet.cm(x), h.y, Packet.cm(z));
        h.map.broadcast(Cmd.BULLET, out);

        List<Mob> victims = new ArrayList<>();
        Targeting.collectCircle(h.map, x, z, cells(h.weapon.explosionRadius), victims);
        victims.removeAll(h.struck);
        h.struck.addAll(victims);
        into.addAll(damage(h, victims, h.weapon.explosionPercent() / 100.0));
    }

    /**
     * Trừ máu các con trong victims: baseDamage x % của đòn x hệ số chỉ số x factor x ngẫu nhiên
     * 0.9-1.1, chí mạng thì nhân (2 + % sát thương chí mạng), ít nhất 1. Hết máu thì đánh dấu chết. Chỉ số HIT_EXP đổi một
     * phần sát thương gây ra thành EXP.
     */
    static List<Damage> damage(Hit h, List<Mob> victims, double factor) {
        Profile pr = h.attacker.profile;
        int percent = damagePercent(h.cast);
        double scale = damageScale(pr, h.cast.id == Casting.BASIC);
        double chance = pr.stats.critChance(critChance);
        List<Damage> results = new ArrayList<>();
        long dealt = 0;
        for (Mob m : victims) {
            if (m.dead) continue;
            double roll = baseDamage * percent / 100.0 * scale * factor * World.rand().nextDouble(0.9, 1.1);
            if (World.rand().nextDouble() < chance) roll *= 2 + pr.stats.bonus[Tables.CRIT_DAMAGE] / 100;
            long dmg = Math.max(1, Math.round(roll));
            dealt += Math.min(dmg, m.hp);
            m.hp = Math.max(0, m.hp - dmg);
            if (m.hp == 0) m.dead = true;
            results.add(new Damage(m, dmg));
        }
        long exp = (long) (dealt * pr.stats.bonus[Tables.HIT_EXP] / 100);
        if (exp > 0) {
            pr.addExp(exp, 0);
            h.attacker.dirty |= Profile.PART_STATS;
        }
        return results;
    }

    /** % sát thương của đòn: theo bảng skill; không có thì đòn thường 100%, skill ô skillDamagePercent. */
    static int damagePercent(Casting.Cast c) {
        if (c.percent > 0) return c.percent;
        return c.id == Casting.BASIC ? 100 : skillDamagePercent;
    }

    /** Hệ số chỉ số: (1 + % sát thương) x (1 + % sức mạnh vũ khí, chỉ đòn thường) x (1 + % sát thương nâng cấp vũ khí). */
    static double damageScale(Profile pr, boolean basic) {
        double weaponPower = basic ? 1 + pr.stats.bonus[Tables.WEAPON_POWER] / 100 : 1;
        return (1 + pr.stats.bonus[Tables.DAMAGE] / 100) * weaponPower * (1 + pr.stats.boosted().damage / 100.0);
    }

    /**
     * Gói BULLET HIT / END: id đòn, điểm hiện hiệu ứng, sát thương và máu còn của tối đa 255 con. END có thêm thời điểm
     * (ms) và một long 0 theo định dạng client.
     */
    static void sendBullet(Hit h, int act, List<Damage> results, double x, double z) throws IOException {
        Packet out = new Packet();
        out.w.writeByte(act);
        out.w.writeShort(h.id);
        out.vec(Packet.cm(x), h.y, Packet.cm(z));
        int n = Math.min(255, results.size());
        out.w.writeByte(n);
        for (int i = 0; i < n; i++) {
            Damage d = results.get(i);
            out.w.writeShort(d.mob.id);
            out.w.writeLong(d.amount);
            out.w.writeLong(d.mob.hp);
        }
        if (act == BULLET_END) {
            out.w.writeInt((int) System.currentTimeMillis());
            out.w.writeLong(0);
        }
        h.map.broadcast(Cmd.BULLET, out);
    }

    /** Sau khi trúng: con còn sống quay sang đuổi người đánh, con chết thì thưởng, rơi đồ, hẹn hồi sinh. Gửi SYNC hồ sơ. */
    static void finish(Hit h, List<Damage> results) throws IOException {
        for (Damage d : results) {
            if (d.mob.dead) MobAI.mobDied(h.map, d.mob, h.attacker);
            else MobAI.onMobHit(h.map, d.mob, h.attacker);
        }
        h.attacker.flush(0);
    }
}
