package game.world;

import java.io.*;
import java.util.*;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import static game.world.World.*;

/** Bàn ghế và cổng đặt trên map: ngồi, đứng, qua cổng, lệnh MAP_OBJECT. */
public class MapObjects {
    static final int OBJ_LIST = 0, OBJ_SIT = 1, OBJ_STAND = 2;
    static final int PORTAL_ARRIVE = 250;           // MapObjectSystem.PortalArriveDistance x100

    /** Lệnh MAP_OBJECT: ngồi (uid object, chỗ) hoặc đứng dậy. */
    public static void command(Player p, DataInputStream r) throws IOException {
        GameMap map = p.room;
        if (map == null) return;
        int sub = r.readUnsignedByte();
        if (sub == OBJ_SIT) sit(map, p, r.readInt(), r.readUnsignedByte());
        else if (sub == OBJ_STAND) releaseSeat(map, p, true);
    }

    /** Chỗ đặt có uid trên map, null khi không có. */
    static Placement placement(int map, int uid) {
        for (Placement p : placements) if (p.map == map && p.uid == uid) return p;
        return null;
    }

    /** Gói SubSit: người playerId ngồi chỗ point của object uid. */
    static Packet sitPacket(int playerId, int uid, int point) throws IOException {
        Packet p = new Packet();
        p.w.writeByte(OBJ_SIT);
        p.w.writeInt(playerId);
        p.w.writeInt(uid);
        p.w.writeByte(point);
        return p;
    }

    /** Gói SubStand: người playerId đứng dậy. */
    static Packet standPacket(int playerId) throws IOException {
        Packet p = new Packet();
        p.w.writeByte(OBJ_STAND);
        p.w.writeInt(playerId);
        return p;
    }

    /**
     * Trả mọi chỗ p đang ngồi trong khu. tell: báo cả khu, và báo riêng p nếu p đã rời danh sách khu (đang chuyển map).
     */
    public static void releaseSeat(GameMap map, Player p, boolean tell) throws IOException {
        boolean had = false;
        for (Map<Integer, Integer> seat : map.seats.values()) had |= seat.values().removeIf(id -> id == p.id);
        if (!had || !tell) return;
        Packet stand = standPacket(p.id);
        map.broadcast(Cmd.MAP_OBJECT, stand);
        if (!map.players.contains(p)) p.conn.post(Cmd.MAP_OBJECT, stand);
    }

    /** Ngồi chỗ point của object uid. Chỗ không có hoặc người khác đang ngồi thì bảo p đứng dậy. */
    static void sit(GameMap map, Player p, int uid, int point) throws IOException {
        Placement place = placement(map.id, uid);
        Integer seatCount = place != null ? objectSeats.get(place.object) : null;
        Map<Integer, Integer> used = map.seats.get(uid);
        Integer holder = used != null ? used.get(point) : null;
        boolean free = seatCount != null && point < seatCount && (holder == null || holder == p.id);
        if (!free) {
            p.conn.post(Cmd.MAP_OBJECT, standPacket(p.id));
            return;
        }
        releaseSeat(map, p, false);
        map.seats.computeIfAbsent(uid, k -> new HashMap<>()).put(point, p.id);
        map.broadcast(Cmd.MAP_OBJECT, sitPacket(p.id, uid, point));
    }

    /**
     * Qua cổng uid: sang map đích, đứng trước cổng nối bên kia (lệch PORTAL_ARRIVE theo hướng cổng). Cổng không nối tới
     * cổng nào thì ra chỗ xuất hiện của map đích.
     */
    static void portal(Player p, int uid) throws IOException {
        Placement from = placement(p.map, uid);
        if (from == null || from.targetMap < 0) {
            p.conn.toast("Cổng chưa nối tới đâu.");
            return;
        }
        Placement to = from.targetUid > 0 ? placement(from.targetMap, from.targetUid) : null;
        if (to == null) {
            Zones.enterMap(p, from.targetMap, spawnX, spawnZ);
            return;
        }
        double rad = Math.toRadians(to.rotation);
        int x = to.x + (int) Math.round(Math.sin(rad) * PORTAL_ARRIVE);
        int z = to.z + (int) Math.round(Math.cos(rad) * PORTAL_ARRIVE);
        Zones.enterMap(p, from.targetMap, x, z);
    }
}
