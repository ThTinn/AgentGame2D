using UnityEngine;

namespace DungeonOnline.Gameplay
{
    /// <summary>Tuning values for the player. The asset lives in Assets/Data.</summary>
    [CreateAssetMenu(fileName = "PlayerConfig", menuName = "DungeonOnline/Player Config")]
    public sealed class PlayerConfig : ScriptableObject
    {
        /// <summary>Horizontal speed in units per second.</summary>
        [Min(0f)] public float MoveSpeed;

        /// <summary>Upward speed applied by a jump, in units per second.</summary>
        [Min(0f)] public float JumpVelocity;

        /// <summary>Multiplier on the project's 2D gravity.</summary>
        [Min(0f)] public float GravityScale;

        /// <summary>How far below the player the ground check reaches, in units.</summary>
        [Min(0f)] public float GroundCheckDistance;

        /// <summary>World height below which the player returns to the start point.</summary>
        public float FallLimitY;
    }
}
