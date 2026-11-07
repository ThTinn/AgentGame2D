package game.world;

import java.io.*;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;

/** Lệnh MOVE: người chơi đi lại, báo cho cả khu. */
public class Movement {
    /**
     * MOVE: vị trí (x100), hướng, action / sub / count / type của anim. Server tin vị trí client gửi. Người chết thì bỏ qua.
     * Sau khi báo cả khu, kiểm mục tiêu "tới nơi" của nhiệm vụ theo vị trí mới.
     */
    public static void move(Player p, DataInputStream r) throws IOException {
        int x = r.readInt(), y = r.readInt(), z = r.readInt(), dir = r.readInt();
        int action = r.readByte(), sub = r.readByte(), count = r.readByte(), type = r.readByte();
        if (p.dead) return;
        p.x = x;
        p.y = y;
        p.z = z;
        if (p.room == null) return;
        sendMove(p.room, p, dir, action, sub, count, type);
        if (Tables.ready && p.profile.story.quests.onPlayerAt(p.map, x / 100.0, z / 100.0)) {
            p.dirty |= Profile.PART_STORY;
            p.sendToasts();
        }
    }

    /** Gửi gói MOVE của p cho mọi người khác trong khu. Bot cũng đi qua đây. */
    public static void sendMove(GameMap map, Player p, int dir, int action, int sub, int count, int type) throws IOException {
        Packet out = new Packet();
        out.w.writeInt(p.id);
        out.vec(p.x, p.y, p.z);
        out.w.writeInt(dir);
        out.w.writeByte(action);
        out.w.writeByte(sub);
        out.w.writeByte(count);
        out.w.writeByte(type);
        for (Player o : map.players) if (o != p) o.post(Cmd.MOVE, out);
    }
}
