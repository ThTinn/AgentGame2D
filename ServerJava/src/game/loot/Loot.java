package game.loot;

import java.io.*;
import java.util.*;
import game.mob.Mob;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;
import game.world.GameMap;
import game.world.World;

/** Đồ rơi trên đất: quay bảng tpl_drops khi quái chết, nhặt, hết hạn. Chỉ gọi khi giữ World.LOCK. */
public class Loot {
    // sub của lệnh GROUND_ITEM
    static final int GROUND_ADD = 0, GROUND_REMOVE = 1, GROUND_PICK = 2, LOOT_LIST = 3;
    static final double LOOT_OWNER = 10;    // giây chỉ người hạ quái nhặt được
    static final double LOOT_LIFE = 60;     // giây món nằm trên đất trước khi biến mất
    static final double GEM_CHANCE = 0.06;  // tỉ lệ rơi ngọc khi không có bảng đồ rơi nào
    static final double SCATTER = 1.2;      // món rơi rải trong vòng chừng này mét quanh quái

    /** Một món trên đất. Vị trí x100; from là chỗ quái chết, món bay từ đó ra (x, z). owner 0 là ai cũng nhặt được. */
    public static class Ground {
        int id, itemId, count, owner, x, z, fromX, fromZ;
        double freeAt, expireAt;
    }

    static int nextGroundId = 1;

    // ---------------- rơi đồ ----------------

    /**
     * Quay đồ rơi khi killer hạ m. Bảng theo loại quái ("mob_<template>"), không có thì bảng chung "monster", tỉ lệ nhân
     * theo % tìm đồ. Không có bảng nào thì thỉnh thoảng rơi một viên ngọc ngẫu nhiên. Đồ thuộc về killer trong LOOT_OWNER
     * giây.
     */
    public static void rollDrops(GameMap map, Mob m, Player killer) throws IOException {
        Profile pr = killer.profile;
        double find = Math.max(0, pr.stats.bonus[Tables.ITEM_FIND]);
        List<Tables.Drop> table = Tables.drops.get("mob_" + m.template);
        if (table == null) table = Tables.drops.get("monster");
        if (table != null) rollTable(map, m, killer, table, 1 + find / 100);
        else rollGem(map, m, killer, find);
    }

    /** Quay từng dòng của bảng: trúng tỉ lệ (chance x scale, tối đa 100%) thì rơi số lượng ngẫu nhiên min..max. */
    static void rollTable(GameMap map, Mob m, Player killer, List<Tables.Drop> table, double scale) throws IOException {
        for (Tables.Drop d : table) {
            if (d.item.isEmpty() || d.chance <= 0) continue;
            if (World.rand().nextDouble() * 100 >= Math.min(100, d.chance * scale)) continue;
            int lo = Math.max(1, d.min), hi = Math.max(lo, d.max);
            drop(map, m, Tables.itemsByName.get(d.item), lo + World.rand().nextInt(hi - lo + 1), killer.id);
        }
    }

    /** Không có bảng: GEM_CHANCE (nhân % tìm đồ, tối đa 90%) rơi một viên ngọc ngẫu nhiên. */
    static void rollGem(GameMap map, Mob m, Player killer, double find) throws IOException {
        if (World.rand().nextDouble() >= Math.min(0.9, GEM_CHANCE * (1 + find / 100))) return;
        List<Tables.Item> gems = new ArrayList<>();
        for (Tables.Item it : Tables.items.values()) if (it.kind == Tables.KIND_GEM) gems.add(it);
        if (!gems.isEmpty()) drop(map, m, gems.get(World.rand().nextInt(gems.size())), 1, killer.id);
    }

    /** Thả count món it quanh chỗ quái m chết, báo cả khu. it null (tên trong bảng không khớp vật phẩm nào) thì bỏ. */
    static void drop(GameMap map, Mob m, Tables.Item it, int count, int owner) throws IOException {
        if (it == null) return;
        double now = World.now();
        double angle = World.rand().nextDouble(Math.PI * 2), radius = SCATTER * Math.sqrt(World.rand().nextDouble());
        Ground g = new Ground();
        g.id = nextGroundId++;
        g.itemId = it.id;
        g.count = count;
        g.owner = owner;
        g.fromX = Packet.cm(m.x);
        g.fromZ = Packet.cm(m.z);
        g.x = Packet.cm(m.x + Math.cos(angle) * radius);
        g.z = Packet.cm(m.z + Math.sin(angle) * radius);
        g.freeAt = now + LOOT_OWNER;
        g.expireAt = now + LOOT_LIFE;
        map.ground.put(g.id, g);
        sendGroundAdd(map, g, now);
    }

    // ---------------- nhặt, hết hạn ----------------

    /**
     * GROUND_ITEM PICK (id món). Món của người khác còn trong thời gian giữ thì không nhặt được. Túi đầy thì báo; nhặt được
     * một phần thì phần còn lại nằm lại tại chỗ.
     */
    public static void pick(Player p, DataInputStream r) throws IOException {
        int sub = r.readUnsignedByte();
        GameMap map = p.room;
        if (sub != GROUND_PICK || map == null) return;
        int id = r.readInt();
        Ground g = map.ground.get(id);
        double now = World.now();
        if (g == null || (g.owner != 0 && g.owner != p.id && now < g.freeAt)) return;
        int lost = p.profile.bag.add(g.itemId, g.count);
        if (lost > 0) p.conn.toast("Hành trang đầy");
        if (lost >= g.count) return;
        p.flush(Profile.PART_BAG);
        if (lost > 0) {     // gói không có "đổi số lượng": bỏ hình cũ rồi thêm lại món tại chỗ
            g.count = lost;
            g.fromX = g.x;
            g.fromZ = g.z;
            sendGroundRemove(map, id, 0);
            sendGroundAdd(map, g, now);
            return;
        }
        map.ground.remove(id);
        sendGroundRemove(map, id, p.id);
    }

    /** Bỏ các món đã hết hạn trong khu (WorldLoop gọi khi khu có người thật). */
    public static void tickGround(GameMap map, double now) throws IOException {
        Iterator<Ground> it = map.ground.values().iterator();
        while (it.hasNext()) {
            Ground g = it.next();
            if (now < g.expireAt) continue;
            it.remove();
            sendGroundRemove(map, g.id, 0);
        }
    }

    // ---------------- gói ----------------

    /** LOOT_LIST: mọi món đang nằm trong khu, gửi lúc MAP_READY. */
    public static Packet listPacket(GameMap map) throws IOException {
        Packet loot = new Packet();
        loot.w.writeByte(LOOT_LIST);
        loot.w.writeShort(map.ground.size());
        double now = World.now();
        for (Ground g : map.ground.values()) writeGround(loot, g, now);
        return loot;
    }

    /** Một món trong gói: id, vật phẩm, số lượng, chỗ bay ra, chỗ nằm, chủ, giây còn giữ cho chủ, giây còn tồn tại. */
    static void writeGround(Packet w, Ground g, double now) throws IOException {
        w.w.writeInt(g.id);
        w.w.writeShort(g.itemId);
        w.w.writeShort(g.count);
        w.vec(g.fromX, World.GROUND_Y, g.fromZ);
        w.w.writeInt(g.x);
        w.w.writeInt(g.z);
        w.w.writeInt(g.owner);
        w.w.writeShort((int) Math.max(0, Math.round(g.freeAt - now)));
        w.w.writeShort((int) Math.max(1, Math.round(g.expireAt - now)));
    }

    /** GROUND_ADD cho cả khu. */
    static void sendGroundAdd(GameMap map, Ground g, double now) throws IOException {
        Packet out = new Packet();
        out.w.writeByte(GROUND_ADD);
        writeGround(out, g, now);
        map.broadcast(Cmd.GROUND_ITEM, out);
    }

    /** GROUND_REMOVE cho cả khu. picker: người nhặt (client cho món bay vào người đó), 0 khi món tự biến mất. */
    static void sendGroundRemove(GameMap map, int id, int picker) throws IOException {
        Packet out = new Packet();
        out.w.writeByte(GROUND_REMOVE);
        out.w.writeInt(id);
        out.w.writeInt(picker);
        map.broadcast(Cmd.GROUND_ITEM, out);
    }
}
