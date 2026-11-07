package game.mob;

import java.io.*;
import game.combat.Life;
import game.loot.Loot;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;
import game.world.GameMap;
import game.world.World;
import static game.combat.CombatConfig.*;

/**
 * Quái: đi dạo, đuổi, đánh, về bãi, chết, hồi sinh, port từ reference/LocalServer (ServerMonsters). Chỉ chạy khi khu có
 * người thật và không đuổi bot. Chỉ gọi khi giữ World.LOCK.
 */
public class MobAI {
    // sub của lệnh MOB
    public static final int MOB_MOVE = 0, MOB_STOP = 1, MOB_ATTACK = 2, MOB_HIT = 3, MOB_HP = 4, MOB_DIE = 5, MOB_RESPAWN = 6;
    // Mob.mode
    static final int WANDER = 0, CHASE = 1, RETURN = 2;
    static final double MOB_SEND = 0.3;         // giây giữa hai gói MOVE của một con
    static final double MOB_LEAD = 0.05;        // gửi điểm tới đi trước thêm chừng này giây để client không bị khựng
    static final double MOB_ARRIVE = 0.15;      // cách đích dưới chừng này mét là tới
    static final double MOB_HOLD = 0.6;         // đứng yên sau khi đánh (giây)
    static final double MOB_REACH_SLACK = 0.7;  // lúc vung trúng, người chơi chạy xa thêm chừng này mét vẫn bị trúng

    // ---------------- nhịp của khu ----------------

    /** Một nhịp của mọi quái trong khu: hồi sinh, đang vung đòn, đứng nghỉ sau đòn, rồi theo trạng thái. */
    public static void tickMobs(GameMap map, double now, double dt) throws IOException {
        for (Mob m : map.mobs) {
            if (m.dead) {
                if (now >= m.respawnAt) respawn(map, m, now);
            } else if (m.swingAt >= 0) {
                updateSwing(map, m, now);
            } else if (now >= m.holdUntil) {
                if (m.mode == WANDER) tickWander(map, m, now, dt);
                else if (m.mode == CHASE) tickChase(map, m, now, dt);
                else tickReturn(map, m, now, dt);
            }
        }
    }

    /** Đi dạo: thấy người trong tầm thì đuổi; không thì tới giờ chọn điểm ngẫu nhiên trong bãi và đi tới đó. */
    static void tickWander(GameMap map, Mob m, double now, double dt) throws IOException {
        Player near = aggroTarget(map, m);
        if (near != null) {
            startChase(m, near);
            return;
        }
        if (!m.wandering) {
            if (now < m.nextWander) return;
            double angle = World.rand().nextDouble(Math.PI * 2), radius = m.radius * 0.7 * Math.sqrt(World.rand().nextDouble());
            m.wanderX = m.campX + Math.cos(angle) * radius;
            m.wanderZ = m.campZ + Math.sin(angle) * radius;
            m.wandering = true;
            m.nextSend = 0;
        }
        if (moveMob(map, m, m.wanderX, m.wanderZ, 0, wanderSpeed, now, dt)) {
            m.wandering = false;
            m.nextWander = now + World.rand().nextDouble(3, 7);
        }
    }

    /** Đuổi: mục tiêu mất (chết, rời khu) hoặc ra khỏi dây xích thì quay về; trong tầm đánh thì vung, không thì chạy tới. */
    static void tickChase(GameMap map, Mob m, double now, double dt) throws IOException {
        Player t = m.target;
        if (!targetable(map, t) || !inLeash(m, t.x / 100.0, t.z / 100.0) || !inLeash(m, m.x, m.z)) {
            startReturn(m);
            return;
        }
        double tx = t.x / 100.0, tz = t.z / 100.0, range = Math.max(0.5, mobRange);
        if (Math.hypot(tx - m.x, tz - m.z) <= range) {
            if (now >= m.attackReadyAt) startSwing(map, m, now);
        } else {
            moveMob(map, m, tx, tz, range * 0.8, chaseSpeed, now, dt);
        }
    }

    /** Quay về chỗ gốc. Tới nơi thì đi dạo lại và hồi đầy máu (báo khu nếu máu đổi). */
    static void tickReturn(GameMap map, Mob m, double now, double dt) throws IOException {
        if (!moveMob(map, m, m.homeX, m.homeZ, 0, chaseSpeed, now, dt)) return;
        m.mode = WANDER;
        m.nextWander = now + World.rand().nextDouble(2, 5);
        if (m.hp < m.maxHp) {
            m.hp = m.maxHp;
            Packet out = mobPacket(MOB_HP, m);
            out.w.writeLong(m.hp);
            out.w.writeLong(m.maxHp);
            map.broadcast(Cmd.MOB, out);
        }
    }

    // ---------------- đổi trạng thái ----------------

    /** Bắt đầu đuổi target. */
    static void startChase(Mob m, Player target) {
        m.mode = CHASE;
        m.target = target;
        m.wandering = false;
        m.nextSend = 0;
    }

    /** Bỏ mục tiêu, quay về chỗ gốc. */
    public static void startReturn(Mob m) {
        m.mode = RETURN;
        m.target = null;
        m.wandering = false;
        m.swingAt = -1;
        m.nextSend = 0;
    }

    /** Quái bị attacker đánh: đuổi người đó nếu đang rảnh và attacker đứng trong dây xích. Đang đánh người khác thì giữ. */
    public static void onMobHit(GameMap map, Mob m, Player attacker) {
        if (m.dead || m.mode == RETURN || attacker.dead) return;
        if (m.mode == CHASE && m.target != null && !m.target.dead) return;
        if (inLeash(m, attacker.x / 100.0, attacker.z / 100.0)) startChase(m, attacker);
    }

    /** Người thật gần nhất trong tầm phát hiện mà đang đứng trong bãi, null khi không có. Bot không bị đuổi. */
    static Player aggroTarget(GameMap map, Mob m) {
        Player best = null;
        double bestD = aggroRange;
        for (Player p : map.players) {
            if (p.dead || p.conn == null) continue;
            double x = p.x / 100.0, z = p.z / 100.0;
            if (Math.hypot(x - m.campX, z - m.campZ) > m.radius) continue;
            double d = Math.hypot(x - m.x, z - m.z);
            if (d <= bestD) {
                bestD = d;
                best = p;
            }
        }
        return best;
    }

    /** Người này còn đánh được: còn sống và vẫn ở khu này. */
    static boolean targetable(GameMap map, Player p) {
        return p != null && !p.dead && p.room == map;
    }

    /** Điểm (x, z) nằm trong dây xích của bãi: bán kính bãi nhân leashScale (ít nhất 1). */
    static boolean inLeash(Mob m, double x, double z) {
        return Math.hypot(x - m.campX, z - m.campZ) <= m.radius * Math.max(1, leashScale);
    }

    // ---------------- di chuyển, đánh ----------------

    /**
     * Chạy về phía (toX, toZ), dừng cách đó stopAt mét. Mỗi MOB_SEND giây báo client điểm sẽ tới (đi trước một chút), tới
     * nơi thì báo dừng nếu đã báo đi. Trả true khi đã tới.
     */
    static boolean moveMob(GameMap map, Mob m, double toX, double toZ, double stopAt, double speed, double now, double dt)
            throws IOException {
        speed = Math.max(0.3, speed);
        double dx = toX - m.x, dz = toZ - m.z, dist = Math.hypot(dx, dz), left = dist - stopAt;
        if (left <= MOB_ARRIVE) return true;
        double ux = dx / dist, uz = dz / dist, step = Math.min(left, speed * dt);
        m.x += ux * step;
        m.z += uz * step;
        if (now >= m.nextSend) {
            m.nextSend = now + MOB_SEND;
            double lead = Math.min(left - step, speed * (MOB_SEND + MOB_LEAD));
            m.moveSent = true;
            Packet out = mobPacket(MOB_MOVE, m);
            mobPos(out, m.x + ux * lead, m.z + uz * lead);
            out.w.writeInt((int) Math.round(speed * 100));
            map.broadcast(Cmd.MOB, out);
        }
        if (left - step > MOB_ARRIVE) return false;
        if (m.moveSent) {
            m.moveSent = false;
            Packet out = mobPacket(MOB_STOP, m);
            mobPos(out, m.x, m.z);
            map.broadcast(Cmd.MOB, out);
        }
        return true;
    }

    /** Bắt đầu vung đòn vào mục tiêu, báo khu (client chạy anim đánh). Trúng hay không tính ở {@link #updateSwing}. */
    static void startSwing(GameMap map, Mob m, double now) throws IOException {
        m.attackReadyAt = now + Math.max(0.3, mobCooldown);
        m.swingAt = now;
        m.moveSent = false;
        Packet out = mobPacket(MOB_ATTACK, m);
        out.w.writeInt(m.target.id);
        mobPos(out, m.x, m.z);
        map.broadcast(Cmd.MOB, out);
    }

    /** Sau mobHitDelay giây kể từ lúc vung: mục tiêu còn trong tầm (cộng MOB_REACH_SLACK) thì trúng, rồi đứng nghỉ MOB_HOLD. */
    static void updateSwing(GameMap map, Mob m, double now) throws IOException {
        if (now - m.swingAt < mobHitDelay) return;
        m.swingAt = -1;
        m.holdUntil = now + MOB_HOLD;
        Player t = m.target;
        if (!targetable(map, t)) return;
        if (Math.hypot(t.x / 100.0 - m.x, t.z / 100.0 - m.z) <= mobRange + MOB_REACH_SLACK) Life.hitPlayer(map, m, t, now);
    }

    // ---------------- chết, hồi sinh ----------------

    /** Quái chết dưới tay killer: báo khu, thưởng cho killer, thả đồ rơi, hẹn giờ hồi sinh. Combat đã đặt m.dead. */
    public static void mobDied(GameMap map, Mob m, Player killer) throws IOException {
        m.hp = 0;
        m.target = null;
        m.swingAt = -1;
        m.moveSent = false;
        Packet out = mobPacket(MOB_DIE, m);
        out.w.writeInt(killer.id);
        map.broadcast(Cmd.MOB, out);
        grantKillRewards(m, killer);
        Loot.rollDrops(map, m, killer);
        m.respawnAt = World.now() + Math.max(0.5, mobRespawn);
    }

    /**
     * Thưởng hạ quái như CoreSystems.GrantKillRewards: đếm nhiệm vụ, EXP = 1/10 EXP cần lên cấp của quái (kèm thưởng EXP),
     * vàng = 10 + 6 x cấp, ngẫu nhiên 0.8-1.2 (kèm % tìm vàng). Quái 3D không có MonsterTemplate nên tính theo cấp của bãi.
     */
    static void grantKillRewards(Mob m, Player killer) throws IOException {
        Profile pr = killer.profile;
        if (pr.story.quests.onMonsterKilled(m.template)) killer.dirty |= Profile.PART_STORY;
        killer.sendToasts();
        pr.addExp(Math.max(1, Tables.expToNext(m.level) / 10), pr.stats.bonus[Tables.EXP_BONUS]);
        long gold = 10 + 6L * Math.max(1, m.level);
        double goldScale = World.rand().nextDouble(0.8, 1.2) * (1 + Math.max(0, pr.stats.bonus[Tables.GOLD_FIND]) / 100);
        pr.gold += Math.max(1, Math.round(gold * goldScale));
        killer.dirty |= Profile.PART_STATS;
    }

    /** Hồi sinh ở chỗ gốc, đầy máu, đi dạo lại sau 1-3 giây. Báo khu. */
    static void respawn(GameMap map, Mob m, double now) throws IOException {
        m.dead = false;
        m.hp = m.maxHp;
        m.x = m.homeX;
        m.z = m.homeZ;
        m.mode = WANDER;
        m.target = null;
        m.wandering = false;
        m.swingAt = -1;
        m.moveSent = false;
        m.nextWander = now + World.rand().nextDouble(1, 3);
        Packet out = mobPacket(MOB_RESPAWN, m);
        out.w.writeLong(m.hp);
        out.w.writeLong(m.maxHp);
        mobPos(out, m.x, m.z);
        map.broadcast(Cmd.MOB, out);
    }

    // ---------------- gói ----------------

    /** Đầu gói MOB: sub và id quái. */
    public static Packet mobPacket(int sub, Mob m) throws IOException {
        Packet p = new Packet();
        p.w.writeByte(sub);
        p.w.writeShort(m.id);
        return p;
    }

    /** Ghi vị trí quái (mét) vào gói, y = GROUND_Y để client tự dò mặt đất. */
    public static void mobPos(Packet p, double x, double z) throws IOException {
        p.vec(Packet.cm(x), World.GROUND_Y, Packet.cm(z));
    }
}
