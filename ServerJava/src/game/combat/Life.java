package game.combat;

import java.io.*;
import game.mob.Mob;
import game.mob.MobAI;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;
import game.world.GameMap;
import game.world.MapObjects;
import game.world.World;
import static game.combat.CombatConfig.*;

/** Người chơi trúng đòn, chết, hồi sinh, hồi máu / mana theo chỉ số. */
public class Life {
    static final int COMBAT_DIE = 1, COMBAT_REVIVE = 2;
    static final int REVIVE_SPREAD = 150;       // hồi sinh rải trong vòng 1.5 m quanh chỗ xuất hiện (x100)

    /**
     * Quái m đánh trúng p: sát thương = damage của quái x (1 - phần giáp giảm) x ngẫu nhiên 0.9-1.1, ít nhất 1. Báo cả khu
     * (MOB HIT), hết máu thì chết.
     */
    public static void hitPlayer(GameMap map, Mob m, Player p, double now) throws IOException {
        Profile pr = p.profile;
        long dmg = Math.max(1, Math.round(m.damage * (1 - pr.stats.defenseCut()) * World.rand().nextDouble(0.9, 1.1)));
        pr.hp = Math.max(0, pr.hp - dmg);
        Packet out = MobAI.mobPacket(MobAI.MOB_HIT, m);
        out.w.writeInt(p.id);
        out.w.writeLong(dmg);
        out.w.writeLong(pr.hp);
        out.w.writeLong(pr.maxHp);
        map.broadcast(Cmd.MOB, out);
        p.dirty |= Profile.PART_STATS;
        if (pr.hp == 0) killPlayer(map, p, now);
        p.flush(0);
    }

    /** p chết: hẹn giờ hồi sinh, bỏ thuốc đang có tác dụng, đứng khỏi ghế, quái đang đuổi p quay về bãi, báo cả khu. */
    static void killPlayer(GameMap map, Player p, double now) throws IOException {
        double wait = Math.max(0.5, playerRespawn);
        p.dead = true;
        p.reviveAt = now + wait;
        p.profile.dead = true;
        p.profile.consumables.clearBuffs();
        MapObjects.releaseSeat(map, p, true);
        for (Mob m : map.mobs) if (m.target == p) MobAI.startReturn(m);
        Packet out = new Packet();
        out.w.writeByte(COMBAT_DIE);
        out.w.writeInt(p.id);
        out.w.writeShort((int) Math.round(wait * 10));     // phần mười giây
        map.broadcast(Cmd.COMBAT, out);
    }

    /** Hồi sinh p ở gần chỗ xuất hiện của map đang đứng, đầy máu và mana, báo cả khu. */
    static void revive(Player p) throws IOException {
        Profile pr = p.profile;
        p.dead = false;
        pr.dead = false;
        pr.hp = pr.maxHp;
        pr.mp = pr.maxMp;
        int[] spawn = World.spawnOf(p.map);     // map 2D: rải ngang quanh chỗ xuất hiện, z là độ cao
        p.x = spawn[0] + World.rand().nextInt(-REVIVE_SPREAD, REVIVE_SPREAD + 1);
        p.y = World.GROUND_Y;
        p.z = spawn[1];
        if (p.room != null) {
            Packet out = new Packet();
            out.w.writeByte(COMBAT_REVIVE);
            out.w.writeInt(p.id);
            out.w.writeInt(p.x);
            out.w.writeInt(p.z);
            out.w.writeLong(pr.hp);
            out.w.writeLong(pr.maxHp);
            p.room.broadcast(Cmd.COMBAT, out);
        }
        p.flush(Profile.PART_STATS);
    }

    /**
     * Một nhịp sống của p (WorldLoop gọi): đã chết thì hồi sinh khi tới giờ; còn sống thì hết hạn thuốc, hồi máu theo chỉ
     * số HP_REGEN và mana theo manaRegen (mỗi giây). Phần lẻ dồn vào hpCarry / mpCarry tới đủ 1 mới cộng.
     */
    public static void tickLife(Player p, double now, double dt) throws IOException {
        Profile pr = p.profile;
        if (p.dead) {
            if (now >= p.reviveAt) revive(p);
            return;
        }
        if (pr.consumables.tickBuffs(now)) p.dirty |= Profile.PART_STATS;

        double hpRate = pr.stats.bonus[Tables.HP_REGEN];
        if (hpRate > 0 && pr.hp < pr.maxHp) {
            p.hpCarry += hpRate * dt;
            long add = (long) p.hpCarry;
            if (add > 0) {
                p.hpCarry -= add;
                pr.hp = Math.min(pr.maxHp, pr.hp + add);
                p.dirty |= Profile.PART_STATS;
            }
        } else {
            p.hpCarry = 0;
        }

        if (manaRegen > 0 && pr.mp < pr.maxMp) {
            p.mpCarry += manaRegen * dt;
            long add = (long) p.mpCarry;
            if (add > 0) {
                p.mpCarry -= add;
                pr.mp = Math.min(pr.maxMp, pr.mp + add);
                p.dirty |= Profile.PART_STATS;
            }
        } else {
            p.mpCarry = 0;
        }
    }
}
