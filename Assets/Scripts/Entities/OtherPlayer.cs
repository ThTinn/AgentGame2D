using UnityEngine;

namespace DungeonOnline.Entities
{
    /// <summary>
    /// Another player seen on this client. Has no input or physics of its own: it walks through the positions
    /// the server sends (<see cref="Entity.PushMove"/>) and plays the state that came with each one.
    /// </summary>
    /// <example>
    /// On the server's ADD_PLAYER, then each MOVE:
    /// <code>
    /// var other = Instantiate(otherPlayerPrefab);
    /// other.SetInfo(new EntityData(id, name, hp, maxHp, mp, maxMp, pos));
    /// other.LoadVisual(outfit);
    /// other.PushMove(pos, facingLeft, EntityState.Run);
    /// </code>
    /// </example>
    public sealed class OtherPlayer : Entity
    {
        private Rigidbody2D _body;

        private void Awake()
        {
            // moved by the server, not by forces; a kinematic body still lets triggers see it
            if (TryGetComponent(out _body)) _body.bodyType = RigidbodyType2D.Kinematic;
            // the local player walks through other players instead of being pushed by them
            foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
        }

        private void FixedUpdate() => FollowMoves(Time.fixedDeltaTime);
    }
}
