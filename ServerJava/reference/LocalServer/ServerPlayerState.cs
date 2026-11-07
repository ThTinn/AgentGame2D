using Game.Players;

namespace Game.ServerSim
{
    /// <summary>
    /// Cấp / máu / bang của người chơi (cả bot) cho những người khác cùng map (Cmd_Message.PLAYER, PlayerSub.STATE). MAP_INFO / ADD_PLAYER chỉ
    /// có máu lúc vào map, MOB.HIT chỉ báo lúc trúng đòn: hồi máu, uống thuốc, NPC chữa, lên cấp, vào / rời bang thì người khác
    /// chỉ thấy qua gói này. Gửi lúc người đó vào map, lúc map của người xem dựng xong, và mỗi <see cref="StateInterval"/> giây
    /// cho người nào vừa đổi.
    /// </summary>
    public sealed partial class ServerWorld
    {
        const float StateInterval = 1f;
        float nextStateCheck;

        void SendPlayerState(ServerConnection to, ServerPlayer o)
        {
            PlayerProfile pr = o.profile;
            if (pr == null || pr.data == null) return;
            ClanRecord clan = ClanOf(o.account);
            Send(to, Cmd_Message.PLAYER, Cmd_Message.PlayerSub.STATE, w =>
            {
                w.writeInt(o.id);
                w.writeShort(pr.Progress.Level);
                w.WriteLong(pr.data.curHp);
                w.WriteLong(pr.data.maxHp);
                w.writeInt(clan != null ? clan.id : -1);
                w.writeUTF(clan != null ? clan.tag : "");
            });
        }

        /// <summary>Ghi lại trạng thái hiện tại của o là đã báo; true nếu khác lần báo trước.</summary>
        bool TakeStateChange(ServerPlayer o)
        {
            PlayerProfile pr = o.profile;
            if (pr == null || pr.data == null) return false;
            ClanRecord clan = ClanOf(o.account);
            int level = pr.Progress.Level, clanId = clan != null ? clan.id : -1;
            long hp = pr.data.curHp, maxHp = pr.data.maxHp;
            if (level == o.sentLevel && clanId == o.sentClan && hp == o.sentHp && maxHp == o.sentMaxHp) return false;
            o.sentLevel = level;
            o.sentClan = clanId;
            o.sentHp = hp;
            o.sentMaxHp = maxHp;
            return true;
        }

        /// <summary>o vừa vào map: báo những người đã ở đó (theo sau ADD_PLAYER).</summary>
        void AnnounceState(ServerMap map, ServerPlayer o)
        {
            TakeStateChange(o);
            foreach (ServerPlayer other in map.players)
                if (other != o) SendPlayerState(other.conn, o);
        }

        void TickPlayerState(float now)
        {
            if (now < nextStateCheck) return;
            nextStateCheck = now + StateInterval;
            foreach (ServerMap map in maps.Values)
            {
                if (!HasHuman(map)) continue;
                foreach (ServerPlayer o in map.players)
                {
                    if (!TakeStateChange(o)) continue;
                    foreach (ServerPlayer other in map.players)
                        if (other != o) SendPlayerState(other.conn, o);
                }
            }
        }
    }
}
