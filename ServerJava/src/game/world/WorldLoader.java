package game.world;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import game.combat.CombatConfig;
import game.data.DataServer;
import game.db.Json;
import static game.world.World.*;

/**
 * Đọc bảng thế giới lúc khởi động: thông số chung và NPC ở core/world.json, map 2D ở template maps.json (template loại
 * {@link #TEMPLATE_MAPS} trong manifest, client tải cùng file đó để dựng map).
 *
 * <p>Map 2D đi ngang: trục x của server là trục ngang, trục z của server là trục dọc của map, trục y không dùng (luôn
 * {@link World#GROUND_Y}). Nhờ vậy khoảng cách (x, z) của chiến đấu, quái, nhiệm vụ giữ nguyên công thức. File JSON ghi
 * mét, server giữ vị trí dạng số nguyên x100 như trong gói.
 */
public class WorldLoader {
    public static final int TEMPLATE_MAPS = 5;

    /** Đọc world.json trong thư mục core và maps.json theo manifest. Thiếu file hoặc sai định dạng thì ném lỗi, server không chạy. */
    public static void load(Path core) throws IOException {
        loadWorld(Json.object(read(core.resolve("world.json"))));
        DataServer.Entry maps = DataServer.templates.get(TEMPLATE_MAPS);
        if (maps == null) throw new IOException("manifest thieu template " + TEMPLATE_MAPS + " (maps.json)");
        loadMaps(Json.object(read(DataServer.root.resolve(maps.file))));
        if (!mapIds.contains(startMap)) throw new IOException("world.json: startMap " + startMap + " khong co trong maps.json");
        int[] spawn = spawns.get(startMap);
        spawnX = spawn[0];
        spawnZ = spawn[1];
    }

    /** Nội dung file dạng chuỗi UTF-8. */
    static String read(Path file) throws IOException {
        return new String(Files.readAllBytes(file), StandardCharsets.UTF_8);
    }

    /** Mét sang số nguyên x100 như trong gói. */
    static int cm(double meters) {
        return (int) Math.round(meters * 100);
    }

    /** Object con của m, thiếu thì object rỗng. */
    @SuppressWarnings("unchecked")
    static Map<String, Object> child(Map<String, Object> m, String key) {
        Object v = m.get(key);
        return v instanceof Map ? (Map<String, Object>) v : new HashMap<>();
    }

    /** world.json: map xuất phát, thông số chiến đấu (CombatConfig), NPC. */
    static void loadWorld(Map<String, Object> w) {
        startMap = (int) Json.num(w, "startMap");
        readCombatConfig(child(w, "combat"));
        npcMap = (int) Json.num(w, "npcMap");
        for (Map<String, Object> n : Json.rows(w, "npcs")) {
            NpcSpawn npc = new NpcSpawn();
            npc.id = (int) Json.num(n, "id");
            npc.x = cm(Json.real(n, "x"));
            npc.z = cm(Json.real(n, "y"));
            npc.rotation = (int) Json.num(n, "rotation");
            npcs.add(npc);
        }
    }

    /** Thông số chiến đấu, tên trường trùng tên biến của CombatConfig. */
    static void readCombatConfig(Map<String, Object> c) {
        CombatConfig.playerRespawn = Json.real(c, "playerRespawn");
        CombatConfig.mobRespawn = Json.real(c, "mobRespawn");
        CombatConfig.manaRegen = Json.real(c, "manaRegen");
        CombatConfig.aggroRange = Json.real(c, "aggroRange");
        CombatConfig.leashScale = Json.real(c, "leashScale");
        CombatConfig.chaseSpeed = Json.real(c, "chaseSpeed");
        CombatConfig.wanderSpeed = Json.real(c, "wanderSpeed");
        CombatConfig.mobRange = Json.real(c, "mobRange");
        CombatConfig.mobCooldown = Json.real(c, "mobCooldown");
        CombatConfig.mobHitDelay = Json.real(c, "mobHitDelay");
        CombatConfig.baseDamage = Json.num(c, "baseDamage");
        CombatConfig.skillDamagePercent = (int) Json.num(c, "skillDamagePercent");
        CombatConfig.critChance = Json.real(c, "critChance");
        CombatConfig.basicCooldown = Json.real(c, "basicCooldown");
        CombatConfig.bulletSpeed = Json.real(c, "bulletSpeed");
        CombatConfig.bulletHitRadius = Json.real(c, "bulletHitRadius");
        CombatConfig.meleeHalfAngle = Json.real(c, "meleeHalfAngle");
        CombatConfig.hitDelay = Json.real(c, "hitDelay");
        CombatConfig.cellSize = Json.real(c, "cellSize");
    }

    /**
     * maps.json: mỗi map một id, chỗ xuất hiện, bãi quái. Nền / bệ nhảy (solids) chỉ client cần để dựng va chạm, server
     * không mô phỏng vật lý nên bỏ qua. Trùng id thì ném lỗi.
     */
    static void loadMaps(Map<String, Object> root) throws IOException {
        for (Map<String, Object> m : Json.rows(root, "maps")) {
            int id = (int) Json.num(m, "id");
            if (!mapIds.add(id)) throw new IOException("maps.json: trung id map " + id);
            Map<String, Object> spawn = child(m, "spawn");
            spawns.put(id, new int[] { cm(Json.real(spawn, "x")), cm(Json.real(spawn, "y")) });
            if (m.containsKey("xMin") && m.containsKey("xMax"))
                walkX.put(id, new int[] { cm(Json.real(m, "xMin")), cm(Json.real(m, "xMax")) });
            List<Camp> list = new ArrayList<>();
            for (Map<String, Object> c : Json.rows(m, "camps")) list.add(readCamp(c));
            camps.put(id, list);
        }
        if (mapIds.isEmpty()) throw new IOException("maps.json khong co map nao");
    }

    /** Một bãi quái: loại quái, số con, tâm (x, y), nửa bề ngang (range), máu, sát thương, cấp. */
    static Camp readCamp(Map<String, Object> m) {
        Camp c = new Camp();
        c.template = (int) Json.num(m, "template");
        c.count = (int) Json.num(m, "count");
        c.x = cm(Json.real(m, "x"));
        c.z = cm(Json.real(m, "y"));
        c.radius = cm(Json.real(m, "range"));
        c.maxHp = Json.num(m, "maxHp");
        c.damage = Json.num(m, "damage");
        c.level = (int) Json.num(m, "level");
        return c;
    }
}
