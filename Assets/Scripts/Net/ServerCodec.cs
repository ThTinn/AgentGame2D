using DungeonOnline.Entities;
using NetKit;
using UnityEngine;

namespace DungeonOnline.Net
{
    /// <summary>
    /// Conversions between ServerJava's packet values and this 2D game. Pure functions, unit-tested.
    /// </summary>
    /// <remarks>
    /// The server writes a position as three ints in hundredths of a metre (x, y, z). On a 2D map x is horizontal,
    /// <b>z is the map's height</b> and y is unused (<see cref="GameCmd.GROUND_Y"/>), so the server's distance maths over
    /// (x, z) stays unchanged. Move states follow GameB's PlayerStateEnum / PlayerAction numbers.
    /// </remarks>
    /// <example>
    /// <code>
    /// Vector2 pos = ServerCodec.ReadPos(reader);
    /// net.Send(GameCmd.MOVE, w => ServerCodec.WriteMove(w, player.transform.position, player.FacingLeft, player.State));
    /// </code>
    /// </example>
    public static class ServerCodec
    {
        // PlayerStateEnum
        public const int STATE_IDLE = 0, STATE_MOVE = 1, STATE_IN_AIR = 2, STATE_DIE = 5;
        // PlayerAction
        public const int ACT_IDLE = 0, ACT_RUN = 2, ACT_IN_AIR_UP = 3, ACT_IN_AIR_FALL = 5, ACT_DIE = 8;
        /// <summary>Facing in the server's degrees: +x is 90, -x is 270.</summary>
        public const int DIR_RIGHT = 90, DIR_LEFT = 270;

        /// <summary>Server ints (hundredths) to a 2D map position: (x, z).</summary>
        public static Vector2 ToMap(int x, int y, int z) => new Vector2(x / 100f, z / 100f);

        /// <summary>Reads a server vec (three ints) as a 2D map position.</summary>
        public static Vector2 ReadPos(PacketReader r)
        {
            int x = r.ReadInt(), y = r.ReadInt(), z = r.ReadInt();
            return ToMap(x, y, z);
        }

        /// <summary>Writes a 2D map position as a server vec: (x, GROUND_Y, y), hundredths.</summary>
        public static void WritePos(PacketWriter w, Vector2 pos)
        {
            w.WriteInt(Mathf.RoundToInt(pos.x * 100f));
            w.WriteInt(GameCmd.GROUND_Y);
            w.WriteInt(Mathf.RoundToInt(pos.y * 100f));
        }

        /// <summary>Whether a server facing (degrees) points left.</summary>
        public static bool IsFacingLeft(int dir)
        {
            int d = ((dir % 360) + 360) % 360;
            return d > 180;
        }

        /// <summary>Entity state from a MOVE's state and action bytes. Unknown states read as idle.</summary>
        public static EntityState ToEntityState(int state, int action)
        {
            switch (state)
            {
                case STATE_MOVE: return EntityState.Run;
                case STATE_IN_AIR: return action == ACT_IN_AIR_UP ? EntityState.Jump : EntityState.Fall;
                default: return EntityState.Idle;   // death comes from combat packets, not from MOVE
            }
        }

        /// <summary>MOVE state and action bytes for an entity state.</summary>
        public static void FromEntityState(EntityState state, out int serverState, out int action)
        {
            switch (state)
            {
                case EntityState.Run: serverState = STATE_MOVE; action = ACT_RUN; break;
                case EntityState.Jump: serverState = STATE_IN_AIR; action = ACT_IN_AIR_UP; break;
                case EntityState.Fall: serverState = STATE_IN_AIR; action = ACT_IN_AIR_FALL; break;
                case EntityState.Die: serverState = STATE_DIE; action = ACT_DIE; break;
                default: serverState = STATE_IDLE; action = ACT_IDLE; break;
            }
        }

        /// <summary>Body of a client MOVE: vec, int facing, byte state, byte action, byte count, byte type.</summary>
        public static void WriteMove(PacketWriter w, Vector2 pos, bool facingLeft, EntityState state)
        {
            FromEntityState(state, out int serverState, out int action);
            WritePos(w, pos);
            w.WriteInt(facingLeft ? DIR_LEFT : DIR_RIGHT);
            w.WriteByte(serverState);
            w.WriteByte(action);
            w.WriteByte(1);     // count: frames the anim step lasts, the server only relays it
            w.WriteByte(0);     // type
        }
    }
}
