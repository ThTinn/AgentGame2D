package game.account;

import java.net.InetAddress;
import java.util.*;
import game.world.World;

/**
 * Giới hạn theo IP để một máy không chiếm hết server: số kết nối cùng lúc, số lần đăng nhập, số tài khoản mới. Quán net
 * dùng chung một IP nên mức đặt rộng hơn một người chơi cần. Máy chạy cùng server (127.0.0.1) không bị giới hạn.
 */
public class LoginGuard {
    static final int MAX_CONNECTIONS = 20, MAX_LOGINS = 30, MAX_NEW_ACCOUNTS = 10;
    static final double LOGIN_WINDOW = 60, ACCOUNT_WINDOW = 3600;   // giây
    static final int PURGE_AT = 10_000;                             // số IP nhớ tối đa trước khi dọn khoảng đã hết

    /** Số lần trong một khoảng thời gian, hết khoảng thì đếm lại. */
    static class Window {
        double endAt;
        int count;
    }

    static final Map<InetAddress, Integer> connections = new HashMap<>();
    static final Map<InetAddress, Window> logins = new HashMap<>(), accounts = new HashMap<>();

    /** false: IP này đã đủ kết nối, không nhận. true thì phải gọi {@link #disconnect} khi kết nối đóng. */
    public static synchronized boolean connect(InetAddress ip) {
        if (ip.isLoopbackAddress()) return true;
        int n = connections.getOrDefault(ip, 0);
        if (n >= MAX_CONNECTIONS) return false;
        connections.put(ip, n + 1);
        return true;
    }

    /** Một kết nối của IP vừa đóng. */
    public static synchronized void disconnect(InetAddress ip) {
        connections.computeIfPresent(ip, (k, n) -> n > 1 ? n - 1 : null);
    }

    /** Tính một lần đăng nhập. false: IP này đăng nhập quá nhiều trong phút vừa qua. */
    static synchronized boolean login(InetAddress ip) {
        return take(logins, ip, MAX_LOGINS, LOGIN_WINDOW, World.now());
    }

    /** Tính một tài khoản mới. false: IP này tạo quá nhiều tài khoản trong giờ vừa qua. */
    static synchronized boolean newAccount(InetAddress ip) {
        return take(accounts, ip, MAX_NEW_ACCOUNTS, ACCOUNT_WINDOW, World.now());
    }

    /** Đếm thêm một lần cho ip trong cửa sổ seconds giây. true khi chưa vượt max. Bảng đầy thì dọn các IP đã hết cửa sổ. */
    static boolean take(Map<InetAddress, Window> map, InetAddress ip, int max, double seconds, double now) {
        if (ip.isLoopbackAddress()) return true;
        if (map.size() >= PURGE_AT) map.values().removeIf(w -> now >= w.endAt);
        Window w = map.computeIfAbsent(ip, k -> new Window());
        if (now >= w.endAt) {
            w.endAt = now + seconds;
            w.count = 0;
        }
        return ++w.count <= max;
    }
}
