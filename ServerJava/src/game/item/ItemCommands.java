package game.item;

import java.io.*;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.world.World;

/** Lệnh ITEM: client làm trước tại máy rồi gửi, server áp lên hồ sơ rồi luôn gửi lại bản chuẩn, kể cả khi từ chối. */
public class ItemCommands {
    static final int ITEM_USE = 0, ITEM_EQUIP = 1, ITEM_UNEQUIP = 2, ITEM_QUICK = 3, ITEM_SORT = 4, ITEM_DISCARD = 5;
    static final int ITEM_UPGRADE = 6, ITEM_UPGRADE_RESULT = 7, ITEM_SOCKET_OPEN = 8, ITEM_SOCKET_RESULT = 9;
    static final int ITEM_GEM_SET = 10, ITEM_GEM_REMOVE = 11;
    static final int BAG = Profile.PART_BAG, STATS = Profile.PART_STATS;

    /** Chia lệnh ITEM theo sub. Định dạng từng sub ở ItemSub của Cmd_Message.cs. */
    public static void item(Player p, DataInputStream r) throws IOException {
        Profile pr = p.profile;
        int sub = r.readUnsignedByte();
        switch (sub) {
            case ITEM_USE:
                pr.consumables.useItem(r.readShort(), World.now());
                p.flush(BAG | STATS);
                break;
            case ITEM_EQUIP:
                pr.gear.equip(r.readShort());
                p.flush(BAG | STATS);
                break;
            case ITEM_UNEQUIP:
                pr.gear.unequip(r.readUnsignedByte());
                p.flush(BAG | STATS);
                break;
            case ITEM_QUICK:
                pr.consumables.setQuick(r.readUnsignedByte(), r.readShort());
                p.flush(BAG);
                break;
            case ITEM_SORT:
                pr.bag.sort();
                p.flush(BAG);
                break;
            case ITEM_DISCARD:
                pr.bag.discard(r.readShort(), r.readShort());
                p.flush(BAG);
                break;
            case ITEM_UPGRADE:
                upgrade(p, r);
                break;
            case ITEM_SOCKET_OPEN:
                openSocket(p, r);
                break;
            case ITEM_GEM_SET:
                pr.forge.inlayGem(r.readShort(), r.readUnsignedByte(), r.readShort());
                p.flush(BAG | STATS);
                break;
            case ITEM_GEM_REMOVE:
                pr.forge.removeGem(r.readShort(), r.readUnsignedByte());
                p.flush(BAG | STATS);
                break;
            default:
                break;
        }
    }

    /** ITEM UPGRADE (spot, đá may mắn, đá bảo hộ): quay tỉ lệ, gửi hồ sơ rồi kết quả kèm cấp mới. */
    static void upgrade(Player p, DataInputStream r) throws IOException {
        int spot = r.readShort();
        boolean lucky = r.readBoolean(), protect = r.readBoolean();
        int outcome = p.profile.forge.upgradeGear(spot, lucky, protect);
        p.flush(BAG | STATS);
        ItemStack g = p.profile.gear.at(spot);
        gearResult(p, ITEM_UPGRADE_RESULT, outcome, g != null ? g.level : 0);
    }

    /** ITEM SOCKET_OPEN (spot): quay tỉ lệ, gửi hồ sơ rồi kết quả kèm số lỗ mới. */
    static void openSocket(Player p, DataInputStream r) throws IOException {
        int spot = r.readShort();
        int outcome = p.profile.forge.openSocket(spot);
        p.flush(BAG | STATS);
        ItemStack g = p.profile.gear.at(spot);
        gearResult(p, ITEM_SOCKET_RESULT, outcome, g != null ? g.sockets : 0);
    }

    /** Gói kết quả cường hóa / khoét lỗ: GearOutcome và giá trị mới (cấp hoặc số lỗ). */
    static void gearResult(Player p, int sub, int outcome, int value) throws IOException {
        Packet out = new Packet();
        out.w.writeByte(sub);
        out.w.writeByte(outcome);
        out.w.writeByte(value);
        p.conn.post(Cmd.ITEM, out);
    }
}
