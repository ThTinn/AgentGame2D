package game.account;

import java.io.*;
import java.net.InetAddress;
import java.nio.charset.StandardCharsets;
import java.util.regex.*;
import game.GameConnection;
import game.bot.Bots;
import game.db.AccountStore;
import game.db.Database;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.world.Zones;
import static game.world.World.*;

/** Lệnh LOGIN: kiểm tên và mật khẩu, đẩy phiên cũ, đưa người chơi vào game. Chưa đăng nhập thì {@link GameConnection#player} là null. */
public class Login {
    static final int MAX_PASS = 64, MAX_BAD_LOGINS = 5;
    static final int IDLE_MS = 120_000;             // client ping mỗi giây, cả đang chơi
    static final Pattern NAME = Pattern.compile("[\\p{L}\\p{N}_.-]{3,24}");
    static final Pattern BOT_NAME = Pattern.compile("(?i)bot\\d+"); // dành riêng cho bot, không ai đăng nhập được

    /**
     * LOGIN (tên, mật khẩu). Chạy trên thread đọc của kết nối, không giữ World.LOCK lúc băm mật khẩu hay đọc MySQL. Tên
     * chưa có thì tạo tài khoản. Sai MAX_BAD_LOGINS lần trên một kết nối thì ngắt. MySQL lỗi thì báo bận, không ngắt.
     */
    public static void login(GameConnection c, DataInputStream r) throws IOException {
        String name = r.available() > 0 ? Packet.readUtf(r).trim() : "";
        String pass = r.available() > 0 ? Packet.readUtf(r) : "";
        String problem = checkInput(name, pass, c.socket.getInetAddress());
        if (problem != null) {
            c.menu(problem);
            return;
        }
        try {
            Account a = findOrCreate(c, name, pass);
            if (a == null) return;
            AccountStore.touchLogin(a.id);
            takeOver(c, name, a.id);
        } catch (Database.Failure e) {
            System.out.println("Loi database khi dang nhap: " + e.getMessage());
            c.menu("Máy chủ đang bận, vui lòng thử lại sau.");
            return;
        }
        c.socket.setSoTimeout(IDLE_MS);
    }

    /** Lý do từ chối trước khi đụng tới database (sai định dạng, tên bot, đăng nhập quá nhiều), null khi được đi tiếp. */
    static String checkInput(String name, String pass, InetAddress ip) {
        if (!NAME.matcher(name).matches() || pass.isEmpty() || pass.length() > MAX_PASS)
            return "Tên 3-24 ký tự (chữ, số, _ . -), mật khẩu không quá " + MAX_PASS + " ký tự.";
        if (BOT_NAME.matcher(name).matches()) return "Tên này dành cho bot của server.";
        if (!LoginGuard.login(ip)) return "Đăng nhập quá nhiều lần, vui lòng thử lại sau.";
        return null;
    }

    /**
     * Tài khoản đúng mật khẩu, tên chưa có thì tạo. Trả null (đã báo cho client) khi tạo quá nhiều tài khoản hoặc sai mật
     * khẩu; sai đủ MAX_BAD_LOGINS lần thì đóng kết nối sau khi gửi thông báo.
     */
    static Account findOrCreate(GameConnection c, String name, String pass) throws IOException {
        Account a = AccountStore.find(name);
        if (a == null) {
            if (!LoginGuard.newAccount(c.socket.getInetAddress())) {
                c.menu("Đã tạo quá nhiều tài khoản, vui lòng thử lại sau.");
                return null;
            }
            a = Accounts.create(name, pass);
        } else if (!Accounts.verify(a, pass)) {
            a = null;
        }
        if (a == null) {
            c.menu("Sai mật khẩu.");
            if (++c.badLogins >= MAX_BAD_LOGINS) c.closeAfterSend();
        }
        return a;
    }

    /**
     * Đóng mọi phiên của tài khoản rồi mới đọc hồ sơ: hàng đợi database chạy lần lượt nên lần đọc thấy đủ bản ghi của phiên
     * vừa đóng. Hồ sơ đọc lúc kiểm mật khẩu có thể đã cũ, dùng nó thì người chơi đăng nhập lại là quay được kết quả cường
     * hóa. Có phiên thoát giữa lúc đọc thì đọc lại.
     */
    static void takeOver(GameConnection c, String name, int id) throws IOException {
        while (true) {
            long seen;
            synchronized (LOCK) {
                if (c.player != null) c.drop(c.player);
                Player old = online.get(id);
                if (old != null) {
                    GameConnection other = old.conn;
                    other.drop(old);    // kết nối cũ đóng sau: phải hết player, không thì nó xóa nhầm phiên mới khỏi online
                    other.menu("Tài khoản vừa đăng nhập ở nơi khác.");
                    other.closeAfterSend();
                }
                seen = drops;
            }
            Account a = AccountStore.find(name);
            synchronized (LOCK) {
                if (drops == seen && !online.containsKey(id)) {
                    enterGame(c, Accounts.newPlayer(a, c), a);
                    return;
                }
            }
        }
    }

    /**
     * Đưa người chơi vào game, thứ tự như ServerWorld.Login: LOGIN, MENU chào, ME_INFO, hồ sơ, rồi vào map lần trước (lần
     * đầu hoặc map đó đã bị bỏ thì ra chỗ xuất hiện). Gọi khi giữ World.LOCK.
     */
    static void enterGame(GameConnection c, Player p, Account a) throws IOException {
        c.player = p;
        online.put(p.id, p);
        int humans = online.size() - Bots.bots.size();
        System.out.println(p.name + " (id " + p.id + ") vao game tu " + c.socket.getRemoteSocketAddress() + ", " + humans + " nguoi");

        Packet ok = new Packet();
        ok.w.writeByte(1);      // TypeLogin.Team
        c.post(Cmd.LOGIN, ok);
        c.menu("Chào mừng " + p.name + " vào game.");
        c.post(Cmd.ME_INFO, meInfoPacket(p));
        c.post(Cmd.PROFILE, profilePacket(p));

        if (mapIds.contains(a.map)) Zones.enterMap(p, a.map, a.x, a.z);
        else Zones.enterMap(p, startMap, spawnX, spawnZ);
    }

    /** ME_INFO: id, tên, máu, mana, coin (chưa dùng), vàng. */
    static Packet meInfoPacket(Player p) throws IOException {
        Packet me = new Packet();
        me.w.writeInt(p.id);
        me.utf(p.name);
        me.w.writeLong(p.profile.hp);
        me.w.writeLong(p.profile.maxHp);
        me.w.writeLong(p.profile.mp);
        me.w.writeLong(p.profile.maxMp);
        me.w.writeLong(0);      // coin
        me.w.writeLong(p.profile.gold);
        return me;
    }

    /** PROFILE DATA: cả hồ sơ dạng JSON để client dựng PlayerData. */
    static Packet profilePacket(Player p) throws IOException {
        byte[] json = p.profile.json().getBytes(StandardCharsets.UTF_8);
        Packet profile = new Packet();
        profile.w.writeByte(0); // ProfileSub.DATA
        profile.w.writeInt(json.length);
        profile.w.write(json);
        return profile;
    }
}
