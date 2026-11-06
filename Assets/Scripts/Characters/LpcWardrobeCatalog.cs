using System.Collections.Generic;
using UnityEngine;

namespace CoinHop.Characters
{
    /// <summary>
    /// Every part the player may wear, and which slots may be left empty. The asset lives in
    /// Assets/Data; Tools &gt; LPC &gt; Build Parts refills <see cref="Parts"/> from the part folders.
    /// </summary>
    [CreateAssetMenu(fileName = "LpcWardrobe", menuName = "Coin Hop/LPC Wardrobe Catalog")]
    public sealed class LpcWardrobeCatalog : ScriptableObject
    {
        /// <summary>All wearable parts.</summary>
        public List<LpcPart> Parts = new List<LpcPart>();

        /// <summary>Slots that can be emptied, e.g. a hat. The others always hold a part.</summary>
        public List<LpcSlot> OptionalSlots = new List<LpcSlot> { LpcSlot.Hat };
    }
}
