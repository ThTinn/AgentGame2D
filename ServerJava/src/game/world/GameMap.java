package game.world;

import java.util.*;
import game.loot.Loot;
import game.mob.Mob;
import game.net.Packet;
import game.player.Player;

/** Một khu của map: người chơi, quái và ghế riêng, chỉ thấy nhau trong cùng khu. */
public class GameMap {
    public int id, zone;
    public final List<Player> players = new ArrayList<>();
    public final List<Mob> mobs = new ArrayList<>();
    final Map<Integer, Map<Integer, Integer>> seats = new HashMap<>();          // uid object -> chỗ -> id người ngồi
    public final Map<Integer, Loot.Ground> ground = new LinkedHashMap<>();    // đồ rơi trên đất theo id

    /** Gửi một gói cho mọi người trong khu (kể cả người gây ra). */
    public void broadcast(int cmd, Packet p) {
        for (Player o : players) o.post(cmd, p);
    }

    /** Khu có người chơi thật (không phải bot) không. Không có thì quái, đồ rơi, bot đứng yên. */
    public boolean hasHuman() {
        for (Player o : players) if (o.conn != null) return true;
        return false;
    }
}
