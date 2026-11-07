namespace Game.EditorTools
{
    /// <summary>
    /// Tên sheet và tên cột của Docs/Items_Data.xlsx, dùng chung cho <see cref="ItemsExcelExport"/> (ghi) và
    /// <see cref="ItemsExcelImport"/> (đọc): đổi chữ ở đây là cả hai đổi theo. Import tìm cột theo tên tiêu đề, nên game
    /// design được đổi thứ tự cột, thêm cột ghi chú riêng; chỉ không được đổi tên các cột dưới đây.
    /// </summary>
    public static class ItemsExcelSchema
    {
        public const string ItemSheet = "Vật phẩm";
        public const string OptionSheet = "Option (chỉ số)";
        public const string GearSheet = "Option đồ mặc";

        // ---- Vật phẩm: cột import đọc (còn lại là công thức / tham khảo, import bỏ qua) ----
        public const string ItemId = "id";
        public const string ItemName = "name (khóa)";
        public const string ItemDisplay = "Tên hiển thị";
        public const string ItemKind = "Loại (ItemKind)";
        public const string ItemRarity = "Phẩm chất";
        public const string ItemValue = "value";
        public const string ItemMaxStack = "maxStack";
        public const string ItemIcon = "Icon";
        public const string ItemDescription = "Mô tả";

        // ---- Option (chỉ số) ----
        public const string OptId = "id";
        public const string OptStat = "PlayerStat";
        public const string OptName = "name (khóa)";
        public const string OptVn = "Tên (VN)";
        public const string OptEn = "Tên (EN)";
        public const string OptOrder = "order";
        public const string OptFormat = "format";
        public const string OptCapKind = "capKind";
        public const string OptCap = "cap";
        public const string OptDescVn = "Mô tả (VN)";
        public const string OptDescEn = "Mô tả (EN)";

        // ---- Option đồ mặc ----
        public const string GearItem = "Vật phẩm (name)";
        public const string GearStat = "Chỉ số (PlayerStat)";
        public const string GearValue = "Giá trị";

        // ---- Rơi đồ (DropDatabase): mỗi dòng một vật phẩm của một bảng ----
        public const string DropSheet = "Rơi đồ";
        public const string DropTable = "Bảng (nguồn)";
        public const string DropItem = "Vật phẩm (name)";
        public const string DropChance = "Tỉ lệ %";
        public const string DropMin = "Min";
        public const string DropMax = "Max";
    }
}
