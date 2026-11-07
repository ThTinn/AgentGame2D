using Game.Players;

namespace Game.Npcs
{
    /// <summary>
    /// Phần thân của NPC trong scene, để <see cref="NpcSystem.Talk"/> bảo nó phản ứng khi người chơi bắt chuyện.
    /// Game: bản GAME_2D gọi thẳng component Npc (entity 2D). Ở đây component NPC của Game (3D) tự cài interface này
    /// trên cùng GameObject truyền vào Talk; không cài thì NPC chỉ không quay người, hội thoại vẫn chạy.
    /// </summary>
    public interface INpcActor
    {
        /// <summary>Người chơi bắt chuyện: đứng lại, quay sang người chơi.</summary>
        void OnTalk(PlayerProfile player);

        /// <summary>NPC đã là kẻ thù: quay lưng, không nói chuyện.</summary>
        void OnShun(PlayerProfile player);
    }
}
