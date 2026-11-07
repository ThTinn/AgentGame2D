package game;

import java.io.*;
import game.combat.Hits;
import game.combat.Life;
import game.loot.Loot;
import game.mob.MobAI;
import game.player.Player;
import game.player.PlayerPackets;
import game.world.GameMap;
import game.world.World;

/** Thread thế giới: chạy {@link #tick} 20 lần mỗi giây khi giữ World.LOCK, một nhịp lỗi không được làm dừng cả server. */
class WorldLoop {
    static final double TICK = 0.05;
    static final double SYNC_EVERY = 0.2;   // giây giữa hai lần gửi phần hồ sơ đã đổi
    static final double STATE_EVERY = 1;    // giây giữa hai lần báo cấp / máu cho khu
    static final double SAVE_EVERY = 30;    // giây giữa hai lần lưu hồ sơ của một người

    static double nextState;

    /** Vòng lặp của thread "world". dt mỗi nhịp kẹp tối đa 0.25 giây để một lần khựng không làm quái nhảy xa. */
    static void run() {
        double last = World.now();
        while (true) {
            try {
                Thread.sleep((long) (TICK * 1000));
                synchronized (World.LOCK) {
                    double t = World.now();
                    tick(t, Math.min(0.25, t - last));
                    last = t;
                }
            } catch (InterruptedException e) {
                return;
            } catch (IOException | RuntimeException e) {
                System.out.println("Loi nhip the gioi: " + e);
            }
        }
    }

    /**
     * Một nhịp: đòn đang bay; quái và đồ rơi của các khu có người thật; rồi với từng người chơi thật: hồi máu / hồi sinh,
     * gửi phần hồ sơ đã đổi, báo cấp / máu cho khu, lưu hồ sơ định kỳ.
     */
    static void tick(double now, double dt) throws IOException {
        Hits.tickHits(now, dt);
        for (GameMap map : World.maps.values()) {
            if (!map.hasHuman()) continue;
            MobAI.tickMobs(map, now, dt);
            Loot.tickGround(map, now);
        }
        boolean sendState = now >= nextState;
        if (sendState) nextState = now + STATE_EVERY;
        for (Player p : World.online.values()) {
            if (p.conn == null) continue;
            Life.tickLife(p, now, dt);
            if (now >= p.nextSync) {
                p.nextSync = now + SYNC_EVERY;
                p.flush(0);
            }
            if (sendState && p.room != null) PlayerPackets.sendState(p);
            if (now >= p.nextSave) {    // mỗi người một mốc: không dựng JSON của cả server trong cùng một nhịp giữ lock
                p.nextSave = now + SAVE_EVERY;
                p.save();
            }
        }
    }
}
