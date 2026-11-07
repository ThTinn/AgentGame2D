namespace Game.Anim.Editor
{
    /// <summary>
    /// Game: bản GAME_2D là công cụ dựng anim cho nhân vật astronaut 2D (clip từ sprite của gói COPY SPRIGHT). Game không
    /// có gói đó nên không chép; chỉ giữ danh sách skin mà <c>ItemsExcelExport</c> cần để viết sheet Skin của
    /// Docs/Core/Items_Data.xlsx. Id ở đây là id bộ anim mà vật phẩm Skin trỏ tới (ItemTemplate.value): bên Game đó là
    /// id bộ trong PlayerAnimData (mặc vật phẩm Skin thì LocalPlayHud đổi sang bộ đó nếu bảng có).
    /// </summary>
    public static class AnimSetup
    {
        public static readonly (int id, string variant, string name)[] Skins =
        {
            (1, "B", "Astronaut Red"), (2, "C", "Astronaut Green"), (3, "D", "Astronaut Yellow"), (4, "E", "Astronaut Pink"),
        };
    }
}
