using UnityEngine;

namespace DungeonOnline.Entities
{
    /// <summary>How entities this client does not control follow the positions the server sends. The asset lives in Assets/Data.</summary>
    [CreateAssetMenu(fileName = "RemoteMotionConfig", menuName = "DungeonOnline/Remote Motion Config")]
    public sealed class RemoteMotionConfig : ScriptableObject
    {
        /// <summary>Speed toward the next queued position, in units per second.</summary>
        [Min(0f)] public float FollowSpeed = 6f;

        /// <summary>Gap, in units, above which the entity jumps straight to the position instead of walking.</summary>
        [Min(0f)] public float SnapDistance = 4f;

        /// <summary>Queued positions kept; older ones are dropped when the client falls this far behind.</summary>
        [Min(1)] public int MaxQueuedMoves = 8;
    }
}
