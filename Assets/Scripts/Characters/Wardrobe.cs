using System.Collections.Generic;
using System.Linq;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// The choices for each equipment slot and how to step through them. Pure logic over a set of
    /// parts, so the outfit UI stays a thin view. Implements the outfit rules in design/game-brief.md.
    /// </summary>
    public sealed class Wardrobe
    {
        private readonly Dictionary<LpcSlot, List<LpcPart>> _options = new Dictionary<LpcSlot, List<LpcPart>>();

        /// <summary>
        /// Groups parts by slot, sorted by display name. An optional slot gets a leading null option
        /// meaning "nothing worn".
        /// </summary>
        public Wardrobe(IEnumerable<LpcPart> parts, IEnumerable<LpcSlot> optionalSlots)
        {
            foreach (var group in parts.Where(p => p != null).Distinct().GroupBy(p => p.Slot))
                _options[group.Key] = group.OrderBy(p => p.DisplayName).ToList();
            foreach (var slot in optionalSlots)
            {
                if (!_options.TryGetValue(slot, out var list)) _options[slot] = list = new List<LpcPart>();
                list.Insert(0, null);
            }
        }

        /// <summary>Slots that have at least one option, in slot order.</summary>
        public IEnumerable<LpcSlot> Slots => _options.Keys.OrderBy(s => s);

        /// <summary>The options for a slot (null = nothing worn), or an empty list.</summary>
        public IReadOnlyList<LpcPart> OptionsFor(LpcSlot slot) =>
            _options.TryGetValue(slot, out var list) ? list : (IReadOnlyList<LpcPart>)new List<LpcPart>();

        /// <summary>Whether a slot has more than one option to step through.</summary>
        public bool HasChoice(LpcSlot slot) => OptionsFor(slot).Count > 1;

        /// <summary>
        /// The option <paramref name="direction"/> steps away from <paramref name="current"/>, wrapping
        /// at either end. A current part that is not in the list counts as sitting before the first option.
        /// </summary>
        /// <param name="slot">The slot to step through.</param>
        /// <param name="current">What the slot holds now; null for nothing.</param>
        /// <param name="direction">+1 for next, -1 for previous.</param>
        /// <returns>The new option; null means take the slot's part off.</returns>
        public LpcPart Step(LpcSlot slot, LpcPart current, int direction)
        {
            var options = OptionsFor(slot);
            if (options.Count == 0) return current;
            var index = IndexOf(options, current);
            if (index < 0) return options[direction >= 0 ? 0 : options.Count - 1];
            var next = ((index + direction) % options.Count + options.Count) % options.Count;
            return options[next];
        }

        private static int IndexOf(IReadOnlyList<LpcPart> options, LpcPart part)
        {
            for (var i = 0; i < options.Count; i++)
                if (options[i] == part) return i;
            return -1;
        }
    }
}
