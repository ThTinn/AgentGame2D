namespace DungeonOnline.Characters
{
    /// <summary>
    /// An equipment slot on a layered LPC character. One part per slot is worn at a time;
    /// equipping a part replaces whatever the slot held.
    /// </summary>
    public enum LpcSlot
    {
        /// <summary>The bare body; every character needs one.</summary>
        Body,
        /// <summary>The head drawn over the body.</summary>
        Head,
        /// <summary>The facial expression drawn over the head.</summary>
        Face,
        /// <summary>Hair.</summary>
        Hair,
        /// <summary>Shirts, jackets and other upper-body clothes.</summary>
        Torso,
        /// <summary>Pants, shorts and skirts.</summary>
        Legs,
        /// <summary>Shoes and boots.</summary>
        Feet,
        /// <summary>Hats, hoods and helmets.</summary>
        Hat,
    }
}
