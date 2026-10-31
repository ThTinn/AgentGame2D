using System;

namespace CoinHop.Gameplay
{
    /// <summary>
    /// Counts the coins collected in one level and detects the win.
    /// Implements the win condition in design/game-brief.md.
    /// </summary>
    public sealed class CoinCounter
    {
        /// <summary>Raised after each collected coin with (collected, total).</summary>
        public event Action<int, int> OnChanged;

        /// <summary>Raised once, when the last coin is collected.</summary>
        public event Action OnCompleted;

        /// <summary>Number of coins in the level.</summary>
        public int Total { get; }

        /// <summary>Number of coins collected so far.</summary>
        public int Collected { get; private set; }

        /// <summary>True once every coin has been collected.</summary>
        public bool IsComplete => Collected >= Total;

        /// <summary>Creates a counter for a level holding <paramref name="total"/> coins.</summary>
        public CoinCounter(int total)
        {
            if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total), "A level needs at least one coin.");
            Total = total;
        }

        /// <summary>Records one collected coin. Ignored once the level is complete.</summary>
        public void Collect()
        {
            if (IsComplete) return;

            Collected++;
            OnChanged?.Invoke(Collected, Total);
            if (IsComplete) OnCompleted?.Invoke();
        }
    }
}
