using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// Draws an LPC character as a stack of parts, one SpriteRenderer per slot, all showing the same
    /// animation frame. Swap clothes with <see cref="Equip"/> and <see cref="Unequip"/>; the
    /// animation keeps playing because every part shares the same frame grid.
    /// Implements the character section of design/game-brief.md.
    /// </summary>
    public sealed class LayeredCharacter : MonoBehaviour
    {
        [SerializeField] private LpcAnimationSet _animations;
        [SerializeField] private List<LpcPart> _startingParts = new List<LpcPart>();
        [SerializeField] private string _startingAnimation = "idle";
        [SerializeField] private string _sortingLayerName = "Default";
        [SerializeField] private int _sortingOrderBase;

        private readonly Dictionary<LpcSlot, LpcPart> _worn = new Dictionary<LpcSlot, LpcPart>();
        private readonly Dictionary<LpcSlot, SpriteRenderer> _renderers = new Dictionary<LpcSlot, SpriteRenderer>();

        private LpcAnimationSet.Entry _current;
        private float _elapsed;
        private bool _facingLeft;
        private bool _initialized;

        /// <summary>Raised after a slot's part changes; the part is null when the slot was emptied.</summary>
        public event Action<LpcSlot, LpcPart> OnPartChanged;

        /// <summary>The animation data this character plays from.</summary>
        public LpcAnimationSet Animations => _animations;

        /// <summary>Name of the animation playing, or null.</summary>
        public string CurrentAnimation => _current?.Name;

        /// <summary>Whether the character is drawn mirrored to face left.</summary>
        public bool FacingLeft => _facingLeft;

        /// <summary>The sheet column showing right now, or -1 when nothing plays.</summary>
        public int CurrentColumn { get; private set; } = -1;

        /// <summary>Sets the animation data and starting outfit. Call before the first frame; used by tools and tests.</summary>
        public void Setup(LpcAnimationSet animations, IEnumerable<LpcPart> startingParts, string startingAnimation = "idle")
        {
            _animations = animations;
            _startingParts = new List<LpcPart>(startingParts);
            _startingAnimation = startingAnimation;
            _initialized = false;
            EnsureInitialized();
        }

        private void Awake() => EnsureInitialized();

        private void Update() => Tick(Time.deltaTime);

        /// <summary>The part worn in a slot, or null.</summary>
        public LpcPart GetPart(LpcSlot slot) => _worn.TryGetValue(slot, out var part) ? part : null;

        /// <summary>The renderer that draws a slot, or null when that slot was never used.</summary>
        public SpriteRenderer GetRenderer(LpcSlot slot) => _renderers.TryGetValue(slot, out var r) ? r : null;

        /// <summary>Wears a part, replacing whatever its slot held. Null is ignored.</summary>
        public void Equip(LpcPart part)
        {
            if (part == null) return;
            EnsureInitialized();
            _worn[part.Slot] = part;
            var renderer = RendererFor(part.Slot);
            renderer.sortingOrder = _sortingOrderBase + part.SortingOrder;
            ApplyFrame(part.Slot);
            OnPartChanged?.Invoke(part.Slot, part);
        }

        /// <summary>Takes off whatever a slot holds. Does nothing when the slot is empty.</summary>
        public void Unequip(LpcSlot slot)
        {
            if (!_worn.Remove(slot)) return;
            if (_renderers.TryGetValue(slot, out var renderer)) renderer.sprite = null;
            OnPartChanged?.Invoke(slot, null);
        }

        /// <summary>
        /// Plays an animation by name. Playing the one already running keeps its timing;
        /// an unknown name is logged and ignored.
        /// </summary>
        public void Play(string animationName)
        {
            EnsureInitialized();
            if (_current != null && _current.Name == animationName) return;
            var entry = _animations != null ? _animations.Find(animationName) : null;
            if (entry == null || entry.Frames == null || entry.Frames.Length == 0)
            {
                Debug.LogWarning($"LayeredCharacter: no animation '{animationName}'", this);
                return;
            }
            _current = entry;
            _elapsed = 0f;
            ApplyAllFrames();
        }

        /// <summary>Mirrors every layer to face left or right.</summary>
        public void SetFacingLeft(bool facingLeft)
        {
            if (_facingLeft == facingLeft) return;
            _facingLeft = facingLeft;
            foreach (var renderer in _renderers.Values) renderer.flipX = facingLeft;
        }

        /// <summary>Advances the animation by <paramref name="deltaTime"/> seconds and redraws.</summary>
        public void Tick(float deltaTime)
        {
            if (_current == null) return;
            _elapsed += deltaTime;
            ApplyAllFrames();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            foreach (var part in _startingParts) Equip(part);
            if (!string.IsNullOrEmpty(_startingAnimation)) Play(_startingAnimation);
        }

        private void ApplyAllFrames()
        {
            foreach (var slot in _worn.Keys) ApplyFrame(slot);
        }

        private void ApplyFrame(LpcSlot slot)
        {
            if (_current == null || !_worn.TryGetValue(slot, out var part)) return;
            var step = LpcFrameClock.StepAt(_elapsed, _current.FramesPerSecond, _current.Frames.Length, _current.Loop);
            CurrentColumn = _current.Frames[step];
            RendererFor(slot).sprite = part.GetFrame(_current.Sheet, CurrentColumn);
        }

        private SpriteRenderer RendererFor(LpcSlot slot)
        {
            if (_renderers.TryGetValue(slot, out var renderer)) return renderer;

            // reuse a child left by a previous session (e.g. a saved prefab), else make one
            var child = transform.Find(slot.ToString());
            if (child == null)
            {
                child = new GameObject(slot.ToString()).transform;
                child.SetParent(transform, false);
            }
            renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = _sortingLayerName;
            renderer.flipX = _facingLeft;
            _renderers[slot] = renderer;
            return renderer;
        }
    }
}
