using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Enemies;
using Game.Items;
using Game.Npcs;
using Game.Players;
using Game.Story;
using static Game.Tests.QuestWorld;

namespace Game.Tests
{
    /// <summary>
    /// Luật nhiệm vụ của <see cref="QuestSystem"/> trên một bảng nhỏ dựng trong test (không phụ thuộc nội dung game):
    /// nhận / trả, từng loại mục tiêu, việc hằng ngày / lặp lại, bậc thân thiện, kẻ thù, cách giải quyết, lựa chọn,
    /// theo dõi, đọc / ghi.
    /// </summary>
    public class QuestSystemTests
    {
        const int Gem = 1, Key = 2, Slime = 7;

        QuestWorld w;

        [SetUp]
        public void SetUp()
        {
            NpcDatabase npcs = Table<NpcDatabase>(t =>
            {
                t.npcs.Add(Npc(1, "elder"));
                t.npcs.Add(Npc(2, "smith"));
                t.npcs.Add(Npc(3, "sage"));
            });
            ItemDatabase items = Table<ItemDatabase>(t =>
            {
                t.items.Add(new ItemTemplate { id = Gem, name = "gem", maxStack = 99 });
                t.items.Add(new ItemTemplate { id = Key, name = "key", maxStack = 99 });
            });
            MonsterDatabase monsters = Table<MonsterDatabase>(t => t.monsters.Add(new MonsterTemplate { id = Slime, name = "slime" }));

            QuestDatabase quests = Table<QuestDatabase>(t =>
            {
                var deliver = Q(1, "deliver", "elder");
                deliver.turnIn = "smith";
                deliver.gold = 100;
                t.quests.Add(deliver);

                var hunt = Q(2, "hunt", "elder", Kill("slime", 3));
                hunt.requires = "deliver";
                t.quests.Add(hunt);

                t.quests.Add(Q(3, "hunt_by_id", "elder", Kill(Slime.ToString(), 1)));

                var gather = Q(4, "gather", "smith", Collect("gem", 2));
                gather.item = "key";
                t.quests.Add(gather);

                t.quests.Add(Q(5, "scout", "sage", Reach(1, new Vector2(10f, 10f), 3f)));
                t.quests.Add(Q(6, "ask", "elder", Talk("smith")));

                var chore = Q(7, "chore", "sage");
                chore.category = QuestCategory.Daily;
                t.quests.Add(chore);

                var errand = Q(8, "errand", "sage");
                errand.repeatable = true;
                t.quests.Add(errand);

                var secret = Q(9, "secret", "elder");
                secret.minTier = AffinityTier.Trusted;
                t.quests.Add(secret);

                var verdict = Q(10, "verdict", "sage");
                verdict.outcomes.Add(new QuestOutcome { name = "spare", label = "spare", gold = 50 });
                var punish = new QuestOutcome { name = "punish", label = "punish", ending = "End A" };
                punish.effects.Add(new StoryEffect { kind = EffectKind.MakeEnemy, target = "smith" });
                verdict.outcomes.Add(punish);
                var hidden = new QuestOutcome { name = "hidden", label = "hidden" };
                hidden.condition.requiresFlag = "clue";
                verdict.outcomes.Add(hidden);
                t.quests.Add(verdict);

                var intro = Q(11, "intro", "sage");
                var agree = new StoryChoice { name = "agree", label = "agree", flag = "chose_agree" };
                agree.effects.Add(new StoryEffect { kind = EffectKind.SetFlag, target = "pass" });
                agree.effects.Add(new StoryEffect { kind = EffectKind.Affinity, target = "sage", amount = 5 });
                intro.offerChoices.Add(agree);
                t.quests.Add(intro);

                var gate = Q(12, "gate", "sage");
                gate.condition.requiresFlag = "pass";
                t.quests.Add(gate);

                var story = Q(13, "story", "sage", Kill("slime", 1));
                story.category = QuestCategory.Main;
                t.quests.Add(story);

                var pay = Q(14, "pay", "smith", Collect("gem", 2));
                var fee = new QuestOutcome { name = "fee", label = "fee" };
                fee.condition.item = "gem";
                fee.condition.itemCount = 1;
                pay.outcomes.Add(fee);
                t.quests.Add(pay);

                var visit = Q(15, "visit", "sage", Talk("smith"));
                t.quests.Add(visit);
            });

            w = new QuestWorld(quests, npcs, items, monsters, ownTables: true);
        }

        [TearDown]
        public void TearDown() => w.Dispose();

        // ---------------- nhận / trả ----------------

        [Test]
        public void Delivery_IsReadyOnAccept_AndPaysRewardScaledByTier()
        {
            Assert.AreEqual(QuestStatus.Available, w.Status("deliver"));
            Assert.IsTrue(w.Accept("deliver"));
            Assert.AreEqual(QuestStatus.Ready, w.Status("deliver"), "việc đưa tin không có mục tiêu: nhận xong là chờ trả");
            Assert.AreEqual(NpcMark.Ready, QuestSystem.MarkOf(w.npcs.Find("smith")));

            Assert.IsTrue(w.TurnIn("deliver", null, out QuestReward reward));
            // thân thiện 10 với người giao = Xa lạ, hệ số 0.8
            Assert.AreEqual(AffinityTier.Stranger, reward.tier);
            Assert.AreEqual(80, reward.gold);
            Assert.AreEqual(80, w.me.Gold);
            Assert.AreEqual(QuestStatus.Done, w.Status("deliver"));
            Assert.IsTrue(w.me.Story.HasFlag(StoryState.DoneFlag + "deliver"));
            Assert.IsFalse(w.TurnIn("deliver"), "đã trả rồi thì không trả lần nữa");
        }

        [Test]
        public void Requires_LocksUntilPreviousQuestIsTurnedIn()
        {
            Assert.AreEqual(QuestStatus.Locked, w.Status("hunt"));
            Assert.IsFalse(w.Accept("hunt"));
            w.Accept("deliver");
            Assert.AreEqual(QuestStatus.Locked, w.Status("hunt"), "mới nhận việc trước, chưa trả");
            w.TurnIn("deliver");
            Assert.AreEqual(QuestStatus.Available, w.Status("hunt"));
        }

        [Test]
        public void Accept_RefusesWhenNotAvailableOrAlreadyActive()
        {
            Assert.IsTrue(w.Accept("hunt_by_id"));
            Assert.IsFalse(w.Accept("hunt_by_id"));
            Assert.IsFalse(QuestSystem.Accept(null));
        }

        // ---------------- mục tiêu ----------------

        [Test]
        public void Kill_CountsByNameAndById_ClampsAtCount_IgnoresOtherMonsters()
        {
            w.Accept("deliver");
            w.TurnIn("deliver");
            w.Accept("hunt");
            w.Accept("hunt_by_id");
            QuestTemplate hunt = w.Quest("hunt");

            QuestSystem.OnMonsterKilled(99);
            Assert.AreEqual(0, QuestSystem.ProgressOf(hunt, 0));

            QuestSystem.OnMonsterKilled(Slime);
            Assert.AreEqual(1, QuestSystem.ProgressOf(hunt, 0));
            Assert.AreEqual(QuestStatus.Ready, w.Status("hunt_by_id"), "mục tiêu ghi id bằng số");
            Assert.AreEqual(QuestStatus.Active, w.Status("hunt"));

            for (int i = 0; i < 5; i++) QuestSystem.OnMonsterKilled(Slime);
            Assert.AreEqual(3, QuestSystem.ProgressOf(hunt, 0), "không vượt quá count");
            Assert.AreEqual(QuestStatus.Ready, w.Status("hunt"));
        }

        [Test]
        public void Kill_DoesNotCountBeforeAccepting()
        {
            QuestSystem.OnMonsterKilled(Slime);
            w.Accept("hunt_by_id");
            Assert.AreEqual(QuestStatus.Active, w.Status("hunt_by_id"));
        }

        [Test]
        public void Collect_CountsBag_TakesItemsOnTurnIn_GivesReward()
        {
            w.Accept("gather");
            Assert.AreEqual(QuestStatus.Active, w.Status("gather"));
            w.me.Inventory.Add(Gem, 1);
            Assert.AreEqual(QuestStatus.Active, w.Status("gather"));
            w.me.Inventory.Add(Gem, 2);
            Assert.AreEqual(QuestStatus.Ready, w.Status("gather"));
            Assert.AreEqual(2, QuestSystem.ProgressOf(w.Quest("gather"), 0), "tiến độ kẹp ở count");

            Assert.IsTrue(w.TurnIn("gather", null, out QuestReward reward));
            Assert.AreEqual(1, w.me.Inventory.Count(Gem), "chỉ lấy đúng số cần nộp");
            Assert.AreEqual(1, w.me.Inventory.Count(Key));
            Assert.IsNotNull(reward.items);
        }

        [Test]
        public void Collect_DroppingItemsAfterReadyGoesBackToActive()
        {
            w.Accept("gather");
            w.me.Inventory.Add(Gem, 2);
            Assert.AreEqual(QuestStatus.Ready, w.Status("gather"));
            w.me.Inventory.Remove(Gem, 1);
            Assert.AreEqual(QuestStatus.Active, w.Status("gather"));
            Assert.IsFalse(w.TurnIn("gather"));
        }

        [Test]
        public void Reach_NeedsRightMapAndInsideRadius()
        {
            w.Accept("scout");
            Assert.AreEqual(GoalKind.Place, QuestSystem.GoalOf(w.Quest("scout")).kind);

            QuestSystem.OnPlayerAt(2, new Vector2(10f, 10f));
            Assert.AreEqual(QuestStatus.Active, w.Status("scout"), "sai map");
            QuestSystem.OnPlayerAt(1, new Vector2(20f, 20f));
            Assert.AreEqual(QuestStatus.Active, w.Status("scout"), "ngoài bán kính");
            QuestSystem.OnPlayerAt(1, new Vector2(11f, 11f));
            Assert.AreEqual(QuestStatus.Ready, w.Status("scout"));
        }

        [Test]
        public void Talk_MarksTargetNpc_CompletesOnce()
        {
            w.Accept("ask");
            NpcTemplate smith = w.npcs.Find("smith");
            Assert.AreEqual(NpcMark.Ready, QuestSystem.MarkOf(smith), "NPC cần gặp có dấu ?");
            Assert.AreEqual(GoalKind.Npc, QuestSystem.GoalOf(w.Quest("ask")).kind);

            var steps = new List<QuestStep>();
            QuestSystem.TalksAt(smith, steps);
            Assert.AreEqual(1, steps.Count);
            Assert.IsTrue(QuestSystem.CompleteTalk(steps[0]));
            Assert.IsFalse(QuestSystem.CompleteTalk(steps[0]), "gặp rồi thì thôi");
            Assert.AreEqual(QuestStatus.Ready, w.Status("ask"));
            Assert.AreEqual("elder", QuestSystem.GoalOf(w.Quest("ask")).npc, "xong thì chỉ đường về người trả");
        }

        // ---------------- hằng ngày / lặp lại ----------------

        [Test]
        public void Daily_DoneUntilTomorrow_ThenAvailableAgain()
        {
            w.Accept("chore");
            w.TurnIn("chore");
            Assert.AreEqual(QuestStatus.Done, w.Status("chore"));
            Assert.IsFalse(w.Accept("chore"));

            w.me.Story.Get("chore").lastDay = StoryState.Today - 1;   // giả như trả hôm qua
            Assert.AreEqual(QuestStatus.Available, w.Status("chore"));
            Assert.IsTrue(w.Accept("chore"));
            Assert.IsTrue(w.TurnIn("chore"));
            Assert.AreEqual(2, QuestSystem.TimesDone(w.Quest("chore")));
        }

        [Test]
        public void Repeatable_AvailableRightAfterTurnIn_ListedAsDone()
        {
            w.Accept("errand");
            w.TurnIn("errand");
            Assert.AreEqual(QuestStatus.Available, w.Status("errand"));
            var done = new List<QuestTemplate>();
            QuestSystem.QuestsWith(done, QuestStatus.Done);
            CollectionAssert.Contains(done, w.Quest("errand"));
        }

        // ---------------- thân thiện / kẻ thù ----------------

        [Test]
        public void MinTier_ShowsButRefusesUntilTrusted()
        {
            NpcTemplate elder = w.npcs.Find("elder");
            Assert.AreEqual(QuestStatus.Available, w.Status("secret"));
            Assert.IsFalse(QuestSystem.TierAllows(w.Quest("secret")));
            Assert.IsFalse(w.Accept("secret"));

            QuestSystem.AddAffinity("elder", 55);   // 10 + 55 = 65: Tin cậy
            Assert.AreEqual(AffinityTier.Trusted, QuestSystem.TierOf(elder));
            Assert.IsTrue(w.Accept("secret"));
        }

        [Test]
        public void Abandon_CostsAffinity_AndFirstTimeForgetsQuest()
        {
            w.Accept("hunt_by_id");
            QuestSystem.Abandon(w.Quest("hunt_by_id"));
            Assert.AreEqual(10 - Affinity.AbandonPenalty, QuestSystem.AffinityOf(w.npcs.Find("elder")));
            Assert.IsNull(w.me.Story.Get("hunt_by_id"));
            Assert.AreEqual(QuestStatus.Available, w.Status("hunt_by_id"));
        }

        [Test]
        public void Enemy_CancelsQuestsGivenToOrTurnedInTo_AndPendingTalks()
        {
            w.Accept("deliver");     // trả cho smith
            w.Accept("ask");         // elder giao, phải gặp smith
            w.Accept("gather");      // smith giao
            w.Accept("hunt_by_id");  // không dính smith

            QuestSystem.AddAffinity("smith", -100);
            Assert.IsTrue(QuestSystem.IsEnemy("smith"));
            Assert.AreEqual(NpcMark.Enemy, QuestSystem.MarkOf(w.npcs.Find("smith")));

            Assert.AreEqual(QuestStatus.Locked, w.Status("deliver"));
            Assert.AreEqual(QuestStatus.Locked, w.Status("gather"));
            Assert.IsNull(w.me.Story.Get("ask"), "việc chờ gặp kẻ thù bị hủy, không kẹt mãi");
            Assert.AreEqual(QuestStatus.Locked, w.Status("ask"), "và không nhận lại được");
            Assert.AreEqual(QuestStatus.Active, w.Status("hunt_by_id"));
        }

        [Test]
        public void Enemy_IsPermanent()
        {
            QuestSystem.AddAffinity("smith", -100);
            Assert.AreEqual(0, QuestSystem.AddAffinity("smith", 100));
            Assert.AreEqual(AffinityTier.Enemy, QuestSystem.TierOf(w.npcs.Find("smith")));
        }

        // ---------------- cách giải quyết / lựa chọn ----------------

        [Test]
        public void Outcomes_RequireAValidChoice_ApplyEffectsAndEnding()
        {
            w.Accept("verdict");
            Assert.IsFalse(w.TurnIn("verdict"), "có nhiều cách giải quyết: phải chọn một");
            Assert.IsFalse(w.TurnIn("verdict", "hidden"), "chưa đủ điều kiện");

            Assert.IsTrue(w.TurnIn("verdict", "punish", out QuestReward reward));
            Assert.AreEqual("End A", reward.ending);
            Assert.IsTrue(QuestSystem.IsEnemy("smith"));
            StringAssert.Contains("smith", reward.enemies);
            Assert.AreEqual("punish", QuestSystem.OutcomeOf(w.Quest("verdict")).name);
        }

        [Test]
        public void Outcome_PayingSameItemAsCollect_NeedsBothAmounts()
        {
            w.Accept("pay");
            w.me.Inventory.Add(Gem, 2);   // đủ mục tiêu (2) và đủ điều kiện (1) khi xét riêng, chưa đủ khi cộng lại (3)
            Assert.AreEqual(QuestStatus.Ready, w.Status("pay"));
            Assert.IsFalse(w.TurnIn("pay", "fee"));
            Assert.AreEqual(2, w.me.Inventory.Count(Gem), "trả hụt thì không lấy gì");

            w.me.Inventory.Add(Gem, 1);
            Assert.IsTrue(w.TurnIn("pay", "fee"));
            Assert.AreEqual(0, w.me.Inventory.Count(Gem));
        }

        [Test]
        public void Choice_SetsFlags_OnlyOnce_UnlocksConditionalQuest()
        {
            Assert.AreEqual(QuestStatus.Locked, w.Status("gate"));
            StoryChoice agree = w.Quest("intro").offerChoices[0];
            Assert.AreEqual("quest:intro", QuestSystem.OwnerOf(agree));
            Assert.AreSame(agree, QuestSystem.FindChoice("quest:intro", "agree"));

            Assert.IsTrue(QuestSystem.Choose(agree, out string summary));
            Assert.IsNotNull(summary);
            Assert.AreEqual(15, QuestSystem.AffinityOf(w.npcs.Find("sage")));
            Assert.IsFalse(QuestSystem.IsShown(agree));
            Assert.IsFalse(QuestSystem.Choose(agree, out _), "lựa chọn có cờ chỉ chọn một lần");
            Assert.AreEqual(QuestStatus.Available, w.Status("gate"));
        }

        // ---------------- theo dõi ----------------

        [Test]
        public void Tracking_MainQuestTakesOver_TurnInRetracks()
        {
            w.Accept("hunt_by_id");
            Assert.AreEqual("hunt_by_id", w.me.Story.Tracked, "chưa theo gì: theo việc vừa nhận");
            w.Accept("errand");
            Assert.AreEqual("hunt_by_id", w.me.Story.Tracked, "việc phụ không giành chỗ");
            w.Accept("story");
            Assert.AreEqual("story", w.me.Story.Tracked, "việc chính tuyến được theo luôn");

            QuestSystem.OnMonsterKilled(Slime);   // xong cả story lẫn hunt_by_id
            Assert.AreSame(w.Quest("story"), QuestSystem.Suggest(), "chính tuyến chờ trả đứng đầu");
            w.TurnIn("story");
            Assert.IsNotNull(QuestSystem.Tracked, "trả việc đang theo: tự theo việc nên làm tiếp");
            Assert.AreNotEqual("story", w.me.Story.Tracked);
        }

        [Test]
        public void Track_RejectsLockedOrDoneQuests()
        {
            Assert.IsFalse(QuestSystem.Track(w.Quest("hunt")), "đang khóa");
            Assert.IsTrue(QuestSystem.Track(w.Quest("deliver")));
            Assert.IsTrue(QuestSystem.Track(null));
            Assert.IsNull(w.me.Story.Tracked);
        }

        // ---------------- đọc / ghi ----------------

        [Test]
        public void ServerBytes_RoundTripKeepsLogic()
        {
            byte[] bytes = w.quests.ToBytes();
            var back = new List<QuestTemplate>();
            Assert.IsTrue(QuestDatabase.Read(bytes, back, out int schema, out string error), error);
            Assert.AreEqual(QuestDatabase.CurrentSchema, schema);
            Assert.AreEqual(w.quests.quests.Count, back.Count);
            for (int i = 0; i < back.Count; i++)
            {
                QuestTemplate a = w.quests.quests[i], b = back[i];
                Assert.AreEqual(a.name, b.name);
                Assert.AreEqual(a.category, b.category);
                Assert.AreEqual(a.repeatable, b.repeatable);
                Assert.AreEqual(a.objectives.Count, b.objectives.Count, a.name);
                for (int k = 0; k < a.objectives.Count; k++)
                {
                    Assert.AreEqual(a.objectives[k].kind, b.objectives[k].kind);
                    Assert.AreEqual(a.objectives[k].target, b.objectives[k].target);
                    Assert.AreEqual(a.objectives[k].where.mapId, b.objectives[k].where.mapId);
                }
                Assert.AreEqual(a.outcomes.Count, b.outcomes.Count, a.name);
            }
            CollectionAssert.IsEmpty(w.quests.Validate(new List<string> { "elder", "smith", "sage" }));
        }

        [Test]
        public void StoryState_SaveLoadKeepsProgressFlagsAndTracking()
        {
            w.Accept("deliver");
            w.TurnIn("deliver");
            w.Accept("hunt");
            QuestSystem.OnMonsterKilled(Slime);
            QuestSystem.OnMonsterKilled(Slime);
            QuestSystem.AddAffinity("smith", 7);
            QuestSystem.Track(w.Quest("hunt"));

            var affinity = new List<AffinityRecord>();
            var records = new List<QuestRecord>();
            var flags = new List<string>();
            w.me.Story.Save(affinity, records, flags);
            var copy = new StoryState();
            copy.Load(affinity, records, flags, w.me.Story.Tracked);

            Assert.AreEqual(2, copy.Get("hunt").Progress(0));
            Assert.AreEqual(1, copy.Get("deliver").timesDone);
            Assert.IsTrue(copy.HasFlag(StoryState.DoneFlag + "deliver"));
            Assert.AreEqual(17, copy.GetAffinity("smith", 10));
            Assert.AreEqual("hunt", copy.Tracked);
        }

        [Test]
        public void ProfileSync_RoundTripCopiesStoryBagGold_KeepsSessionGreetings()
        {
            w.Accept("deliver");
            w.TurnIn("deliver");
            w.Accept("hunt");
            QuestSystem.OnMonsterKilled(Slime);
            w.me.Inventory.Add(Gem, 5);

            // client online nhận bản chuẩn của server (PROFILE_SYNC) đè lên bản của mình
            var client = new PlayerProfile();
            client.Story.MarkGreeted("elder");
            byte[] block = ProfileSync.Write(w.me, ProfileSync.All);
            Assert.IsTrue(ProfileSync.Apply(client, block, out byte parts, out string error), error);

            Assert.AreEqual(ProfileSync.All, parts);
            Assert.AreEqual(1, client.Story.Get("hunt").Progress(0));
            Assert.AreEqual(1, client.Story.Get("deliver").timesDone);
            Assert.IsTrue(client.Story.HasFlag(StoryState.DoneFlag + "deliver"));
            Assert.AreEqual(w.me.Story.Tracked, client.Story.Tracked);
            Assert.AreEqual(5, client.Inventory.Count(Gem));
            Assert.AreEqual(w.me.Gold, client.Gold);
            Assert.IsFalse(client.Story.MarkGreeted("elder"), "đồng bộ không xóa việc đã chào trong phiên (chào lại không được cộng thân thiện lần nữa)");
        }

        [Test]
        public void NotifyChanged_OutsidePlayMode_FiresImmediately()
        {
            int fired = 0;
            System.Action count = () => fired++;
            QuestSystem.Changed += count;
            try
            {
                QuestSystem.NotifyChanged();
                Assert.AreEqual(1, fired);
            }
            finally { QuestSystem.Changed -= count; }
        }
    }
}
