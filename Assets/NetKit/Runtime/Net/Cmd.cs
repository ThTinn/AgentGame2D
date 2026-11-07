namespace NetKit
{
    /// <summary>
    /// Mã lệnh chính, phải trùng <c>NetKit/Server/src/netkit/net/Cmd.java</c>. Mã lệnh 9 bit có dấu (-256..255). Một chức
    /// năng lớn dùng một mã cha, byte đầu thân gói là sub-command (các lớp *Sub). Định dạng từng gói: README.md của NetKit.
    /// </summary>
    public static class Cmd
    {
        // tải dữ liệu, dùng được trước khi đăng nhập
        public const int REQUEST_DATA = 1;        // c->s byte type, short id. s->c byte type, short id, int tổng (-1: không có), int vị trí, dữ liệu
        public const int CLIENT_INFO = 2;         // c->s byte thiết bị (1 Android, 2 iOS, 3 PC), short phiên bản client
        public const int CHECK_DATA_VERSION = 3;  // s->c byte n { byte type, byte ver }, short m { byte type, short id, int ver, int size }
        public const int UPDATE_DATA = 4;         // c->s byte type. s->c byte type, byte ver, dữ liệu
        public const int PING = 25;               // c->s long giờ client. s->c long giờ server (ms)

        // đăng nhập, thông tin của mình, thông báo
        public const int LOGIN = 7;               // c->s UTF tên, UTF mật khẩu. s->c bool ok, UTF lý do
        public const int ME_INFO = 14;            // s->c int id, UTF tên
        public const int NOTIFY = 66;             // s->c byte sub (NotifySub), ...

        // map, khu, người chơi khác, di chuyển. vec = 3 int x100
        public const int MAP_INFO = 9;            // s->c short map, byte khu, vec mình, byte n { player }
        public const int ADD_PLAYER = 10;         // s->c player = int id, UTF tên, vec
        public const int LEAVE_MAP = 11;          // s->c int id
        public const int MOVE = 12;               // c->s vec, int hướng (độ x100). s->c int id, vec, int hướng
        public const int MAP = 60;                // byte sub (MapSub), ...

        // chat: c->s UTF nội dung. s->c int id, UTF tên, UTF nội dung
        public const int CHAT_MAP = 15;
        public const int CHAT_WORLD = 19;

        // hồ sơ người chơi và ví dụ lệnh hồ sơ (client làm trước, server ghi đè)
        public const int PROFILE = 65;            // s->c byte sub (ProfileSub), int độ dài, JSON UTF-8
        public const int SHOP = 72;               // c->s byte sub (ShopSub), ...

        public static class NotifySub
        {
            public const byte TOAST = 0;          // s->c UTF nội dung, short phần mười giây
        }

        public static class MapSub
        {
            public const byte TRAVEL = 1;         // c->s short map
            public const byte ZONE_LIST = 3;      // c->s trống. s->c byte khu đang ở, byte sức chứa, byte n { byte số người }
            public const byte ZONE_CHANGE = 4;    // c->s byte khu
        }

        public static class ProfileSub
        {
            public const byte DATA = 0;           // cả hồ sơ lúc vào game
            public const byte SYNC = 1;           // bản của server, ghi đè bản client đoán trước
        }

        public static class ShopSub
        {
            public const byte BUY = 0;            // c->s UTF mã món, short số lượng. Server luôn trả PROFILE SYNC
        }
    }
}
