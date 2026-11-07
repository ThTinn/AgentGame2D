package game.bot;

import java.io.*;
import java.util.*;
import java.util.concurrent.ThreadLocalRandom;
import game.account.Account;
import game.account.Accounts;
import game.chat.Chat;
import game.player.Player;
import game.world.GameMap;
import game.world.Movement;
import game.world.Zones;
import static game.world.World.*;

/**
 * Người chơi ảo do server lái, đứng trong map và online như người thật. Không lưu vị trí. Chạy trên thread "bots" riêng,
 * mỗi nhịp giữ World.LOCK.
 */
public class Bots {
    static final int BOT_TICK_MS = 120, BOT_SEND_TICKS = 6;         // ServerBots.BotSendInterval, BotSendTicks
    static final int BOT_SPEED = 450, BOT_WANDER = 1200, BOT_ARRIVE = 30, BOT_SPAWN = 400;  // LocalPlaySettings, ServerBots x100
    static final int STATE_IDLE = 0, STATE_MOVE = 1, ACTION_IDLE = 0, ACTION_RUN = 2;
    static final String[] BOT_CHATS = {
        "Có ai đi săn không?", "Làng hôm nay vui ghê", "Ai biết Lão Thạch ở đâu không?", "Mua bình máu nhỏ", "Chào cả nhà",
    };

    /** Một bot: người chơi ảo, chỗ nhà, đích đang đi tới, lịch đi / chat (ms). */
    static class Bot {
        Player player;
        int homeX, homeZ, destX, destZ;
        boolean walking;
        long nextWalk, nextChat;
    }

    public static final List<Bot> bots = new ArrayList<>();

    /**
     * Tạo sẵn count bot khi khởi động, như ServerBots.cs của server giả lập: tài khoản Bot001, Bot002... trong MySQL, có rồi
     * thì dùng lại. Bot số lẻ ở map xuất hiện, bot số chẵn rải đều các map khác. Server lái hết, client chỉ thấy MOVE như
     * người chơi khác.
     */
    public static void spawnBots(int count) throws IOException {
        List<Integer> others = new ArrayList<>(new TreeSet<>(mapIds));
        others.remove((Integer) startMap);
        ThreadLocalRandom rand = ThreadLocalRandom.current();
        for (int i = 0; i < count; i++) {
            Account a = Accounts.findOrCreateBot(String.format("Bot%03d", i + 1));
            Bot b = new Bot();
            b.player = Accounts.newPlayer(a, null);
            double angle = rand.nextDouble(Math.PI * 2), radius = BOT_SPAWN * Math.sqrt(rand.nextDouble());
            b.homeX = spawnX + (int) (Math.cos(angle) * radius);
            b.homeZ = spawnZ + (int) (Math.sin(angle) * radius);
            int map = i % 2 == 0 || others.isEmpty() ? startMap : others.get(i / 2 % others.size());
            Zones.enterMap(b.player, map, b.homeX, b.homeZ);
            online.put(b.player.id, b.player);
            bots.add(b);
        }
        if (bots.isEmpty()) return;
        Thread loop = new Thread(Bots::runBots, "bots");
        loop.setDaemon(true);
        loop.start();
    }

    /** Thread bot: mỗi BOT_TICK_MS chạy một nhịp. Một nhịp lỗi không được làm bot dừng hẳn. */
    static void runBots() {
        while (true) {
            try {
                Thread.sleep(BOT_TICK_MS);
                synchronized (LOCK) {
                    tickBots(System.currentTimeMillis());
                }
            } catch (InterruptedException e) {
                return;
            } catch (IOException | RuntimeException e) {
                System.out.println("Loi bot: " + e);
            }
        }
    }

    /**
     * Một nhịp của mọi bot: đang đi thì bước tiếp, tới nơi thì nghỉ 1-4 giây rồi chọn đích mới quanh nhà, thỉnh thoảng chat.
     * Khu không có người thật thì bot đứng yên và hoãn chat, để có người vào cũng không chat dồn cùng lúc.
     */
    static void tickBots(long now) throws IOException {
        ThreadLocalRandom rand = ThreadLocalRandom.current();
        for (Bot b : bots) {
            GameMap map = b.player.room;
            if (!map.hasHuman()) {
                b.nextChat = now + rand.nextInt(20_000, 90_000);
                continue;
            }
            if (b.walking) {
                if (stepBot(b, map)) {
                    b.walking = false;
                    b.nextWalk = now + rand.nextInt(1000, 4000);
                }
            } else if (now >= b.nextWalk) {
                pickDestination(b, rand);
            }
            if (now >= b.nextChat) {
                Chat.chatMap(b.player, BOT_CHATS[rand.nextInt(BOT_CHATS.length)]);
                b.nextChat = now + rand.nextInt(30_000, 120_000);
            }
        }
    }

    /** Chọn điểm ngẫu nhiên trong bán kính BOT_WANDER quanh nhà rồi bắt đầu đi. */
    static void pickDestination(Bot b, ThreadLocalRandom rand) {
        double angle = rand.nextDouble(Math.PI * 2), radius = BOT_WANDER * Math.sqrt(rand.nextDouble());
        b.destX = b.homeX + (int) (Math.cos(angle) * radius);
        b.destZ = b.homeZ + (int) (Math.sin(angle) * radius);
        b.walking = true;
    }

    /**
     * Đi một nhịp về phía đích và gửi MOVE cho khu. y = GROUND_Y để client tự đặt xuống đất. Trả true khi đã tới (gửi
     * MOVE đứng yên).
     */
    static boolean stepBot(Bot b, GameMap map) throws IOException {
        Player p = b.player;
        double dx = b.destX - p.x, dz = b.destZ - p.z, dist = Math.hypot(dx, dz);
        int dir = Math.floorMod((int) Math.round(Math.toDegrees(Math.atan2(dx, dz))), 360);
        if (dist <= BOT_ARRIVE) {
            Movement.sendMove(map, p, dir, STATE_IDLE, ACTION_IDLE, BOT_SEND_TICKS, 0);
            return true;
        }
        double step = Math.min(dist, BOT_SPEED * BOT_TICK_MS / 1000.0);
        p.x += (int) Math.round(dx / dist * step);
        p.z += (int) Math.round(dz / dist * step);
        Movement.sendMove(map, p, dir, STATE_MOVE, ACTION_RUN, BOT_SEND_TICKS, 0);
        return false;
    }
}
