package game.data;

import java.io.*;
import java.net.*;
import java.util.*;
import java.util.concurrent.*;
import game.net.Cmd;
import game.net.Packet;
import game.net.PacketConnection;
import static game.data.DataServer.*;

/** Phần tải dữ liệu của một kết nối: CLIENT_INFO, UPDATE_DATA, REQUEST_DATA, PING. */
public class DataConnection extends PacketConnection {
    static final int DEVICE_ANDROID = 1, DEVICE_PC = 3;
    static final int CHUNK = 128 * 1024;    // bundle gửi theo khối: bundle map lớn hơn giới hạn 23 bit độ dài của một gói

    // bundle gửi ở thread riêng, lần lượt từng file, không chặn thread đọc gói
    final ExecutorService uploads = Executors.newSingleThreadExecutor();
    final Set<String> queued = ConcurrentHashMap.newKeySet();   // file đang chờ gửi hoặc gửi dở
    final Set<Integer> unknown = new HashSet<>();               // lệnh lạ đã báo, mỗi lệnh chỉ in một lần
    String platform;                                            // thư mục bundle theo thiết bị, null khi thiết bị lạ

    public DataConnection(Socket socket) throws IOException {
        super(socket);
    }

    /** Dừng thread gửi bundle khi kết nối đóng. */
    @Override
    protected void closed() {
        uploads.shutdownNow();
    }

    /** Chia gói tải dữ liệu. Lệnh khác in ra một lần rồi bỏ qua. */
    @Override
    protected void handle(int cmd, DataInputStream r) throws IOException {
        switch (cmd) {
            case Cmd.CLIENT_INFO:
                clientInfo(r.readByte());
                break;
            case Cmd.UPDATE_DATA:
                sendTemplate(r.readByte());
                break;
            case Cmd.REQUEST_DATA:
                requestBundle(r.readByte(), r.readShort());
                break;
            case Cmd.PING:
                Packet pong = new Packet();
                pong.w.writeLong(System.currentTimeMillis());
                send(Cmd.PING, pong);
                break;
            default:
                if (unknown.add(cmd)) System.out.println("Chua xu ly cmd " + cmd);
        }
    }

    /**
     * CLIENT_INFO: ghi nhớ nền tảng, trả CHECK_DATA_VERSION gồm phiên bản các template rồi danh sách bundle của nền tảng đó.
     * Thiết bị lạ hoặc không có bundle thì trả danh sách rỗng.
     */
    void clientInfo(int device) throws IOException {
        platform = device == DEVICE_ANDROID ? "Android" : device == DEVICE_PC ? "Windows" : null;
        List<Entry> files = platform == null ? null : bundles.get(platform);
        Packet p = new Packet();
        if (files == null) {
            p.w.writeByte(0);
            send(Cmd.CHECK_DATA_VERSION, p);
            return;
        }
        p.w.writeByte(templates.size());
        for (Entry t : templates.values()) {
            p.w.writeByte(t.type);
            p.w.writeByte(t.version);
        }
        p.w.writeShort(files.size());
        for (Entry f : files) {
            p.w.writeByte(f.type);
            p.w.writeShort(f.id);
            p.w.writeInt(f.version);
            p.w.writeInt(f.size);
        }
        send(Cmd.CHECK_DATA_VERSION, p);
    }

    /** UPDATE_DATA: gửi cả template loại type trong một gói. Không có thì không trả gì. */
    void sendTemplate(int type) throws IOException {
        Entry t = templates.get(type);
        if (t == null) return;
        Packet p = new Packet();
        p.w.writeByte(type);
        p.w.writeByte(t.version);
        p.w.write(load(t));
        send(Cmd.UPDATE_DATA, p);
    }

    /** REQUEST_DATA: xếp bundle vào hàng gửi. Client hỏi lại lúc file đang gửi dở thì bỏ qua. */
    void requestBundle(int type, int id) {
        String key = type + "/" + id;
        if (!queued.add(key)) return;
        uploads.execute(() -> {
            try {
                sendBundle(type, id);
            } catch (IOException e) {
                System.out.println("Loi gui " + key + ": " + e.getMessage());
            } finally {
                queued.remove(key);
            }
        });
    }

    /**
     * Gửi một bundle theo khối CHUNK byte: mỗi khối ghi type, id, tổng số byte, vị trí khối rồi dữ liệu. Không có file thì
     * gửi tổng số byte = -1. Chỉ tra trong manifest, không bao giờ ghép đường dẫn từ dữ liệu client gửi.
     */
    void sendBundle(int type, int id) throws IOException {
        Entry f = index.get(platform + "/" + type + "/" + id);
        if (f == null) {
            Packet p = new Packet();
            p.w.writeByte(type);
            p.w.writeShort(id);
            p.w.writeInt(-1);
            send(Cmd.REQUEST_DATA, p);
            return;
        }
        byte[] data = load(f);
        int offset = 0;
        do {
            int n = Math.min(CHUNK, data.length - offset);
            Packet p = new Packet();
            p.w.writeByte(type);
            p.w.writeShort(id);
            p.w.writeInt(data.length);
            p.w.writeInt(offset);
            p.w.write(data, offset, n);
            send(Cmd.REQUEST_DATA, p);
            offset += n;
        } while (offset < data.length);
    }
}
