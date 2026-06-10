using UnityEngine;

namespace Game.Play.Battle.Collision
{
    public enum BattleCollisionShapeType
    {
        Circle,
        Rect,
        Sector,
        CapsuleSegment
    }

    public struct BattleCollisionShape
    {
        public BattleCollisionShapeType type;
        public Vector2 center;
        public Vector2 direction;
        public Vector2 size;
        public Vector2 start;
        public Vector2 end;
        public float radius;
        public float angleDeg;
        public float width;
    }
}
