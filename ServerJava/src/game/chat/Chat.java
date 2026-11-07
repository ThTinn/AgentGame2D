package game.chat;

import java.io.*;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.world.GameMap;
import static game.world.World.*;

/** Chat map, thế giới và riêng. */
public class Chat {
    static final int MAX_CHAT = 300;

    /** Nội dung rỗng hoặc dài quá MAX_CHAT ký tự thì bỏ. */
    static boolean badChat(String content) {
        return content.isEmpty() || content.length() > MAX_CHAT;
    }

    /** Giờ hiện tại tính bằng giây (Unix), client hiện cạnh tin nhắn. */
    static int timestamp() {
        return (int) (System.currentTimeMillis() / 1000);
    }

    /** CHAT_MAP: gửi cho mọi người trong khu, kể cả người gửi. Bot cũng chat qua đây. */
    public static void chatMap(Player p, String content) throws IOException {
        GameMap map = p.room;
        if (map == null || badChat(content)) return;
        Packet out = new Packet();
        out.w.writeByte(1);         // cat 1: người chơi
        out.w.writeInt(p.id);
        out.utf(content);
        map.broadcast(Cmd.CHAT_MAP, out);
    }

    /** CHAT_WORLD: gửi cho mọi người đang online. */
    public static void chatWorld(Player p, String content) throws IOException {
        if (badChat(content)) return;
        Packet out = new Packet();
        out.w.writeInt(p.id);
        out.utf(p.name);
        out.w.writeShort(0);        // avatar
        out.utf(content);
        out.w.writeInt(timestamp());
        for (Player o : online.values()) o.post(Cmd.CHAT_WORLD, out);
    }

    /**
     * CHAT_PM: nội dung, byte 0, rồi id (int) hoặc tên (UTF) người nhận. Phân biệt bằng số byte còn lại như
     * ServerWorld.ChatPrivate. Gửi cho người nhận và gửi lại cho người gửi để hiện trong khung chat.
     */
    public static void chatPrivate(Player p, DataInputStream r) throws IOException {
        String content = Packet.readUtf(r);
        r.readByte();
        Player to = r.available() == 4 ? online.get(r.readInt()) : findByName(Packet.readUtf(r));
        if (badChat(content)) return;
        if (to == null) {
            p.conn.toast("Người nhận không online.");
            return;
        }
        Packet out = new Packet();
        out.w.writeInt(p.id);
        out.utf(content);
        out.w.writeInt(timestamp());
        to.post(Cmd.CHAT_PM, out);
        if (to != p) p.conn.post(Cmd.CHAT_PM, out);
    }

    /** Người đang online có tên này (không phân biệt hoa thường). Trùng tên thì lấy người sau cùng trong danh sách. */
    static Player findByName(String name) {
        Player found = null;
        for (Player o : online.values()) if (o.name.equalsIgnoreCase(name)) found = o;
        return found;
    }
}
