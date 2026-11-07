package game.story;

import java.io.*;
import game.net.Packet;
import game.player.Player;
import game.player.Profile;
import game.tables.Tables;

/** Lệnh QUEST: nhận, bỏ, trả, xong câu nói, theo dõi. Luôn gửi lại bản chuẩn, kể cả khi từ chối. */
public class QuestCommands {
    static final int QUEST_ACCEPT = 0, QUEST_ABANDON = 1, QUEST_TURN_IN = 2, QUEST_TALK_DONE = 3, QUEST_TRACK = 4;
    static final int STORY = Profile.PART_STORY;

    /** Chia lệnh QUEST theo sub. Tham số đầu luôn là tên nhiệm vụ; trả nhiệm vụ có thêm tên kết cục, xong câu nói có thêm số mục tiêu. */
    public static void quest(Player p, DataInputStream r) throws IOException {
        Story story = p.profile.story;
        int sub = r.readUnsignedByte();
        String name = Packet.readUtf(r);
        Tables.Quest q = Tables.quests.get(name);
        switch (sub) {
            case QUEST_ACCEPT:
                if (q != null) story.quests.accept(q);
                p.flush(STORY);
                break;
            case QUEST_ABANDON:
                if (q != null) story.quests.abandon(q);
                p.flush(STORY);
                break;
            case QUEST_TURN_IN: {
                String outcome = Packet.readUtf(r);
                if (q != null) story.quests.turnIn(q, outcome);
                p.flush(Profile.PART_ALL);
                break;
            }
            case QUEST_TALK_DONE:
                story.quests.completeTalk(q, r.readUnsignedByte());
                p.flush(STORY);
                break;
            case QUEST_TRACK:
                story.quests.track(name);
                p.flush(STORY);
                break;
            default:
                break;
        }
    }
}
