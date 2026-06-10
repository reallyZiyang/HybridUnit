using Game.Play.Battle.Collision;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Projectile
{
    public sealed partial class BattleProjectileManager
    {
        private readonly BattleRuntimeData data;
        private readonly BattleUnitManager units;
        private readonly BattleCollisionManager collisions;
        private readonly BattleEffectExecutor effects;
        private readonly IBattleRenderWorld renderWorld;
        private readonly BattleCollisionQueryBuffer queryBuffer;
        private readonly int capacity;
        private readonly int hitRecordStride;
        private readonly Vector2[] positions;
        private readonly Vector2[] directions;
        private readonly int[] projectileIds;
        private readonly int[] remainingMs;
        private readonly int[] pierceRemaining;
        private readonly int[] sourceCamps;
        private readonly int[] renderHandles;
        private readonly BattleUnitHandle[] sources;
        private readonly bool[] active;
        private readonly int[] generations;
        private readonly int[] freeStack;
        private readonly int[] hitUnitIndices;
        private readonly int[] hitUnitGenerations;
        private readonly int[] hitCooldownMs;

        private int allocatedCount;
        private int freeCount;

        public BattleProjectileManager(
            BattleRuntimeData data,
            BattleUnitManager units,
            BattleCollisionManager collisions,
            BattleEffectExecutor effects,
            IBattleRenderWorld renderWorld,
            int capacity,
            int queryCapacity,
            int hitRecordStride = 8)
        {
            this.data = data;
            this.units = units;
            this.collisions = collisions;
            this.effects = effects;
            this.renderWorld = renderWorld;
            this.capacity = Mathf.Max(1, capacity);
            this.hitRecordStride = Mathf.Max(1, hitRecordStride);
            positions = new Vector2[this.capacity];
            directions = new Vector2[this.capacity];
            projectileIds = new int[this.capacity];
            remainingMs = new int[this.capacity];
            pierceRemaining = new int[this.capacity];
            sourceCamps = new int[this.capacity];
            renderHandles = new int[this.capacity];
            sources = new BattleUnitHandle[this.capacity];
            active = new bool[this.capacity];
            generations = new int[this.capacity];
            freeStack = new int[this.capacity];
            hitUnitIndices = new int[this.capacity * this.hitRecordStride];
            hitUnitGenerations = new int[this.capacity * this.hitRecordStride];
            hitCooldownMs = new int[this.capacity * this.hitRecordStride];
            queryBuffer = new BattleCollisionQueryBuffer(Mathf.Max(1, queryCapacity));

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
            positions[index] = position;
            directions[index] = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            remainingMs[index] = Mathf.Max(1, projectile.lifetimeMs);
            pierceRemaining[index] = Mathf.Max(1, projectile.pierceCount);
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
                positions[i] += directions[i] * projectile.speed * dt;
                remainingMs[i] -= deltaMs;
                renderWorld?.SetPosition(renderHandles[i], positions[i]);
                TryHitTargets(i, projectile);

                if (active[i] && remainingMs[i] <= 0)
                {
                    DespawnAt(i);
                }
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

        private void TryHitTargets(int index, BattleProjectileRuntimeData projectile)
        {
            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = positions[index],
                radius = projectile.radius
            };
            collisions.Query(shape, EnemyOptions(index), queryBuffer);
            for (int i = 0; i < queryBuffer.Count; i++)
            {
                BattleUnitHandle target = collisions.GetUnitHandle(queryBuffer.TargetIndices[i]);
                if (!units.IsAlive(target) || !CanHit(index, target, projectile.hitIntervalMs))
                {
                    continue;
                }

                SetHitCooldown(index, target, projectile.hitIntervalMs);
                effects.ExecuteEffects(projectile.hitEffects, sources[index], target, positions[index], directions[index]);
                pierceRemaining[index]--;
                if (pierceRemaining[index] <= 0)
                {
                    DespawnAt(index);
                    return;
                }
            }
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
            remainingMs[index] = 0;
            pierceRemaining[index] = 0;
            renderHandles[index] = -1;
            sources[index] = BattleUnitHandle.Invalid;
            ClearHitRecords(index);
            freeStack[freeCount++] = index;
        }
    }
}
