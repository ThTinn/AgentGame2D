using DungeonOnline.Gameplay;
using Game;
using Game.Core;
using Game.Players;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DungeonOnline.Entities
{
    /// <summary>
    /// The local player: runs and jumps from input with 2D physics and returns to the start point after a fall.
    /// Name, HP and MP follow the Core profile (<see cref="CoreSystems.Me"/>) when one is running.
    /// Implements the movement and fall rules in design/game-brief.md.
    /// </summary>
    /// <example>
    /// Placed in the scene from Player.prefab; other code reads it as an <see cref="Entity"/>:
    /// <code>
    /// var player = FindFirstObjectByType&lt;Player&gt;();
    /// player.StatsChanged += (hp, maxHp, mp, maxMp) => Debug.Log(hp);
    /// </code>
    /// </example>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class Player : Entity
    {
        [SerializeField] private PlayerConfig _config;
        [SerializeField] private InputActionReference _move;
        [SerializeField] private InputActionReference _jump;

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[1];

        private Rigidbody2D _body;
        private Collider2D _collider;
        private ContactFilter2D _groundFilter;
        private Vector2 _startPosition;
        private float _fallLimitY;
        private float _moveInput;
        private bool _jumpQueued;
        private PlayerProfile _profile;

        /// <summary>Whether the player stood on ground at the last physics step.</summary>
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _body.gravityScale = _config.GravityScale;
            _groundFilter = new ContactFilter2D { useTriggers = false };
            _startPosition = _body.position;
            _fallLimitY = _config.FallLimitY;
        }

        private void OnEnable()
        {
            // Left enabled on disable: GameSession reads the same actions after the player stops.
            _move.action.Enable();
            _jump.action.Enable();
            CoreSystems.MeChanged += BindProfile;
            BindProfile(CoreSystems.Me);
        }

        private void OnDisable()
        {
            CoreSystems.MeChanged -= BindProfile;
            BindProfile(null);
        }

        private void Update()
        {
            _moveInput = _move.action.ReadValue<Vector2>().x;
            if (_jump.action.WasPressedThisFrame()) _jumpQueued = true;
        }

        private void FixedUpdate()
        {
            if (_body.position.y < _fallLimitY)
            {
                ReturnToStart();
                return;
            }

            IsGrounded = CheckGrounded();
            var velocity = _body.linearVelocity;
            velocity.x = IsDead ? 0f : _moveInput * _config.MoveSpeed;
            if (_jumpQueued && IsGrounded && !IsDead) velocity.y = _config.JumpVelocity;
            _jumpQueued = false;
            _body.linearVelocity = velocity;
        }

        protected override void LateUpdate()
        {
            if (!IsDead && Visual != null && Visual.Animations != null)
            {
                float threshold = Visual.Animations.MovingSpeedThreshold;
                Vector2 velocity = _body.linearVelocity;
                State = StateFromMotion(velocity, IsGrounded, threshold);
                if (Mathf.Abs(velocity.x) > threshold) FacingLeft = velocity.x < 0f;
            }
            base.LateUpdate();
        }

        /// <summary>
        /// Puts the player at a map's spawn point, which becomes the return point after a fall below
        /// <paramref name="fallLimitY"/>, and stops any motion.
        /// </summary>
        public void EnterMap(Vector2 spawn, float fallLimitY)
        {
            _startPosition = spawn;
            _fallLimitY = fallLimitY;
            _body.position = spawn;
            transform.position = spawn;
            _body.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// Freezes (false) or frees (true) the player: while frozen it ignores input and physics, e.g. before login.
        /// Unlike <see cref="Stop"/> it can be turned back on.
        /// </summary>
        public void SetControllable(bool controllable)
        {
            enabled = controllable;
            _body.simulated = controllable;
            if (!controllable) _body.linearVelocity = Vector2.zero;
        }

        /// <summary>Stops the player and hands control back to the game.</summary>
        public void Stop()
        {
            enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
        }

        // the profile owns HP / MP; this entity mirrors it
        private void BindProfile(PlayerProfile profile)
        {
            if (_profile != null) _profile.StatChanged -= OnProfileStat;
            _profile = profile;
            if (_profile == null || _profile.data == null) return;
            _profile.StatChanged += OnProfileStat;
            EntityData data = _profile.data;
            SetInfo(new EntityData(data.id, data.nameObject, data.curHp, data.maxHp, data.curMp, data.maxMp));
        }

        private void OnProfileStat(TypeHP_KI type, long cur, long max)
        {
            if (type == TypeHP_KI.Type_Hp) UpdateStats(cur, max, Mp, MaxMp);
            else if (type == TypeHP_KI.Type_MP) UpdateStats(Hp, MaxHp, cur, max);
        }

        private bool CheckGrounded()
        {
            return _collider.Cast(Vector2.down, _groundFilter, _groundHits, _config.GroundCheckDistance) > 0;
        }

        private void ReturnToStart()
        {
            _body.position = _startPosition;
            _body.linearVelocity = Vector2.zero;
        }
    }
}
