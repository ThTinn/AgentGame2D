package game.db;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.sql.*;
import java.util.*;

/**
 * Đẩy bảng Core lên MySQL: chạy file templates.sql do menu Tools/Core/Push Templates To MySQL ghi ra, mỗi dòng một lệnh.
 * Lệnh tạo bảng chạy trước, phần dữ liệu chạy trong một transaction: lỗi giữa chừng thì MySQL giữ nguyên bản cũ.
 * Chạy bằng push-templates.bat, hoặc: java game.db.TemplateImport <templates.sql> [db.properties]
 */
public class TemplateImport {
    static final int DUPLICATE_COLUMN = 1060, DUPLICATE_KEY_NAME = 1061, NO_SUCH_COLUMN = 1091;

    /** Điểm vào: chạy schema, rồi dữ liệu trong một transaction, cuối cùng in số hàng và phiên bản mới. */
    public static void main(String[] args) throws IOException, SQLException {
        Path sql = Paths.get(args[0]);
        Path config = Paths.get(args.length > 1 ? args[1] : "db.properties");
        Properties p = new Properties();
        try (InputStream in = Files.newInputStream(config)) {
            p.load(in);
        }
        List<String> lines = Files.readAllLines(sql, StandardCharsets.UTF_8);
        try (Connection c = DriverManager.getConnection(p.getProperty("url"), p.getProperty("user"), p.getProperty("password", ""));
             Statement s = c.createStatement()) {
            for (String line : lines) if (isSchema(line)) schema(s, line);
            int rows = importData(c, s, lines);
            try (ResultSet r = s.executeQuery("SELECT version FROM tpl_meta WHERE id = 1")) {
                r.next();
                System.out.println("Da day " + rows + " hang, phien ban " + r.getInt(1));
            }
        }
    }

    /** Chạy mọi dòng không phải schema trong một transaction, lỗi thì rollback. Trả số dòng INSERT. */
    static int importData(Connection c, Statement s, List<String> lines) throws SQLException {
        c.setAutoCommit(false);
        int rows = 0;
        try {
            for (String line : lines) {
                if (line.isEmpty() || isSchema(line)) continue;
                s.execute(strip(line));
                if (line.startsWith("INSERT")) rows++;
            }
            c.commit();
            return rows;
        } catch (SQLException e) {
            c.rollback();
            throw e;
        }
    }

    /** Dòng tạo / sửa bảng. */
    static boolean isSchema(String line) {
        return line.startsWith("CREATE") || line.startsWith("ALTER");
    }

    /** Bỏ dấu ; cuối dòng. */
    static String strip(String line) {
        return line.substring(0, line.length() - 1);
    }

    /** Chạy một lệnh schema. Đã có cột / khóa, hoặc cột cần bỏ đã bỏ rồi thì bỏ qua: MySQL không có ADD COLUMN IF NOT EXISTS. */
    static void schema(Statement s, String line) throws SQLException {
        try {
            s.execute(strip(line));
        } catch (SQLException e) {
            int code = e.getErrorCode();
            if (code != DUPLICATE_COLUMN && code != DUPLICATE_KEY_NAME && code != NO_SUCH_COLUMN) throw e;
        }
    }
}
