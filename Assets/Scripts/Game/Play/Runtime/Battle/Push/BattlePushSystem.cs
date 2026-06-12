using Game.Play.Battle.Collision;
using Game.Play.Battle.Skill;
using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Push
{
    public sealed class BattlePushSystem
    {
        public const float DefaultPushRadius = BattleUnitManager.DefaultPushRadius;
        public const float MaxPushDistancePerTick = 0.08f;
        public const float TangentSlideScale = 0.25f;
        public const int Iterations = 1;

        private readonly BattleUnitManager units;
        private readonly BattleCollisionManager collisions;
        private readonly BattleCollisionQueryBuffer queryBuffer;
        private readonly Vector2[] pushOffsets;
        private readonly bool[] canReceivePushNow;

        public BattlePushSystem(
            BattleUnitManager units,
            BattleCollisionManager collisions,
            BattleSkillManager skills,
            int unitCapacity,
            int queryCapacity)
        {
            this.units = units;
            this.collisions = collisions;
            int capacity = Mathf.Max(1, unitCapacity);
            queryBuffer = new BattleCollisionQueryBuffer(Mathf.Max(1, queryCapacity));
            pushOffsets = new Vector2[capacity];
            canReceivePushNow = new bool[capacity];
        }

        public void Tick()
        {
            if (units == null || collisions == null)
            {
                return;
            }

            for (int i = 0; i < Iterations; i++)
            {
                TickSingleIteration();
            }
        }

        private void TickSingleIteration()
        {
            PrepareUnitState();
            AccumulatePushOffsets();
            ApplyPushOffsets();
        }

        private void PrepareUnitState()
        {
            for (int i = 0; i < units.AllocatedCount; i++)
            {
                pushOffsets[i] = Vector2.zero;
                canReceivePushNow[i] = false;

                if (!units.TryGetHandleByIndex(i, out BattleUnitHandle unit) || !units.CanBePushed(unit))
                {
                    continue;
                }

                if (units.HasEndure(unit))
                {
                    continue;
                }

                canReceivePushNow[i] = true;
            }
        }

        private void AccumulatePushOffsets()
        {
            float maxPushRadius = Mathf.Max(0f, units.MaxPushRadius);
            if (maxPushRadius <= 0f)
            {
                return;
            }

            BattleCollisionQueryOptions options = new()
            {
                campMask = 0,
                stateMask = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layerMask = 0,
                maxHits = 0,
                sortByDistance = false
            };

            for (int i = 0; i < units.AllocatedCount; i++)
            {
                if (!units.TryGetHandleByIndex(i, out BattleUnitHandle source) || !units.IsAlive(source))
                {
                    continue;
                }

                float sourcePushRadius = units.GetPushRadius(source);
                BattleCollisionShape shape = new()
                {
                    type = BattleCollisionShapeType.Circle,
                    center = units.GetPosition(source),
                    radius = sourcePushRadius + maxPushRadius
                };

                collisions.Query(shape, options, queryBuffer);
                for (int j = 0; j < queryBuffer.Count; j++)
                {
                    BattleUnitHandle target = collisions.GetUnitHandle(queryBuffer.TargetIndices[j]);
                    if (!target.IsValid || target.index <= source.index || !units.IsAlive(target))
                    {
                        continue;
                    }

                    AccumulatePair(source, target, sourcePushRadius);
                }
            }
        }

        private void AccumulatePair(BattleUnitHandle a, BattleUnitHandle b, float aPushRadius)
        {
            float bPushRadius = units.GetPushRadius(b);
            float combinedRadius = aPushRadius + bPushRadius;
            if (combinedRadius <= 0f)
            {
                return;
            }

            bool sameCamp = units.GetCamp(a) == units.GetCamp(b);
            if (!sameCamp)
            {
                return;
            }

            bool moveA = canReceivePushNow[a.index] && units.CanPushOthers(b);
            bool moveB = canReceivePushNow[b.index] && units.CanPushOthers(a);
            if (!moveA && !moveB)
            {
                return;
            }

            Vector2 aPosition = units.GetPosition(a);
            Vector2 bPosition = units.GetPosition(b);
            Vector2 delta = bPosition - aPosition;
            float distanceSqr = delta.sqrMagnitude;
            Vector2 direction;
            float distance;
            if (distanceSqr <= 0.000001f)
            {
                direction = ResolveZeroDistanceDirection(a.index, b.index);
                distance = 0f;
            }
            else
            {
                distance = Mathf.Sqrt(distanceSqr);
                direction = delta / distance;
            }

            float overlap = combinedRadius - distance;
            if (overlap <= 0f)
            {
                return;
            }

            if (moveA && moveB)
            {
                Vector2 offset = ResolvePushOffset(direction, overlap * 0.5f, a.index, b.index, TangentSlideScale);
                pushOffsets[a.index] -= offset;
                pushOffsets[b.index] += offset;
            }
            else if (moveA)
            {
                pushOffsets[a.index] -= ResolvePushOffset(direction, overlap, a.index, b.index, TangentSlideScale);
            }
            else
            {
                pushOffsets[b.index] += ResolvePushOffset(direction, overlap, a.index, b.index, TangentSlideScale);
            }
        }

        private void ApplyPushOffsets()
        {
            for (int i = 0; i < units.AllocatedCount; i++)
            {
                if (!canReceivePushNow[i] || pushOffsets[i].sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                if (!units.TryGetHandleByIndex(i, out BattleUnitHandle unit))
                {
                    continue;
                }

                Vector2 offset = Vector2.ClampMagnitude(pushOffsets[i], MaxPushDistancePerTick);
                units.SetPosition(unit, units.GetPosition(unit) + offset);
            }
        }

        private static Vector2 ResolveZeroDistanceDirection(int aIndex, int bIndex)
        {
            int hash = (aIndex * 73856093) ^ (bIndex * 19349663);
            float angle = (hash & 1023) * (Mathf.PI * 2f / 1024f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static Vector2 ResolvePushOffset(Vector2 direction, float overlap, int aIndex, int bIndex, float tangentSlideScale)
        {
            float sideRatio = Mathf.Clamp01(tangentSlideScale);
            float normalRatio = 1f - sideRatio;
            Vector2 tangent = new(-direction.y, direction.x);
            float side = ResolveTangentSide(aIndex, bIndex);
            Vector2 normalOffset = direction * (overlap * normalRatio);
            Vector2 tangentOffset = tangent * (overlap * sideRatio * side);
            return normalOffset - tangentOffset;
        }

        private static float ResolveTangentSide(int aIndex, int bIndex)
        {
            int hash = (aIndex * 1103515245) ^ (bIndex * 12345);
            return (hash & 1) == 0 ? 1f : -1f;
        }
    }
}
