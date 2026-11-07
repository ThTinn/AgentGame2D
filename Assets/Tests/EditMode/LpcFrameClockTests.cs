using System;
using DungeonOnline.Characters;
using NUnit.Framework;

namespace DungeonOnline.Tests
{
    public class LpcFrameClockTests
    {
        private const float Fps = 10f;
        private const int Steps = 4;

        [Test]
        public void StepAt_Start_ReturnsFirstStep()
        {
            // Act
            var step = LpcFrameClock.StepAt(0f, Fps, Steps, loop: true);

            // Assert
            Assert.AreEqual(0, step);
        }

        [Test]
        public void StepAt_MidSequence_AdvancesByFrameRate()
        {
            // Act: 0.25 s at 10 fps is the third step
            var step = LpcFrameClock.StepAt(0.25f, Fps, Steps, loop: true);

            // Assert
            Assert.AreEqual(2, step);
        }

        [Test]
        public void StepAt_PastEndLooping_WrapsAround()
        {
            // Act: 0.45 s is step 4, which wraps to 0
            var step = LpcFrameClock.StepAt(0.45f, Fps, Steps, loop: true);

            // Assert
            Assert.AreEqual(0, step);
        }

        [Test]
        public void StepAt_PastEndOneShot_HoldsLastStep()
        {
            // Act
            var step = LpcFrameClock.StepAt(5f, Fps, Steps, loop: false);

            // Assert
            Assert.AreEqual(Steps - 1, step);
        }

        [Test]
        public void StepAt_NegativeTime_ReturnsFirstStep()
        {
            // Act
            var step = LpcFrameClock.StepAt(-1f, Fps, Steps, loop: true);

            // Assert
            Assert.AreEqual(0, step);
        }

        [Test]
        public void StepAt_ZeroSteps_Throws()
        {
            // Act / Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => LpcFrameClock.StepAt(0f, Fps, 0, loop: true));
        }
    }
}
