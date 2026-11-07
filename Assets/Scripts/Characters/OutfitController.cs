using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// Owns what the character wears: takes outfit requests (step a slot to its next or previous
    /// option) and applies them to the <see cref="LayeredCharacter"/>. UI calls <see cref="Cycle"/>
    /// and listens to <see cref="OnOutfitChanged"/>; it never dresses the character itself.
    /// Implements the outfit rules in design/game-brief.md.
    /// </summary>
    public sealed class OutfitController : MonoBehaviour
    {
        [SerializeField] private LayeredCharacter _character;
        [SerializeField] private LpcWardrobeCatalog _catalog;

        private Wardrobe _wardrobe;

        /// <summary>Raised after a slot changes; the part is null when the slot was emptied.</summary>
        public event Action<LpcSlot, LpcPart> OnOutfitChanged;

        /// <summary>The choices available per slot.</summary>
        public Wardrobe Wardrobe => _wardrobe ??= new Wardrobe(_catalog.Parts, _catalog.OptionalSlots);

        /// <summary>Sets the character and catalog. Used by tools and tests.</summary>
        public void Setup(LayeredCharacter character, LpcWardrobeCatalog catalog)
        {
            Unsubscribe();
            _character = character;
            _catalog = catalog;
            _wardrobe = null;
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        /// <summary>What a slot holds now, or null.</summary>
        public LpcPart Current(LpcSlot slot) => _character.GetPart(slot);

        /// <summary>Steps a slot to its next (+1) or previous (-1) option and dresses the character.</summary>
        public void Cycle(LpcSlot slot, int direction)
        {
            var chosen = Wardrobe.Step(slot, _character.GetPart(slot), direction);
            if (chosen == null) _character.Unequip(slot);
            else _character.Equip(chosen);
        }

        private void Subscribe()
        {
            if (_character != null) _character.OnPartChanged += Relay;
        }

        private void Unsubscribe()
        {
            if (_character != null) _character.OnPartChanged -= Relay;
        }

        private void Relay(LpcSlot slot, LpcPart part) => OnOutfitChanged?.Invoke(slot, part);
    }
}
