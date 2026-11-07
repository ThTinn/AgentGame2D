package game.world;

import java.util.*;
import java.util.concurrent.ThreadLocalRandom;
import game.player.Player;

/**
 * Trạng thái dùng chung của thế giới. Bảng đọc từ ServerData chỉ đọc sau khi khởi động. Phần thay đổi khi chơi: mọi lần
 * đọc / ghi đều giữ {@link #LOCK}, gửi gói không chặn nên giữ lock rất ngắn.
 */
public class World {
    public static final int GROUND_Y = -99900;      // CoreNet.GroundY x100: client tự dò mặt đất

    /** Bãi quái của một map (vị trí x100). */
    static class Camp {
        int template, count, x, z, radius, level;
        long maxHp, damage;
    }

    /** Chỗ đứng của một NPC (vị trí x100). */
    static class NpcSpawn {
        int id, x, z, rotation;
    }

    /** Một object đặt trên map: ghế, cổng (vị trí x100). Cổng có map / cổng đích, targetMap < 0 là chưa nối. */
    static class Placement {
        int uid, map, object, x, z, rotation, targetMap, targetUid;
    }

    // bảng từ ServerData/core, chỉ đọc sau khi khởi động
    public static int startMap, spawnX, spawnZ, npcMap;
    public static final List<NpcSpawn> npcs = new ArrayList<>();
    public static final Map<Integer, List<Camp>> camps = new HashMap<>();      // id map -> bãi quái
    public static final Map<Integer, Integer> objectSeats = new HashMap<>();   // id loại object -> số chỗ ngồi
    public static final List<Placement> placements = new ArrayList<>();
    public static final Set<Integer> mapIds = new HashSet<>();

    // trạng thái khi chơi, chỉ đụng tới khi giữ LOCK
    public static final Object LOCK = new Object();
    public static final Map<Integer, GameMap> maps = new HashMap<>();      // theo Zones.roomKey
    public static final Map<Integer, Player> online = new LinkedHashMap<>();
    public static long drops;       // số phiên đã đóng, Login dùng để biết hồ sơ vừa đọc có còn mới

    /** Thời điểm hiện tại tính bằng giây, chỉ dùng để đo khoảng cách thời gian. */
    public static double now() {
        return System.nanoTime() / 1e9;
    }

    /** Bộ sinh số ngẫu nhiên của thread đang chạy. */
    public static ThreadLocalRandom rand() {
        return ThreadLocalRandom.current();
    }
}
