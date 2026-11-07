package game.db;

import java.sql.*;
import java.util.*;
import java.util.function.IntFunction;
import game.account.Account;
import static game.db.Database.*;

/**
 * Tài khoản và hồ sơ người chơi trong MySQL. Không gọi {@link #find} / {@link #create} khi giữ World.LOCK: MySQL chậm chỉ
 * được làm chậm đăng nhập, không làm chậm người đang chơi.
 *
 * <p>Chế độ tạm không MySQL ({@link #useMemory}): tài khoản và hồ sơ giữ trong RAM, mất khi tắt server. Dùng để chạy thử
 * luồng game khi chưa cài MySQL, không dùng khi có người chơi thật.
 */
public class AccountStore {
    // chế độ RAM: khóa là tên viết thường (không phân biệt hoa thường, vẫn phân biệt dấu như MySQL)
    static boolean memory;
    static final Map<String, Account> memoryAccounts = new HashMap<>();
    static int memoryNextId = 1001;

    /** Bật chế độ RAM, gọi trước mọi hàm khác. Sau đó không hàm nào đụng tới MySQL. */
    public static void useMemory() {
        memory = true;
    }

    /** Đang ở chế độ RAM (không MySQL) không. */
    public static boolean inMemory() {
        return memory;
    }

    /** Bản sao của tài khoản trong RAM: người gọi sửa Account không làm đổi bản lưu. */
    static Account copy(Account a) {
        Account c = new Account();
        c.id = a.id;
        c.name = a.name;
        c.rounds = a.rounds;
        c.salt = a.salt;
        c.hash = a.hash;
        c.profile = a.profile;
        c.map = a.map;
        c.x = a.x;
        c.z = a.z;
        return c;
    }

    static String memoryKey(String name) {
        return name.toLowerCase(Locale.ROOT);
    }
    // id người chơi bắt đầu từ 1001. Tên so không phân biệt hoa thường nhưng phân biệt dấu (Tín khác Tin).
    // profile để MEDIUMTEXT, không dùng kiểu JSON: MySQL sẽ sắp xếp lại khóa, client đọc lại nguyên văn
    static final String[] SCHEMA = {
        "CREATE TABLE IF NOT EXISTS accounts ("
            + " id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,"
            + " name VARCHAR(24) NOT NULL COLLATE utf8mb4_0900_as_ci,"
            + " rounds INT NOT NULL,"
            + " pw_salt VARBINARY(32) NOT NULL,"
            + " pw_hash VARBINARY(64) NOT NULL,"
            + " created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,"
            + " last_login TIMESTAMP NULL,"
            + " UNIQUE KEY uq_accounts_name (name)"
            + ") ENGINE=InnoDB AUTO_INCREMENT=1001 DEFAULT CHARSET=utf8mb4",
        "CREATE TABLE IF NOT EXISTS players ("
            + " id INT NOT NULL PRIMARY KEY,"
            + " profile MEDIUMTEXT NOT NULL,"
            + " map_id SMALLINT NOT NULL DEFAULT -1,"
            + " x INT NOT NULL DEFAULT 0,"
            + " z INT NOT NULL DEFAULT 0,"
            + " updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,"
            + " CONSTRAINT fk_players_account FOREIGN KEY (id) REFERENCES accounts (id)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    };

    static final String SELECT_ACCOUNT = "SELECT a.id, a.name, a.rounds, a.pw_salt, a.pw_hash, p.profile, p.map_id, p.x, p.z"
            + " FROM accounts a JOIN players p ON p.id = a.id WHERE a.name = ?";
    static final String INSERT_ACCOUNT = "INSERT INTO accounts (name, rounds, pw_salt, pw_hash) VALUES (?, ?, ?, ?)";
    static final String INSERT_PLAYER = "INSERT INTO players (id, profile) VALUES (?, ?)";
    static final String TOUCH_LOGIN = "UPDATE accounts SET last_login = CURRENT_TIMESTAMP WHERE id = ?";
    static final String SAVE_PLAYER = "UPDATE players SET profile = ?, map_id = ?, x = ?, z = ? WHERE id = ?";

    /** Tạo bảng nếu chưa có. Lỗi thì ném ra: server không chạy khi không có database. */
    public static void createTables() throws Failure {
        if (memory) return;
        call(() -> {
            try (Statement s = connection().createStatement()) {
                for (String sql : SCHEMA) s.execute(sql);
            }
            return null;
        });
    }

    /** Tài khoản theo tên kèm hồ sơ và vị trí cuối, null khi chưa có. */
    public static Account find(String name) throws Failure {
        if (memory) {
            synchronized (memoryAccounts) {
                Account a = memoryAccounts.get(memoryKey(name));
                return a == null ? null : copy(a);
            }
        }
        return call(() -> {
            try (PreparedStatement s = connection().prepareStatement(SELECT_ACCOUNT)) {
                s.setString(1, name);
                try (ResultSet r = s.executeQuery()) {
                    return r.next() ? readAccount(r) : null;
                }
            }
        });
    }

    /** Một hàng của SELECT_ACCOUNT thành Account. */
    static Account readAccount(ResultSet r) throws SQLException {
        Account a = new Account();
        a.id = r.getInt(1);
        a.name = r.getString(2);
        a.rounds = r.getInt(3);
        a.salt = r.getBytes(4);
        a.hash = r.getBytes(5);
        a.profile = r.getString(6);
        a.map = r.getInt(7);
        a.x = r.getInt(8);
        a.z = r.getInt(9);
        return a;
    }

    /**
     * Tạo tài khoản và hồ sơ trong một transaction. Hồ sơ dựng bằng profileFor(id) vì cần id vừa cấp. Trả null khi tên vừa
     * bị kết nối khác lấy mất.
     */
    public static Account create(String name, int rounds, byte[] salt, byte[] hash, IntFunction<String> profileFor) throws Failure {
        if (memory) return createInMemory(name, rounds, salt, hash, profileFor);
        return call(() -> {
            Connection c = connection();
            c.setAutoCommit(false);
            try {
                Account a = new Account();
                a.name = name;
                a.rounds = rounds;
                a.salt = salt;
                a.hash = hash;
                a.map = -1;
                a.id = insertAccount(c, a);
                a.profile = profileFor.apply(a.id);
                try (PreparedStatement s = c.prepareStatement(INSERT_PLAYER)) {
                    s.setInt(1, a.id);
                    s.setString(2, a.profile);
                    s.executeUpdate();
                }
                c.commit();
                return a;
            } catch (SQLIntegrityConstraintViolationException e) {
                c.rollback();
                return null;
            } catch (SQLException e) {
                try {
                    c.rollback();
                } catch (SQLException ignored) {
                }
                throw e;
            } finally {
                c.setAutoCommit(true);
            }
        });
    }

    /** Như {@link #create} ở chế độ RAM. Tên đã có thì trả null. */
    static Account createInMemory(String name, int rounds, byte[] salt, byte[] hash, IntFunction<String> profileFor) {
        synchronized (memoryAccounts) {
            if (memoryAccounts.containsKey(memoryKey(name))) return null;
            Account a = new Account();
            a.id = memoryNextId++;
            a.name = name;
            a.rounds = rounds;
            a.salt = salt;
            a.hash = hash;
            a.map = -1;
            a.profile = profileFor.apply(a.id);
            memoryAccounts.put(memoryKey(name), a);
            return copy(a);
        }
    }

    /** Thêm hàng accounts, trả id MySQL vừa cấp. */
    static int insertAccount(Connection c, Account a) throws SQLException {
        try (PreparedStatement s = c.prepareStatement(INSERT_ACCOUNT, Statement.RETURN_GENERATED_KEYS)) {
            s.setString(1, a.name);
            s.setInt(2, a.rounds);
            s.setBytes(3, a.salt);
            s.setBytes(4, a.hash);
            s.executeUpdate();
            try (ResultSet keys = s.getGeneratedKeys()) {
                keys.next();
                return keys.getInt(1);
            }
        }
    }

    /** Ghi nền thời điểm đăng nhập cuối. */
    public static void touchLogin(int id) {
        if (memory) return;
        later(() -> {
            try (PreparedStatement s = connection().prepareStatement(TOUCH_LOGIN)) {
                s.setInt(1, id);
                s.executeUpdate();
            }
            return null;
        });
    }

    /** Ghi nền hồ sơ (JSON) và map, vị trí hiện tại của người chơi. */
    public static void savePlayer(int id, String profile, int map, int x, int z) {
        if (memory) {
            synchronized (memoryAccounts) {
                for (Account a : memoryAccounts.values())
                    if (a.id == id) {
                        a.profile = profile;
                        a.map = map;
                        a.x = x;
                        a.z = z;
                    }
            }
            return;
        }
        later(() -> {
            try (PreparedStatement s = connection().prepareStatement(SAVE_PLAYER)) {
                s.setString(1, profile);
                s.setInt(2, map);
                s.setInt(3, x);
                s.setInt(4, z);
                s.setInt(5, id);
                s.executeUpdate();
            }
            return null;
        });
    }
}
