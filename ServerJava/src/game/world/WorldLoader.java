package game.world;

import java.io.*;
import java.nio.file.*;
import java.util.*;
import game.combat.CombatConfig;
import game.data.DataServer;
import game.net.Packet;
import static game.world.World.*;

/**
 * Đọc bảng thế giới trong ServerData/core lúc khởi động: thông số, NPC, bãi quái (world.bin), bàn ghế, cổng
 * (mapobjects.bin). Danh sách map lấy theo các bundle map có trong manifest.
 */
public class WorldLoader {
    static final int ASSET_MAP = 2;

    /** Đọc world.bin, mapobjects.bin trong thư mục core, rồi ghi nhận các map có bundle. */
    public static void load(Path core) throws IOException {
        loadWorld(core.resolve("world.bin"));
        loadMapObjects(core.resolve("mapobjects.bin"));
        for (List<DataServer.Entry> files : DataServer.bundles.values())
            for (DataServer.Entry e : files) if (e.type == ASSET_MAP) mapIds.add(e.id);
    }

    /** Mở file nhị phân để đọc tuần tự. */
    static DataInputStream open(Path file) throws IOException {
        return new DataInputStream(new ByteArrayInputStream(Files.readAllBytes(file)));
    }

    /** Một số thực ghi dạng int x100. */
    static double hundredths(DataInputStream r) throws IOException {
        return r.readInt() / 100.0;
    }

    /**
     * world.bin, định dạng ở LocalPlaySettings.ToBytes: chỗ xuất hiện, thông số chiến đấu (CombatConfig), NPC, bãi quái
     * theo map. Số thực là int x100. Vị trí giữ nguyên số nguyên x100 như trong gói.
     */
    static void loadWorld(Path file) throws IOException {
        DataInputStream r = open(file);
        int schema = r.readShort();
        if (schema != 1) throw new IOException("world.bin schema " + schema);
        startMap = r.readShort();
        spawnX = r.readInt();
        spawnZ = r.readInt();
        readCombatConfig(r);
        npcMap = r.readShort();
        int npcCount = r.readUnsignedByte();
        for (int i = 0; i < npcCount; i++) npcs.add(readNpc(r));
        int mapCount = r.readUnsignedByte();
        for (int i = 0; i < mapCount; i++) {
            int map = r.readShort();
            List<Camp> list = new ArrayList<>();
            int campCount = r.readUnsignedByte();
            for (int k = 0; k < campCount; k++) list.add(readCamp(r));
            camps.put(map, list);
        }
        if (r.available() != 0) throw new IOException("world.bin thua " + r.available() + " byte");
    }

    /** Thông số chiến đấu, đúng thứ tự trong world.bin. */
    static void readCombatConfig(DataInputStream r) throws IOException {
        CombatConfig.playerRespawn = hundredths(r);
        CombatConfig.mobRespawn = hundredths(r);
        CombatConfig.manaRegen = hundredths(r);
        CombatConfig.aggroRange = hundredths(r);
        CombatConfig.leashScale = hundredths(r);
        CombatConfig.chaseSpeed = hundredths(r);
        CombatConfig.wanderSpeed = hundredths(r);
        CombatConfig.mobRange = hundredths(r);
        CombatConfig.mobCooldown = hundredths(r);
        CombatConfig.mobHitDelay = hundredths(r);
        CombatConfig.baseDamage = r.readLong();
        CombatConfig.skillDamagePercent = r.readInt();
        CombatConfig.critChance = hundredths(r);
        CombatConfig.basicCooldown = hundredths(r);
        CombatConfig.bulletSpeed = hundredths(r);
        CombatConfig.bulletHitRadius = hundredths(r);
        CombatConfig.meleeHalfAngle = hundredths(r);
        CombatConfig.hitDelay = hundredths(r);
        CombatConfig.cellSize = hundredths(r);
    }

    /** Một NPC: id, vị trí, góc quay. */
    static NpcSpawn readNpc(DataInputStream r) throws IOException {
        NpcSpawn npc = new NpcSpawn();
        npc.id = r.readShort();
        npc.x = r.readInt();
        npc.z = r.readInt();
        npc.rotation = r.readShort();
        return npc;
    }

    /** Một bãi quái: loại quái, số con, tâm, bán kính, máu, sát thương, cấp. */
    static Camp readCamp(DataInputStream r) throws IOException {
        Camp c = new Camp();
        c.template = r.readShort();
        c.count = r.readShort();
        c.x = r.readInt();
        c.z = r.readInt();
        c.radius = r.readInt();
        c.maxHp = r.readLong();
        c.damage = r.readLong();
        c.level = r.readShort();
        return c;
    }

    /**
     * mapobjects.bin, định dạng ở MapObjectDatabase.ToBytes: loại object (server chỉ cần số chỗ ngồi) rồi các chỗ đặt
     * trên map (ghế, cổng kèm map / cổng đích).
     */
    static void loadMapObjects(Path file) throws IOException {
        DataInputStream r = open(file);
        int schema = r.readShort();
        if (schema != 2) throw new IOException("mapobjects.bin schema " + schema);
        int objectCount = r.readShort();
        for (int i = 0; i < objectCount; i++) {
            int id = r.readShort();
            Packet.readUtf(r);      // tên
            Packet.readUtf(r);      // prefab
            r.readByte();           // MapObjectKind
            objectSeats.put(id, r.readUnsignedByte());
        }
        int placementCount = r.readShort();
        for (int i = 0; i < placementCount; i++) placements.add(readPlacement(r));
        if (r.available() != 0) throw new IOException("mapobjects.bin thua " + r.available() + " byte");
    }

    /** Một chỗ đặt object trên map. */
    static Placement readPlacement(DataInputStream r) throws IOException {
        Placement p = new Placement();
        p.uid = r.readInt();
        p.map = r.readShort();
        p.object = r.readShort();
        p.x = r.readInt();
        p.z = r.readInt();
        p.rotation = r.readShort();
        p.targetMap = r.readShort();
        p.targetUid = r.readInt();
        return p;
    }
}
