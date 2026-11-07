using UnityEngine;
using Game.Items;
using Game.Npcs;
using Game.Players;
using Game.Story;

namespace Game.ServerSim
{
    /// <summary>
    /// Các lệnh Core ở server giả lập (Cmd_Message.NPC / QUEST / ITEM / BUILD + sub-command, ServerWorld.Handle chuyển tới): chạy đúng các hàm
    /// luật client dùng (QuestSystem, NpcSystem, PlayerProfile) trên hồ sơ của server qua CoreSystems.AsServer, rồi gửi phần
    /// hồ sơ liên quan về (PROFILE_SYNC) dù làm được hay không: client đã làm trước tại máy, bản của server ghi đè lên là khớp.
    /// Không làm được thì báo thêm một dòng (TOAST). Thông báo sinh ra lúc xử lý yêu cầu thì bỏ (client đã tự hiện khi làm trước).
    /// </summary>
    public sealed partial class ServerWorld
    {
        // lệnh hồ sơ: khóa = cmd cha << 8 | sub-command (Cmd_Message.NPC / QUEST / ITEM / BUILD)
        const int NpcTalk = Cmd_Message.NPC << 8 | Cmd_Message.NpcSub.TALK;
        const int NpcOption = Cmd_Message.NPC << 8 | Cmd_Message.NpcSub.OPTION;
        const int NpcChoice = Cmd_Message.NPC << 8 | Cmd_Message.NpcSub.CHOICE;
        const int NpcGift = Cmd_Message.NPC << 8 | Cmd_Message.NpcSub.GIFT;
        const int QuestAccept = Cmd_Message.QUEST << 8 | Cmd_Message.QuestSub.ACCEPT;
        const int QuestAbandon = Cmd_Message.QUEST << 8 | Cmd_Message.QuestSub.ABANDON;
        const int QuestTurnIn = Cmd_Message.QUEST << 8 | Cmd_Message.QuestSub.TURN_IN;
        const int QuestTalkDone = Cmd_Message.QUEST << 8 | Cmd_Message.QuestSub.TALK_DONE;
        const int QuestTrack = Cmd_Message.QUEST << 8 | Cmd_Message.QuestSub.TRACK;
        const int ItemUse = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.USE;
        const int ItemEquip = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.EQUIP;
        const int ItemUnequip = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.UNEQUIP;
        const int ItemQuick = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.QUICK;
        const int ItemSort = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.SORT;
        const int ItemDiscard = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.DISCARD;
        const int ItemUpgrade = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.UPGRADE;
        const int ItemSocketOpen = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.SOCKET_OPEN;
        const int ItemGemSet = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.GEM_SET;
        const int ItemGemRemove = Cmd_Message.ITEM << 8 | Cmd_Message.ItemSub.GEM_REMOVE;
        const int SkillSet = Cmd_Message.BUILD << 8 | Cmd_Message.BuildSub.SKILL_SET;
        const int WeaponUpgrade = Cmd_Message.BUILD << 8 | Cmd_Message.BuildSub.WEAPON_UPGRADE;
        const int WeaponReset = Cmd_Message.BUILD << 8 | Cmd_Message.BuildSub.WEAPON_RESET;
        const int TreeUpgrade = Cmd_Message.BUILD << 8 | Cmd_Message.BuildSub.TREE_UPGRADE;
        const int TreeReset = Cmd_Message.BUILD << 8 | Cmd_Message.BuildSub.TREE_RESET;

        static int Op(short cmd, byte sub) => cmd << 8 | sub;

        void HandleCore(ServerPlayer p, short cmd, byte sub, MyReader r)
        {
            int op = Op(cmd, sub);
            bool ok;
            byte parts;
            string what;
            int gearSpot = 0;
            Game.Items.GearOutcome? gearOutcome = null;
            using (CoreSystems.AsServer(p.profile, null))
            {
                switch (op)
                {
                    case NpcTalk:
                    {
                        NpcTemplate npc = Npc(what = r.ReadUTF());
                        ok = npc != null;
                        if (ok) QuestSystem.Greet(npc);
                        parts = ProfileSync.Story;
                        break;
                    }
                    case NpcOption:
                    {
                        NpcTemplate npc = Npc(r.ReadUTF());
                        string option = what = r.ReadUTF();
                        NpcOption o = null;
                        if (npc?.options != null) foreach (NpcOption x in npc.options) if (x != null && x.name == option) o = x;
                        ok = o != null && !QuestSystem.IsEnemy(npc.name) && NpcSystem.Use(npc, o);
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case NpcChoice:
                    {
                        string owner = r.ReadUTF();
                        what = r.ReadUTF();
                        StoryChoice c = QuestSystem.FindChoice(owner, what);
                        ok = c != null && QuestSystem.Choose(c, out _);
                        parts = ProfileSync.All;
                        break;
                    }
                    case NpcGift:
                    {
                        NpcTemplate npc = Npc(r.ReadUTF());
                        string item = what = r.ReadUTF();
                        NpcGift gift = null;
                        if (npc?.gifts != null) foreach (NpcGift g in npc.gifts) if (g != null && g.item == item) gift = g;
                        ok = gift != null && QuestSystem.GiveGift(npc, gift) > 0;
                        parts = ProfileSync.Story | ProfileSync.Bag;
                        break;
                    }
                    case QuestAccept:
                        ok = QuestSystem.Accept(Quest(what = r.ReadUTF()));
                        parts = ProfileSync.Story;
                        break;
                    case QuestAbandon:
                    {
                        QuestTemplate q = Quest(what = r.ReadUTF());
                        QuestStatus before = QuestSystem.StatusOf(q);
                        QuestSystem.Abandon(q);
                        ok = q != null && (before == QuestStatus.Active || before == QuestStatus.Ready);
                        parts = ProfileSync.Story;
                        break;
                    }
                    case QuestTurnIn:
                    {
                        QuestTemplate q = Quest(what = r.ReadUTF());
                        string outcomeName = r.ReadUTF();
                        QuestOutcome outcome = null;
                        if (q?.outcomes != null) foreach (QuestOutcome o in q.outcomes) if (o != null && o.name == outcomeName) outcome = o;
                        ok = q != null && QuestSystem.TurnIn(q, outcome, out _);
                        parts = ProfileSync.All;
                        break;
                    }
                    case QuestTalkDone:
                    {
                        QuestTemplate q = Quest(what = r.ReadUTF());
                        int objective = r.ReadByte();
                        ok = q != null && QuestSystem.CompleteTalk(new QuestStep { quest = q, objective = objective });
                        parts = ProfileSync.Story;
                        break;
                    }
                    case QuestTrack:
                    {
                        what = r.ReadUTF();
                        QuestTemplate q = string.IsNullOrEmpty(what) ? null : Quest(what);
                        ok = QuestSystem.Track(q);
                        parts = ProfileSync.Story;
                        break;
                    }
                    case ItemUse:
                    {
                        int id = r.ReadShort();
                        what = "item " + id;
                        ok = p.profile.UseItem(id);
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case ItemEquip:
                    {
                        int slot = r.ReadShort();
                        what = "ô " + slot;
                        ok = p.profile.Equip(slot);
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case ItemUnequip:
                    {
                        int slot = r.ReadByte();
                        what = ((EquipSlot)slot).ToString();
                        ok = p.profile.Unequip((EquipSlot)slot);
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case ItemQuick:
                    {
                        int slot = r.ReadByte();
                        int item = r.ReadShort();
                        what = "ô nhanh " + slot;
                        p.profile.SetQuickItem(slot, item);
                        ok = true;
                        parts = ProfileSync.Bag;
                        break;
                    }
                    case ItemSort:
                        what = "hành trang";
                        p.profile.SortBag();
                        ok = true;
                        parts = ProfileSync.Bag;
                        break;
                    case ItemDiscard:
                    {
                        int slot = r.ReadShort();
                        int count = r.ReadShort();
                        what = "ô " + slot + " x" + count;
                        ok = p.profile.Discard(slot, count);
                        parts = ProfileSync.Bag;
                        break;
                    }
                    case ItemUpgrade:
                    {
                        gearSpot = r.ReadShort();
                        bool lucky = r.ReadBool();
                        bool protect = r.ReadBool();
                        what = "chỗ " + gearSpot;
                        gearOutcome = p.profile.UpgradeGear(gearSpot, lucky, protect);
                        ok = gearOutcome != Game.Items.GearOutcome.Refused;
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case ItemSocketOpen:
                    {
                        gearSpot = r.ReadShort();
                        what = "chỗ " + gearSpot;
                        gearOutcome = p.profile.OpenSocket(gearSpot);
                        ok = gearOutcome != Game.Items.GearOutcome.Refused;
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case ItemGemSet:
                    {
                        int spot = r.ReadShort();
                        int index = r.ReadByte();
                        int gem = r.ReadShort();
                        what = "chỗ " + spot + " lỗ " + index + " đá " + gem;
                        ok = p.profile.InlayGem(spot, index, gem);
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case ItemGemRemove:
                    {
                        int spot = r.ReadShort();
                        int index = r.ReadByte();
                        what = "chỗ " + spot + " lỗ " + index;
                        ok = p.profile.RemoveGem(spot, index);
                        parts = ProfileSync.Bag | ProfileSync.Stats;
                        break;
                    }
                    case SkillSet:
                    {
                        int slot = r.ReadByte();
                        int id = r.ReadShort();
                        what = "skill " + id + " ô " + slot;
                        ok = SetSkill(p.profile, slot, id);
                        parts = ProfileSync.Build;
                        break;
                    }
                    case WeaponUpgrade:
                    {
                        Game.Skills.WeaponTemplate w = Weapon(r.ReadShort());
                        int up = r.ReadShort();
                        what = "nút " + up;
                        ok = w != null && p.profile.Upgrade(w, up);
                        parts = ProfileSync.Build;
                        break;
                    }
                    case WeaponReset:
                    {
                        Game.Skills.WeaponTemplate w = Weapon(r.ReadShort());
                        what = w != null ? w.name : "?";
                        ok = w != null;
                        if (ok) p.profile.ResetUpgrades(w);
                        parts = ProfileSync.Build;
                        break;
                    }
                    case TreeUpgrade:
                    {
                        int node = r.ReadShort();
                        what = "nút " + node;
                        ok = p.profile.Progress.Upgrade(node);
                        parts = ProfileSync.Build | ProfileSync.Stats;
                        break;
                    }
                    case TreeReset:
                        what = "cây nhân vật";
                        p.profile.Progress.ResetTree();
                        ok = true;
                        parts = ProfileSync.Build | ProfileSync.Stats;
                        break;
                    default:
                        Debug.LogWarning("LocalServer: lệnh Core " + cmd + " sub " + sub + " chưa có.");
                        return;
                }
            }

            if (!ok)
            {
                Debug.Log($"LocalServer: {p.account} - server không làm được {CmdName(op)} ({what}), gửi lại bản chuẩn.");
                SendToast(p.conn, "[LocalServer] Không thực hiện được: " + CmdName(op));
            }
            Flush(p, parts);

            // sau Flush: client nhận kết quả thì hồ sơ đã là bản mới
            if (gearOutcome.HasValue)
            {
                byte outcome = (byte)gearOutcome.Value;
                Game.Items.ItemStack gear = p.profile.GearAt(gearSpot);
                bool socket = op == ItemSocketOpen;
                byte sub2 = socket ? Cmd_Message.ItemSub.SOCKET_RESULT : Cmd_Message.ItemSub.UPGRADE_RESULT;
                int value = socket ? gear.sockets : gear.level;
                Send(p.conn, Cmd_Message.ITEM, sub2, w =>
                {
                    w.writeByte(outcome);
                    w.writeByte(value);
                });
            }
        }

        static string CmdName(int op)
        {
            switch (op)
            {
                case NpcTalk: return "bắt chuyện";
                case NpcOption: return "dịch vụ NPC";
                case NpcChoice: return "câu đáp";
                case NpcGift: return "tặng quà";
                case QuestAccept: return "nhận nhiệm vụ";
                case QuestAbandon: return "bỏ nhiệm vụ";
                case QuestTurnIn: return "trả nhiệm vụ";
                case QuestTalkDone: return "gặp NPC";
                case QuestTrack: return "theo dõi nhiệm vụ";
                case ItemUse: return "dùng vật phẩm";
                case ItemEquip: return "mặc đồ";
                case ItemUnequip: return "tháo đồ";
                case ItemQuick: return "ô dùng nhanh";
                case ItemSort: return "sắp xếp hành trang";
                case ItemDiscard: return "bỏ đồ";
                case ItemUpgrade: return "cường hóa trang bị";
                case ItemSocketOpen: return "khoét lỗ";
                case ItemGemSet: return "khảm đá";
                case ItemGemRemove: return "gỡ đá";
                case SkillSet: return "gắn skill";
                case WeaponUpgrade: return "nâng cấp vũ khí";
                case WeaponReset: return "tẩy nâng cấp vũ khí";
                case TreeUpgrade: return "nâng cây nhân vật";
                case TreeReset: return "tẩy cây nhân vật";
                default: return "lệnh " + (op >> 8) + " sub " + (op & 0xFF);
            }
        }

        NpcTemplate Npc(string name) => settings.npcs != null ? settings.npcs.Find(name) : null;

        static Game.Skills.WeaponTemplate Weapon(int id) =>
            Game.Skills.WeaponDatabase.Current != null ? Game.Skills.WeaponDatabase.Current.Get(id) : null;

        // cùng luật với client (PlayerProfile.EquipSkill: chuyển ô giữ cấp, cấp theo hồ sơ của server); skill không có trong
        // bảng thì từ chối
        static bool SetSkill(PlayerProfile pr, int slot, int id)
        {
            Game.Skills.SkillDatabase db = Game.Skills.SkillDatabase.Current;
            if (id != 0 && (db == null || db.Get(id) == null)) return false;
            return pr.EquipSkill(slot, id);
        }

        QuestTemplate Quest(string name) => settings.quests != null ? settings.quests.Find(name) : null;
    }
}
