using System;

namespace CoinHop.Characters
{
    /// <summary>
    /// Pure frame timing for sprite animations: which step of a frame sequence shows at a given time.
    /// Kept free of Unity types so it is unit-testable.
    /// </summary>
    public static class LpcFrameClock
    {
        /// <summary>
        /// The step of a sequence to show after <paramref name="elapsed"/> seconds.
        /// A looping sequence wraps; a one-shot holds its last step.
        /// </summary>
        /// <param name="elapsed">Seconds since the sequence started; negative counts as zero.</param>
        /// <param name="framesPerSecond">Playback rate; must be positive.</param>
        /// <param name="stepCount">Number of steps in the sequence; must be positive.</param>
        /// <param name="loop">Whether the sequence wraps around.</param>
        /// <returns>An index in [0, stepCount).</returns>
        public static int StepAt(float elapsed, float framesPerSecond, int stepCount, bool loop)
        {
            if (framesPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            if (stepCount <= 0) throw new ArgumentOutOfRangeException(nameof(stepCount));

            var step = (int)Math.Floor(Math.Max(0f, elapsed) * framesPerSecond);
            return loop ? step % stepCount : Math.Min(step, stepCount - 1);
        }
    }
}
