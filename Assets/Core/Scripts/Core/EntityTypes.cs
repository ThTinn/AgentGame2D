using System;
using UnityEngine;

namespace Game.Core
{
    // Giá trị số giữ nguyên bản 3D ở những state/action còn dùng, để protocol server cũ vẫn đọc được.
    // Các số bị bỏ (IN_AIR=2, LANDING=3 ...) để trống, không tái sử dụng.
    public enum PlayerStateEnum : byte
    {
        IDLE = 0,
        MOVE = 1,
        DASH = 4,
        DIE = 5,
        ATTACK = 6,
        HURT = 7,
        JUMP = 8,   // mới ở 2D (số 2, 3 của bản 3D vẫn để trống); server cũ chưa biết state này
    }

    public enum PlayerAction : short
    {
        NONE = -1,
        IDLE = 0,
        WALK = 1,
        RUN = 2,
        DASH = 7,
        DIE = 8,
        ATTACK1 = 9,
        ATTACK2 = 10,
        ATTACK3 = 11,
        ATTACKFINAL = 12,
        SKILL_1 = 13,
        SKILL_2 = 14,
        SKILL_3 = 15,
        SKILL_4 = 16,
        HURT = 17,
        JUMP = 18,
    }

    public enum EnemyAct : short
    {
        NONE = -1,
        IDLE,
        MOVE,
        DIE,
        HURT,
        ATTACK,
    }

    // Số giữ nguyên bản 3D. Exp/Level/Potential chưa có ở 2D nên chưa khai báo.
    public enum TypeHP_KI : byte
    {
        Type_Hp = 1,
        Type_MP = 2,
    }

    public enum DameType : byte
    {
        Normal,
        Crit,
        Miss
    }

    /// <summary>Trùng giá trị với Player.TypeSentMove cũ.</summary>
    public enum MoveType : byte
    {
        Move,
        ResetPoint
    }

    /// <summary>8 hướng, 45 độ mỗi bước, tính ngược chiều kim đồng hồ từ trục +X.</summary>
    public enum Direction8 : byte
    {
        Right, UpRight, Up, UpLeft, Left, DownLeft, Down, DownRight
    }

    public static class DirectionUtil
    {
        static readonly Vector2[] vectors =
        {
            new Vector2(1f, 0f),
            new Vector2(0.7071f, 0.7071f),
            new Vector2(0f, 1f),
            new Vector2(-0.7071f, 0.7071f),
            new Vector2(-1f, 0f),
            new Vector2(-0.7071f, -0.7071f),
            new Vector2(0f, -1f),
            new Vector2(0.7071f, -0.7071f),
        };

        public static Direction8 FromVector(Vector2 v)
        {
            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return (Direction8)(Mathf.RoundToInt(angle / 45f) % 8);
        }

        public static Vector2 ToVector(this Direction8 dir) => vectors[(int)dir];
    }

    [Serializable]
    public class EntityData
    {
        public int id;
        public string nameObject;
        public long curHp;
        public long maxHp;
        public long curMp;
        public long maxMp;
        public Vector2 startPos;

        public EntityData() { }

        public EntityData(int id, string name, long curHp, long maxHp, long curMp, long maxMp, Vector2 pos = default)
        {
            this.id = id;
            this.nameObject = name;
            this.curHp = curHp;
            this.maxHp = maxHp;
            this.curMp = curMp;
            this.maxMp = maxMp;
            this.startPos = pos;
        }
    }

    /// <summary>Một gói di chuyển nhận từ server cho entity không phải mình (thay cho Point cũ).</summary>
    public class MovePoint
    {
        public Vector2 pos;
        public Direction8 dir;
        public short state;
        public short act;
        /// <summary>Số FixedUpdate để đi hết đoạn này.</summary>
        public int nTime = 1;
        public MoveType type;
    }

    /// <summary>Dữ liệu 1 gói MOVE gửi lên server (Player raise qua event OnSendMove).</summary>
    public struct MoveSnapshot
    {
        public Vector2 pos;
        public Direction8 dir;
        public PlayerStateEnum state;
        public PlayerAction act;
        /// <summary>Số FixedUpdate kể từ gói trước (tối đa 127 để vừa sbyte).</summary>
        public int ticks;
        public MoveType type;
    }
}
