using System.Collections.Generic;
using DungeonOnline.Characters;
using UnityEngine;
using UnityEngine.UIElements;

namespace DungeonOnline.UI
{
    /// <summary>
    /// Temporary outfit panel: one row per slot with ‹ › buttons. Display only: a click sends a
    /// request to <see cref="OutfitController"/>, and labels update from its change event.
    /// Implements the outfit rules in design/game-brief.md.
    /// </summary>
    /// <remarks>
    /// Debug tool, not a shipping screen: English text outside localization (the game has none
    /// yet), mouse only, no gamepad navigation. Replace before release.
    /// </remarks>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WardrobeView : MonoBehaviour
    {
        // rows read head to toe
        private static readonly LpcSlot[] DisplayOrder =
        {
            LpcSlot.Hat, LpcSlot.Hair, LpcSlot.Face, LpcSlot.Head, LpcSlot.Torso, LpcSlot.Legs, LpcSlot.Feet, LpcSlot.Body,
        };

        private const string NothingWorn = "(none)";
        private const string HiddenRowsClass = "wardrobe__rows--hidden";

        [SerializeField] private OutfitController _outfit;

        private readonly Dictionary<LpcSlot, Label> _partLabels = new Dictionary<LpcSlot, Label>();
        private VisualElement _rows;
        private Button _toggle;

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _rows = root.Q<VisualElement>("wardrobe-rows");
            _toggle = root.Q<Button>("wardrobe-toggle");
            _toggle.clicked += ToggleRows;

            foreach (var slot in DisplayOrder)
                if (_outfit.Wardrobe.OptionsFor(slot).Count > 0) _rows.Add(BuildRow(slot));

            _outfit.OnOutfitChanged += ShowPart;
        }

        private void OnDestroy()
        {
            if (_outfit != null) _outfit.OnOutfitChanged -= ShowPart;
        }

        private VisualElement BuildRow(LpcSlot slot)
        {
            var row = new VisualElement();
            row.AddToClassList("wardrobe__row");

            var slotLabel = new Label(slot.ToString());
            slotLabel.AddToClassList("wardrobe__slot");
            var partLabel = new Label(NameOf(_outfit.Current(slot)));
            partLabel.AddToClassList("wardrobe__part");
            _partLabels[slot] = partLabel;

            row.Add(slotLabel);
            row.Add(StepButton("‹", slot, -1));
            row.Add(partLabel);
            row.Add(StepButton("›", slot, +1));
            return row;
        }

        private Button StepButton(string text, LpcSlot slot, int direction)
        {
            // not focusable, so Space keeps jumping instead of pressing the last clicked button
            var button = new Button(() => _outfit.Cycle(slot, direction)) { text = text, focusable = false };
            button.AddToClassList("wardrobe__step");
            button.SetEnabled(_outfit.Wardrobe.HasChoice(slot));
            return button;
        }

        private void ShowPart(LpcSlot slot, LpcPart part)
        {
            if (_partLabels.TryGetValue(slot, out var label)) label.text = NameOf(part);
        }

        private void ToggleRows()
        {
            _rows.ToggleInClassList(HiddenRowsClass);
            _toggle.text = _rows.ClassListContains(HiddenRowsClass) ? "Outfit ▸" : "Outfit ▾";
        }

        private static string NameOf(LpcPart part) => part != null ? part.DisplayName : NothingWorn;
    }
}
