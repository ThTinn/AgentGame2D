using DungeonOnline.Characters;
using NUnit.Framework;
using UnityEngine;

namespace DungeonOnline.Tests
{
    public class PlayerAnimationDriverTests
    {
        private const float Threshold = 0.1f;

        [Test]
        public void Choose_GroundedStill_ReturnsIdle()
        {
            // Act
            var name = PlayerAnimationDriver.Choose(Vector2.zero, grounded: true, Threshold);

            // Assert
            Assert.AreEqual(PlayerAnimationDriver.IDLE, name);
        }

        [Test]
        public void Choose_GroundedMovingLeft_ReturnsRun()
        {
            // Act
            var name = PlayerAnimationDriver.Choose(new Vector2(-3f, 0f), grounded: true, Threshold);

            // Assert
            Assert.AreEqual(PlayerAnimationDriver.RUN, name);
        }

        [Test]
        public void Choose_GroundedBelowThreshold_ReturnsIdle()
        {
            // Act: drifting slower than the threshold still counts as standing
            var name = PlayerAnimationDriver.Choose(new Vector2(Threshold * 0.5f, 0f), grounded: true, Threshold);

            // Assert
            Assert.AreEqual(PlayerAnimationDriver.IDLE, name);
        }

        [Test]
        public void Choose_AirborneRising_ReturnsJump()
        {
            // Act
            var name = PlayerAnimationDriver.Choose(new Vector2(0f, 5f), grounded: false, Threshold);

            // Assert
            Assert.AreEqual(PlayerAnimationDriver.JUMP, name);
        }

        [Test]
        public void Choose_AirborneDropping_ReturnsFall()
        {
            // Act
            var name = PlayerAnimationDriver.Choose(new Vector2(2f, -1f), grounded: false, Threshold);

            // Assert
            Assert.AreEqual(PlayerAnimationDriver.FALL, name);
        }
    }
}
