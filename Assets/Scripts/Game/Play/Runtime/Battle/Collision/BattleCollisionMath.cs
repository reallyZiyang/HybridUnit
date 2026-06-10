using UnityEngine;

namespace Game.Play.Battle.Collision
{
    public static class BattleCollisionMath
    {
        private const float Epsilon = 0.000001f;
        private const float Rad2Deg = 57.2957795f;

        public static Rect ShapeAabb(in BattleCollisionShape shape)
        {
            switch (shape.type)
            {
                case BattleCollisionShapeType.Circle:
                    return CircleAabb(shape.center, shape.radius);
                case BattleCollisionShapeType.Rect:
                    return RectAabb(shape.center, shape.size, shape.direction);
                case BattleCollisionShapeType.Sector:
                    return CircleAabb(shape.center, shape.radius);
                case BattleCollisionShapeType.CapsuleSegment:
                    return CapsuleSegmentAabb(shape.start, shape.end, shape.width);
                default:
                    return new Rect(shape.center, Vector2.zero);
            }
        }

        public static Rect CircleAabb(Vector2 center, float radius)
        {
            float safeRadius = Mathf.Max(0f, radius);
            Vector2 extent = new(safeRadius, safeRadius);
            return Rect.MinMaxRect(center.x - extent.x, center.y - extent.y, center.x + extent.x, center.y + extent.y);
        }

        public static Rect RectAabb(Vector2 center, Vector2 size, Vector2 direction)
        {
            Vector2 halfSize = new(Mathf.Max(0f, size.x) * 0.5f, Mathf.Max(0f, size.y) * 0.5f);
            Vector2 right = SafeDirection(direction);
            Vector2 up = new(-right.y, right.x);
            float extentX = Mathf.Abs(right.x) * halfSize.x + Mathf.Abs(up.x) * halfSize.y;
            float extentY = Mathf.Abs(right.y) * halfSize.x + Mathf.Abs(up.y) * halfSize.y;
            return Rect.MinMaxRect(center.x - extentX, center.y - extentY, center.x + extentX, center.y + extentY);
        }

        public static Rect CapsuleSegmentAabb(Vector2 start, Vector2 end, float width)
        {
            float radius = Mathf.Max(0f, width) * 0.5f;
            return Rect.MinMaxRect(
                Mathf.Min(start.x, end.x) - radius,
                Mathf.Min(start.y, end.y) - radius,
                Mathf.Max(start.x, end.x) + radius,
                Mathf.Max(start.y, end.y) + radius);
        }

        public static Rect Expand(Rect rect, float amount)
        {
            float safeAmount = Mathf.Max(0f, amount);
            return Rect.MinMaxRect(
                rect.xMin - safeAmount,
                rect.yMin - safeAmount,
                rect.xMax + safeAmount,
                rect.yMax + safeAmount);
        }

        public static bool ShapeHitsCircle(in BattleCollisionShape shape, Vector2 targetCenter, float targetRadius)
        {
            switch (shape.type)
            {
                case BattleCollisionShapeType.Circle:
                    return CircleHitsCircle(shape.center, shape.radius, targetCenter, targetRadius);
                case BattleCollisionShapeType.Rect:
                    return RectHitsCircle(shape.center, shape.size, shape.direction, targetCenter, targetRadius);
                case BattleCollisionShapeType.Sector:
                    return SectorHitsCircle(shape.center, shape.direction, shape.radius, shape.angleDeg, targetCenter, targetRadius);
                case BattleCollisionShapeType.CapsuleSegment:
                    return CapsuleSegmentHitsCircle(shape.start, shape.end, shape.width, targetCenter, targetRadius);
                default:
                    return false;
            }
        }

        public static bool CircleHitsCircle(Vector2 center, float radius, Vector2 targetCenter, float targetRadius)
        {
            float combinedRadius = Mathf.Max(0f, radius) + Mathf.Max(0f, targetRadius);
            return (targetCenter - center).sqrMagnitude <= combinedRadius * combinedRadius;
        }

        public static bool RectHitsCircle(Vector2 center, Vector2 size, Vector2 direction, Vector2 targetCenter, float targetRadius)
        {
            Vector2 halfSize = new(Mathf.Max(0f, size.x) * 0.5f, Mathf.Max(0f, size.y) * 0.5f);
            Vector2 right = SafeDirection(direction);
            Vector2 up = new(-right.y, right.x);
            Vector2 offset = targetCenter - center;
            Vector2 localPoint = new(Vector2.Dot(offset, right), Vector2.Dot(offset, up));

            float dx = Mathf.Max(Mathf.Abs(localPoint.x) - halfSize.x, 0f);
            float dy = Mathf.Max(Mathf.Abs(localPoint.y) - halfSize.y, 0f);
            float safeTargetRadius = Mathf.Max(0f, targetRadius);
            return dx * dx + dy * dy <= safeTargetRadius * safeTargetRadius;
        }

        public static bool SectorHitsCircle(Vector2 center, Vector2 forward, float radius, float angleDeg, Vector2 targetCenter, float targetRadius)
        {
            Vector2 safeForward = SafeDirection(forward);
            Vector2 toTarget = targetCenter - center;
            float distance = toTarget.magnitude;
            float safeRadius = Mathf.Max(0f, radius);
            float safeTargetRadius = Mathf.Max(0f, targetRadius);

            if (distance > safeRadius + safeTargetRadius)
            {
                return false;
            }

            if (distance <= safeTargetRadius)
            {
                return true;
            }

            float halfAngle = Mathf.Clamp(angleDeg * 0.5f, 0f, 180f);
            float targetAngleAllowance = Mathf.Asin(Mathf.Clamp01(safeTargetRadius / Mathf.Max(0.0001f, distance))) * Rad2Deg;
            return Vector2.Angle(safeForward, toTarget) <= halfAngle + targetAngleAllowance;
        }

        public static bool CapsuleSegmentHitsCircle(Vector2 start, Vector2 end, float width, Vector2 targetCenter, float targetRadius)
        {
            float radius = Mathf.Max(0f, width) * 0.5f + Mathf.Max(0f, targetRadius);
            float distanceSqr = DistancePointToSegmentSqr(targetCenter, start, end);
            return distanceSqr <= radius * radius;
        }

        public static float DistancePointToSegmentSqr(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= Epsilon)
            {
                return (point - start).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
            Vector2 closest = start + segment * t;
            return (point - closest).sqrMagnitude;
        }

        public static Vector2 SortOrigin(in BattleCollisionShape shape)
        {
            return shape.type == BattleCollisionShapeType.CapsuleSegment ? shape.start : shape.center;
        }

        private static Vector2 SafeDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }
    }
}
