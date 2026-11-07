package game.net;

/** Mã lệnh chính, trùng Cmd_Message.cs của client. Sub-command nằm ở module dùng lệnh đó. */
public class Cmd {
    // tải dữ liệu (game.data)
    public static final int REQUEST_DATA = 1, CLIENT_INFO = 2, CHECK_DATA_VERSION = 3, UPDATE_DATA = 4, PING = 25;

    // đăng nhập, thông báo
    public static final int LOGIN = 7, MENU = 8, ME_INFO = 14, NOTIFY = 66;

    // map, người chơi khác, di chuyển
    public static final int MAP_INFO = 9, ADD_PLAYER = 10, LEAVE_MAP = 11, MOVE = 12, PLAYER_INFO = 20, PLAYER = 68;
    public static final int MAP_OBJECT = 57, MAP = 60;

    // chat
    public static final int CHAT_MAP = 15, CHAT_PM = 16, CHAT_WORLD = 19;

    // chiến đấu, quái, đồ rơi
    public static final int ATTACK = 21, BULLET = 22, COMBAT = 67, MOB = 71, GROUND_ITEM = 59;

    // hồ sơ: NPC, nhiệm vụ, túi đồ, nâng cấp
    public static final int NPC = 61, QUEST = 62, ITEM = 63, BUILD = 64, PROFILE = 65;
}
