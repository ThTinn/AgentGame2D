using DungeonOnline.Gameplay;
using UnityEngine;

namespace DungeonOnline.Characters
{
    /// <summary>
    /// Picks the player's animation from movement and mirrors the character toward the direction
    /// of travel. Implements the character section of design/game-brief.md.
    /// </summary>
    /// <remarks>
    /// States are chosen fresh each frame from physics, so there are no stored transitions:
    /// <list type="table">
    /// <item><term>grounded, |vx| &lt;= threshold</term><description>idle</description></item>
    /// <item><term>grounded, |vx| &gt; threshold</term><description>run</description></item>
    /// <item><term>airborne, vy &gt; 0</term><description>jump</description></item>
    /// <item><term>airborne, vy &lt;= 0</term><description>fall</description></item>
    /// </list>
    /// The threshold is <see cref="LpcAnimationSet.MovingSpeedThreshold"/>.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerController))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        /// <summary>Animation names this driver plays; they must exist in the character's animation set.</summary>
        public const string IDLE = "idle", RUN = "run", JUMP = "jump", FALL = "fall";

        [SerializeField] private LayeredCharacter _character;

        private Rigidbody2D _body;
        private PlayerController _controller;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _controller = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (_character == null || _character.Animations == null) return;
            var threshold = _character.Animations.MovingSpeedThreshold;
            var velocity = _body.linearVelocity;
            _character.Play(Choose(velocity, _controller.IsGrounded, threshold));
            if (Mathf.Abs(velocity.x) > threshold) _character.SetFacingLeft(velocity.x < 0f);
        }

        /// <summary>The animation for a velocity and ground state. Pure, for tests.</summary>
        public static string Choose(Vector2 velocity, bool grounded, float movingThreshold)
        {
            if (!grounded) return velocity.y > 0f ? JUMP : FALL;
            return Mathf.Abs(velocity.x) > movingThreshold ? RUN : IDLE;
        }
    }
}
