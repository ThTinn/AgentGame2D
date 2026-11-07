package game.player;

import java.io.*;
import game.net.Cmd;
import game.net.Packet;
import static game.world.World.*;

/** Gói mô tả một người chơi cho người khác xem: tư thế, cấp, máu, bảng thông tin. */
public class PlayerPackets {
    static final int PLAYER_ANIM = 0, PLAYER_STATE = 1;

    /** Phần mô tả người chơi trong MAP_INFO / ADD_PLAYER: id, tên, máu, mana, vị trí. */
    public static void writePlayer(Packet w, Player o) throws IOException {
        w.w.writeInt(o.id);
        w.utf(o.name);
        w.w.writeLong(o.profile.hp);
        w.w.writeLong(o.profile.maxHp);
        w.w.writeLong(o.profile.mp);
        w.w.writeLong(o.profile.maxMp);
        w.vec(o.x, o.y, o.z);
    }

    /** PLAYER ANIM: bộ anim và các biến thể anim của người chơi. */
    public static Packet animPacket(Player o) throws IOException {
        Packet p = new Packet();
        p.w.writeByte(PLAYER_ANIM);
        p.w.writeInt(o.id);
        p.w.writeShort(o.animSet);
        p.w.writeShort(o.anim.length);
        for (int v : o.anim) p.w.writeShort(v);
        return p;
    }

    /** PLAYER STATE: cấp, máu, bang (chưa có). */
    public static Packet statePacket(Player o) throws IOException {
        Packet p = new Packet();
        p.w.writeByte(PLAYER_STATE);
        p.w.writeInt(o.id);
        p.w.writeShort(o.profile.level);
        p.w.writeLong(o.profile.hp);
        p.w.writeLong(o.profile.maxHp);
        p.w.writeInt(-1);   // chưa có bang
        p.utf("");
        return p;
    }

    /** Báo cấp, máu của p cho cả khu, chỉ khi khác lần gửi trước. WorldLoop gọi mỗi giây. */
    public static void sendState(Player p) throws IOException {
        Profile pr = p.profile;
        long key = pr.hp * 31 + pr.maxHp * 7 + pr.level;
        if (key == p.lastState) return;
        p.lastState = key;
        p.room.broadcast(Cmd.PLAYER, statePacket(p));
    }

    /** PLAYER_INFO: bảng thông tin của người chơi id (đang online) gửi cho p. */
    public static void info(Player p, int id) throws IOException {
        Player o = online.get(id);
        if (o == null) return;
        Packet out = new Packet();
        out.w.writeInt(o.id);
        out.utf(o.name);
        out.w.writeShort(0);        // avatar
        out.w.writeByte(o.conn == null ? 1 : 0);    // bot
        out.w.writeShort(o.profile.level);
        out.w.writeLong(o.profile.hp);
        out.w.writeLong(o.profile.maxHp);
        out.w.writeLong(o.profile.mp);
        out.w.writeLong(o.profile.maxMp);
        out.w.writeShort(o.map);
        out.w.writeShort(o.profile.build.weapon);
        out.w.writeByte(0);         // trang bị: làm cùng phần túi đồ
        p.conn.post(Cmd.PLAYER_INFO, out);
    }
}
