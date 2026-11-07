package game.data;

import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;
import game.net.PacketConnection;

/**
 * Server chỉ có phần tải dữ liệu: đọc gói ServerData, trả CHECK_DATA_VERSION, UPDATE_DATA và REQUEST_DATA (xem
 * {@link DataConnection}). Không có đăng nhập hay luật chơi. GameServer dùng lại danh sách file ở đây.
 * Chạy bằng run.bat / run.sh, hoặc: java game.data.DataServer <thư mục ServerData> [cổng]
 */
public class DataServer {
    /** Một dòng của manifest.csv: template (theo type) hoặc bundle (theo nền tảng, type, id). */
    public static class Entry {
        public int type, id, version, size;
        public String file;     // đường dẫn tương đối trong ServerData, hoặc "mysql:..." với template dựng từ MySQL
    }

    public static Path root;
    public static final Map<Integer, Entry> templates = new TreeMap<>();            // type -> template
    public static final Map<String, List<Entry>> bundles = new HashMap<>();         // nền tảng -> danh sách bundle
    static final Map<String, Entry> index = new HashMap<>();                        // "nền tảng/type/id" -> bundle
    public static final Map<String, byte[]> cache = new ConcurrentHashMap<>();      // file -> nội dung đã đọc

    /** Điểm vào khi chạy riêng DataServer. */
    public static void main(String[] args) throws IOException, InterruptedException {
        root = Paths.get(args.length > 0 ? args[0] : "ServerData");
        int port = args.length > 1 ? Integer.parseInt(args[1]) : 18888;
        loadManifest();
        try (ServerSocket listener = new ServerSocket(port)) {
            System.out.println("Dang nghe cong " + port + ", " + templates.size() + " template, " + index.size() + " bundle");
            while (true) {
                Socket socket = accept(listener);
                if (socket == null) continue;
                try {
                    new Thread(new DataConnection(socket)).start();
                } catch (IOException e) {
                    // chỉ bỏ kết nối này, các khách sau vẫn vào được
                    System.out.println("Loi nhan ket noi: " + e.getMessage());
                    PacketConnection.closeQuietly(socket);
                }
            }
        }
    }

    /**
     * Nhận một kết nối. Lỗi accept (ví dụ hết file descriptor) thì chờ 1 giây rồi trả null để vòng ngoài thử lại, không quay
     * vòng 100% CPU. GameServer cũng dùng hàm này.
     */
    public static Socket accept(ServerSocket listener) throws InterruptedException {
        try {
            return listener.accept();
        } catch (IOException e) {
            System.out.println("Loi accept: " + e.getMessage());
            Thread.sleep(1000);
            return null;
        }
    }

    /** Đọc manifest.csv (cột: platform,type,id,version,size,file). Dòng platform = "template" là template, còn lại là bundle. */
    public static void loadManifest() throws IOException {
        List<String> lines = Files.readAllLines(root.resolve("manifest.csv"), StandardCharsets.UTF_8);
        for (String line : lines.subList(1, lines.size())) {
            if (line.startsWith("#") || line.trim().isEmpty()) continue;   // chú thích, dòng trống
            String[] cols = line.split(",", -1);
            if (cols.length < 6) continue;
            String platform = cols[0];
            Entry e = new Entry();
            e.type = Integer.parseInt(cols[1]);
            e.version = Integer.parseInt(cols[3]);
            e.size = Integer.parseInt(cols[4]);
            e.file = cols[5];
            if (platform.equals("template")) {
                templates.put(e.type, e);
            } else {
                e.id = Integer.parseInt(cols[2]);
                bundles.computeIfAbsent(platform, k -> new ArrayList<>()).add(e);
                index.put(platform + "/" + e.type + "/" + e.id, e);
            }
        }
    }

    /** Nội dung file của entry. Đọc đĩa một lần rồi dùng chung cho mọi kết nối. */
    static byte[] load(Entry e) throws IOException {
        byte[] data = cache.get(e.file);
        if (data == null) {
            data = Files.readAllBytes(root.resolve(e.file));
            cache.put(e.file, data);
        }
        return data;
    }
}
