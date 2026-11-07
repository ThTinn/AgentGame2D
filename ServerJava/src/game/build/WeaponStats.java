package game.build;

import game.tables.Tables;

/** Chỉ số cộng thêm của vũ khí, gộp từ các nút đã mở (WeaponStats của client). */
public class WeaponStats {
    static final double MAX_COOLDOWN_CUT = 0.6;
    static final int BASE_EXPLOSION_DAMAGE = 50;    // % sát thương nổ khi mới có nút nổ

    public int damage, projectiles, targets, explosionDamage;   // damage, explosionDamage: %
    public double explosionRadius, range, arc;                 // ô, ô, độ
    public double attackSpeed;                                  // phần giảm hồi chiêu đòn thường (0..MAX_COOLDOWN_CUT)

    /** Cộng một chỉ số times lần. Chỉ số không thuộc vũ khí thì bỏ qua. */
    void add(Tables.Effect e, int times) {
        double v = e.value * times;
        switch (e.stat) {
            case Tables.WEAPON_DAMAGE: damage += Math.rint(v); break;
            case Tables.PROJECTILES: projectiles += Math.rint(v); break;
            case Tables.ATTACK_TARGETS: targets += Math.rint(v); break;
            case Tables.EXPLOSION_RADIUS: explosionRadius += v; break;
            case Tables.EXPLOSION_DAMAGE: explosionDamage += Math.rint(v); break;
            case Tables.ATTACK_RANGE: range += v; break;
            case Tables.ATTACK_SPEED: attackSpeed = Math.min(MAX_COOLDOWN_CUT, attackSpeed + v / 100); break;
            case Tables.ARC: arc += v; break;
            default: break;
        }
    }

    /** % sát thương của vụ nổ, 0 khi vũ khí chưa có nút nổ. */
    public int explosionPercent() {
        return explosionRadius > 0 ? BASE_EXPLOSION_DAMAGE + explosionDamage : 0;
    }
}
