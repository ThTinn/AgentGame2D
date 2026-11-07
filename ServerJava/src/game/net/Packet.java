package game.net;

import java.io.*;
import java.nio.charset.StandardCharsets;

/**
 * Thân một gói đang ghi, kèm các hàm đọc / ghi chung của giao thức. Đã gửi thì không ghi thêm: một gói có thể đi tới nhiều
 * kết nối cùng lúc.
 */
public class Packet {
    public final ByteArrayOutputStream bytes = new ByteArrayOutputStream();
    public final DataOutputStream w = new DataOutputStream(bytes);

    /** Đọc chuỗi UTF-8 có độ dài short đứng trước. Độ dài 0 hoặc âm là chuỗi rỗng. */
    public static String readUtf(DataInputStream r) throws IOException {
        int length = r.readShort();
        if (length <= 0) return "";
        byte[] raw = new byte[length];
        r.readFully(raw);
        return new String(raw, StandardCharsets.UTF_8);
    }

    /**
     * Ghi chuỗi UTF-8 có độ dài short đứng trước. Không dùng writeUTF của Java: đó là UTF-8 biến thể, client đọc UTF-8
     * chuẩn.
     */
    public static void utf(DataOutputStream w, String s) throws IOException {
        byte[] raw = s.getBytes(StandardCharsets.UTF_8);
        w.writeShort(raw.length);
        w.write(raw);
    }

    /** Ghi chuỗi vào gói này, xem {@link #utf(DataOutputStream, String)}. */
    public void utf(String s) throws IOException {
        utf(w, s);
    }

    /** Ghi một vị trí: ba số nguyên x, y, z (đơn vị cm). */
    public void vec(int x, int y, int z) throws IOException {
        w.writeInt(x);
        w.writeInt(y);
        w.writeInt(z);
    }

    /** Mét sang số nguyên x100 của gói. */
    public static int cm(double metres) {
        return (int) Math.round(metres * 100);
    }
}
