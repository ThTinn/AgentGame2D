using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// One wearable LPC layer (a body, a hat, a pair of shoes): the slot it fills, where it draws in
    /// the stack, and its right-facing frames for each sheet. Built by Tools &gt; LPC &gt; Build Parts.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcPart", menuName = "DungeonOnline/LPC Part")]
    public sealed class LpcPart : ScriptableObject
    {
        /// <summary>The frames cut from one sheet, indexed by sheet column.</summary>
        [Serializable]
        public sealed class Sheet
        {
            /// <summary>Sheet name, e.g. "run".</summary>
            public string Name;

            /// <summary>Right-facing frames; index = sheet column. A column can be null or empty.</summary>
            public Sprite[] Frames;
        }

        [SerializeField] private LpcSlot _slot;
        [SerializeField] private int _sortingOrder;
        [SerializeField] private string _displayName;
        [SerializeField] private List<Sheet> _sheets = new List<Sheet>();

        /// <summary>The slot this part fills.</summary>
        public LpcSlot Slot => _slot;

        /// <summary>Draw order within the character; higher draws on top (LPC zPos).</summary>
        public int SortingOrder => _sortingOrder;

        /// <summary>Name shown to players and in credits.</summary>
        public string DisplayName => _displayName;

        /// <summary>All sheets of this part.</summary>
        public IReadOnlyList<Sheet> Sheets => _sheets;

        /// <summary>Creates a part in memory, for tools and tests.</summary>
        public static LpcPart Create(LpcSlot slot, int sortingOrder, string displayName, IEnumerable<Sheet> sheets)
        {
            var part = CreateInstance<LpcPart>();
            part.Configure(slot, sortingOrder, displayName, sheets);
            part.name = displayName;
            return part;
        }

        /// <summary>Replaces this part's data. Used by the editor import tool.</summary>
        public void Configure(LpcSlot slot, int sortingOrder, string displayName, IEnumerable<Sheet> sheets)
        {
            _slot = slot;
            _sortingOrder = sortingOrder;
            _displayName = displayName;
            _sheets = new List<Sheet>(sheets);
        }

        /// <summary>
        /// The frame at a column of a sheet, or null when this part has no such sheet or column
        /// (the layer is then simply not drawn for that frame).
        /// </summary>
        public Sprite GetFrame(string sheetName, int column)
        {
            foreach (var sheet in _sheets)
            {
                if (sheet.Name != sheetName) continue;
                return sheet.Frames != null && column >= 0 && column < sheet.Frames.Length ? sheet.Frames[column] : null;
            }
            return null;
        }
    }
}
