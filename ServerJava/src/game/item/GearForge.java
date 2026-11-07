package game.item;

import java.util.concurrent.ThreadLocalRandom;
import game.player.Profile;
import game.tables.Tables;

/**
 * Cường hóa, khoét lỗ, khảm đá. Tỉ lệ quay ở server, kết quả gửi về bằng ITEM_UPGRADE_RESULT / ITEM_SOCKET_RESULT. Món
 * được chỉ bằng spot: ô túi (>= 0) hoặc ô đồ mặc (-EquipSlot), xem {@link Equipment#at}.
 */
public class GearForge {
    static final int REFUSED = 0, SUCCESS = 2, FAILED = 3, DROPPED = 4, PROTECTED = 5;     // GearOutcome

    final Profile me;

    public GearForge(Profile me) {
        this.me = me;
    }

    /** Có đủ amount viên đá loại stone (STONE_*) không. */
    boolean hasStone(int stone, int amount) {
        Tables.Item it = Tables.stone(stone);
        return it != null && me.bag.has(it.id, amount);
    }

    /** Trừ amount viên đá loại stone. */
    void spendStone(int stone, int amount) {
        me.bag.remove(Tables.stone(stone).id, amount);
    }

    /** Món ở spot cường hóa / khoét lỗ được không. */
    ItemStack upgradableAt(int spot) {
        ItemStack g = me.gear.at(spot);
        Tables.Item it = g != null ? Tables.items.get(g.itemId) : null;
        return it != null && it.upgradable() ? g : null;
    }

    /**
     * Cường hóa món ở spot lên một cấp, theo mốc của cấp hiện tại: tốn đá cường hóa và vàng, đá may mắn cộng tỉ lệ. Hỏng
     * ở mốc có rớt cấp thì rớt một cấp, trừ khi dùng đá bảo hộ (khi đó tốn đá bảo hộ, giữ cấp). Trả GearOutcome.
     */
    public int upgradeGear(int spot, boolean lucky, boolean protect) {
        ItemStack g = upgradableAt(spot);
        if (g == null || g.level >= Tables.gearSteps.size()) return REFUSED;
        Tables.Step step = Tables.gearSteps.get(g.level);
        if (!canPayUpgrade(step, lucky, protect)) return REFUSED;

        spendStone(Tables.STONE_UPGRADE, step.stones);
        if (lucky) spendStone(Tables.STONE_LUCKY, 1);
        me.gold -= step.gold;
        int outcome = FAILED;
        double chance = Math.max(0, Math.min(100, step.chance + (lucky ? Tables.luckyBonus : 0)));
        if (ThreadLocalRandom.current().nextDouble() * 100 < chance) {
            outcome = SUCCESS;
            g.level++;
        } else if (step.dropOnFail && g.level > 0) {
            if (protect) {
                spendStone(Tables.STONE_PROTECT, step.protectStones);
                outcome = PROTECTED;
            } else {
                outcome = DROPPED;
                g.level--;
            }
        }
        me.stats.recompute();
        return outcome;
    }

    /** Đủ đá cường hóa, đá may mắn (nếu chọn), đá bảo hộ (nếu chọn và mốc có rớt cấp), và vàng cho mốc này. */
    boolean canPayUpgrade(Tables.Step step, boolean lucky, boolean protect) {
        if (!hasStone(Tables.STONE_UPGRADE, step.stones)) return false;
        if (lucky && !hasStone(Tables.STONE_LUCKY, 1)) return false;
        if (protect && step.dropOnFail && !hasStone(Tables.STONE_PROTECT, step.protectStones)) return false;
        return me.gold >= step.gold;
    }

    /** Khoét thêm một lỗ cho món ở spot: tốn đá khoan và vàng theo mốc của số lỗ hiện có. Trả GearOutcome. */
    public int openSocket(int spot) {
        ItemStack g = upgradableAt(spot);
        if (g == null || g.sockets >= Tables.socketSteps.size()) return REFUSED;
        Tables.Step step = Tables.socketSteps.get(g.sockets);
        if (!hasStone(Tables.STONE_DRILL, step.stones) || me.gold < step.gold) return REFUSED;
        spendStone(Tables.STONE_DRILL, step.stones);
        me.gold -= step.gold;
        if (ThreadLocalRandom.current().nextDouble() * 100 >= step.chance) return FAILED;
        g.sockets++;
        return SUCCESS;
    }

    /** Khảm đá gemId (lấy từ túi) vào lỗ trống index của món ở spot. Không khảm hai viên cùng loại trên một món. */
    public boolean inlayGem(int spot, int index, int gemId) {
        ItemStack g = upgradableAt(spot);
        Tables.Item gem = Tables.items.get(gemId);
        if (g == null || index < 0 || index >= g.sockets || g.gems[index] > 0) return false;
        if (gem == null || gem.kind != Tables.KIND_JEWEL || !me.bag.has(gemId, 1)) return false;
        for (int i = 0; i < g.sockets; i++) if (gem.sameJewel(Tables.items.get(g.gems[i]))) return false;
        me.bag.remove(gemId, 1);
        g.gems[index] = gemId;
        me.stats.recompute();
        return true;
    }

    /** Gỡ đá ở lỗ index của món ở spot về túi, tốn removeGemGold vàng. false khi không đủ vàng hoặc túi đầy. */
    public boolean removeGem(int spot, int index) {
        ItemStack g = me.gear.at(spot);
        if (g == null || index < 0 || index >= g.sockets || g.gems[index] <= 0 || me.gold < Tables.removeGemGold) return false;
        if (me.bag.add(g.gems[index], 1) > 0) return false;
        me.gold -= Tables.removeGemGold;
        g.gems[index] = 0;
        me.stats.recompute();
        return true;
    }
}
