package game.item;

import java.io.*;
import java.util.*;
import game.db.Json;
import game.tables.Tables;

/** Hành trang, port Inventory của client. Chỉ gọi khi giữ World.LOCK. */
public class Inventory {
    public static final int BAG_SIZE = 48;

    public final ItemStack[] slots = new ItemStack[BAG_SIZE];      // null là ô trống

    // ---------------- lưu, gửi ----------------

    /**
     * Nạp túi từ hồ sơ, trả true khi có bản lưu. Như CoreSystems.StartOnline: chưa có thì lấy đồ khởi đầu, đồ đang mặc
     * được Equipment rút ra sau.
     */
    public boolean load(Map<String, Object> root) {
        boolean saved = Json.bool(root, "bagSaved");
        if (saved) {
            List<Map<String, Object>> rows = Json.rows(root, "bag");
            for (int i = 0; i < BAG_SIZE && i < rows.size(); i++) {
                ItemStack s = new ItemStack(rows.get(i));
                if (!s.empty()) slots[i] = s;
            }
        } else {
            for (Map<String, Object> r : Json.rows(root, "startItems")) add((int) Json.num(r, "itemId"), (int) Json.num(r, "amount"));
        }
        return saved;
    }

    /** Ghi cả túi (đủ BAG_SIZE ô) vào hồ sơ và đánh dấu đã có bản lưu. */
    public void save(Map<String, Object> root) {
        List<Object> list = new ArrayList<>();
        for (int i = 0; i < BAG_SIZE; i++) list.add((slots[i] != null ? slots[i] : new ItemStack(0, 0)).json());
        root.put("bag", list);
        root.put("bagSaved", Boolean.TRUE);
    }

    /** Phần túi trong gói SYNC: số ô, rồi từng ô (vật phẩm, số lượng, cường hóa). */
    public void write(DataOutputStream w) throws IOException {
        w.writeShort(BAG_SIZE);
        for (ItemStack s : slots) {
            w.writeShort(s != null ? s.itemId : 0);
            w.writeShort(s != null ? s.count : 0);
            ItemStack.writeGear(w, s);
        }
    }

    // ---------------- đếm, thêm, bớt ----------------

    /** Tổng số cái của itemId trong túi. */
    public int count(int itemId) {
        int n = 0;
        for (ItemStack s : slots) if (s != null && s.itemId == itemId) n += s.count;
        return n;
    }

    /** Có đủ amount cái itemId không (amount <= 0 luôn đủ). */
    boolean has(int itemId, int amount) {
        return amount <= 0 || count(itemId) >= amount;
    }

    /** Số tối đa một ô của itemId; vật phẩm lạ thì không giới hạn. */
    static int stackLimit(int itemId) {
        Tables.Item it = Tables.items.get(itemId);
        return it != null ? it.stackLimit() : Integer.MAX_VALUE;
    }

    /** Thêm amount cái itemId: dồn vào các ô cùng loại chưa đầy trước, rồi tới ô trống. Trả số cái không bỏ vào được vì hết ô. */
    public int add(int itemId, int amount) {
        if (itemId <= 0 || amount <= 0) return 0;
        int limit = stackLimit(itemId), left = amount;
        if (limit > 1)
            for (int i = 0; i < BAG_SIZE && left > 0; i++) {
                if (slots[i] == null || slots[i].itemId != itemId || slots[i].count >= limit) continue;
                int put = Math.min(left, limit - slots[i].count);
                slots[i].count += put;
                left -= put;
            }
        for (int i = 0; i < BAG_SIZE && left > 0; i++) {
            if (slots[i] != null) continue;
            int put = Math.min(left, limit);
            slots[i] = new ItemStack(itemId, put);
            left -= put;
        }
        return left;
    }

    /** Bỏ một món (giữ cường hóa, khảm) vào ô trống đầu tiên. false khi túi đầy. */
    boolean addGearToBag(ItemStack g) {
        for (int i = 0; i < BAG_SIZE; i++) {
            if (slots[i] != null) continue;
            slots[i] = g.copy();
            slots[i].count = 1;
            return true;
        }
        return false;
    }

    /** Bớt amount cái ở ô slot, hết thì ô thành trống. Không kiểm tra, người gọi phải chắc ô đủ. */
    void take(int slot, int amount) {
        slots[slot].count -= amount;
        if (slots[slot].count <= 0) slots[slot] = null;
    }

    /** Bỏ amount cái itemId, lấy từ cuối túi, món trơn trước rồi mới tới món đã cường hóa. false (không bỏ gì) khi không đủ. */
    public boolean remove(int itemId, int amount) {
        if (amount <= 0) return true;
        if (!has(itemId, amount)) return false;
        int left = amount;
        for (int pass = 0; pass < 2 && left > 0; pass++)
            for (int i = BAG_SIZE - 1; i >= 0 && left > 0; i--) {
                if (slots[i] == null || slots[i].itemId != itemId) continue;
                if (pass == 0 && !slots[i].plain()) continue;
                int n = Math.min(left, slots[i].count);
                take(i, n);
                left -= n;
            }
        return true;
    }

    /** Vứt amount cái ở ô slot. false khi ô sai hoặc không đủ. */
    public boolean discard(int slot, int amount) {
        if (slot < 0 || slot >= BAG_SIZE || amount <= 0 || slots[slot] == null || slots[slot].count < amount) return false;
        take(slot, amount);
        return true;
    }

    // ---------------- sắp xếp ----------------

    /**
     * Sắp túi: gộp các món trơn cùng loại thành chồng đầy, rồi xếp theo {@link #compare}. Gộp xong mà không vừa túi thì giữ
     * nguyên.
     */
    public void sort() {
        Map<Integer, Integer> counts = new HashMap<>();
        List<ItemStack> all = new ArrayList<>();
        for (ItemStack s : slots) {
            if (s == null) continue;
            if (s.plain()) counts.merge(s.itemId, s.count, Integer::sum);
            else all.add(s);
        }
        for (Map.Entry<Integer, Integer> e : counts.entrySet()) {
            int limit = stackLimit(e.getKey());
            for (int left = e.getValue(); left > 0; left -= limit) all.add(new ItemStack(e.getKey(), Math.min(left, limit)));
        }
        if (all.size() > BAG_SIZE) return;
        all.sort(Inventory::compare);
        for (int i = 0; i < BAG_SIZE; i++) slots[i] = i < all.size() ? all.get(i) : null;
    }

    /** Thứ tự sắp: loại lớn trước, hiếm trước, id nhỏ trước, cấp cao trước, nhiều lỗ trước, chồng nhiều trước. */
    static int compare(ItemStack a, ItemStack b) {
        Tables.Item x = Tables.items.get(a.itemId), y = Tables.items.get(b.itemId);
        if (x != null && y != null) {
            if (x.kind != y.kind) return Integer.compare(y.kind, x.kind);
            if (x.rarity != y.rarity) return Integer.compare(y.rarity, x.rarity);
        }
        if (a.itemId != b.itemId) return Integer.compare(a.itemId, b.itemId);
        if (a.level != b.level) return Integer.compare(b.level, a.level);
        if (a.sockets != b.sockets) return Integer.compare(b.sockets, a.sockets);
        return Integer.compare(b.count, a.count);
    }
}
