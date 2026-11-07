package game.item;

import java.io.*;
import java.util.*;
import game.db.Json;

/** Một ô đồ: vật phẩm, số lượng, cấp cường hóa, lỗ và đá khảm. */
public class ItemStack {
    public static final int MAX_SOCKETS = 3;

    public int itemId, count, level, sockets;
    public final int[] gems = new int[MAX_SOCKETS];     // id đá khảm từng lỗ, 0 là lỗ trống

    ItemStack(int itemId, int count) {
        this.itemId = itemId;
        this.count = count;
    }

    /** Đọc từ JSON của hồ sơ (ItemStack của client: itemId, count, level, sockets, gem0..gem2). */
    ItemStack(Map<String, Object> m) {
        this((int) Json.num(m, "itemId"), (int) Json.num(m, "count"));
        level = (int) Json.num(m, "level");
        sockets = (int) Json.num(m, "sockets");
        for (int i = 0; i < MAX_SOCKETS; i++) gems[i] = (int) Json.num(m, "gem" + i);
    }

    /** Bản sao độc lập. */
    ItemStack copy() {
        ItemStack s = new ItemStack(itemId, count);
        s.level = level;
        s.sockets = sockets;
        System.arraycopy(gems, 0, s.gems, 0, MAX_SOCKETS);
        return s;
    }

    /** Ô trống (không có vật phẩm hoặc số lượng 0). */
    boolean empty() {
        return itemId <= 0 || count <= 0;
    }

    /** Chưa cường hóa, chưa khoét lỗ: gộp chồng được với món cùng loại. */
    boolean plain() {
        return level <= 0 && sockets <= 0;
    }

    /** JSON để lưu vào hồ sơ. Ô trống ghi itemId, count = 0. */
    Map<String, Object> json() {
        Map<String, Object> m = new LinkedHashMap<>();
        m.put("itemId", (long) (empty() ? 0 : itemId));
        m.put("count", (long) (empty() ? 0 : count));
        m.put("level", (long) level);
        m.put("sockets", (long) sockets);
        for (int i = 0; i < MAX_SOCKETS; i++) m.put("gem" + i, (long) gems[i]);
        return m;
    }

    /** Phần cường hóa trong gói SYNC: cấp, số lỗ, id đá từng lỗ. s null ghi như món trơn. */
    static void writeGear(DataOutputStream w, ItemStack s) throws IOException {
        w.writeByte(s != null ? s.level : 0);
        w.writeByte(s != null ? s.sockets : 0);
        if (s != null) for (int i = 0; i < s.sockets; i++) w.writeShort(s.gems[i]);
    }
}
