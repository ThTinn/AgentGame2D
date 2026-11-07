using System;
using UnityEngine;
using Game.Core;
using Game.Items;
using Game.Players;
using Game.Story;
using static Game.Story.StoryText;

namespace Game.Npcs
{
    /// <summary>
    /// Nói chuyện với NPC: phía scene (NPC của Game) gọi <see cref="Talk"/> với id NPC trong NpcDatabase.
    /// Lớp gameplay không được biết UI, nên Talk chỉ bắn <see cref="Opened"/>; hộp thoại bên UI nghe và
    /// vẽ, rồi gọi lại <see cref="Use"/> cho các lựa chọn làm đổi người chơi (hồi máu, mua đồ).
    /// Game: bản GAME_2D nhận MapObject (object trên map 2D, param = id NPC) và component Npc 2D; ở đây nhận id + GameObject.
    /// </summary>
    public static class NpcSystem
    {
        /// <summary>Người chơi vừa bắt chuyện với một NPC (kèm GameObject của nó trong scene, có thể null). UI nghe để mở hộp thoại.</summary>
        public static event Action<NpcTemplate, GameObject> Opened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Opened = null;

        /// <summary>
        /// Bắt chuyện với NPC id <paramref name="npcId"/> (NpcTemplate.id). <paramref name="o"/> là thân NPC trong scene: có
        /// <see cref="INpcActor"/> thì nó quay sang / quay lưng. False khi thiếu dữ liệu.
        /// </summary>
        public static bool Talk(int npcId, GameObject o = null)
        {
            NpcDatabase table = NpcDatabase.Current;
            if (table == null)
            {
                Debug.LogWarning("NpcSystem: chưa có NpcDatabase. Gán qua CoreSystems.Use.");
                return false;
            }

            NpcTemplate npc = table.Get(npcId);
            if (npc == null)
            {
                Debug.LogWarning($"NpcSystem: không có NPC id {npcId}" + (o != null ? $" ('{o.name}')." : "."), o);
                return false;
            }

            PlayerProfile me = CoreSystems.Me;
            INpcActor entity = o != null ? o.GetComponent<INpcActor>() : null;

            // kẻ thù: quay lưng, không mở hộp thoại
            if (QuestSystem.IsEnemy(npc.name))
            {
                if (entity != null) entity.OnShun(me);
                Notify.Toast(QuestSystem.ShunLineOf(npc), 2.5f);
                return true;
            }

            // NPC là entity: đứng lại và quay sang người chơi trong lúc nói chuyện
            if (entity != null) entity.OnTalk(me);

            NetSync.Send?.Talk(npc.name);   // online: server tính thân thiện lần chào đầu của nó
            QuestSystem.Greet(npc);   // lần đầu mỗi phiên: thân thêm một chút

            // scene không có UI hội thoại thì ít ra vẫn nghe được câu chào, và việc "gặp NPC này" coi như xong;
            // có UI thì hộp thoại cho NPC nói lời của mục tiêu Talk trước rồi mới đánh dấu (NpcDialog)
            if (Opened == null)
            {
                Notify.Toast(T(npc.Label) + ": " + QuestSystem.GreetingOf(npc), 3f);
                QuestSystem.OnTalkedTo(npc);
            }
            else Opened(npc, o);
            return true;
        }

        /// <summary>
        /// Chạy lựa chọn Heal / BuyItem của NPC cho người chơi mình. Talk và OpenPanel là việc của UI nên trả false.
        /// Giá tính theo bậc thân thiện với NPC (<see cref="QuestSystem.PriceAt"/>). Kết quả báo bằng toast.
        /// </summary>
        public static bool Use(NpcTemplate npc, NpcOption option)
        {
            if (option == null || (option.action != NpcAction.Heal && option.action != NpcAction.BuyItem)) return false;

            PlayerProfile me = CoreSystems.Me;
            if (me == null || me.data == null) return false;
            if (!NetSync.CanAct)
            {
                Notify.Toast(L("Chưa kết nối server|Not connected to the server"));
                return false;
            }

            // online: làm tại máy cho UI phản hồi ngay, được thì báo server làm theo (server gửi bản chuẩn về)
            long price = QuestSystem.PriceAt(npc, option.price);
            bool ok = option.action == NpcAction.Heal ? Heal(me, price) : Buy(me, option, price);
            if (ok) NetSync.Send?.NpcOption(npc.name, option.name);
            return ok;
        }

        static bool Heal(PlayerProfile me, long price)
        {
            EntityData d = me.data;
            if (me.isDie) return false;
            if (d.curHp >= d.maxHp && d.curMp >= d.maxMp)
            {
                Notify.Toast(L("Bạn đang khỏe rồi|You're already at full health"));
                return false;
            }
            if (!CanPay(me, price)) return false;

            me.AddGold(-price);
            me.UpdateStats(TypeHP_KI.Type_Hp, d.maxHp);
            me.UpdateStats(TypeHP_KI.Type_MP, d.maxMp);
            Notify.Toast(L("Đã hồi đầy máu và mana|HP and MP fully restored"));
            return true;
        }

        static bool Buy(PlayerProfile me, NpcOption option, long price)
        {
            ItemDatabase items = ItemDatabase.Current;
            ItemTemplate item = items != null ? items.Find(option.item) : null;
            if (item == null)
            {
                Debug.LogWarning($"NpcSystem: không có vật phẩm '{option.item}' trong ItemDatabase.");
                return false;
            }
            if (!CanPay(me, price)) return false;

            // bỏ vào túi trước rồi mới trừ vàng: túi đầy thì không mất tiền
            if (me.Inventory.Add(item.id) > 0)
            {
                Notify.Toast(L("Hành trang đầy|Inventory is full"));
                return false;
            }

            me.AddGold(-price);
            Notify.Toast(L("Đã mua {0}|Bought {0}", T(item.Label)));
            return true;
        }

        static bool CanPay(PlayerProfile me, long price)
        {
            if (me.Gold >= price) return true;
            Notify.Toast(L("Không đủ vàng (cần {0})|Not enough gold (need {0})", price));
            return false;
        }
    }
}
