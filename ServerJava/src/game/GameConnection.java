package game;

import java.io.*;
import java.net.*;
import java.util.*;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.concurrent.atomic.*;
import java.util.concurrent.locks.LockSupport;
import game.account.Login;
import game.account.LoginGuard;
import game.bot.Bots;
import game.build.BuildCommands;
import game.chat.Chat;
import game.combat.Casting;
import game.data.DataConnection;
import game.item.ItemCommands;
import game.loot.Loot;
import game.net.Cmd;
import game.net.Packet;
import game.player.Player;
import game.player.PlayerPackets;
import game.story.NpcCommands;
import game.story.QuestCommands;
import game.tables.Tables;
import game.world.MapObjects;
import game.world.Movement;
import game.world.World;
import game.world.Zones;

/**
 * Thêm phần game lên kết nối tải dữ liệu, và chia lệnh về module. Gói game gửi qua hàng đợi riêng của từng kết nối: một
 * client mạng chậm chỉ làm chậm chính nó, không giữ World.LOCK của cả server; đầy hàng đợi thì ngắt. {@link #post} chỉ thêm
 * vào hàng đợi, thread ghi của kết nối gom gói trong {@link #GATHER_NS} rồi ghi và flush một lần: gửi cho cả khu trong lúc
 * giữ lock không phải đánh thức từng thread.
 */
public class GameConnection extends DataConnection {
    static final int MAX_PENDING = 4096, MENU_ICON = 12652;
    static final long GATHER_NS = 10_000_000;      // đang có gói thì chờ 10 ms để gom thêm, rảnh thì gói đầu tiên đánh thức ngay
    static final AtomicInteger clients = new AtomicInteger();

    /** Một gói chờ gửi. */
    static class Outgoing {
        final int cmd;
        final Packet packet;

        Outgoing(int cmd, Packet packet) {
            this.cmd = cmd;
            this.packet = packet;
        }
    }

    static final Outgoing CLOSE = new Outgoing(0, null);   // gửi hết gói đứng trước rồi đóng

    final Queue<Outgoing> outbox = new ConcurrentLinkedQueue<>();
    final AtomicInteger pending = new AtomicInteger();  // số gói chờ gửi, quá MAX_PENDING thì ngắt
    final AtomicBoolean idle = new AtomicBoolean();     // thread ghi đang ngủ chờ gói
    final Thread writer = new Thread(this::writeLoop, "out");
    volatile boolean closed;
    public Player player;           // null khi chưa đăng nhập
    public int badLogins;           // số lần sai mật khẩu trên kết nối này

    GameConnection(Socket socket) throws IOException {
        super(socket);
        writer.setDaemon(true);
    }

    /** Thread đọc của kết nối. Kết nối dứt thì rời game (lưu hồ sơ), dừng thread ghi, trả suất kết nối của IP. */
    @Override
    public void run() {
        writer.start();
        try {
            super.run();
        } finally {
            synchronized (World.LOCK) {
                try {
                    if (player != null) drop(player);
                } catch (IOException e) {
                    System.out.println("Loi roi map: " + e.getMessage());
                }
            }
            closed = true;
            LockSupport.unpark(writer);
            clients.decrementAndGet();
            LoginGuard.disconnect(socket.getInetAddress());
        }
    }

    /** Xếp một gói vào hàng gửi. Không chặn, gọi được khi giữ World.LOCK. Hàng quá MAX_PENDING gói (client không đọc) thì ngắt. */
    public void post(int cmd, Packet p) {
        if (closed) return;
        if (pending.incrementAndGet() > MAX_PENDING) {
            closeQuietly(socket);
            pending.decrementAndGet();
            return;
        }
        enqueue(new Outgoing(cmd, p));
    }

    /** Thêm vào hàng, thread ghi đang ngủ thì đánh thức. */
    void enqueue(Outgoing o) {
        outbox.add(o);
        if (idle.get() && idle.compareAndSet(true, false)) LockSupport.unpark(writer);
    }

    /** Thread ghi: còn gói thì ghi hết, flush một lần rồi chờ GATHER_NS cho gói kế; hết gói thì ngủ tới khi enqueue đánh thức. */
    void writeLoop() {
        try {
            while (!closed) {
                if (writeAll()) {
                    LockSupport.parkNanos(GATHER_NS);
                    continue;
                }
                idle.set(true);
                if (outbox.isEmpty() && !closed) LockSupport.park();    // enqueue chen giữa thì unpark đã để sẵn quyền, park trả về ngay
                idle.set(false);
            }
        } catch (IOException e) {
            closeQuietly(socket);
        }
    }

    /** Ghi mọi gói đang chờ rồi flush một lần. Gặp CLOSE thì đóng socket sau các gói đứng trước. false khi hàng đợi trống. */
    boolean writeAll() throws IOException {
        Outgoing o = outbox.poll();
        if (o == null) return false;
        int n = 0;
        synchronized (out) {
            for (; o != null && o != CLOSE; o = outbox.poll()) {
                write(o.cmd, o.packet);
                n++;
            }
            out.flush();
        }
        pending.addAndGet(-n);
        if (o == CLOSE) {
            closed = true;
            closeQuietly(socket);
        }
        return true;
    }

    /** Thông báo nhỏ (NOTIFY TOAST) hiện 2.5 giây. */
    public void toast(String text) throws IOException {
        Packet p = new Packet();
        p.w.writeByte(0);       // NotifySub.TOAST
        p.utf(text);
        p.w.writeShort(25);     // phần mười giây
        post(Cmd.NOTIFY, p);
    }

    /** Hộp thông báo (MENU) có một nút OK. */
    public void menu(String text) throws IOException {
        Packet p = new Packet();
        p.w.writeShort(MENU_ICON);
        p.utf(text);
        p.w.writeByte(1);
        p.utf("OK");
        post(Cmd.MENU, p);
    }

    /** Gửi hết gói đang chờ rồi mới đóng kết nối. */
    public void closeAfterSend() {
        if (closed) closeQuietly(socket);
        else enqueue(CLOSE);
    }

    /** Đưa người chơi ra khỏi game: lưu hồ sơ, rời map, bỏ khỏi danh sách online. Gọi khi giữ World.LOCK. */
    public void drop(Player p) throws IOException {
        p.save();
        World.drops++;
        Zones.leaveMap(p);
        World.online.remove(p.id);
        player = null;
        System.out.println(p.name + " roi server, con " + (World.online.size() - Bots.bots.size()) + " nguoi");
    }

    /** Chia lệnh của phần game về module. true khi đã xử lý; chưa đăng nhập hoặc lệnh khác thì false để đi tiếp xuống DataConnection. */
    boolean game(int cmd, DataInputStream r) throws IOException {
        Player p = player;
        if (p == null) return false;
        switch (cmd) {
            case Cmd.MOVE:
                Movement.move(p, r);
                return true;
            case Cmd.CHAT_MAP:
                Chat.chatMap(p, Packet.readUtf(r));
                return true;
            case Cmd.CHAT_WORLD:
                Chat.chatWorld(p, Packet.readUtf(r));
                return true;
            case Cmd.CHAT_PM:
                Chat.chatPrivate(p, r);
                return true;
            case Cmd.PLAYER_INFO:
                PlayerPackets.info(p, r.readInt());
                return true;
            case Cmd.MAP:
                Zones.command(p, r);
                return true;
            case Cmd.MAP_OBJECT:
                MapObjects.command(p, r);
                return true;
            case Cmd.ATTACK:
                Casting.attack(p, r);
                return true;
            case Cmd.GROUND_ITEM:
                if (Tables.ready) Loot.pick(p, r);
                return true;
            case Cmd.ITEM:
                if (Tables.ready) ItemCommands.item(p, r);
                return true;
            case Cmd.BUILD:
                if (Tables.ready) BuildCommands.build(p, r);
                return true;
            case Cmd.NPC:
                if (Tables.ready) NpcCommands.npc(p, r);
                return true;
            case Cmd.QUEST:
                if (Tables.ready) QuestCommands.quest(p, r);
                return true;
            default:
                return false;
        }
    }

    /** LOGIN chạy ngoài World.LOCK (băm mật khẩu, đọc MySQL). Lệnh game chạy khi giữ lock. Còn lại là lệnh tải dữ liệu. */
    @Override
    protected void handle(int cmd, DataInputStream r) throws IOException {
        if (cmd == Cmd.LOGIN) {
            Login.login(this, r);
            return;
        }
        synchronized (World.LOCK) {
            if (game(cmd, r)) return;
        }
        super.handle(cmd, r);
    }
}
