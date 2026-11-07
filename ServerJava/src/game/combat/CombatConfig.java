package game.combat;

/** Thông số chiến đấu của LocalPlaySettings, đọc từ world.bin lúc khởi động. Tọa độ của phần chiến đấu tính bằng mét. */
public class CombatConfig {
    // thời gian tính bằng giây, tốc độ m/giây, khoảng cách mét
    public static double playerRespawn, mobRespawn, manaRegen;
    public static double aggroRange, leashScale, chaseSpeed, wanderSpeed, mobRange, mobCooldown, mobHitDelay;
    public static double critChance, basicCooldown, bulletSpeed, bulletHitRadius, meleeHalfAngle, hitDelay, cellSize;
    public static long baseDamage;
    public static int skillDamagePercent;

    /** Đổi số ô (đơn vị tầm / bán kính trong bảng skill) sang mét. */
    static double cells(double n) {
        return n * cellSize;
    }
}
