package game.mob;

import game.player.Player;

/** Một con quái trong khu. Tọa độ tính bằng mét, thời điểm tính theo World.now() (giây). */
public class Mob {
    public int id, template, level;
    public int mode;                    // MobAI.WANDER / CHASE / RETURN
    public long hp, maxHp, damage;
    public double x, z;                 // vị trí hiện tại
    public double homeX, homeZ;         // chỗ đứng gốc, hồi sinh ở đây
    public double campX, campZ, radius; // bãi: đuổi quá xa tâm bãi thì quay về
    public double wanderX, wanderZ;     // đích đi dạo
    public double nextWander, nextSend, respawnAt, attackReadyAt, holdUntil;
    public double swingAt = -1;         // lúc cú đánh đang vung trúng, -1 khi không vung
    public boolean dead, wandering, moveSent;
    public Player target;               // người đang đuổi, null khi không đuổi ai
}
