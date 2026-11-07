using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Game.Enemies;
using Game.Items;
using Game.Npcs;
using Game.Players;
using Game.Story;

namespace Game.Tests
{
    /// <summary>
    /// Thế giới nhỏ cho test nhiệm vụ: đăng ký các bảng (NPC / vật phẩm / quái / nhiệm vụ) làm X.Current, dựng một người chơi
    /// mới và chạy QuestSystem trên người đó qua <see cref="CoreSystems.AsServer"/> (không cần scene, mạng hay file lưu;
    /// thông báo gom vào <see cref="toasts"/>). Dispose trả lại các bảng đang dùng trước đó và hủy các bảng tự tạo.
    /// </summary>
    sealed class QuestWorld : IDisposable
    {
        public readonly PlayerProfile me = new PlayerProfile();
        public readonly List<string> toasts = new List<string>();
        public readonly QuestDatabase quests;
        public readonly NpcDatabase npcs;
        public readonly ItemDatabase items;

        readonly List<KeyValuePair<FieldInfo, object>> previous = new List<KeyValuePair<FieldInfo, object>>();
        readonly List<ScriptableObject> owned = new List<ScriptableObject>();
        CoreSystems.ServerScope scope;

        /// <param name="ownTables">true: các bảng do test tạo, Dispose hủy luôn. false: asset thật, chỉ mượn.</param>
        public QuestWorld(QuestDatabase quests, NpcDatabase npcs, ItemDatabase items, MonsterDatabase monsters, bool ownTables)
        {
            this.quests = quests;
            this.npcs = npcs;
            this.items = items;
            Swap(typeof(QuestDatabase), quests);
            Swap(typeof(NpcDatabase), npcs);
            Swap(typeof(ItemDatabase), items);
            Swap(typeof(MonsterDatabase), monsters);
            if (ownTables)
                foreach (ScriptableObject so in new ScriptableObject[] { quests, npcs, items, monsters })
                    if (so != null) owned.Add(so);
            quests?.ClearCache();

            // như CoreSystems.Begin nối cho người chơi mình
            me.Story.BecameEnemy += QuestSystem.OnBecameEnemy;
            scope = CoreSystems.AsServer(me, toasts);
        }

        // X.Use() không cho trả về null và cảnh báo khi thay bảng: test gán thẳng trường tĩnh `current`
        void Swap(Type table, object value)
        {
            FieldInfo f = table.GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) throw new MissingFieldException(table.Name, "current");
            previous.Add(new KeyValuePair<FieldInfo, object>(f, f.GetValue(null)));
            f.SetValue(null, value);
        }

        public void Dispose()
        {
            scope.Dispose();
            me.Story.BecameEnemy -= QuestSystem.OnBecameEnemy;
            for (int i = previous.Count - 1; i >= 0; i--) previous[i].Key.SetValue(null, previous[i].Value);
            foreach (ScriptableObject so in owned) UnityEngine.Object.DestroyImmediate(so);
        }

        public QuestTemplate Quest(string name) => quests.Find(name);
        public QuestStatus Status(string name) => QuestSystem.StatusOf(Quest(name));
        public bool Accept(string name) => QuestSystem.Accept(Quest(name));

        public bool TurnIn(string name, string outcome = null) => TurnIn(name, outcome, out _);

        public bool TurnIn(string name, string outcome, out QuestReward reward)
        {
            QuestTemplate q = Quest(name);
            QuestOutcome o = null;
            if (outcome != null) foreach (QuestOutcome x in q.outcomes) if (x.name == outcome) o = x;
            return QuestSystem.TurnIn(q, o, out reward);
        }

        public int ItemId(string name) => items.Find(name).id;

        /// <summary>Làm cho xong mọi mục tiêu của một việc đang làm (hạ đủ quái, tới chỗ, gặp người, gom đủ đồ).</summary>
        public void CompleteObjectives(QuestTemplate q)
        {
            for (int i = 0; i < q.objectives.Count; i++)
            {
                QuestObjective o = q.objectives[i];
                switch (o.kind)
                {
                    case ObjectiveKind.Kill:
                        int id = QuestSystem.MonsterIdOf(o.target);
                        for (int k = QuestSystem.ProgressOf(q, i); k < o.count; k++) QuestSystem.OnMonsterKilled(id);
                        break;
                    case ObjectiveKind.Reach:
                        QuestSystem.OnPlayerAt(o.where.mapId, o.where.point);
                        break;
                    case ObjectiveKind.Talk:
                        QuestSystem.CompleteTalk(new QuestStep { quest = q, objective = i });
                        break;
                    case ObjectiveKind.Collect:
                        int have = QuestSystem.ProgressOf(q, i);
                        if (have < o.count) me.Inventory.Add(ItemId(o.target), o.count - have);
                        break;
                }
            }
        }

        // ---------------- dựng bảng ----------------

        public static NpcTemplate Npc(int id, string name, int startAffinity = 10) =>
            new NpcTemplate { id = id, name = name, displayName = name, startAffinity = startAffinity };

        public static QuestTemplate Q(int id, string name, string giver, params QuestObjective[] objectives)
        {
            var q = new QuestTemplate { id = id, name = name, title = name, giver = giver };
            q.objectives.AddRange(objectives);
            return q;
        }

        public static QuestObjective Kill(string target, int count) => new QuestObjective { kind = ObjectiveKind.Kill, target = target, count = count };
        public static QuestObjective Collect(string item, int count) => new QuestObjective { kind = ObjectiveKind.Collect, target = item, count = count };
        public static QuestObjective Talk(string npc) => new QuestObjective { kind = ObjectiveKind.Talk, target = npc, count = 1 };

        public static QuestObjective Reach(short map, Vector2 point, float radius) => new QuestObjective
        {
            kind = ObjectiveKind.Reach, target = "spot", count = 1,
            where = new QuestLocation { mapId = map, point = point, radius = radius },
        };

        public static T Table<T>(Action<T> fill) where T : ScriptableObject
        {
            var t = ScriptableObject.CreateInstance<T>();
            t.hideFlags = HideFlags.HideAndDontSave;
            fill(t);
            return t;
        }
    }
}
