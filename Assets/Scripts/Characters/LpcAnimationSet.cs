using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// Playback data for LPC animations: which sheet each animation reads, which columns of that
    /// sheet it steps through, and how fast. The asset lives in Assets/Data.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcAnimations", menuName = "DungeonOnline/LPC Animation Set")]
    public sealed class LpcAnimationSet : ScriptableObject
    {
        /// <summary>One named animation.</summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>Name the game plays it by, e.g. "run" or "fall".</summary>
            public string Name;

            /// <summary>Sheet the frames come from, e.g. "jump" (matches the part's PNG name).</summary>
            public string Sheet;

            /// <summary>Sheet columns to show, in order. A column may repeat to hold it longer.</summary>
            public int[] Frames;

            /// <summary>Steps per second.</summary>
            [Min(0.01f)] public float FramesPerSecond = 10f;

            /// <summary>Whether it wraps; a one-shot holds its last step.</summary>
            public bool Loop = true;
        }

        /// <summary>All animations in this set.</summary>
        public List<Entry> Entries = new List<Entry>();

        /// <summary>Horizontal speed, in units per second, above which the player counts as moving.</summary>
        [Min(0f)] public float MovingSpeedThreshold = 0.1f;

        /// <summary>Finds an animation by name, or null.</summary>
        public Entry Find(string animationName)
        {
            foreach (var entry in Entries)
                if (entry.Name == animationName) return entry;
            return null;
        }
    }
}
