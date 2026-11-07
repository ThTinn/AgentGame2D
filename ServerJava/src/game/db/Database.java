package game.db;

import java.io.*;
import java.nio.file.*;
import java.sql.*;
import java.util.*;
import java.util.concurrent.*;

/**
 * Kết nối MySQL dùng chung. Mọi truy vấn chạy lần lượt trên một thread giữ một kết nối: đăng nhập chờ kết quả, ghi nền thì
 * không. Nhờ chạy lần lượt, thoát rồi vào lại luôn đọc được vị trí vừa ghi. Truy vấn nằm ở {@link AccountStore} và
 * {@link TemplateStore}.
 */
public class Database {
    /** Lỗi database, phân biệt với lỗi socket của người chơi: báo bảo trì, không ngắt kết nối. */
    public static class Failure extends IOException {
        private static final long serialVersionUID = 1L;

        Failure(Throwable cause) {
            super(cause.getMessage(), cause);
        }
    }

    /** Một việc chạy trên thread database. */
    interface Query<T> {
        T run() throws SQLException;
    }

    static final int WAIT_SECONDS = 15, VALID_SECONDS = 2;

    static final ExecutorService worker = Executors.newSingleThreadExecutor(r -> {
        Thread t = new Thread(r, "database");
        t.setDaemon(true);
        return t;
    });

    static String url, user, password;
    static Connection connection;   // chỉ thread worker dùng

    /** Đọc cấu hình kết nối (url, user, password) từ file properties. Chưa kết nối ở đây. */
    public static void open(Path config) throws IOException {
        Properties p = new Properties();
        try (InputStream in = Files.newInputStream(config)) {
            p.load(in);
        }
        url = p.getProperty("url");
        user = p.getProperty("user");
        password = p.getProperty("password", "");
        if (url == null || user == null) throw new IOException(config + " thieu url hoac user");
    }

    /**
     * Kết nối đang dùng, mở lại nếu đã hỏng. MySQL đóng kết nối rảnh sau wait_timeout (mặc định 8 giờ), mạng cũng có thể
     * đứt nên kiểm trước mỗi lần dùng. Chỉ gọi trên thread worker.
     */
    static Connection connection() throws SQLException {
        if (connection == null || !connection.isValid(VALID_SECONDS)) {
            if (connection != null) {
                try {
                    connection.close();
                } catch (SQLException ignored) {
                }
            }
            connection = DriverManager.getConnection(url, user, password);
        }
        return connection;
    }

    /** Chạy query trên thread database và chờ kết quả, tối đa WAIT_SECONDS giây. Mọi lỗi đổi thành {@link Failure}. */
    static <T> T call(Query<T> query) throws Failure {
        try {
            return worker.submit(query::run).get(WAIT_SECONDS, TimeUnit.SECONDS);
        } catch (ExecutionException e) {
            throw new Failure(e.getCause());
        } catch (TimeoutException | InterruptedException | RejectedExecutionException e) {
            throw new Failure(e);
        }
    }

    /** Ghi nền: xếp query vào hàng rồi đi tiếp, người chơi không phải chờ. Lỗi chỉ mất lần ghi này. */
    static void later(Query<?> query) {
        try {
            worker.execute(() -> {
                try {
                    query.run();
                } catch (SQLException e) {
                    System.out.println("Loi ghi database: " + e.getMessage());
                }
            });
        } catch (RejectedExecutionException e) {
            System.out.println("Bo qua ghi database: server dang tat");
        }
    }

    /** Ghi nốt hàng đợi rồi dừng, dùng lúc tắt server. */
    public static void finish() {
        worker.shutdown();
        try {
            if (!worker.awaitTermination(WAIT_SECONDS, TimeUnit.SECONDS)) System.out.println("Chua ghi het database khi tat server");
        } catch (InterruptedException e) {
            System.out.println("Bi ngat khi cho ghi database");
        }
    }
}
