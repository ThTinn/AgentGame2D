package game.db;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.sql.*;
import java.util.*;
import static game.db.Database.*;

/** Đọc các table tpl_*: đóng gói gửi client ({@link #loadTemplates}) và parse làm luật ({@link #loadRules}). */
public class TemplateStore {
    /** Bảng Core đóng gói từ các table tpl_*, gửi client như template. */
    public static class Templates {
        public int version, tables, rows;
        public byte[] data;
    }

    /** Mọi hàng của các table tpl_* ở dạng JSON, đọc một lần cho cả hai cách dùng. */
    static class RawTables {
        final Map<String, List<String>> rows = new LinkedHashMap<>();      // tên table -> JSON từng hàng
        final Map<String, String> settings = new LinkedHashMap<>();        // tên trong tpl_settings -> JSON
        Integer version;                                                   // tpl_meta.version, null khi chưa có hàng
    }

    /**
     * Đọc mọi table tpl_* thành một gói, null khi chưa đẩy lần nào. Định dạng: int số bảng, mỗi bảng short + tên, int số
     * hàng, mỗi hàng int + JSON UTF-8 đúng tên trường của client (CoreTemplates). tpl_settings tách thành bảng
     * "settings:tên" một hàng.
     */
    public static Templates loadTemplates() throws Failure {
        return call(() -> {
            RawTables raw = readAll(connection());
            if (raw == null || raw.version == null) return null;
            Templates t = new Templates();
            t.version = raw.version;
            ByteArrayOutputStream body = new ByteArrayOutputStream();
            for (Map.Entry<String, List<String>> e : raw.rows.entrySet()) writeTable(t, body, e.getKey(), e.getValue());
            for (Map.Entry<String, String> e : raw.settings.entrySet())
                writeTable(t, body, "settings:" + e.getKey(), Collections.singletonList(e.getValue()));
            ByteArrayOutputStream all = new ByteArrayOutputStream();
            writeInt(all, t.tables);
            all.write(body.toByteArray(), 0, body.size());
            t.data = all.toByteArray();
            return t;
        });
    }

    /** Các table tpl_* dạng JSON đã parse cho Tables, null khi chưa đẩy lần nào. tpl_settings tách thành "settings:tên" một hàng. */
    public static Map<String, List<Map<String, Object>>> loadRules() throws Failure {
        return call(() -> {
            RawTables raw = readAll(connection());
            if (raw == null) return null;
            Map<String, List<Map<String, Object>>> tables = new HashMap<>();
            for (Map.Entry<String, List<String>> e : raw.rows.entrySet()) {
                List<Map<String, Object>> rows = new ArrayList<>();
                for (String json : e.getValue()) rows.add(Json.object(json));
                tables.put(e.getKey(), rows);
            }
            for (Map.Entry<String, String> e : raw.settings.entrySet())
                tables.put("settings:" + e.getKey(), Collections.singletonList(Json.object(e.getValue())));
            return tables;
        });
    }

    /** Một bảng của tpl_settings (trường đơn của cả bảng, ví dụ luckyBonus); chưa có thì trả map rỗng. */
    public static Map<String, Object> setting(Map<String, List<Map<String, Object>>> tables, String name) {
        List<Map<String, Object>> rows = tables.get("settings:" + name);
        return rows != null ? rows.get(0) : new HashMap<>();
    }

    /**
     * Đọc mọi table tpl_* thành JSON từng hàng, null khi chưa có tpl_meta (chưa đẩy lần nào). Hàng của tpl_items kèm mảng
     * "options" lấy từ tpl_item_options, luôn có kể cả rỗng để ghi đè bản trong build của client.
     */
    static RawTables readAll(Connection c) throws SQLException {
        List<String> names = tableNames(c);
        if (!names.remove("tpl_meta")) return null;
        boolean hasSettings = names.remove("tpl_settings");
        Map<Long, StringBuilder> itemOptions = names.remove("tpl_item_options") ? loadItemOptions(c) : new HashMap<>();

        RawTables raw = new RawTables();
        for (String name : names) {
            List<String> rows = new ArrayList<>();
            try (Statement s = c.createStatement(); ResultSet r = s.executeQuery("SELECT * FROM " + name + " ORDER BY row_no")) {
                while (r.next()) {
                    String json = rowJson(r);
                    if (name.equals("tpl_items")) json = withOptions(json, itemOptions.get(r.getLong("id")));
                    rows.add(json);
                }
            }
            raw.rows.put(name, rows);
        }
        if (hasSettings)
            try (Statement s = c.createStatement(); ResultSet r = s.executeQuery("SELECT name, data FROM tpl_settings")) {
                while (r.next()) raw.settings.put(r.getString(1), r.getString(2));
            }
        try (Statement s = c.createStatement(); ResultSet r = s.executeQuery("SELECT version FROM tpl_meta WHERE id = 1")) {
            if (r.next()) raw.version = r.getInt(1);
        }
        return raw;
    }

    /** Thêm trường "options" (mảng chỉ số) vào cuối JSON của một hàng vật phẩm. */
    static String withOptions(String json, StringBuilder options) {
        String head = json.substring(0, json.length() - 1);    // bỏ dấu } cuối
        return head + (head.length() > 1 ? "," : "") + "\"options\":[" + (options != null ? options : "") + "]}";
    }

    /** Tên các table bắt đầu bằng tpl_ trong database đang dùng. */
    static List<String> tableNames(Connection c) throws SQLException {
        List<String> names = new ArrayList<>();
        String sql = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'tpl%'";
        try (Statement s = c.createStatement(); ResultSet r = s.executeQuery(sql)) {
            while (r.next()) if (r.getString(1).startsWith("tpl_")) names.add(r.getString(1));
        }
        return names;
    }

    /** Chỉ số theo vật phẩm (id -> các phần tử JSON của PlayerStatEffect, cách nhau dấu phẩy). stat là id trong tpl_options. */
    static Map<Long, StringBuilder> loadItemOptions(Connection c) throws SQLException {
        Map<Long, StringBuilder> result = new HashMap<>();
        String sql = "SELECT item_id, option_id, value FROM tpl_item_options ORDER BY item_id, row_no";
        try (Statement s = c.createStatement(); ResultSet r = s.executeQuery(sql)) {
            while (r.next()) {
                StringBuilder sb = result.computeIfAbsent(r.getLong(1), k -> new StringBuilder());
                if (sb.length() > 0) sb.append(',');
                sb.append("{\"stat\":").append(r.getLong(2)).append(",\"value\":").append(r.getDouble(3)).append('}');
            }
        }
        return result;
    }

    /** Ghi một bảng vào gói template: short + tên, int số hàng, mỗi hàng int + JSON. Đếm thêm vào số bảng / số hàng của t. */
    static void writeTable(Templates t, ByteArrayOutputStream out, String name, List<String> rows) {
        byte[] n = name.getBytes(StandardCharsets.UTF_8);
        out.write(n.length >> 8);
        out.write(n.length);
        out.write(n, 0, n.length);
        writeInt(out, rows.size());
        for (String row : rows) {
            byte[] b = row.getBytes(StandardCharsets.UTF_8);
            writeInt(out, b.length);
            out.write(b, 0, b.length);
        }
        t.tables++;
        t.rows += rows.size();
    }

    /** Ghi int 4 byte big-endian như DataOutputStream. */
    static void writeInt(ByteArrayOutputStream out, int v) {
        out.write(v >>> 24);
        out.write(v >>> 16);
        out.write(v >>> 8);
        out.write(v);
    }

    /**
     * Một hàng thành JSON cho JsonUtility: bỏ cột row_no và cột NULL, cột kiểu JSON giữ nguyên, số / BOOLEAN ghi thẳng,
     * chuỗi thì thoát ký tự.
     */
    static String rowJson(ResultSet r) throws SQLException {
        ResultSetMetaData m = r.getMetaData();
        StringBuilder sb = new StringBuilder("{");
        for (int i = 1; i <= m.getColumnCount(); i++) {
            String name = m.getColumnLabel(i);
            Object v = r.getObject(i);
            if (v == null || name.equals("row_no")) continue;
            if (sb.length() > 1) sb.append(',');
            sb.append('"').append(name).append("\":");
            boolean raw = v instanceof Number || v instanceof Boolean || "JSON".equals(m.getColumnTypeName(i));
            if (raw) sb.append(v);
            else Json.quote(sb, v.toString());
        }
        return sb.append('}').toString();
    }
}
