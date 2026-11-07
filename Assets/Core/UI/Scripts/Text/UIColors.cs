using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Bảng màu chữ dùng chung cho UI client: chữ trong panel, tên / thanh máu / số sát thương (cả panel lẫn chữ nổi trên đầu
    /// Entity qua WorldUIStyle). Đổi ở đây là đổi mọi nơi. Khác <see cref="TxtColor"/>: bảng đó tra theo mã sbyte server gửi.
    /// Màu nền / khung / nút vẫn để ở từng panel vì không dùng chung.
    /// </summary>
    public static class UIColors
    {
        #region Chữ trong panel

        public static readonly Color TextNormal = Color.white;
        /// <summary>Chữ phụ, trạng thái trống (vd "No player").</summary>
        public static readonly Color TextMuted = new Color(1f, 1f, 1f, 0.5f);
        /// <summary>Mục chưa mở khóa.</summary>
        public static readonly Color TextLocked = new Color(1f, 1f, 1f, 0.4f);
        /// <summary>Mục đang chọn trong danh sách.</summary>
        public static readonly Color TextSelected = new Color(1f, 0.85f, 0.3f);
        /// <summary>Mục đang mặc / đang cầm / đang gắn.</summary>
        public static readonly Color TextEquipped = new Color(0.45f, 0.95f, 0.5f);
        /// <summary>Nâng cấp đã lên vài cấp nhưng chưa tối đa.</summary>
        public static readonly Color TextRanked = new Color(0.55f, 0.85f, 1f);
        /// <summary>Nâng cấp đã tối đa.</summary>
        public static readonly Color TextMaxed = TextEquipped;

        /// <summary>Nhãn trong khối chỉ số. Hằng chuỗi để ghép vào thẻ &lt;color&gt; lúc biên dịch.</summary>
        public const string LabelHex = "#8FA3BF";

        #endregion

        #region Chữ trên nút HUD (nút skill / nút Đánh, nền tròn xanh rất nhạt)

        /// <summary>Tên skill / chữ "Đánh": xanh đậm, viền trắng (<see cref="HudButtonOutline"/>).</summary>
        public static readonly Color HudButtonText = new Color(0.07f, 0.15f, 0.30f);
        /// <summary>Dòng phụ: kiểu đòn, tên vũ khí. Cam đậm, nền xanh nhạt vẫn đọc được.</summary>
        public static readonly Color HudButtonSub = new Color(0.62f, 0.30f, 0f);
        /// <summary>Mana tốn mỗi lần tung.</summary>
        public static readonly Color HudButtonMana = new Color(0.02f, 0.24f, 0.62f);
        public static readonly Color32 HudButtonOutline = new Color32(255, 255, 255, 200);

        #endregion

        #region Trạng thái tốt / tạm / xấu (FPS...)

        public static readonly Color StatusGood = new Color(0.4f, 1f, 0.4f);
        public static readonly Color StatusWarn = new Color(1f, 0.85f, 0.3f);
        public static readonly Color StatusBad = new Color(1f, 0.35f, 0.35f);

        #endregion

        #region Tên theo loại Entity

        public static readonly Color NameMe = new Color(0.6f, 1f, 1f);
        public static readonly Color NamePlayer = Color.white;
        public static readonly Color NameEnemyPlayer = new Color(1f, 0.5f, 0.5f);
        public static readonly Color NameNpc = new Color(1f, 0.7f, 0.3f);
        public static readonly Color NameMonster = new Color(1f, 0.5f, 0.5f);
        public static readonly Color NameItem = new Color(0.6f, 1f, 0.6f);
        /// <summary>Tên trên map object (cổng: tên map đích). Tím như vòng cổng.</summary>
        public static readonly Color NameMapObject = new Color(0.8f, 0.55f, 1f);

        #endregion

        #region Máu / năng lượng

        public static readonly Color HpFriendly = new Color(0.3f, 0.9f, 0.3f);
        public static readonly Color HpHostile = new Color(0.9f, 0.25f, 0.25f);
        public static readonly Color HpTrail = new Color(1f, 0.85f, 0.3f);
        public static readonly Color HpDead = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color Mp = new Color(0.4f, 0.7f, 1f);
        /// <summary>Thanh EXP, chữ "+EXP" bay trên đầu.</summary>
        public static readonly Color Exp = new Color(0.78f, 0.55f, 1f);
        /// <summary>Chữ lên cấp, huy hiệu cấp, điểm tiềm năng còn tiêu được.</summary>
        public static readonly Color LevelUp = new Color(1f, 0.85f, 0.25f);
        /// <summary>Vàng: số vàng đang có, chữ "+vàng" bay trên đầu khi hạ quái.</summary>
        public static readonly Color Gold = new Color(1f, 0.7f, 0.2f);
        /// <summary>Vật phẩm quái rơi ra (chữ "+Ngọc ..." bay trên đầu).</summary>
        public static readonly Color Loot = new Color(0.55f, 1f, 0.75f);

        #endregion

        #region Số sát thương / chat

        public static readonly Color Damage = new Color(1f, 0.92f, 0.2f);
        /// <summary>Sát thương người chơi của mình phải nhận.</summary>
        public static readonly Color DamageTaken = new Color(1f, 0.45f, 0.6f);
        public static readonly Color DamageCrit = new Color(1f, 0.25f, 0.2f);
        public static readonly Color DamageMiss = new Color(0.8f, 0.8f, 0.8f);

        public static readonly Color ChatText = new Color(0.1f, 0.1f, 0.1f);

        #endregion

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>"#RRGGBB" để chèn vào thẻ &lt;color=...&gt; của TMP.</summary>
        public static string Hex(Color c) => "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Bọc chuỗi trong thẻ &lt;color&gt; của TMP.</summary>
        public static string Colored(string text, Color c) => $"<color={Hex(c)}>{text}</color>";
    }
}
