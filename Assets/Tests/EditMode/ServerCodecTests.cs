using DungeonOnline.Entities;
using DungeonOnline.Net;
using NetKit;
using NUnit.Framework;
using UnityEngine;

namespace DungeonOnline.Tests
{
    public class ServerCodecTests
    {
        [Test]
        public void ToMap_UsesXAndZAsHundredths()
        {
            // Act
            Vector2 pos = ServerCodec.ToMap(-700, GameCmd.GROUND_Y, -350);

            // Assert
            Assert.AreEqual(new Vector2(-7f, -3.5f), pos);
        }

        [Test]
        public void WritePos_ThenReadPos_RoundTrips()
        {
            // Arrange
            var w = new PacketWriter();
            var pos = new Vector2(3.25f, -1.5f);

            // Act
            ServerCodec.WritePos(w, pos);
            Vector2 back = ServerCodec.ReadPos(new PacketReader(w.ToArray()));

            // Assert
            Assert.AreEqual(pos, back);
        }

        [Test]
        public void WritePos_PutsGroundYInTheMiddle()
        {
            // Arrange
            var w = new PacketWriter();

            // Act
            ServerCodec.WritePos(w, new Vector2(1f, 2f));
            var r = new PacketReader(w.ToArray());

            // Assert
            Assert.AreEqual(100, r.ReadInt());
            Assert.AreEqual(GameCmd.GROUND_Y, r.ReadInt());
            Assert.AreEqual(200, r.ReadInt());
        }

        [TestCase(90, false)]
        [TestCase(270, true)]
        [TestCase(-90, true)]
        [TestCase(0, false)]
        public void IsFacingLeft_ByDegrees(int dir, bool left)
        {
            Assert.AreEqual(left, ServerCodec.IsFacingLeft(dir));
        }

        [TestCase(EntityState.Idle)]
        [TestCase(EntityState.Run)]
        [TestCase(EntityState.Jump)]
        [TestCase(EntityState.Fall)]
        public void FromEntityState_ThenToEntityState_RoundTrips(EntityState state)
        {
            // Act
            ServerCodec.FromEntityState(state, out int serverState, out int action);

            // Assert
            Assert.AreEqual(state, ServerCodec.ToEntityState(serverState, action));
        }

        [Test]
        public void ToEntityState_BotRunning_ReturnsRun()
        {
            // bots send STATE_MOVE with ACTION_RUN (Bots.java)
            Assert.AreEqual(EntityState.Run, ServerCodec.ToEntityState(ServerCodec.STATE_MOVE, ServerCodec.ACT_RUN));
        }

        [Test]
        public void WriteMove_WritesPosFacingAndState()
        {
            // Arrange
            var w = new PacketWriter();

            // Act
            ServerCodec.WriteMove(w, new Vector2(-2f, 0.5f), facingLeft: true, EntityState.Run);
            var r = new PacketReader(w.ToArray());

            // Assert
            Assert.AreEqual(new Vector2(-2f, 0.5f), ServerCodec.ReadPos(r));
            Assert.AreEqual(ServerCodec.DIR_LEFT, r.ReadInt());
            Assert.AreEqual(ServerCodec.STATE_MOVE, r.ReadByte());
            Assert.AreEqual(ServerCodec.ACT_RUN, r.ReadByte());
            Assert.AreEqual(2, r.Available);   // count, type
        }
    }
}
