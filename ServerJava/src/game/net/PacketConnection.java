package game.net;

import java.io.*;
import java.net.*;

/**
 * Một kết nối ở mức khung gói: đọc gói đưa cho {@link #handle}, ghi gói bằng {@link #send}. Lớp con làm phần nội dung.
 * Khung gói: int đầu là 9 bit mã lệnh (có dấu) và 23 bit độ dài thân, rồi tới thân gói.
 */
public abstract class PacketConnection implements Runnable {
    static final int MAX_PACKET = 64 * 1024;    // gói client gửi lên rất nhỏ, không cấp phát theo số byte client tự khai
    static final int IDLE_MS = 60_000;          // client ping mỗi giây ngay từ lúc kết nối: im lặng từng này là kết nối treo

    public final Socket socket;
    final DataInputStream in;
    protected final DataOutputStream out;  // ghi khi giữ lock của chính nó

    protected PacketConnection(Socket socket) throws IOException {
        this.socket = socket;
        socket.setTcpNoDelay(true);
        socket.setSoTimeout(IDLE_MS);
        in = new DataInputStream(new BufferedInputStream(socket.getInputStream()));
        out = new DataOutputStream(new BufferedOutputStream(socket.getOutputStream()));
    }

    /** Thread đọc của kết nối: đọc từng gói cho tới khi socket đóng, lỗi, hoặc client gửi gói quá lớn. */
    @Override
    public void run() {
        System.out.println("Ket noi: " + socket.getRemoteSocketAddress());
        try {
            while (true) {
                int header = in.readInt();
                int cmd = header >> 23;
                int size = header & 0x7FFFFF;
                if (size > MAX_PACKET) {
                    System.out.println("Goi qua lon " + size + ": " + socket.getRemoteSocketAddress());
                    return;
                }
                byte[] body = new byte[size];
                in.readFully(body);
                handle(cmd, new DataInputStream(new ByteArrayInputStream(body)));
            }
        } catch (IOException e) {
            System.out.println("Ngat: " + socket.getRemoteSocketAddress());
        } finally {
            closed();
            closeQuietly(socket);
        }
    }

    /** Xử lý một gói client gửi lên. Chạy trên thread đọc của kết nối. */
    protected abstract void handle(int cmd, DataInputStream r) throws IOException;

    /** Kết nối vừa dứt, socket sắp đóng. */
    protected void closed() {
    }

    /**
     * Gửi một gói và flush ngay. Mọi gói của kết nối đều ghi khi giữ lock của {@link #out}: một khung gói không bị gói khác
     * chen vào giữa. Gói gửi cho cả khu dùng chung một thân, không chép lại.
     */
    public void send(int cmd, Packet p) throws IOException {
        synchronized (out) {
            write(cmd, p);
            out.flush();
        }
    }

    /** Ghi một khung gói vào bộ đệm, chưa flush. Phải giữ lock của {@link #out}: dùng để ghi nhiều gói rồi flush một lần. */
    protected void write(int cmd, Packet p) throws IOException {
        out.writeInt(((cmd & 0x1FF) << 23) | p.bytes.size());
        p.bytes.writeTo(out);
    }

    /** Đóng socket, bỏ qua lỗi (socket có thể đã đóng). */
    public static void closeQuietly(Socket socket) {
        try {
            socket.close();
        } catch (IOException ignored) {
        }
    }
}
