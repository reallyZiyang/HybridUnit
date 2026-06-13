using Game.Play.Battle.Collision;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Projectile
{
    public sealed partial class BattleProjectileManager : IBattleCollisionQueryVisitor
    {
        private enum ProjectileVisitMode
        {
            Immediate,
            NearestOnPath
        }

        private readonly BattleRuntimeData data;
        private readonly BattleUnitManager units;
        private readonly BattleCollisionManager collisions;
        private readonly BattleCollisionQueryBuffer areaQueryBuffer;
        private readonly BattleEffectExecutor effects;
        private readonly BattleSkillEnhancementContext enhancements;
        private readonly IBattleRenderWorld renderWorld;
        private readonly int capacity;
        private readonly int hitRecordStride;
        private readonly Vector2[] positions;
        private readonly Vector2[] previousPositions;
        private readonly Vector2[] directions;
        private readonly int[] projectileIds;
        private readonly float[] speeds;
        private readonly float[] radii;
        private readonly float[] hitAreaRadii;
        private readonly int[] hitIntervalMs;
        private readonly int[] remainingMs;
        private readonly int[] pierceRemaining;
        private readonly int[] sourceCamps;
        private readonly int[] renderHandles;
        private readonly BattleUnitHandle[] sources;
        private readonly BattleEffectContext[] sourceContexts;
        private readonly bool[] active;
        private readonly int[] generations;
        private readonly int[] freeStack;
        private readonly int[] hitUnitIndices;
        private readonly int[] hitUnitGenerations;
        private readonly int[] hitCooldownMs;

        private int allocatedCount;
        private int freeCount;
        private int visitProjectileIndex = -1;
        private int nearestTargetIndex = -1;
        private float nearestTargetT = float.MaxValue;
        private Vector2 visitSegmentStart;
        private Vector2 visitSegmentEnd;
        private BattleProjectileRuntimeData visitProjectile;
        private ProjectileVisitMode visitMode;

        public BattleProjectileManager(
            BattleRuntimeData data,
            BattleUnitManager units,
            BattleCollisionManager collisions,
            BattleEffectExecutor effects,
            BattleSkillEnhancementContext enhancements,
            IBattleRenderWorld renderWorld,
            int capacity,
            int queryCapacity,
            int hitRecordStride = 8)
        {
            this.data = data;
            this.units = units;
            this.collisions = collisions;
            areaQueryBuffer = new BattleCollisionQueryBuffer(Mathf.Max(1, queryCapacity));
            this.effects = effects;
            this.enhancements = enhancements ?? BattleSkillEnhancementContext.Empty;
            this.renderWorld = renderWorld;
            this.capacity = Mathf.Max(1, capacity);
            this.hitRecordStride = Mathf.Max(1, hitRecordStride);
            positions = new Vector2[this.capacity];
            previousPositions = new Vector2[this.capacity];
            directions = new Vector2[this.capacity];
            projectileIds = new int[this.capacity];
            speeds = new float[this.capacity];
            radii = new float[this.capacity];
            hitAreaRadii = new float[this.capacity];
            hitIntervalMs = new int[this.capacity];
            remainingMs = new int[this.capacity];
            pierceRemaining = new int[this.capacity];
            sourceCamps = new int[this.capacity];
            renderHandles = new int[this.capacity];
            sources = new BattleUnitHandle[this.capacity];
            sourceContexts = new BattleEffectContext[this.capacity];
            active = new bool[this.capacity];
            generations = new int[this.capacity];
            freeStack = new int[this.capacity];
            hitUnitIndices = new int[this.capacity * this.hitRecordStride];
            hitUnitGenerations = new int[this.capacity * this.hitRecordStride];
            hitCooldownMs = new int[this.capacity * this.hitRecordStride];

            for (int i = 0; i < renderHandles.Length; i++)
            {
                renderHandles[i] = -1;
            }
            for (int i = 0; i < hitUnitIndices.Length; i++)
            {
                hitUnitIndices[i] = -1;
            }
        }

        public BattleProjectileHandle Spawn(int projectileId, BattleUnitHandle source, Vector2 position, Vector2 direction)
        {
            return Spawn(projectileId, source, position, direction, BattleEffectContext.None);
        }

        public BattleProjectileHandle Spawn(int projectileId, BattleUnitHandle source, Vector2 position, Vector2 direction, BattleEffectContext sourceContext)
        {
            if (!data.TryGetProjectileEffect(projectileId, out BattleProjectileRuntimeData projectile))
            {
                return BattleProjectileHandle.Invalid;
            }

            int index = Allocate();
            if (index < 0)
            {
                return BattleProjectileHandle.Invalid;
            }

            int generation = generations[index] + 1;
            generations[index] = generation > 0 ? generation : 1;
            active[index] = true;
            projectileIds[index] = projectileId;
            sources[index] = source;
            sourceContexts[index] = sourceContext;
            positions[index] = position;
            previousPositions[index] = position;
            directions[index] = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            speeds[index] = enhancements.ResolveProjectileSpeed(source, projectileId, sourceContext, projectile.speed);
            radii[index] = enhancements.ResolveProjectileRadius(source, projectileId, sourceContext, projectile.radius);
            hitAreaRadii[index] = enhancements.ResolveProjectileHitAreaRadius(source, projectileId, sourceContext);
            hitIntervalMs[index] = enhancements.ResolveProjectileHitIntervalMs(source, projectileId, sourceContext, projectile.hitIntervalMs);
            remainingMs[index] = enhancements.ResolveProjectileLifetimeMs(source, projectileId, sourceContext, projectile.lifetimeMs);
            pierceRemaining[index] = enhancements.ResolveProjectilePierceCount(source, projectileId, sourceContext, projectile.pierceCount);
            sourceCamps[index] = units.GetCamp(source);
            ClearHitRecords(index);

            float angle = Mathf.Atan2(directions[index].y, directions[index].x) * Mathf.Rad2Deg;
            renderHandles[index] = renderWorld?.SpawnProjectile(projectile.projectileKey, position, angle) ?? -1;
            return new BattleProjectileHandle(index, generations[index]);
        }

        public void Tick(int deltaMs)
        {
            float dt = deltaMs / 1000f;
            for (int i = 0; i < allocatedCount; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                if (!data.TryGetProjectileEffect(projectileIds[i], out BattleProjectileRuntimeData projectile))
                {
                    DespawnAt(i);
                    continue;
                }

                TickHitCooldowns(i, deltaMs);
                Vector2 previousPosition = positions[i];
                previousPositions[i] = previousPosition;
                positions[i] += directions[i] * speeds[i] * dt;
                remainingMs[i] -= deltaMs;
                TryHitTargets(i, projectile, previousPosition);

                if (active[i] && remainingMs[i] <= 0)
                {
                    DespawnAt(i);
                }
            }
        }

        public void SyncRenderPositions(float alpha)
        {
            float t = Mathf.Clamp01(alpha);
            for (int i = 0; i < allocatedCount; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                Vector2 renderPosition = Vector2.Lerp(previousPositions[i], positions[i], t);
                renderWorld?.SetPosition(renderHandles[i], renderPosition);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < allocatedCount; i++)
            {
                if (active[i])
                {
                    DespawnAt(i);
                }
            }
        }

        public bool Visit(int targetIndex)
        {
            if (visitProjectileIndex < 0 || !active[visitProjectileIndex])
            {
                return false;
            }

            BattleUnitHandle target = collisions.GetUnitHandle(targetIndex);
            if (!CanHitTarget(visitProjectileIndex, target, visitProjectile))
            {
                return true;
            }

            if (visitMode == ProjectileVisitMode.NearestOnPath)
            {
                float t = SegmentCircleEnterT(
                    visitSegmentStart,
                    visitSegmentEnd,
                    units.GetPosition(target),
                    radii[visitProjectileIndex] + units.GetRadius(target));
                if (t < nearestTargetT)
                {
                    nearestTargetT = t;
                    nearestTargetIndex = targetIndex;
                }

                return true;
            }

            ExecuteHit(visitProjectileIndex, target, visitProjectile);
            return active[visitProjectileIndex] && pierceRemaining[visitProjectileIndex] > 0;
        }

        private void TryHitTargets(int index, BattleProjectileRuntimeData projectile, Vector2 previousPosition)
        {
            if (projectile.queryQuality == ConfigBattle.QueryQuality.Low)
            {
                BattleCollisionShape shape = new()
                {
                    type = BattleCollisionShapeType.Circle,
                    center = positions[index],
                    radius = radii[index]
                };
                VisitImmediate(index, projectile, shape, previousPosition, positions[index]);
                return;
            }

            BattleCollisionShape pathShape = new()
            {
                type = BattleCollisionShapeType.CapsuleSegment,
                start = previousPosition,
                end = positions[index],
                width = radii[index] * 2f
            };

            if (projectile.queryQuality == ConfigBattle.QueryQuality.High)
            {
                VisitNearestOnPath(index, projectile, pathShape, previousPosition, positions[index]);
            }
            else
            {
                VisitImmediate(index, projectile, pathShape, previousPosition, positions[index]);
            }
        }

        private void VisitImmediate(int index, BattleProjectileRuntimeData projectile, BattleCollisionShape shape, Vector2 start, Vector2 end)
        {
            BeginVisit(index, projectile, ProjectileVisitMode.Immediate, start, end);
            collisions.QueryVisit(shape, EnemyOptions(index), this);
            EndVisit();
        }

        private void VisitNearestOnPath(int index, BattleProjectileRuntimeData projectile, BattleCollisionShape shape, Vector2 start, Vector2 end)
        {
            BeginVisit(index, projectile, ProjectileVisitMode.NearestOnPath, start, end);
            collisions.QueryVisit(shape, EnemyOptions(index), this);
            int targetIndex = nearestTargetIndex;
            EndVisit();

            if (active[index] && targetIndex >= 0)
            {
                BattleUnitHandle target = collisions.GetUnitHandle(targetIndex);
                if (CanHitTarget(index, target, projectile))
                {
                    ExecuteHit(index, target, projectile);
                }
            }
        }

        private void BeginVisit(int index, BattleProjectileRuntimeData projectile, ProjectileVisitMode mode, Vector2 start, Vector2 end)
        {
            visitProjectileIndex = index;
            visitProjectile = projectile;
            visitMode = mode;
            visitSegmentStart = start;
            visitSegmentEnd = end;
            nearestTargetIndex = -1;
            nearestTargetT = float.MaxValue;
        }

        private void EndVisit()
        {
            visitProjectileIndex = -1;
            nearestTargetIndex = -1;
            nearestTargetT = float.MaxValue;
        }

        private bool CanHitTarget(int index, BattleUnitHandle target, BattleProjectileRuntimeData projectile)
        {
            return !target.SameAs(sources[index])
                && units.IsAlive(target)
                && CanHit(index, target, hitIntervalMs[index]);
        }

        private void ExecuteHit(int index, BattleUnitHandle target, BattleProjectileRuntimeData projectile)
        {
            BattleEffectContext context = sourceContexts[index].AsProjectileHit(projectileIds[index]);
            if (hitAreaRadii[index] > 0f)
            {
                ExecuteAreaHit(index, target, projectile, context);
            }
            else
            {
                SetHitCooldown(index, target, hitIntervalMs[index]);
                effects.ExecuteEffects(projectile.hitEffects, sources[index], target, positions[index], directions[index], context);
            }

            pierceRemaining[index]--;
            if (pierceRemaining[index] <= 0)
            {
                DespawnAt(index);
            }
        }

        private void ExecuteAreaHit(int index, BattleUnitHandle primaryTarget, BattleProjectileRuntimeData projectile, BattleEffectContext context)
        {
            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = positions[index],
                radius = hitAreaRadii[index]
            };
            bool primaryHit = false;
            collisions.Query(shape, EnemyOptions(index), areaQueryBuffer);
            for (int i = 0; i < areaQueryBuffer.Count; i++)
            {
                BattleUnitHandle target = collisions.GetUnitHandle(areaQueryBuffer.TargetIndices[i]);
                if (!CanHitTarget(index, target, projectile))
                {
                    continue;
                }

                if (target.SameAs(primaryTarget))
                {
                    primaryHit = true;
                }

                SetHitCooldown(index, target, hitIntervalMs[index]);
                effects.ExecuteEffects(projectile.hitEffects, sources[index], target, positions[index], directions[index], context);
            }

            if (!primaryHit && CanHitTarget(index, primaryTarget, projectile))
            {
                SetHitCooldown(index, primaryTarget, hitIntervalMs[index]);
                effects.ExecuteEffects(projectile.hitEffects, sources[index], primaryTarget, positions[index], directions[index], context);
            }
        }

        private static float SegmentCircleEnterT(Vector2 start, Vector2 end, Vector2 center, float radius)
        {
            Vector2 segment = end - start;
            float a = Vector2.Dot(segment, segment);
            if (a <= 0.000001f)
            {
                return 0f;
            }

            Vector2 fromCenter = start - center;
            float c = Vector2.Dot(fromCenter, fromCenter) - radius * radius;
            if (c <= 0f)
            {
                return 0f;
            }

            float b = 2f * Vector2.Dot(fromCenter, segment);
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return Mathf.Clamp01(Vector2.Dot(center - start, segment) / a);
            }

            float sqrt = Mathf.Sqrt(discriminant);
            float t = (-b - sqrt) / (2f * a);
            if (t < 0f || t > 1f)
            {
                t = (-b + sqrt) / (2f * a);
            }

            return Mathf.Clamp01(t);
        }

        private BattleCollisionQueryOptions EnemyOptions(int index)
        {
            int camp = sourceCamps[index];
            int campMask = camp >= 0 && camp < 32 ? ~(1 << camp) : 0;
            return new BattleCollisionQueryOptions
            {
                campMask = campMask,
                stateMask = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layerMask = 0,
                maxHits = 0,
                sortByDistance = false
            };
        }

        private int Allocate()
        {
            if (freeCount > 0)
            {
                return freeStack[--freeCount];
            }

            if (allocatedCount >= capacity)
            {
                Debug.LogError($"[BattleProjectile] Projectile capacity exceeded: {capacity}");
                return -1;
            }

            return allocatedCount++;
        }

        private void DespawnAt(int index)
        {
            if (!active[index])
            {
                return;
            }

            renderWorld?.Despawn(renderHandles[index]);
            active[index] = false;
            projectileIds[index] = 0;
            speeds[index] = 0f;
            radii[index] = 0f;
            hitAreaRadii[index] = 0f;
            hitIntervalMs[index] = 0;
            remainingMs[index] = 0;
            pierceRemaining[index] = 0;
            renderHandles[index] = -1;
            sources[index] = BattleUnitHandle.Invalid;
            sourceContexts[index] = BattleEffectContext.None;
            ClearHitRecords(index);
            freeStack[freeCount++] = index;
        }
    }
}
