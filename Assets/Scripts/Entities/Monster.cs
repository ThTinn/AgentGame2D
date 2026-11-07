using UnityEngine;

namespace DungeonOnline.Entities
{
    /// <summary>
    /// A monster seen on this client. The server decides where it goes, what it hits and when it dies
    /// (design/game-brief.md, Chiến đấu): the client only walks it through the positions the server sends and
    /// shows the damage and death it reports.
    /// </summary>
    /// <example>
    /// <code>
    /// var monster = Instantiate(monsterPrefab);
    /// monster.SetTemplate(templateId);
    /// monster.SetInfo(new EntityData(id, name, hp, maxHp, 0, 0, pos));
    /// monster.PushMove(pos, facingLeft, EntityState.Run);
    /// monster.TakeDamage(damageFromServer);
    /// </code>
    /// </example>
    public sealed class Monster : Entity
    {
        [Tooltip("Monster type id in the server's monster table.")]
        [SerializeField] private int _templateId;

        private Rigidbody2D _body;

        /// <summary>Monster type id in the server's monster table.</summary>
        public int TemplateId => _templateId;

        /// <summary>Sets the monster type; call before <see cref="Entity.SetInfo"/>.</summary>
        public void SetTemplate(int templateId) => _templateId = templateId;

        private void Awake()
        {
            if (TryGetComponent(out _body)) _body.bodyType = RigidbodyType2D.Kinematic;
            // contact damage is the server's call; the body only marks where the monster is
            foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
        }

        private void FixedUpdate() => FollowMoves(Time.fixedDeltaTime);
    }
}
