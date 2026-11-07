package game;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import game.account.Accounts;
import game.account.LoginGuard;
import game.bot.Bots;
import game.data.DataServer;
import game.db.AccountStore;
import game.db.Database;
import game.db.TemplateStore;
import game.net.PacketConnection;
import game.player.Player;
import game.tables.TableLoader;
import game.tables.Tables;
import game.world.World;
import game.world.WorldLoader;

/**
 * Điểm vào của server game: nạp dữ liệu, tạo bot, chạy thread thế giới rồi nhận kết nối. Luật nằm ở các package con theo
 * chức năng, {@link GameConnection} chia lệnh về đó. Lệnh tải dữ liệu đi tiếp xuống game.data, tài khoản và hồ sơ ở MySQL
 * (game.db).
 * Chạy bằng run-game.bat / run-game.sh, hoặc: java game.GameServer [thư mục dữ liệu, mặc định data] [cổng] [db.properties] [số bot]
 * Không có file db.properties thì chạy tạm không MySQL (AccountStore.useMemory).
 */
public class GameServer {
    static final int MAX_CLIENTS = 500;
    static final int TEMPLATE_CORE = 3;             // GameData.Type_Core_Template

    /** Khởi động: đọc ServerData và MySQL, tạo bot, chạy WorldLoop, rồi vòng nhận kết nối. */
    public static void main(String[] args) throws IOException, InterruptedException {
        DataServer.root = Paths.get(args.length > 0 ? args[0] : "data");
        int port = args.length > 1 ? Integer.parseInt(args[1]) : 18889;
        Path config = Paths.get(args.length > 2 ? args[2] : "db.properties");
        int botCount = args.length > 3 ? Integer.parseInt(args[3]) : 6;

        loadData(config);
        Bots.spawnBots(botCount);
        Thread world = new Thread(WorldLoop::run, "world");
        world.setDaemon(true);
        world.start();
        Runtime.getRuntime().addShutdownHook(new Thread(GameServer::shutdown, "shutdown"));

        try (ServerSocket listener = new ServerSocket(port)) {
            System.out.println("GameServer nghe cong " + port + ", " + World.mapIds.size() + " map 2D, " + World.npcs.size()
                    + " NPC, " + Bots.bots.size() + " bot");
            while (true) {
                Socket socket = DataServer.accept(listener);
                if (socket != null) accept(socket);
            }
        }
    }

    /** Nạp manifest, bảng thế giới, hồ sơ khởi đầu, mở MySQL, tạo bảng, nạp bảng Core (gửi client và làm luật). */
    static void loadData(Path config) throws IOException {
        DataServer.loadManifest();
        Path core = DataServer.root.resolve("core");
        WorldLoader.load(core);
        Accounts.startProfile = new String(Files.readAllBytes(core.resolve("start_profile.json")), StandardCharsets.UTF_8);
        if (!Files.exists(config)) {
            // tạm thời: chưa cài MySQL vẫn chạy được luồng tải dữ liệu -> đăng nhập -> vào map
            AccountStore.useMemory();
            System.out.println("Khong thay " + config + ": chay KHONG MySQL, tai khoan va ho so chi o RAM, mat khi tat server,"
                    + " chua co chien dau");
            return;
        }
        Database.open(config);
        AccountStore.createTables();
        loadTemplates();
        TableLoader.load();
        if (!Tables.ready) System.out.println("Chua co bang tpl_*: chua co chien dau");
    }

    /**
     * Nhận một kết nối mới, mỗi kết nối một thread đọc. Server đầy (MAX_CLIENTS) hoặc IP đã đủ kết nối (LoginGuard) thì
     * đóng ngay.
     */
    static void accept(Socket socket) {
        InetAddress ip = socket.getInetAddress();
        if (GameConnection.clients.get() >= MAX_CLIENTS || !LoginGuard.connect(ip)) {
            PacketConnection.closeQuietly(socket);
            return;
        }
        try {
            GameConnection conn = new GameConnection(socket);
            GameConnection.clients.incrementAndGet();
            new Thread(conn).start();
        } catch (IOException e) {
            System.out.println("Loi nhan ket noi: " + e.getMessage());
            LoginGuard.disconnect(ip);
            PacketConnection.closeQuietly(socket);
        }
    }

    /**
     * Tắt server (Ctrl+C, đóng cửa sổ, lệnh kill): lưu mọi người rồi chờ database ghi xong. Thread database là daemon nên
     * JVM không tự chờ.
     */
    static void shutdown() {
        synchronized (World.LOCK) {
            for (Player p : World.online.values()) if (p.conn != null) p.save();
        }
        Database.finish();
        System.out.println("Da luu nguoi choi, tat server");
    }

    /**
     * Đóng gói bảng Core trong MySQL thành template loại 3, đi chung luồng UPDATE_DATA với các template khác. Chưa đẩy lần
     * nào thì client dùng bản trong build.
     */
    static void loadTemplates() throws IOException {
        TemplateStore.Templates t = TemplateStore.loadTemplates();
        if (t == null) {
            System.out.println("Chua co bang tpl_*: client dung du lieu trong ban build");
            return;
        }
        DataServer.Entry e = new DataServer.Entry();
        e.type = TEMPLATE_CORE;
        e.version = t.version & 0xFF;   // phiên bản template chỉ có 1 byte trong gói
        e.size = t.data.length;
        e.file = "mysql:templates";
        DataServer.cache.put(e.file, t.data);
        DataServer.templates.put(e.type, e);
        System.out.println("Template tu MySQL: " + t.tables + " bang, " + t.rows + " hang, " + t.data.length + " byte, phien ban "
                + t.version);
    }
}
