package game.item;

import java.io.*;
import java.util.*;
import game.build.Build;
import game.db.Json;
import game.player.Profile;
import game.tables.Tables;

/** Đồ đang mặc và vũ khí đang cầm. Chỉ gọi khi giữ World.LOCK. */
public class Equipment {
    final Profile me;
    public final ItemStack[] slots = new ItemStack[Tables.SLOT_COUNT + 1];     // chỉ số = EquipSlot, ô 0 không dùng

    public Equipment(Profile me) {
        this.me = me;
    }

    /**
     * Mặc lại đồ theo hồ sơ: vũ khí theo weaponId, giáp theo "equipped"; cường hóa của từng ô lấy ở "equipGear". Chưa có
     * túi lưu (saved = false) thì đồ đang mặc được rút khỏi đồ khởi đầu.
     */
    public void load(Map<String, Object> root, boolean saved) {
        List<Map<String, Object>> worn = Json.rows(root, "equipGear");
        Build b = me.build;
        if (!Tables.weapons.containsKey(b.weapon)) b.weapon = 0;
        if (b.weapon > 0) {
            Tables.Weapon w = Tables.weapons.get(b.weapon);
            b.basicId = w.basicSkill;
            wear(Tables.weaponItem(b.weapon), worn, !saved);
        }
        for (int id : Json.ints(root, "equipped")) {
            Tables.Item it = Tables.items.get(id);
            if (it != null && it.slot() != 0 && it.slot() != Tables.SLOT_WEAPON) wear(it, worn, !saved);
        }
    }

    /** Mặc it vào ô của nó, giữ cường hóa đã lưu của ô. fromBag: lấy một cái ra khỏi túi. */
    void wear(Tables.Item it, List<Map<String, Object>> worn, boolean fromBag) {
        if (it == null) return;
        if (fromBag) me.bag.remove(it.id, 1);
        int at = it.slot();
        ItemStack s = at < worn.size() ? new ItemStack(worn.get(at)) : new ItemStack(it.id, 1);
        s.itemId = it.id;
        s.count = 1;
        slots[at] = s;
    }

    /** Ghi các ô đồ mặc ("equipGear", đủ mọi ô) và danh sách id giáp đang mặc ("equipped", trừ vũ khí). */
    public void save(Map<String, Object> root) {
        List<Object> list = new ArrayList<>();
        List<Object> armor = new ArrayList<>();
        for (int i = 0; i <= Tables.SLOT_COUNT; i++) {
            list.add((slots[i] != null ? slots[i] : new ItemStack(0, 0)).json());
            if (i != Tables.SLOT_WEAPON && slots[i] != null) armor.add((long) slots[i].itemId);
        }
        root.put("equipGear", list);
        root.put("equipped", armor);
    }

    /** Phần đồ mặc trong gói SYNC: số ô, rồi ô 1..SLOT_COUNT (vật phẩm, cường hóa). */
    public void write(DataOutputStream w) throws IOException {
        w.writeByte(Tables.SLOT_COUNT);
        for (int i = 1; i <= Tables.SLOT_COUNT; i++) {
            w.writeShort(slots[i] != null ? slots[i].itemId : 0);
            ItemStack.writeGear(w, slots[i]);
        }
    }

    /** Món ở ô hành trang (spot >= 0) hoặc đang mặc (spot = -EquipSlot). null khi ô sai hoặc trống. */
    public ItemStack at(int spot) {
        if (spot >= 0) return spot < Inventory.BAG_SIZE ? me.bag.slots[spot] : null;
        return -spot <= Tables.SLOT_COUNT ? slots[-spot] : null;
    }

    /**
     * Mặc món ở ô túi slot. Món cũ cùng ô đồ về đúng ô túi đó nếu món mới chỉ có một cái, không thì vào ô trống; túi đầy
     * thì trả lại món mới và thôi. Đổi vũ khí thì đổi cả skill đánh thường. Trả false khi không mặc được.
     */
    public boolean equip(int slot) {
        if (slot < 0 || slot >= Inventory.BAG_SIZE || me.bag.slots[slot] == null) return false;
        ItemStack s = me.bag.slots[slot];
        Tables.Item it = Tables.items.get(s.itemId);
        if (it == null || it.slot() == 0) return false;
        int at = it.slot();
        ItemStack old = slots[at];
        ItemStack put = s.copy();
        put.count = 1;
        if (old != null && s.count == 1) {
            me.bag.slots[slot] = old.copy();
        } else {
            me.bag.take(slot, 1);
            if (old != null && !me.bag.addGearToBag(old)) {
                me.bag.add(put.itemId, 1);      // chỉ tới đây khi món mới là chồng nhiều cái, tức món trơn
                return false;
            }
        }
        slots[at] = put;
        if (at == Tables.SLOT_WEAPON) me.build.setWeapon(it.value);
        me.stats.recompute();
        return true;
    }

    /** Tháo món ở ô đồ slot về túi. false khi ô sai, trống, hoặc túi đầy. */
    public boolean unequip(int slot) {
        if (slot <= 0 || slot > Tables.SLOT_COUNT || slots[slot] == null || !me.bag.addGearToBag(slots[slot])) return false;
        slots[slot] = null;
        if (slot == Tables.SLOT_WEAPON) me.build.setWeapon(0);
        me.stats.recompute();
        return true;
    }
}
