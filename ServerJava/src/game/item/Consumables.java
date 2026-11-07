package game.item;

import java.io.*;
import java.util.*;
import game.db.Json;
import game.player.Profile;
import game.tables.Tables;

/** Bình, thuốc đang có tác dụng và ô dùng nhanh. Chỉ gọi khi giữ World.LOCK. */
public class Consumables {
    static final int QUICK_SLOTS = 2;
    static final double USE_COOLDOWN = 1;   // giây giữa hai lần dùng cùng nhóm
    static final double USE_SLACK = 0.1;    // gói tới lệch nhịp: cho dùng sớm chừng này giây

    final Profile me;
    final int[] quick = new int[QUICK_SLOTS];                               // id vật phẩm ở ô dùng nhanh, 0 là trống
    public final Map<Integer, Double> buffs = new LinkedHashMap<>();      // thuốc đang có tác dụng -> hết lúc
    final Map<Integer, Double> useReady = new HashMap<>();                // nhóm -> lúc dùng lại được

    public Consumables(Profile me) {
        this.me = me;
    }

    /** Nạp ô dùng nhanh từ hồ sơ. */
    public void load(Map<String, Object> root) {
        int[] saved = Json.ints(root, "quickItems");
        for (int i = 0; i < QUICK_SLOTS && i < saved.length; i++) quick[i] = saved[i];
    }

    /** Ghi ô dùng nhanh vào hồ sơ. */
    public void save(Map<String, Object> root) {
        List<Object> list = new ArrayList<>();
        for (int v : quick) list.add((long) v);
        root.put("quickItems", list);
    }

    /** Phần ô dùng nhanh trong gói SYNC. */
    public void write(DataOutputStream w) throws IOException {
        w.writeByte(QUICK_SLOTS);
        for (int v : quick) w.writeShort(v);
    }

    /** Đặt itemId vào ô dùng nhanh slot. Vật phẩm không dùng được thì làm trống ô. Đã có ở ô khác thì hai ô đổi chỗ cho nhau. */
    public void setQuick(int slot, int itemId) {
        if (slot < 0 || slot >= QUICK_SLOTS) return;
        Tables.Item it = Tables.items.get(itemId);
        if (it == null || !it.usable()) itemId = 0;
        int old = quick[slot];
        if (old == itemId) return;
        if (itemId > 0) for (int i = 0; i < QUICK_SLOTS; i++) if (i != slot && quick[i] == itemId) quick[i] = old;
        quick[slot] = itemId;
    }

    /**
     * Dùng một cái itemId từ túi. Bình máu / mana hồi theo option RESTORE_HP / RESTORE_MP (đang đầy thì không dùng). Thuốc
     * tăng chỉ số có tác dụng trong value giây. Hồi chiêu USE_COOLDOWN theo nhóm: mỗi loại thuốc một nhóm, bình máu một
     * nhóm, bình mana một nhóm. Trả false khi không dùng được.
     */
    public boolean useItem(int itemId, double now) {
        Tables.Item it = Tables.items.get(itemId);
        if (it == null || !it.usable() || me.dead || !me.bag.has(itemId, 1)) return false;
        int group = it.kind == Tables.KIND_BUFF ? it.id : -it.kind;
        if (now < useReady.getOrDefault(group, 0.0) - USE_SLACK) return false;
        switch (it.kind) {
            case Tables.KIND_HP_POTION:
                if (me.hp >= me.maxHp) return false;
                me.hp = Math.min(me.maxHp, me.hp + Math.round(it.option(Tables.RESTORE_HP)));
                break;
            case Tables.KIND_MP_POTION:
                if (me.mp >= me.maxMp) return false;
                me.mp = Math.min(me.maxMp, me.mp + Math.round(it.option(Tables.RESTORE_MP)));
                break;
            default:
                buffs.put(it.id, now + Math.max(0, it.value));
                me.stats.recompute();
                break;
        }
        me.bag.remove(itemId, 1);
        useReady.put(group, now + USE_COOLDOWN);
        return true;
    }

    /** Bỏ các thuốc đã hết tác dụng. true khi có thuốc vừa hết (chỉ số đã tính lại). */
    public boolean tickBuffs(double now) {
        if (buffs.isEmpty() || !buffs.values().removeIf(until -> now >= until)) return false;
        me.stats.recompute();
        return true;
    }

    /** Bỏ mọi thuốc đang có tác dụng (lúc chết). */
    public void clearBuffs() {
        if (buffs.isEmpty()) return;
        buffs.clear();
        me.stats.recompute();
    }
}
