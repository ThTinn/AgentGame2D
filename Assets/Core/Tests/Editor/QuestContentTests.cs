using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Enemies;
using Game.Items;
using Game.Npcs;
using Game.Story;

namespace Game.Tests
{
    /// <summary>
    /// Nội dung nhiệm vụ thật của game (Assets/_Project/Core/Data/Npcs/QuestDatabase.asset): dữ liệu hợp lệ, file cho server khớp
    /// asset, và chơi thử được từ đầu tới cuối với một người chơi mới (không việc nào kẹt).
    /// </summary>
    public class QuestContentTests
    {
        const string QuestAsset = "Assets/_Project/Core/Data/Npcs/QuestDatabase.asset";
        const string NpcAsset = "Assets/_Project/Core/Data/Npcs/NpcDatabase.asset";

        static T Load<T>(string path = null) where T : Object
        {
            if (path != null) return AssetDatabase.LoadAssetAtPath<T>(path);
            string guid = AssetDatabase.FindAssets("t:" + typeof(T).Name).FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        QuestDatabase quests;
        NpcDatabase npcs;

        [SetUp]
        public void SetUp()
        {
            quests = Load<QuestDatabase>(QuestAsset);
            npcs = Load<NpcDatabase>(NpcAsset);
            Assert.IsNotNull(quests, QuestAsset);
            Assert.IsNotNull(npcs, NpcAsset);
        }

        QuestWorld NewWorld() => new QuestWorld(quests, npcs, Load<ItemDatabase>(), Load<MonsterDatabase>(), ownTables: false);

        [Test]
        public void Data_IsValid()
        {
            List<string> problems = quests.Validate(npcs.npcs.Where(n => n != null).Select(n => n.name).ToList());
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void ServerFile_MatchesAsset()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Core/quests.bin");
            Assert.IsTrue(File.Exists(path), path);
            CollectionAssert.AreEqual(quests.ToBytes(), File.ReadAllBytes(path),
                "quests.bin cũ hơn QuestDatabase.asset: chạy Tools/Core/Quests/Export Server Data");
        }

        [Test]
        public void CollectAndRewardItems_ExistInItemDatabase()
        {
            ItemDatabase items = Load<ItemDatabase>();
            Assert.IsNotNull(items);
            var missing = new List<string>();
            foreach (QuestTemplate q in quests.quests)
            {
                foreach (QuestObjective o in q.objectives)
                    if (o.kind == ObjectiveKind.Collect && items.Find(o.target) == null) missing.Add(q.name + ": " + o.target);
                foreach (string item in new[] { q.item, q.bonusItem })
                    if (!string.IsNullOrEmpty(item) && items.Find(item) == null) missing.Add(q.name + ": " + item);
                foreach (QuestOutcome o in q.outcomes)
                    if (!string.IsNullOrEmpty(o.item) && items.Find(o.item) == null) missing.Add(q.name + "/" + o.name + ": " + o.item);
            }
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        /// <summary>
        /// Người chơi mới làm lần lượt mọi việc mở ra được (nhận, làm xong mục tiêu, trả; việc nhiều nhánh chọn nhánh đầu đủ
        /// điều kiện) cho tới khi hết việc. Cốt truyện chính phải xong hết; việc phụ / hằng ngày không làm được thì liệt kê ra.
        /// Chưa đủ thân thiện để nhận thì tặng thân thiện cho đủ (thân thiện là chuyện chơi lâu, không phải chỗ kẹt).
        /// </summary>
        [Test]
        public void FullPlaythrough_EveryMainQuestCanBeFinished()
        {
            using (QuestWorld w = NewWorld())
            {
                var log = new StringBuilder();
                bool progressed = true;
                for (int pass = 0; pass < 50 && progressed; pass++)
                {
                    progressed = false;
                    foreach (QuestTemplate q in quests.quests)
                    {
                        if (QuestSystem.StatusOf(q) != QuestStatus.Available || QuestSystem.TimesDone(q) > 0) continue;
                        if (!QuestSystem.TierAllows(q))
                        {
                            NpcTemplate giver = QuestSystem.NpcOf(q.giver);
                            QuestSystem.AddAffinity(q.giver, Affinity.FloorOf(q.minTier) - QuestSystem.AffinityOf(giver));
                        }
                        Assert.IsTrue(QuestSystem.Accept(q), "nhận " + q.name);
                        w.CompleteObjectives(q);
                        Assert.AreEqual(QuestStatus.Ready, QuestSystem.StatusOf(q), "làm xong mục tiêu " + q.name + "\n" + QuestSystem.ProgressText(q));

                        QuestOutcome pick = null;
                        if (QuestSystem.HasOutcomes(q))
                        {
                            pick = q.outcomes.FirstOrDefault(o => QuestSystem.Check(o.condition));
                            Assert.IsNotNull(pick, q.name + ": không cách giải quyết nào đủ điều kiện");
                        }
                        Assert.IsTrue(QuestSystem.TurnIn(q, pick, out QuestReward reward), "trả " + q.name);
                        log.Append(q.name).Append(pick != null ? " [" + pick.name + "]" : "").Append(" +").Append(reward.gold).Append("g\n");
                        progressed = true;
                    }
                }

                var unfinished = quests.quests.Where(q => QuestSystem.TimesDone(q) == 0).ToList();
                var mainLeft = unfinished.Where(q => q.category == QuestCategory.Main).Select(q => q.name + " (" + QuestSystem.StatusOf(q) + ")").ToList();
                Debug.Log("Quest playthrough:\n" + log + (unfinished.Count > 0
                    ? "Chưa làm được: " + string.Join(", ", unfinished.Select(q => q.name + " (" + QuestSystem.StatusOf(q) + ")"))
                    : "Mọi việc đều làm được."));
                Assert.IsEmpty(mainLeft, "cốt truyện chính kẹt: " + string.Join(", ", mainLeft));
                QuestTemplate tracked = QuestSystem.Tracked;
                Assert.IsFalse(tracked != null && tracked.category == QuestCategory.Main, "xong cốt truyện mà vẫn chỉ đường tới " + tracked?.name);
            }
        }
    }
}
