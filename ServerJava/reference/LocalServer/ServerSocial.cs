using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.ServerSim
{
    /// <summary>
    /// Tổ đội / bang ở server giả lập (Cmd_Message.PARTY / CLAN + sub-command), và chat hai kênh đó chỉ tới thành viên.
    /// <list type="bullet">
    /// <item>Tổ đội: chỉ trong phiên (rời server là rời tổ đội), tối đa <see cref="PartyMax"/> người, trưởng mời. Mời bot thì bot tự
    /// đồng ý. Còn một người thật (hoặc chỉ còn bot) thì giải tán. Máu thành viên đổi thì gửi lại PARTY_INFO (mỗi giây xét một lần).</item>
    /// <item>Bang: lưu ở local_server/clans.json theo tài khoản (id người chơi đổi mỗi lần đăng nhập). Chủ bang rời thì người kế tiếp làm
    /// chủ, hết người thì xóa bang. Thành viên offline gửi id âm (-1, -2...) để client vẫn phân biệt được.</item>
    /// </list>
    /// </summary>
    public sealed partial class ServerWorld
    {
        const int PartyMax = 5;
        const int ClanMax = 30;
        const float InviteSeconds = 30f;
        const float PartyHpInterval = 1f;

        sealed class ServerParty
        {
            public int id, leader;
            public readonly List<ServerPlayer> members = new List<ServerPlayer>();
            public long hpSignature;
        }

        [Serializable]
        public sealed class ClanRecord
        {
            public int id;
            public string name, tag, leader;
            public List<string> members = new List<string>();
        }

        [Serializable]
        public sealed class ClanFile
        {
            public int nextId = 1;
            public List<ClanRecord> clans = new List<ClanRecord>();
        }

        readonly Dictionary<ServerPlayer, ServerParty> partyOf = new Dictionary<ServerPlayer, ServerParty>();
        readonly Dictionary<int, (int inviter, float until)> partyInvites = new Dictionary<int, (int, float)>();   // người được mời -> ai mời
        int nextPartyId = 1;
        float nextPartyHp;
        ClanFile clanFile;

        static string ClanPath => Path.Combine(ServerPlayer.Folder, "clans.json");

        // ================================================================ tổ đội

        // PartySub: mời / trả lời / rời
        void HandleParty(ServerPlayer p, byte sub, MyReader r)
        {
            switch (sub)
            {
                case Cmd_Message.PartySub.INVITE:
                    PartyInvite(p, r.ReadInt());
                    break;
                case Cmd_Message.PartySub.ANSWER:
                    PartyAnswer(p, r.ReadInt(), r.ReadBool());
                    break;
                case Cmd_Message.PartySub.LEAVE:
                    LeaveParty(p);
                    break;
                default:
                    Debug.LogWarning("LocalServer: PARTY sub " + sub + " chưa có.");
                    break;
            }
        }

        // ClanSub: lập / vào / rời
        void HandleClan(ServerPlayer p, byte sub, MyReader r)
        {
            switch (sub)
            {
                case Cmd_Message.ClanSub.CREATE:
                    ClanCreate(p, r.ReadUTF(), r.ReadUTF());
                    break;
                case Cmd_Message.ClanSub.JOIN:
                    ClanJoin(p, r.ReadUTF());
                    break;
                case Cmd_Message.ClanSub.LEAVE:
                    ClanLeave(p);
                    break;
                default:
                    Debug.LogWarning("LocalServer: CLAN sub " + sub + " chưa có.");
                    break;
            }
        }

        void PartyInvite(ServerPlayer p, int targetId)
        {
            ServerPlayer target = FindPlayer(targetId);   // cả bot
            if (target == null || target == p)
            {
                SendToast(p.conn, "[LocalServer] Không mời được người này.");
                return;
            }
            partyOf.TryGetValue(p, out ServerParty party);
            if (party != null && party.leader != p.id)
            {
                SendToast(p.conn, "[LocalServer] Chỉ trưởng nhóm mới mời được.");
                return;
            }
            if (party != null && party.members.Count >= PartyMax)
            {
                SendToast(p.conn, "[LocalServer] Tổ đội đã đủ " + PartyMax + " người.");
                return;
            }
            if (partyOf.ContainsKey(target))
            {
                SendToast(p.conn, "[LocalServer] " + target.Name + " đã có tổ đội.");
                return;
            }
            if (target.conn == null)   // bot: đồng ý luôn
            {
                JoinParty(p, target);
                return;
            }
            partyInvites[target.id] = (p.id, Now + InviteSeconds);
            Send(target.conn, Cmd_Message.PARTY, Cmd_Message.PartySub.INVITED, w =>
            {
                w.writeInt(p.id);
                w.writeUTF(p.Name);
            });
        }

        void PartyAnswer(ServerPlayer p, int inviterId, bool accept)
        {
            if (!partyInvites.TryGetValue(p.id, out var invite) || invite.inviter != inviterId) return;
            partyInvites.Remove(p.id);
            ServerPlayer inviter = FindPlayer(inviterId);
            if (inviter == null || Now > invite.until)
            {
                SendToast(p.conn, "[LocalServer] Lời mời đã hết hạn.");
                return;
            }
            if (!accept)
            {
                SendToast(inviter.conn, "[LocalServer] " + p.Name + " từ chối vào tổ đội.");
                return;
            }
            JoinParty(inviter, p);
        }

        // leader: người mời (chưa có tổ đội thì lập mới, làm trưởng)
        void JoinParty(ServerPlayer leader, ServerPlayer member)
        {
            if (partyOf.ContainsKey(member)) return;
            if (!partyOf.TryGetValue(leader, out ServerParty party))
            {
                party = new ServerParty { id = nextPartyId++, leader = leader.id };
                party.members.Add(leader);
                partyOf[leader] = party;
            }
            if (party.members.Count >= PartyMax)
            {
                SendToast(leader.conn, "[LocalServer] Tổ đội đã đủ " + PartyMax + " người.");
                return;
            }
            party.members.Add(member);
            partyOf[member] = party;
            foreach (ServerPlayer o in party.members)
                SendToast(o.conn, "[LocalServer] " + member.Name + " vào tổ đội.");
            SendPartyInfo(party);
        }

        void LeaveParty(ServerPlayer p)
        {
            partyInvites.Remove(p.id);
            if (!partyOf.TryGetValue(p, out ServerParty party)) return;
            party.members.Remove(p);
            partyOf.Remove(p);
            SendNoParty(p);

            int humans = 0;
            foreach (ServerPlayer o in party.members) if (o.conn != null) humans++;
            if (party.members.Count <= 1 || humans == 0)
            {
                foreach (ServerPlayer o in party.members)
                {
                    partyOf.Remove(o);
                    SendNoParty(o);
                    SendToast(o.conn, "[LocalServer] Tổ đội đã giải tán.");
                }
                return;
            }
            if (party.leader == p.id)
            {
                ServerPlayer next = party.members[0];
                foreach (ServerPlayer o in party.members) if (o.conn != null) { next = o; break; }   // ưu tiên người thật
                party.leader = next.id;
            }
            foreach (ServerPlayer o in party.members) SendToast(o.conn, "[LocalServer] " + p.Name + " rời tổ đội.");
            SendPartyInfo(party);
        }

        void SendPartyInfo(ServerParty party)
        {
            party.hpSignature = HpSignature(party);
            foreach (ServerPlayer to in party.members)
                Send(to.conn, Cmd_Message.PARTY, Cmd_Message.PartySub.INFO, w =>
                {
                    w.writeInt(party.id);
                    w.writeInt(party.leader);
                    w.writeByte(party.members.Count);
                    foreach (ServerPlayer m in party.members)
                    {
                        w.writeInt(m.id);
                        w.writeUTF(m.Name);
                        w.writeShort(m.profile != null ? m.profile.Progress.Level : 1);
                        w.WriteLong(m.profile != null && m.profile.data != null ? m.profile.data.curHp : 0);
                        w.WriteLong(m.profile != null && m.profile.data != null ? m.profile.data.maxHp : 0);
                    }
                });
        }

        static void SendNoParty(ServerPlayer p) =>
            Send(p.conn, Cmd_Message.PARTY, Cmd_Message.PartySub.INFO, w =>
            {
                w.writeInt(-1);
                w.writeInt(-1);
                w.writeByte(0);
            });

        static long HpSignature(ServerParty party)
        {
            long s = 17;
            foreach (ServerPlayer m in party.members)
                s = s * 31 + (m.profile != null && m.profile.data != null ? m.profile.data.curHp * 7 + m.profile.Progress.Level : 0);
            return s;
        }

        // ================================================================ bang

        ClanFile Clans
        {
            get
            {
                if (clanFile != null) return clanFile;
                clanFile = new ClanFile();
                try
                {
                    if (File.Exists(ClanPath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(ClanPath), clanFile);
                }
                catch (Exception e)
                {
                    Debug.LogError("LocalServer: không đọc được " + ClanPath + ": " + e.Message);
                }
                return clanFile;
            }
        }

        void SaveClans()
        {
            try
            {
                Directory.CreateDirectory(ServerPlayer.Folder);
                File.WriteAllText(ClanPath, JsonUtility.ToJson(Clans, true));
            }
            catch (Exception e)
            {
                Debug.LogError("LocalServer: không ghi được " + ClanPath + ": " + e.Message);
            }
        }

        ClanRecord ClanOf(string account)
        {
            foreach (ClanRecord c in Clans.clans) if (c.members.Contains(account)) return c;
            return null;
        }

        ClanRecord ClanNamed(string name)
        {
            foreach (ClanRecord c in Clans.clans) if (string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        void ClanCreate(ServerPlayer p, string name, string tag)
        {
            name = (name ?? "").Trim();
            tag = (tag ?? "").Trim();
            if (ClanOf(p.account) != null)
            {
                SendToast(p.conn, "[LocalServer] Bạn đã ở trong một bang.");
                return;
            }
            if (name.Length < 3 || name.Length > 16 || tag.Length < 1 || tag.Length > 4)
            {
                SendToast(p.conn, "[LocalServer] Tên bang 3-16 ký tự, tag 1-4 ký tự.");
                return;
            }
            if (ClanNamed(name) != null)
            {
                SendToast(p.conn, "[LocalServer] Đã có bang tên " + name + ".");
                return;
            }
            var clan = new ClanRecord { id = Clans.nextId++, name = name, tag = tag, leader = p.account };
            clan.members.Add(p.account);
            Clans.clans.Add(clan);
            SaveClans();
            SendToast(p.conn, "[LocalServer] Đã lập bang " + name + ".");
            SendClanInfo(clan);
        }

        void ClanJoin(ServerPlayer p, string name)
        {
            if (ClanOf(p.account) != null)
            {
                SendToast(p.conn, "[LocalServer] Bạn đã ở trong một bang.");
                return;
            }
            ClanRecord clan = ClanNamed((name ?? "").Trim());
            if (clan == null)
            {
                SendToast(p.conn, "[LocalServer] Không có bang tên " + name + ".");
                return;
            }
            if (clan.members.Count >= ClanMax)
            {
                SendToast(p.conn, "[LocalServer] Bang đã đủ " + ClanMax + " người.");
                return;
            }
            clan.members.Add(p.account);
            SaveClans();
            foreach (ServerPlayer o in players.Values)
                if (clan.members.Contains(o.account)) SendToast(o.conn, "[LocalServer] " + p.Name + " vào bang.");
            SendClanInfo(clan);
        }

        void ClanLeave(ServerPlayer p)
        {
            ClanRecord clan = ClanOf(p.account);
            if (clan == null) return;
            clan.members.Remove(p.account);
            if (clan.members.Count == 0) Clans.clans.Remove(clan);
            else if (clan.leader == p.account) clan.leader = clan.members[0];
            SaveClans();
            SendNoClan(p);
            if (clan.members.Count == 0) return;
            foreach (ServerPlayer o in players.Values)
                if (clan.members.Contains(o.account)) SendToast(o.conn, "[LocalServer] " + p.Name + " rời bang.");
            SendClanInfo(clan);
        }

        ServerPlayer OnlineAccount(string account)
        {
            foreach (ServerPlayer o in players.Values) if (o.account == account) return o;
            return null;
        }

        /// <summary>CLAN_INFO cho mọi thành viên đang online (skip: bỏ người này, vd đang rời server).</summary>
        void SendClanInfo(ClanRecord clan, ServerPlayer skip = null)
        {
            var ids = new List<int>(clan.members.Count);
            var names = new List<string>(clan.members.Count);
            var online = new List<bool>(clan.members.Count);
            int leaderId = 0;
            for (int i = 0; i < clan.members.Count; i++)
            {
                string account = clan.members[i];
                ServerPlayer o = OnlineAccount(account);
                if (o == skip) o = null;
                int id = o != null ? o.id : -(i + 1);   // offline: id âm, mỗi người một số
                ids.Add(id);
                names.Add(o != null ? o.Name : account);
                online.Add(o != null);
                if (account == clan.leader) leaderId = id;
            }
            foreach (ServerPlayer to in players.Values)
            {
                if (to == skip || !clan.members.Contains(to.account)) continue;
                Send(to.conn, Cmd_Message.CLAN, Cmd_Message.ClanSub.INFO, w =>
                {
                    w.writeInt(clan.id);
                    w.writeUTF(clan.name);
                    w.writeUTF(clan.tag);
                    w.writeInt(leaderId);
                    w.writeShort(ids.Count);
                    for (int i = 0; i < ids.Count; i++)
                    {
                        w.writeInt(ids[i]);
                        w.writeUTF(names[i]);
                        w.writeBool(online[i]);
                    }
                });
            }
        }

        static void SendNoClan(ServerPlayer p) =>
            Send(p.conn, Cmd_Message.CLAN, Cmd_Message.ClanSub.INFO, w =>
            {
                w.writeInt(-1);
                w.writeUTF("");
                w.writeUTF("");
                w.writeInt(-1);
                w.writeShort(0);
            });

        // ================================================================ chat theo nhóm

        // CHAT_PARTY gửi lên: UTF; gửi về (BaseChatData.AddChat): int id, UTF, int time — chỉ thành viên tổ đội
        void ChatParty(ServerPlayer p, MyReader r)
        {
            string content = r.ReadUTF();
            if (!partyOf.TryGetValue(p, out ServerParty party))
            {
                SendToast(p.conn, "[LocalServer] Bạn chưa có tổ đội.");
                return;
            }
            int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (ServerPlayer o in party.members)
                Send(o.conn, Cmd_Message.CHAT_PARTY, w =>
                {
                    w.writeInt(p.id);
                    w.writeUTF(content);
                    w.writeInt(time);
                });
        }

        // CHAT_CLAN gửi lên: byte loại (0 thường), UTF; gửi về: int id, UTF, int time — chỉ thành viên bang đang online
        void ChatClan(ServerPlayer p, MyReader r)
        {
            r.ReadByte();
            string content = r.ReadUTF();
            ClanRecord clan = ClanOf(p.account);
            if (clan == null)
            {
                SendToast(p.conn, "[LocalServer] Bạn chưa có bang.");
                return;
            }
            int time = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (ServerPlayer o in players.Values)
                if (clan.members.Contains(o.account))
                    Send(o.conn, Cmd_Message.CHAT_CLAN, w =>
                    {
                        w.writeInt(p.id);
                        w.writeUTF(content);
                        w.writeInt(time);
                    });
        }

        // ================================================================ vào / rời server, mỗi frame

        /// <summary>Đăng nhập xong: gửi bang của người này, báo các thành viên khác là đã online.</summary>
        void SocialOnLogin(ServerPlayer p)
        {
            ClanRecord clan = ClanOf(p.account);
            if (clan != null) SendClanInfo(clan);
        }

        /// <summary>Rời server: rời tổ đội, báo bang là đã offline.</summary>
        void SocialOnDisconnect(ServerPlayer p)
        {
            LeaveParty(p);
            ClanRecord clan = ClanOf(p.account);
            if (clan != null) SendClanInfo(clan, skip: p);
        }

        void TickSocial(float now)
        {
            if (now < nextPartyHp) return;
            nextPartyHp = now + PartyHpInterval;
            var done = new HashSet<ServerParty>();
            foreach (ServerParty party in partyOf.Values)
            {
                if (!done.Add(party)) continue;
                if (HpSignature(party) != party.hpSignature) SendPartyInfo(party);
            }
        }
    }
}
