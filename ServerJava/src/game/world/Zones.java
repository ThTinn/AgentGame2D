package game.world;

import java.io.*;
import java.util.*;
import java.util.concurrent.ThreadLocalRandom;
import game.loot.Loot;
import game.mob.Mob;
import game.mob.MobAI;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.PlayerPackets;
import static game.world.World.*;

/**
 * Map và khu: dựng khu kèm quái, vào / rời khu, đổi khu, lệnh MAP và gói dựng map cho client. Mỗi map có ít nhất
 * ZONE_MIN khu, mỗi khu tối đa ZONE_PLAYERS người (cả bot); mọi khu đều đầy thì tự mở khu tiếp theo.
 */
public class Zones {
    static final int MAP_READY = 0, MAP_TRAVEL = 1, MAP_PORTAL = 2, MAP_ZONE_LIST = 3, MAP_ZONE_CHANGE = 4;
    static final int ZONE_PLAYERS = 50, ZONE_MIN = 5;

    /** Lệnh MAP: map dựng xong, đi tới map khác, qua cổng, xem danh sách khu, đổi khu. */
    public static void command(Player p, DataInputStream r) throws IOException {
        int sub = r.readUnsignedByte();
        switch (sub) {
            case MAP_READY:
                if (p.map >= 0) mapReady(p);
                break;
            case MAP_TRAVEL:
                travel(p, r.readShort());
                break;
            case MAP_PORTAL:
                MapObjects.portal(p, r.readInt());
                break;
            case MAP_ZONE_LIST:
                if (p.room != null) sendZones(p);
                break;
            case MAP_ZONE_CHANGE:
                if (p.room != null) changeZone(p, r.readUnsignedByte());
                break;
            default:
                break;
        }
    }

    // ---------------- vào / rời map ----------------

    /** Vào map mapId ở vị trí (x, z), khu số nhỏ nhất còn chỗ. Rời khu cũ trước. */
    public static void enterMap(Player p, int mapId, int x, int z) throws IOException {
        leaveMap(p);
        enterZone(p, mapOf(mapId, pickZone(mapId)), x, z);
    }

    /** Rời khu đang đứng: trả ghế, báo LEAVE_MAP cho cả khu. */
    public static void leaveMap(Player p) throws IOException {
        GameMap map = p.room;
        if (map == null) return;
        map.players.remove(p);
        MapObjects.releaseSeat(map, p, true);
        Packet leave = new Packet();
        leave.w.writeInt(p.id);
        map.broadcast(Cmd.LEAVE_MAP, leave);
        p.map = -1;
        p.room = null;
    }

    /** MAP_TRAVEL: đi tới chỗ xuất hiện của map khác (bảng chọn map). */
    static void travel(Player p, int mapId) throws IOException {
        if (mapId == p.map) return;
        if (mapIds.contains(mapId)) enterMap(p, mapId, spawnX, spawnZ);
        else p.conn.toast("Không có map " + mapId + ".");
    }

    /**
     * Vào khu map ở (x, z): gửi MAP_INFO (người và quái đang có) cho p, báo cả khu có người mới, rồi thêm p vào khu. y đặt
     * GROUND_Y để client tự dò mặt đất.
     */
    static void enterZone(Player p, GameMap map, int x, int z) throws IOException {
        p.map = map.id;
        p.room = map;
        p.x = x;
        p.y = GROUND_Y;
        p.z = z;
        p.post(Cmd.MAP_INFO, mapInfoPacket(p, map));

        Packet add = new Packet();
        PlayerPackets.writePlayer(add, p);
        add.w.writeByte(0);         // portal_type
        Packet anim = PlayerPackets.animPacket(p), state = PlayerPackets.statePacket(p);
        for (Player o : map.players) {
            o.post(Cmd.ADD_PLAYER, add);
            o.post(Cmd.PLAYER, anim);
            o.post(Cmd.PLAYER, state);
        }
        map.players.add(p);
    }

    /** MAP_INFO: id map, khu, vị trí của p, người đang ở trong khu (tối đa 255), quái của khu. */
    static Packet mapInfoPacket(Player p, GameMap map) throws IOException {
        Packet info = new Packet();
        info.w.writeShort(map.id);
        info.w.writeByte(map.zone);
        info.vec(p.x, p.y, p.z);
        info.w.writeByte(0);        // portal_type
        int count = Math.min(255, map.players.size());
        info.w.writeByte(count);
        for (int i = 0; i < count; i++) PlayerPackets.writePlayer(info, map.players.get(i));
        info.w.writeShort(map.mobs.size());
        for (Mob m : map.mobs) {
            info.w.writeShort(m.id);
            info.w.writeShort(m.template);
            info.w.writeLong(m.hp);
            info.w.writeLong(m.maxHp);
            info.w.writeShort(m.level);
            MobAI.mobPos(info, m.x, m.z);
        }
        return info;
    }

    // ---------------- khu ----------------

    /** Khóa của khu trong World.maps: id map ở các bit cao, số khu ở 8 bit thấp. */
    static int roomKey(int id, int zone) {
        return id << 8 | zone;
    }

    /** Khu zone của map id, chưa có thì dựng mới kèm quái. */
    static GameMap mapOf(int id, int zone) {
        GameMap map = maps.get(roomKey(id, zone));
        if (map != null) return map;
        map = new GameMap();
        map.id = id;
        map.zone = zone;
        spawnMobs(map);
        maps.put(roomKey(id, zone), map);
        return map;
    }

    /**
     * Thả quái theo các bãi của map (map không có bãi riêng thì dùng bãi của id -1). Như ServerWorld.MapOf: con đầu ở
     * giữa bãi, các con sau xếp vòng tròn bán kính 0.45 bán kính bãi. Id quái đếm từ 1 trong khu.
     */
    static void spawnMobs(GameMap map) {
        List<Camp> list = camps.get(map.id);
        if (list == null || list.isEmpty()) list = camps.get(-1);
        if (list == null) return;
        int mobId = 1;
        for (Camp c : list)
            for (int i = 0; i < c.count; i++) {
                double angle = i * Math.PI * 2 / Math.max(1, c.count - 1);
                double ring = i == 0 ? 0 : c.radius * 0.45;
                Mob m = new Mob();
                m.id = mobId++;
                m.template = c.template;
                m.level = Math.max(1, c.level);
                m.hp = m.maxHp = c.maxHp;
                m.damage = c.damage;
                m.campX = c.x / 100.0;
                m.campZ = c.z / 100.0;
                m.radius = c.radius / 100.0;
                m.x = m.homeX = (c.x + Math.cos(angle) * ring) / 100;
                m.z = m.homeZ = (c.z + Math.sin(angle) * ring) / 100;
                m.nextWander = World.now() + ThreadLocalRandom.current().nextDouble(3, 8);
                map.mobs.add(m);
            }
    }

    /** Khu số nhỏ nhất còn chỗ. Mọi khu đều đầy thì trả khu tiếp theo (mapOf sẽ dựng nó). */
    static int pickZone(int mapId) {
        int zone = 0;
        while (mapOf(mapId, zone).players.size() >= ZONE_PLAYERS) zone++;
        return zone;
    }

    /** Số khu hiện ra của map: ZONE_MIN, cộng thêm các khu cao hơn đang có người. Hết người thì tự giảm về ZONE_MIN. */
    static int zoneCount(int mapId) {
        int count = ZONE_MIN;
        for (GameMap m : maps.values())
            if (m.id == mapId && !m.players.isEmpty()) count = Math.max(count, m.zone + 1);
        return count;
    }

    /** Gửi danh sách khu của map đang đứng: khu hiện tại, sức chứa, số người từng khu. */
    static void sendZones(Player p) throws IOException {
        int count = zoneCount(p.map);
        Packet out = new Packet();
        out.w.writeByte(MAP_ZONE_LIST);
        out.w.writeByte(p.room.zone);
        out.w.writeByte(ZONE_PLAYERS);
        out.w.writeByte(count);
        for (int zone = 0; zone < count; zone++) out.w.writeByte(mapOf(p.map, zone).players.size());
        p.conn.post(Cmd.MAP, out);
    }

    /** Đổi sang khu khác, giữ nguyên chỗ đứng. Không đổi được thì gửi lại danh sách để bảng trên client hiện số mới. */
    static void changeZone(Player p, int zone) throws IOException {
        if (zone == p.room.zone || zone >= zoneCount(p.map)) {
            sendZones(p);
            return;
        }
        GameMap target = mapOf(p.map, zone);
        if (target.players.size() >= ZONE_PLAYERS) {
            p.conn.toast("Khu này đã đầy.");
            sendZones(p);
            return;
        }
        int x = p.x, z = p.z;
        leaveMap(p);
        enterZone(p, target, x, z);
    }

    // ---------------- MAP_READY ----------------

    /** Map của client dựng xong: gửi NPC, bàn ghế / cổng, chỗ đang có người ngồi, đồ trên đất, tư thế của người đã ở sẵn. */
    static void mapReady(Player p) throws IOException {
        GameMap map = p.room;
        p.conn.post(Cmd.NPC, npcListPacket(map.id));
        p.conn.post(Cmd.MAP_OBJECT, objectListPacket(map.id));
        for (Map.Entry<Integer, Map<Integer, Integer>> seat : map.seats.entrySet())
            for (Map.Entry<Integer, Integer> point : seat.getValue().entrySet())
                p.conn.post(Cmd.MAP_OBJECT, MapObjects.sitPacket(point.getValue(), seat.getKey(), point.getKey()));
        p.conn.post(Cmd.GROUND_ITEM, Loot.listPacket(map));
        for (Player o : map.players)
            if (o != p) {
                p.conn.post(Cmd.PLAYER, PlayerPackets.animPacket(o));
                p.conn.post(Cmd.PLAYER, PlayerPackets.statePacket(o));
            }
    }

    /** NpcSub.LIST của map: NPC chỉ đứng ở npcMap (npcMap < 0 là map nào cũng có). */
    static Packet npcListPacket(int mapId) throws IOException {
        boolean hasNpc = npcMap < 0 || npcMap == mapId;
        Packet list = new Packet();
        list.w.writeByte(0);        // NpcSub.LIST
        list.w.writeShort(mapId);
        list.w.writeByte(hasNpc ? npcs.size() : 0);
        if (hasNpc)
            for (NpcSpawn n : npcs) {
                list.w.writeShort(n.id);
                list.w.writeInt(n.x);
                list.w.writeInt(n.z);
                list.w.writeShort(n.rotation);
            }
        return list;
    }

    /** Danh sách object đặt trên map (OBJ_LIST). */
    static Packet objectListPacket(int mapId) throws IOException {
        List<Placement> onMap = new ArrayList<>();
        for (Placement o : placements) if (o.map == mapId) onMap.add(o);
        Packet objects = new Packet();
        objects.w.writeByte(MapObjects.OBJ_LIST);
        objects.w.writeShort(mapId);
        objects.w.writeShort(onMap.size());
        for (Placement o : onMap) {
            objects.w.writeInt(o.uid);
            objects.w.writeShort(o.map);
            objects.w.writeShort(o.object);
            objects.w.writeInt(o.x);
            objects.w.writeInt(o.z);
            objects.w.writeShort(o.rotation);
            objects.w.writeShort(o.targetMap);
            objects.w.writeInt(o.targetUid);
        }
        return objects;
    }
}
