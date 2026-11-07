package game.player;

import java.io.*;
import java.util.*;
import game.GameConnection;
import game.db.AccountStore;
import game.net.Cmd;
import game.net.Packet;
import game.tables.Tables;
import game.world.GameMap;

/** Một người chơi đang online, bot cũng vậy nhưng không có kết nối. Luật của hồ sơ ở {@link Profile}. */
public class Player {
    public int id, animSet, map = -1, x, y, z;     // vị trí x100, map = -1 khi chưa vào map
    public int dirty;                              // các phần hồ sơ đã đổi, chờ gửi SYNC (Profile.PART_*)
    public String name;
    public int[] anim;
    public Profile profile;
    public GameConnection conn;    // bot: null
    public GameMap room;           // khu đang đứng, null khi chưa vào map
    public boolean dead;
    public double reviveAt, nextSync, nextSave, nextDeny, hpCarry, mpCarry;
    public long lastState;         // khóa của lần gửi PLAYER STATE gần nhất, xem PlayerPackets.sendState
    public final Map<Integer, Double> readyAt = new HashMap<>();   // skill -> lúc hết hồi chiêu

    /** Gửi gói cho người chơi này. Bot thì bỏ qua. */
    public void post(int cmd, Packet p) {
        if (conn != null) conn.post(cmd, p);
    }

    /** Ghi nền hồ sơ và vị trí vào MySQL. Chưa vào map thì không ghi (chưa có vị trí để lưu). */
    public void save() {
        if (map >= 0) AccountStore.savePlayer(id, profile.json(), map, x, z);
    }

    /** Gửi các thông báo tiến độ nhiệm vụ đang chờ rồi xóa. */
    public void sendToasts() throws IOException {
        if (conn != null) for (String t : profile.story.toasts) conn.toast(t);
        profile.story.toasts.clear();
    }

    /**
     * Gửi các phần hồ sơ vừa đổi (dirty) cộng các phần force về client: bản của server luôn ghi đè bản client đoán trước.
     * Không có gì để gửi, là bot, hoặc chưa có bảng tpl_* thì thôi.
     */
    public void flush(int force) throws IOException {
        int parts = dirty | force;
        if (parts == 0 || conn == null || !Tables.ready) return;
        dirty = 0;
        byte[] body = profile.sync(parts);
        Packet out = new Packet();
        out.w.writeByte(1);     // ProfileSub.SYNC
        out.w.writeInt(body.length);
        out.w.write(body);
        conn.post(Cmd.PROFILE, out);
    }
}
