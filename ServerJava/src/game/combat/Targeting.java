package game.combat;

import java.util.*;
import game.mob.Mob;
import game.world.GameMap;

/** Tìm quái trúng đòn trên mặt phẳng ngang (x, z), tọa độ mét. Quái đã chết bị bỏ qua. */
class Targeting {
    /** Vị trí (0..1) trên đoạn A-B gần điểm P nhất. Đoạn quá ngắn thì 0. */
    static double closestT(double ax, double az, double bx, double bz, double px, double pz) {
        double abx = bx - ax, abz = bz - az, len = abx * abx + abz * abz;
        if (len <= 1e-6) return 0;
        double t = ((px - ax) * abx + (pz - az) * abz) / len;
        return Math.max(0, Math.min(1, t));
    }

    /** Quái đầu tiên (gần A nhất) cách đoạn A-B không quá radius, bỏ các con trong skip. null khi không có. */
    static Mob firstOnSegment(GameMap map, double ax, double az, double bx, double bz, double radius, List<Mob> skip) {
        Mob best = null;
        double bestT = Double.MAX_VALUE;
        for (Mob m : map.mobs) {
            if (m.dead || skip.contains(m)) continue;
            double t = closestT(ax, az, bx, bz, m.x, m.z);
            double cx = ax + (bx - ax) * t, cz = az + (bz - az) * t;
            if (Math.hypot(m.x - cx, m.z - cz) > radius) continue;
            if (t < bestT) {
                bestT = t;
                best = m;
            }
        }
        return best;
    }

    /** Thêm vào into mọi quái trong vòng tròn tâm (x, z). */
    static void collectCircle(GameMap map, double x, double z, double radius, List<Mob> into) {
        for (Mob m : map.mobs) if (!m.dead && Math.hypot(m.x - x, m.z - z) <= radius) into.add(m);
    }

    /**
     * Thêm vào into mọi quái trong hình quạt: tâm (x, z), hướng (dirX, dirZ) đã chuẩn hóa, bán kính radius, nửa góc
     * halfAngle độ. Quái đứng sát tâm (dưới 0.1 m) luôn tính là trúng.
     */
    static void collectCone(GameMap map, double x, double z, double dirX, double dirZ, double radius, double halfAngle, List<Mob> into) {
        for (Mob m : map.mobs) {
            if (m.dead) continue;
            double tx = m.x - x, tz = m.z - z, d = Math.hypot(tx, tz);
            if (d > radius) continue;
            if (d > 0.1) {
                double cos = Math.max(-1, Math.min(1, (dirX * tx + dirZ * tz) / d));
                if (Math.toDegrees(Math.acos(cos)) > halfAngle) continue;
            }
            into.add(m);
        }
    }
}
