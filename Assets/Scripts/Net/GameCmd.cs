namespace DungeonOnline.Net
{
    /// <summary>
    /// Command codes of ServerJava (src/game/net/Cmd.java); both files must keep the same numbers. Data-download codes
    /// (REQUEST_DATA, CLIENT_INFO, CHECK_DATA_VERSION, UPDATE_DATA, PING) are NetKit's and match too.
    /// </summary>
    public static class GameCmd
    {
        public const int LOGIN = 7, MENU = 8, ME_INFO = 14, NOTIFY = 66;
        public const int MAP_INFO = 9, ADD_PLAYER = 10, LEAVE_MAP = 11, MOVE = 12, PLAYER = 68, MAP = 60;
        public const int CHAT_MAP = 15, CHAT_WORLD = 19;
        public const int MOB = 71;
        public const int PROFILE = 65;

        /// <summary>Sub-commands of <see cref="MOB"/> (MobAI.MOB_*).</summary>
        public const int MOB_MOVE = 0, MOB_STOP = 1, MOB_ATTACK = 2, MOB_HIT = 3, MOB_HP = 4, MOB_DIE = 5, MOB_RESPAWN = 6;

        /// <summary>Sub-commands of <see cref="PROFILE"/>.</summary>
        public const int PROFILE_DATA = 0, PROFILE_SYNC = 1;

        /// <summary>Sub-command of <see cref="NOTIFY"/> carrying a toast text.</summary>
        public const int NOTIFY_TOAST = 0;

        /// <summary>Server's "no height" value for the vec's middle axis (World.GROUND_Y); unused on 2D maps.</summary>
        public const int GROUND_Y = -99900;
    }
}
