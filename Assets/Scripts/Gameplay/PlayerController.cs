using UnityEngine;
using UnityEngine.InputSystem;

namespace CoinHop.Gameplay
{
    /// <summary>
    /// Runs, jumps and returns the player to the start point after a fall.
    /// Implements the movement and fall rules in design/game-brief.md.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerConfig _config;
        [SerializeField] private InputActionReference _move;
        [SerializeField] private InputActionReference _jump;

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[1];

        private Rigidbody2D _body;
        private Collider2D _collider;
        private ContactFilter2D _groundFilter;
        private Vector2 _startPosition;
        private float _moveInput;
        private bool _jumpQueued;

        /// <summary>Whether the player stood on ground at the last physics step.</summary>
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _body.gravityScale = _config.GravityScale;
            _groundFilter = new ContactFilter2D { useTriggers = false };
            _startPosition = _body.position;
        }

        private void OnEnable()
        {
            // Left enabled on disable: GameSession reads the same actions after the player stops.
            _move.action.Enable();
            _jump.action.Enable();
        }

        private void Update()
        {
            _moveInput = _move.action.ReadValue<Vector2>().x;
            if (_jump.action.WasPressedThisFrame()) _jumpQueued = true;
        }

        private void FixedUpdate()
        {
            if (_body.position.y < _config.FallLimitY)
            {
                ReturnToStart();
                return;
            }

            IsGrounded = CheckGrounded();
            var velocity = _body.linearVelocity;
            velocity.x = _moveInput * _config.MoveSpeed;
            if (_jumpQueued && IsGrounded) velocity.y = _config.JumpVelocity;
            _jumpQueued = false;
            _body.linearVelocity = velocity;
        }

        /// <summary>Stops the player and hands control back to the game.</summary>
        public void Stop()
        {
            enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
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
