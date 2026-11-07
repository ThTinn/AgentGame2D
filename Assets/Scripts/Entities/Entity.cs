using System;
using System.Collections.Generic;
using DungeonOnline.Characters;
using Game.Core;
using UnityEngine;

namespace DungeonOnline.Entities
{
    /// <summary>What an entity is doing, which picks its animation. Sent by the server for remote entities.</summary>
    public enum EntityState : byte
    {
        Idle,
        Run,
        Jump,
        Fall,
        Die,
    }

    /// <summary>
    /// Base of every character in the world: the local <see cref="Player"/>, an <see cref="OtherPlayer"/> and a
    /// <see cref="Monster"/>. Owns the visual (an LPC <see cref="LayeredCharacter"/>), the values all of them share
    /// (id, name, HP, MP, facing, alive) and the queue of positions the server sends for entities this client does
    /// not control. Modelled on GameB's <c>Entity</c>.
    /// </summary>
    /// <example>
    /// Spawning a remote entity from a server packet:
    /// <code>
    /// var other = Instantiate(otherPlayerPrefab, pos, Quaternion.identity);
    /// other.SetInfo(new EntityData(id, name, hp, maxHp, mp, maxMp, pos));
    /// other.LoadVisual(outfitParts);
    /// // each MOVE packet:
    /// other.PushMove(pos, facingLeft, EntityState.Run);
    /// </code>
    /// </example>
    /// <remarks>Single-thread only: call from Unity's main thread.</remarks>
    public abstract class Entity : MonoBehaviour
    {
        /// <summary>Animation names the LPC set must hold for each state.</summary>
        public const string IDLE = "idle", RUN = "run", JUMP = "jump", FALL = "fall";

        [SerializeField] private LayeredCharacter _visual;
        [Tooltip("How remote positions are followed. Unused by the local player.")]
        [SerializeField] private RemoteMotionConfig _remoteMotion;

        private readonly Queue<MovePoint> _moves = new Queue<MovePoint>();

        /// <summary>One position the server reported, with what the entity was doing there.</summary>
        protected readonly struct MovePoint
        {
            public readonly Vector2 Position;
            public readonly bool FacingLeft;
            public readonly EntityState State;

            public MovePoint(Vector2 position, bool facingLeft, EntityState state)
            {
                Position = position;
                FacingLeft = facingLeft;
                State = state;
            }
        }

        /// <summary>Server id; 0 until <see cref="SetInfo"/>.</summary>
        public int Id { get; private set; }

        /// <summary>Name shown above the head.</summary>
        public string DisplayName { get; private set; } = "";

        public long Hp { get; private set; }
        public long MaxHp { get; private set; }
        public long Mp { get; private set; }
        public long MaxMp { get; private set; }

        /// <summary>True from <see cref="Die"/> until <see cref="Respawn"/>.</summary>
        public bool IsDead { get; private set; }

        /// <summary>What the entity is doing now; the visual follows it every frame.</summary>
        public EntityState State { get; protected set; }

        /// <summary>Whether the entity faces left.</summary>
        public bool FacingLeft { get; protected set; }

        /// <summary>The LPC visual, or null when the prefab has none.</summary>
        public LayeredCharacter Visual => _visual;

        /// <summary>Raised after HP or MP changes: (hp, maxHp, mp, maxMp).</summary>
        public event Action<long, long, long, long> StatsChanged;

        /// <summary>Raised once when the entity dies.</summary>
        public event Action<Entity> Died;

        // ---------------- values ----------------

        /// <summary>Applies the shared values from the server or the local profile; moves to its start position.</summary>
        public virtual void SetInfo(EntityData data)
        {
            if (data == null) return;
            Id = data.id;
            DisplayName = data.nameObject ?? "";
            gameObject.name = $"{GetType().Name}_{Id}_{DisplayName}";
            if (data.startPos != Vector2.zero) transform.position = data.startPos;
            UpdateStats(data.curHp, data.maxHp, data.curMp, data.maxMp);
        }

        /// <summary>Sets HP and MP. A max below 1 keeps the current max. HP reaching 0 kills the entity.</summary>
        public void UpdateStats(long hp, long maxHp, long mp, long maxMp)
        {
            if (maxHp > 0) MaxHp = maxHp;
            if (maxMp > 0) MaxMp = maxMp;
            Hp = Math.Max(0, Math.Min(hp, MaxHp));
            Mp = Math.Max(0, Math.Min(mp, MaxMp));
            StatsChanged?.Invoke(Hp, MaxHp, Mp, MaxMp);
            if (Hp == 0 && MaxHp > 0 && !IsDead) Die();
        }

        /// <summary>Shows damage the server decided. Does nothing to a dead entity.</summary>
        public virtual void TakeDamage(long amount)
        {
            if (IsDead || amount <= 0) return;
            UpdateStats(Hp - amount, MaxHp, Mp, MaxMp);
        }

        /// <summary>Stops the entity and freezes its visual on the current frame.</summary>
        public virtual void Die()
        {
            if (IsDead) return;
            IsDead = true;
            State = EntityState.Die;
            _moves.Clear();
            if (_visual != null) _visual.enabled = false;   // LayeredCharacter stops advancing frames
            Died?.Invoke(this);
        }

        /// <summary>Brings the entity back with full HP and MP at <paramref name="position"/>.</summary>
        public virtual void Respawn(Vector2 position)
        {
            IsDead = false;
            State = EntityState.Idle;
            _moves.Clear();
            transform.position = position;
            if (_visual != null) _visual.enabled = true;
            UpdateStats(MaxHp, MaxHp, MaxMp, MaxMp);
        }

        // ---------------- visual ----------------

        /// <summary>Dresses the visual in <paramref name="parts"/>, emptying every slot not in the list.</summary>
        public void LoadVisual(IEnumerable<LpcPart> parts)
        {
            if (_visual == null || parts == null) return;
            var keep = new HashSet<LpcSlot>();
            foreach (var part in parts)
            {
                if (part == null) continue;
                _visual.Equip(part);
                keep.Add(part.Slot);
            }
            foreach (LpcSlot slot in Enum.GetValues(typeof(LpcSlot)))
                if (!keep.Contains(slot)) _visual.Unequip(slot);
        }

        /// <summary>Plays the animation for <see cref="State"/> and mirrors toward <see cref="FacingLeft"/>.</summary>
        protected virtual void LateUpdate()
        {
            if (_visual == null || IsDead) return;
            _visual.SetFacingLeft(FacingLeft);
            _visual.Play(AnimationFor(State));
        }

        /// <summary>LPC animation name for a state. Die keeps the last one (the visual freezes).</summary>
        public static string AnimationFor(EntityState state)
        {
            switch (state)
            {
                case EntityState.Run: return RUN;
                case EntityState.Jump: return JUMP;
                case EntityState.Fall: return FALL;
                default: return IDLE;
            }
        }

        /// <summary>State from a velocity and ground contact. Pure, for tests.</summary>
        /// <param name="movingThreshold">Horizontal speed above which the entity counts as moving.</param>
        public static EntityState StateFromMotion(Vector2 velocity, bool grounded, float movingThreshold)
        {
            if (!grounded) return velocity.y > 0f ? EntityState.Jump : EntityState.Fall;
            return Mathf.Abs(velocity.x) > movingThreshold ? EntityState.Run : EntityState.Idle;
        }

        // ---------------- remote movement ----------------

        /// <summary>Queues a position the server sent. Ignored for a dead entity.</summary>
        public void PushMove(Vector2 position, bool facingLeft, EntityState state)
        {
            if (IsDead) return;
            int max = _remoteMotion != null ? _remoteMotion.MaxQueuedMoves : 8;
            while (_moves.Count >= max) _moves.Dequeue();   // too far behind: drop the oldest
            _moves.Enqueue(new MovePoint(position, facingLeft, state));
        }

        /// <summary>Points still to walk through.</summary>
        public int PendingMoves => _moves.Count;

        /// <summary>
        /// Walks toward the queued server positions; call from FixedUpdate of entities this client does not control.
        /// Snaps when the gap exceeds the config's snap distance. With nothing queued the entity idles in place.
        /// </summary>
        protected void FollowMoves(float deltaTime)
        {
            if (IsDead) return;
            if (_moves.Count == 0)
            {
                if (State != EntityState.Jump && State != EntityState.Fall) State = EntityState.Idle;
                return;
            }

            MovePoint target = _moves.Peek();
            Vector2 position = transform.position;
            float speed = _remoteMotion != null ? _remoteMotion.FollowSpeed : 6f;
            float snap = _remoteMotion != null ? _remoteMotion.SnapDistance : 4f;
            Vector2 next = Vector2.Distance(position, target.Position) > snap
                ? target.Position
                : Vector2.MoveTowards(position, target.Position, speed * deltaTime);

            transform.position = next;
            FacingLeft = target.FacingLeft;
            State = target.State;
            if (next == target.Position) _moves.Dequeue();
        }
    }
}
