using DungeonOnline.Entities;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace DungeonOnline.Tests
{
    public class EntityTests
    {
        private const float Threshold = 0.1f;
        private const long MaxHp = 100;
        private const long MaxMp = 50;
        private const float Step = 0.1f;   // seconds per FollowMoves call

        /// <summary>Exposes the protected remote-movement step; no visual, no config (built-in fallbacks).</summary>
        private sealed class TestEntity : Entity
        {
            public void Follow(float deltaTime) => FollowMoves(deltaTime);
        }

        private GameObject _go;
        private TestEntity _entity;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Entity");
            _entity = _go.AddComponent<TestEntity>();
            _entity.SetInfo(new EntityData(7, "Bob", MaxHp, MaxHp, MaxMp, MaxMp));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ---------------- state from motion (was PlayerAnimationDriver.Choose) ----------------

        [Test]
        public void StateFromMotion_GroundedStill_ReturnsIdle()
        {
            Assert.AreEqual(EntityState.Idle, Entity.StateFromMotion(Vector2.zero, grounded: true, Threshold));
        }

        [Test]
        public void StateFromMotion_GroundedMovingLeft_ReturnsRun()
        {
            Assert.AreEqual(EntityState.Run, Entity.StateFromMotion(new Vector2(-3f, 0f), grounded: true, Threshold));
        }

        [Test]
        public void StateFromMotion_GroundedBelowThreshold_ReturnsIdle()
        {
            // drifting slower than the threshold still counts as standing
            Assert.AreEqual(EntityState.Idle, Entity.StateFromMotion(new Vector2(Threshold * 0.5f, 0f), grounded: true, Threshold));
        }

        [Test]
        public void StateFromMotion_AirborneRising_ReturnsJump()
        {
            Assert.AreEqual(EntityState.Jump, Entity.StateFromMotion(new Vector2(0f, 5f), grounded: false, Threshold));
        }

        [Test]
        public void StateFromMotion_AirborneDropping_ReturnsFall()
        {
            Assert.AreEqual(EntityState.Fall, Entity.StateFromMotion(new Vector2(2f, -1f), grounded: false, Threshold));
        }

        [Test]
        public void AnimationFor_EachState_ReturnsLpcName()
        {
            Assert.AreEqual(Entity.IDLE, Entity.AnimationFor(EntityState.Idle));
            Assert.AreEqual(Entity.RUN, Entity.AnimationFor(EntityState.Run));
            Assert.AreEqual(Entity.JUMP, Entity.AnimationFor(EntityState.Jump));
            Assert.AreEqual(Entity.FALL, Entity.AnimationFor(EntityState.Fall));
        }

        // ---------------- values ----------------

        [Test]
        public void SetInfo_CopiesSharedValues()
        {
            Assert.AreEqual(7, _entity.Id);
            Assert.AreEqual("Bob", _entity.DisplayName);
            Assert.AreEqual(MaxHp, _entity.Hp);
            Assert.AreEqual(MaxMp, _entity.MaxMp);
            Assert.IsFalse(_entity.IsDead);
        }

        [Test]
        public void UpdateStats_AboveMax_ClampsToMax()
        {
            // Act
            _entity.UpdateStats(MaxHp + 40, MaxHp, MaxMp + 1, MaxMp);

            // Assert
            Assert.AreEqual(MaxHp, _entity.Hp);
            Assert.AreEqual(MaxMp, _entity.Mp);
        }

        [Test]
        public void TakeDamage_ReducesHpAndRaisesStatsChanged()
        {
            // Arrange
            long reported = -1;
            _entity.StatsChanged += (hp, maxHp, mp, maxMp) => reported = hp;

            // Act
            _entity.TakeDamage(30);

            // Assert
            Assert.AreEqual(MaxHp - 30, _entity.Hp);
            Assert.AreEqual(MaxHp - 30, reported);
        }

        [Test]
        public void TakeDamage_ToZero_DiesOnce()
        {
            // Arrange
            int deaths = 0;
            _entity.Died += _ => deaths++;

            // Act
            _entity.TakeDamage(MaxHp);
            _entity.TakeDamage(MaxHp);

            // Assert
            Assert.IsTrue(_entity.IsDead);
            Assert.AreEqual(EntityState.Die, _entity.State);
            Assert.AreEqual(0, _entity.Hp);
            Assert.AreEqual(1, deaths);
        }

        [Test]
        public void Respawn_AfterDeath_RestoresFullHpAtPosition()
        {
            // Arrange
            _entity.TakeDamage(MaxHp);
            var spot = new Vector2(3f, 1f);

            // Act
            _entity.Respawn(spot);

            // Assert
            Assert.IsFalse(_entity.IsDead);
            Assert.AreEqual(MaxHp, _entity.Hp);
            Assert.AreEqual(MaxMp, _entity.Mp);
            Assert.AreEqual((Vector3)spot, _entity.transform.position);
        }

        // ---------------- remote movement ----------------

        [Test]
        public void FollowMoves_NearPoint_WalksThereAndTakesItsState()
        {
            // Arrange: 0.5 away, under the fallback snap distance
            var target = new Vector2(0.5f, 0f);
            _entity.PushMove(target, facingLeft: true, EntityState.Run);

            // Act: fallback speed covers 0.5 within a few steps
            for (int i = 0; i < 5; i++) _entity.Follow(Step);

            // Assert
            Assert.AreEqual((Vector3)target, _entity.transform.position);
            Assert.AreEqual(EntityState.Run, _entity.State);
            Assert.IsTrue(_entity.FacingLeft);
            Assert.AreEqual(0, _entity.PendingMoves);
        }

        [Test]
        public void FollowMoves_FarPoint_Snaps()
        {
            // Arrange: far beyond the fallback snap distance
            var target = new Vector2(100f, 0f);
            _entity.PushMove(target, facingLeft: false, EntityState.Run);

            // Act
            _entity.Follow(Step);

            // Assert
            Assert.AreEqual((Vector3)target, _entity.transform.position);
        }

        [Test]
        public void FollowMoves_QueueEmpty_GoesIdle()
        {
            // Arrange
            _entity.PushMove(Vector2.zero, facingLeft: false, EntityState.Run);
            _entity.Follow(Step);

            // Act
            _entity.Follow(Step);

            // Assert
            Assert.AreEqual(EntityState.Idle, _entity.State);
        }

        [Test]
        public void PushMove_WhenDead_IsIgnored()
        {
            // Arrange
            _entity.TakeDamage(MaxHp);

            // Act
            _entity.PushMove(Vector2.one, facingLeft: false, EntityState.Run);

            // Assert
            Assert.AreEqual(0, _entity.PendingMoves);
        }
    }
}
