using System;
using CoinHop.Gameplay;
using NUnit.Framework;

namespace CoinHop.Tests
{
    public class CoinCounterTests
    {
        [Test]
        public void Collect_OneCoin_RaisesChangedWithProgress()
        {
            // Arrange
            var counter = new CoinCounter(3);
            var reported = (collected: 0, total: 0);
            counter.OnChanged += (collected, total) => reported = (collected, total);

            // Act
            counter.Collect();

            // Assert
            Assert.AreEqual((1, 3), reported);
            Assert.IsFalse(counter.IsComplete);
        }

        [Test]
        public void Collect_LastCoin_RaisesCompletedOnce()
        {
            // Arrange
            var counter = new CoinCounter(2);
            var completedCount = 0;
            counter.OnCompleted += () => completedCount++;

            // Act
            counter.Collect();
            counter.Collect();
            counter.Collect();

            // Assert
            Assert.AreEqual(1, completedCount);
            Assert.IsTrue(counter.IsComplete);
        }

        [Test]
        public void Collect_AfterComplete_KeepsCount()
        {
            // Arrange
            var counter = new CoinCounter(1);
            counter.Collect();

            // Act
            counter.Collect();

            // Assert
            Assert.AreEqual(1, counter.Collected);
        }

        [Test]
        public void Constructor_NoCoins_Throws()
        {
            // Act and assert
            Assert.Throws<ArgumentOutOfRangeException>(() => new CoinCounter(0));
        }
    }
}
