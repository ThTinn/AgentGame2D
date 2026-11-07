package game.story;

import java.io.*;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;

/** Lệnh NPC: bắt chuyện, dịch vụ, câu đáp, tặng quà. Luôn gửi lại bản chuẩn, kể cả khi từ chối. */
public class NpcCommands {
    static final int NPC_TALK = 1, NPC_OPTION = 2, NPC_CHOICE = 3, NPC_GIFT = 4;
    static final int STORY = Profile.PART_STORY, BAG = Profile.PART_BAG, STATS = Profile.PART_STATS;

    /** Chia lệnh NPC theo sub. Mọi tham số là chuỗi UTF: tên NPC, tên lựa chọn / câu đáp / vật phẩm. */
    public static void npc(Player p, DataInputStream r) throws IOException {
        Story story = p.profile.story;
        int sub = r.readUnsignedByte();
        switch (sub) {
            case NPC_TALK: {
                Tables.Npc npc = Tables.npcs.get(Packet.readUtf(r));
                if (npc != null) story.greet(npc);
                p.flush(STORY);
                break;
            }
            case NPC_OPTION: {
                Tables.Npc npc = Tables.npcs.get(Packet.readUtf(r));
                Tables.NpcOption o = npc != null ? npc.options.get(Packet.readUtf(r)) : null;
                if (o != null) story.useOption(npc, o);
                p.flush(BAG | STATS);
                break;
            }
            case NPC_CHOICE: {
                String owner = Packet.readUtf(r);
                story.choose(Story.findChoice(owner, Packet.readUtf(r)));
                p.flush(Profile.PART_ALL);
                break;
            }
            case NPC_GIFT: {
                Tables.Npc npc = Tables.npcs.get(Packet.readUtf(r));
                String item = Packet.readUtf(r);
                if (npc != null) story.giveGift(npc, item);
                p.flush(STORY | BAG);
                break;
            }
            default:
                break;
        }
    }
}
