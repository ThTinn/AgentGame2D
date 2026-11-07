package game.account;

import java.io.*;
import java.security.GeneralSecurityException;
import java.security.MessageDigest;
import java.security.SecureRandom;
import java.util.regex.Matcher;
import javax.crypto.SecretKeyFactory;
import javax.crypto.spec.PBEKeySpec;
import game.GameConnection;
import game.db.AccountStore;
import game.db.Json;
import game.player.Player;
import game.player.Profile;

/** Mật khẩu, tạo tài khoản và hồ sơ mới. Hồ sơ khởi đầu đọc từ core/start_profile.json. */
public class Accounts {
    static final int PBKDF2_ROUNDS = 120_000, SALT_BYTES = 16, HASH_BITS = 256;
    static final SecureRandom random = new SecureRandom();
    public static String startProfile;

    /** Băm mật khẩu bằng PBKDF2-HMAC-SHA256. Tốn khoảng 100 ms với số vòng mặc định: không gọi khi giữ World.LOCK. */
    static byte[] hash(String pass, byte[] salt, int rounds) throws IOException {
        try {
            PBEKeySpec spec = new PBEKeySpec(pass.toCharArray(), salt, rounds, HASH_BITS);
            return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).getEncoded();
        } catch (GeneralSecurityException e) {
            throw new IOException(e);
        }
    }

    /** Mật khẩu đúng với tài khoản không. So sánh thời gian cố định. */
    static boolean verify(Account a, String pass) throws IOException {
        return MessageDigest.isEqual(a.hash, hash(pass, a.salt, a.rounds));
    }

    /**
     * Tạo tài khoản ở lần đăng nhập đầu của tên này. Hai kết nối cùng tạo một tên thì bên chậm hơn kiểm mật khẩu với tài
     * khoản bên kia vừa tạo. Trả null khi sai mật khẩu.
     */
    static Account create(String name, String pass) throws IOException {
        byte[] salt = randomBytes(SALT_BYTES);
        Account a = AccountStore.create(name, PBKDF2_ROUNDS, salt, hash(pass, salt, PBKDF2_ROUNDS), id -> newProfile(id, name));
        if (a != null) return a;
        a = AccountStore.find(name);
        return a != null && verify(a, pass) ? a : null;
    }

    /** Tài khoản của bot, chưa có thì tạo. Bot không có mật khẩu: hash ngẫu nhiên, đăng nhập bằng tên bot cũng bị Login chặn. */
    public static Account findOrCreateBot(String name) throws IOException {
        Account a = AccountStore.find(name);
        if (a != null) return a;
        return AccountStore.create(name, PBKDF2_ROUNDS, randomBytes(SALT_BYTES), randomBytes(HASH_BITS / 8), id -> newProfile(id, name));
    }

    /** Mảng byte ngẫu nhiên an toàn. */
    static byte[] randomBytes(int n) {
        byte[] b = new byte[n];
        random.nextBytes(b);
        return b;
    }

    /**
     * Hồ sơ mới từ start_profile.json, thay id và tên hiển thị. Hồ sơ là JSON của PlayerData (JsonUtility): phẳng, trường
     * cấp trên đứng trước trường lồng cùng tên nên replaceFirst trúng đúng trường cần đổi.
     */
    public static String newProfile(int id, String name) {
        return startProfile.replaceFirst("\"id\"\\s*:\\s*\\d+", "\"id\": " + id)
                .replaceFirst("\"displayName\"\\s*:\\s*\"[^\"]*\"", "\"displayName\": \"" + Matcher.quoteReplacement(name) + "\"");
    }

    /** Người chơi online từ tài khoản vừa đọc. conn = null với bot. */
    public static Player newPlayer(Account a, GameConnection conn) {
        Player p = new Player();
        p.id = a.id;
        p.name = a.name;
        p.conn = conn;
        p.profile = new Profile(a.profile);
        p.animSet = (int) Json.num(p.profile.root, "animSetId");
        p.anim = Json.ints(p.profile.root, "animVariants");
        return p;
    }
}
