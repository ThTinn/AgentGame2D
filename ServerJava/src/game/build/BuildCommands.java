package game.build;

import java.io.*;
import game.player.Player;
import game.player.Profile;

/** Lệnh BUILD: skill trên nút, nâng cấp vũ khí, cây nhân vật. Luôn gửi lại bản chuẩn, kể cả khi từ chối. */
public class BuildCommands {
    static final int BUILD_SKILL = 0, BUILD_WEAPON_UPGRADE = 1, BUILD_WEAPON_RESET = 2, BUILD_TREE_UPGRADE = 3, BUILD_TREE_RESET = 4;
    static final int STATS = Profile.PART_STATS, BUILD = Profile.PART_BUILD;

    /** Chia lệnh BUILD theo sub. Định dạng từng sub ở BuildSub của Cmd_Message.cs. */
    public static void build(Player p, DataInputStream r) throws IOException {
        Profile pr = p.profile;
        int sub = r.readUnsignedByte();
        switch (sub) {
            case BUILD_SKILL:
                pr.build.setSkill(r.readUnsignedByte(), r.readShort());
                p.flush(BUILD);
                break;
            case BUILD_WEAPON_UPGRADE:
                pr.build.upgradeWeapon(r.readShort(), r.readShort());
                p.flush(BUILD);
                break;
            case BUILD_WEAPON_RESET:
                pr.build.resetWeapon(r.readShort());
                p.flush(BUILD);
                break;
            case BUILD_TREE_UPGRADE:
                pr.build.upgradeNode(r.readShort());
                p.flush(BUILD | STATS);
                break;
            case BUILD_TREE_RESET:
                pr.build.resetTree();
                p.flush(BUILD | STATS);
                break;
            default:
                break;
        }
    }
}
