using System;
using System.Collections.Generic;
using Game.Core;
using Game.Items;
using Game.Net;
using Game.Skills;
using Game.Story;

namespace Game.Players
{
    /// <summary>
    /// Gói đồng bộ hồ sơ người chơi từ server về client (chơi online): server là bên giữ tiến trình, client nhận bản chuẩn
    /// và ghi đè bản đang dự đoán. Chia ba phần, gửi phần nào đổi phần đó:
    /// <list type="bullet">
    /// <item><see cref="Story"/>: thân thiện, nhiệm vụ, cờ cốt truyện, nhiệm vụ đang theo dõi.</item>
    /// <item><see cref="Bag"/>: các ô hành trang, đồ đang mặc / cầm, ô dùng nhanh.</item>
    /// <item><see cref="Stats"/>: cấp, EXP, điểm tiềm năng, vàng, máu / mana.</item>
    /// <item><see cref="Build"/>: skill đang gắn, cây nâng cấp vũ khí, cây nhân vật.</item>
    /// </list>
    /// Định dạng (Game.Net.MyWriter, big-endian):
    ///   byte parts,
    ///   [Story] short affinityCount { UTF npc, short value }, short questCount { UTF quest, bool active, short timesDone,
    ///           UTF outcome, int lastDay, byte n { short progress } }, short flagCount { UTF flag }, UTF tracked
    ///   [Bag]   short slotCount { short itemId, short count, Gear }, byte equipCount { short itemId, Gear } (EquipSlot 1..n),
    ///           byte quickCount { short itemId }. Gear = byte level, byte sockets, { short gemId } x sockets (0 = lỗ trống)
    ///   [Stats] short level, long exp, int points, long gold, long curHp, long maxHp, long curMp, long maxMp
    ///   [Build] byte skillCount { short templateId (0 = trống), byte level }, short upgradeCount { short weaponId,
    ///           short upgradeId, byte rank }, short nodeCount { short nodeId, byte rank }
    /// Server C# dùng lại được: chỉ cần PlayerProfile, không dính UI.
    /// </summary>
    public static class ProfileSync
    {
        public const byte Story = 1, Bag = 2, Stats = 4, Build = 8, All = Story | Bag | Stats | Build;

        static readonly List<AffinityRecord> affinityBuffer = new List<AffinityRecord>();
        static readonly List<QuestRecord> questBuffer = new List<QuestRecord>();
        static readonly List<string> flagBuffer = new List<string>();
        static readonly List<int> intBuffer = new List<int>();

        public static byte[] Write(PlayerProfile p, byte parts)
        {
            var w = new MyWriter(512);
            w.WriteByte(parts);
            if ((parts & Story) != 0) WriteStory(w, p.Story);
            if ((parts & Bag) != 0) WriteBag(w, p);
            if ((parts & Stats) != 0) WriteStats(w, p);
            if ((parts & Build) != 0) WriteBuild(w, p);
            return w.ToArray();
        }

        /// <summary>
        /// Áp bản của server lên hồ sơ <paramref name="p"/>. Trong lúc áp không gửi gì ngược lên server (ô dùng nhanh, theo
        /// dõi...). Trả các phần đã áp; false và lý do khi dữ liệu hỏng (hồ sơ có thể đã đổi một phần).
        /// </summary>
        public static bool Apply(PlayerProfile p, byte[] data, out byte parts, out string error)
        {
            parts = 0;
            error = null;
            NetSync.Suppress(true);
            try
            {
                var r = new MyReader(data);
                parts = r.ReadByte();
                if ((parts & Story) != 0) ReadStory(r, p.Story);
                if ((parts & Bag) != 0) ReadBag(r, p);
                if ((parts & Stats) != 0) ReadStats(r, p);
                if ((parts & Build) != 0) ReadBuild(r, p);
                if (r.Available != 0) error = "unexpected bytes at the end";
            }
            catch (Exception e)
            {
                error = "bad profile sync: " + e.Message;
            }
            finally
            {
                NetSync.Suppress(false);
            }
            return error == null;
        }

        // ---------------- cốt truyện ----------------

        static void WriteStory(MyWriter w, StoryState s)
        {
            s.Save(affinityBuffer, questBuffer, flagBuffer);
            w.WriteShort(checked((short)affinityBuffer.Count));
            foreach (AffinityRecord a in affinityBuffer)
            {
                w.WriteUTF(a.npc ?? "");
                w.WriteShort((short)a.value);
            }
            w.WriteShort(checked((short)questBuffer.Count));
            foreach (QuestRecord q in questBuffer)
            {
                w.WriteUTF(q.quest ?? "");
                w.WriteBool(q.active);
                w.WriteShort(checked((short)q.timesDone));
                w.WriteUTF(q.outcome ?? "");
                w.WriteInt(q.lastDay);
                w.WriteByte(checked((byte)q.progress.Count));
                foreach (int n in q.progress) w.WriteShort(checked((short)n));
            }
            w.WriteShort(checked((short)flagBuffer.Count));
            foreach (string f in flagBuffer) w.WriteUTF(f);
            w.WriteUTF(s.Tracked ?? "");
        }

        static void ReadStory(MyReader r, StoryState s)
        {
            var affinity = new List<AffinityRecord>();
            int n = r.ReadShort();
            for (int i = 0; i < n; i++) affinity.Add(new AffinityRecord { npc = r.ReadUTF(), value = r.ReadShort() });
            var quests = new List<QuestRecord>();
            n = r.ReadShort();
            for (int i = 0; i < n; i++)
            {
                var q = new QuestRecord { quest = r.ReadUTF(), active = r.ReadBool(), timesDone = r.ReadShort() };
                q.outcome = r.ReadUTF();
                if (q.outcome.Length == 0) q.outcome = null;
                q.lastDay = r.ReadInt();
                int k = r.ReadByte();
                for (int j = 0; j < k; j++) q.progress.Add(r.ReadShort());
                quests.Add(q);
            }
            var flags = new List<string>();
            n = r.ReadShort();
            for (int i = 0; i < n; i++) flags.Add(r.ReadUTF());
            s.Load(affinity, quests, flags, r.ReadUTF());
        }

        // ---------------- túi đồ ----------------

        static void WriteBag(MyWriter w, PlayerProfile p)
        {
            IReadOnlyList<ItemStack> slots = p.Inventory.Slots;
            w.WriteShort(checked((short)slots.Count));
            foreach (ItemStack s in slots)
            {
                w.WriteShort(checked((short)(s.IsEmpty ? 0 : s.itemId)));
                w.WriteShort(checked((short)(s.IsEmpty ? 0 : s.count)));
                WriteGear(w, s.IsEmpty ? default : s);
            }
            w.WriteByte((byte)Equipment.SlotCount);
            for (int i = 1; i <= Equipment.SlotCount; i++)
            {
                w.WriteShort(checked((short)p.Equipment[(EquipSlot)i]));
                WriteGear(w, p.Equipment.Stack((EquipSlot)i));
            }
            p.SaveQuickItems(intBuffer);
            w.WriteByte(checked((byte)intBuffer.Count));
            foreach (int id in intBuffer) w.WriteShort(checked((short)id));
        }

        static void WriteGear(MyWriter w, in ItemStack s)
        {
            w.WriteByte(checked((byte)s.level));
            w.WriteByte(checked((byte)s.sockets));
            for (int i = 0; i < s.sockets; i++) w.WriteShort(checked((short)s.Gem(i)));
        }

        static ItemStack ReadGear(MyReader r, int itemId, int count)
        {
            var s = new ItemStack(itemId, count, r.ReadByte()) { sockets = r.ReadByte() };
            for (int i = 0; i < s.sockets; i++) s.SetGem(i, r.ReadShort());
            return s;
        }

        static void ReadBag(MyReader r, PlayerProfile p)
        {
            int n = r.ReadShort();
            var slots = new List<ItemStack>(n);
            for (int i = 0; i < n; i++)
            {
                int itemId = r.ReadShort(), count = r.ReadShort();
                slots.Add(ReadGear(r, itemId, count));
            }
            p.Inventory.Load(slots);

            n = r.ReadByte();
            ItemDatabase items = ItemDatabase.Current;
            for (int i = 1; i <= n; i++)
            {
                int id = r.ReadShort();
                ItemStack gear = ReadGear(r, id, 1);
                var slot = (EquipSlot)i;
                if (!Equipment.IsValid(slot)) continue;
                bool sameItem = p.Equipment[slot] == id;
                p.Equipment.Set(slot, gear);
                if (!sameItem && slot == EquipSlot.Weapon)
                {
                    ItemTemplate it = items != null && id > 0 ? items.Get(id) : null;
                    p.SetWeapon(it != null ? it.WeaponId : 0);
                }
            }

            n = r.ReadByte();
            for (int i = 0; i < n; i++) p.SetQuickItem(i, r.ReadShort());
        }

        // ---------------- chỉ số ----------------

        static readonly List<NodeRank> rankBuffer = new List<NodeRank>();

        static void WriteStats(MyWriter w, PlayerProfile p)
        {
            w.WriteShort(checked((short)p.Progress.Level));
            w.WriteLong(p.Progress.Exp);
            w.WriteInt(p.Progress.Points);
            w.WriteLong(p.Gold);
            w.WriteLong(p.data != null ? p.data.curHp : 0);
            w.WriteLong(p.data != null ? p.data.maxHp : 0);
            w.WriteLong(p.data != null ? p.data.curMp : 0);
            w.WriteLong(p.data != null ? p.data.maxMp : 0);
        }

        static void ReadStats(MyReader r, PlayerProfile p)
        {
            int level = r.ReadShort();
            long exp = r.ReadLong();
            int points = r.ReadInt();
            long gold = r.ReadLong();
            long curHp = r.ReadLong(), maxHp = r.ReadLong(), curMp = r.ReadLong(), maxMp = r.ReadLong();

            // cấp / EXP / điểm: nạp lại với chính các nút cây đang có (server không đổi cây ở đây)
            if (level != p.Progress.Level || exp != p.Progress.Exp || points != p.Progress.Points)
            {
                p.Progress.Save(rankBuffer);
                p.LoadProgress(level, exp, points, rankBuffer);
            }
            if (gold != p.Gold) p.SetGold(gold);
            if (p.data != null && maxHp > 0)
            {
                p.UpdateStats(TypeHP_KI.Type_Hp, curHp, maxHp);
                p.UpdateStats(TypeHP_KI.Type_MP, curMp, maxMp);
            }
        }

        // ---------------- skill / nâng cấp vũ khí / cây nhân vật ----------------

        static readonly List<UpgradeRank> upgradeBuffer = new List<UpgradeRank>();

        static void WriteBuild(MyWriter w, PlayerProfile p)
        {
            w.WriteByte((byte)PlayerProfile.SkillSlots);
            for (int i = 0; i < PlayerProfile.SkillSlots; i++)
            {
                PlayerSkill s = p.GetSkill(i);
                w.WriteShort(checked((short)(s != null ? s.templateId : 0)));
                w.WriteByte(checked((byte)(s != null ? s.level : 0)));
            }
            p.SaveUpgrades(upgradeBuffer);
            w.WriteShort(checked((short)upgradeBuffer.Count));
            foreach (UpgradeRank u in upgradeBuffer)
            {
                w.WriteShort(checked((short)u.weaponId));
                w.WriteShort(checked((short)u.upgradeId));
                w.WriteByte(checked((byte)u.rank));
            }
            p.Progress.Save(rankBuffer);
            w.WriteShort(checked((short)rankBuffer.Count));
            foreach (NodeRank n in rankBuffer)
            {
                w.WriteShort(checked((short)n.id));
                w.WriteByte(checked((byte)n.rank));
            }
        }

        static void ReadBuild(MyReader r, PlayerProfile p)
        {
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadShort(), level = r.ReadByte();
                PlayerSkill cur = p.GetSkill(i);
                bool same = id == 0 ? cur == null : cur != null && cur.templateId == id && cur.level == level;
                if (!same && i < PlayerProfile.SkillSlots) p.SetSkill(i, id, level);
            }

            n = r.ReadShort();
            var upgrades = new List<UpgradeRank>(n);
            for (int i = 0; i < n; i++)
                upgrades.Add(new UpgradeRank { weaponId = r.ReadShort(), upgradeId = r.ReadShort(), rank = r.ReadByte() });
            p.ApplyUpgrades(upgrades);

            n = r.ReadShort();
            var nodes = new List<NodeRank>(n);
            for (int i = 0; i < n; i++) nodes.Add(new NodeRank { id = r.ReadShort(), rank = r.ReadByte() });
            // cây khác bản đang có: nạp lại cây với cấp / EXP / điểm hiện tại (phần Stats đã đọc trước)
            bool differs = false;
            foreach (NodeRank node in nodes) if (p.Progress.Rank(node.id) != node.rank) differs = true;
            p.Progress.Save(rankBuffer);
            if (rankBuffer.Count != nodes.Count) differs = true;
            if (differs) p.LoadProgress(p.Progress.Level, p.Progress.Exp, p.Progress.Points, nodes);
        }
    }
}
