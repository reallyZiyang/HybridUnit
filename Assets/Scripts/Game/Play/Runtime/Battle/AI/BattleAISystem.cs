using Game.Data.Configs.Attr;
using Game.Play.Battle.Collision;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Skill;
using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.AI
{
    public sealed class BattleAISystem
    {
        private const int NoTargetSearchIntervalMs = 250;
        private const int HasTargetSearchIntervalMs = 2000;
        private const int CacheClearIntervalMs = 2000;
        private const int MaxCampCount = 32;
        private const float SpeedScale = 0.01f;
        private const float FlipEpsilon = 0.001f;

        private readonly BattleUnitManager units;
        private readonly BattleCollisionManager collisions;
        private readonly BattleSkillManager skills;
        private readonly IBattleRenderWorld renderWorld;
        private readonly BattleUnitHandle[] targets;
        private readonly int[] searchRemainingMs;
        private readonly bool[] moving;
        private readonly bool[] flipX;
        private readonly BattleUnitHandle[] cachedTargets;

        private int cacheClearRemainingMs = CacheClearIntervalMs;

        public BattleAISystem(BattleUnitManager units, BattleCollisionManager collisions, BattleSkillManager skills, IBattleRenderWorld renderWorld, int unitCapacity)
        {
            this.units = units;
            this.collisions = collisions;
            this.skills = skills;
            this.renderWorld = renderWorld;
            int capacity = Mathf.Max(1, unitCapacity);
            targets = new BattleUnitHandle[capacity];
            searchRemainingMs = new int[capacity];
            moving = new bool[capacity];
            flipX = new bool[capacity];
            cachedTargets = new BattleUnitHandle[Mathf.Max(1, collisions.GridCellCount * MaxCampCount)];

            ClearTargets();
            ClearTargetCache();
        }

        public void Tick(int deltaMs)
        {
            TickCache(deltaMs);
            float deltaSeconds = Mathf.Max(0, deltaMs) / 1000f;

            for (int i = 0; i < units.AllocatedCount; i++)
            {
                if (!units.TryGetHandleByIndex(i, out BattleUnitHandle unit))
                {
                    ClearUnitState(i);
                    continue;
                }

                if (!units.IsAlive(unit))
                {
                    ClearUnitState(i);
                    continue;
                }

                if (units.IsHitLocked(unit))
                {
                    moving[i] = false;
                    continue;
                }

                if (skills.IsUnitBusy(unit))
                {
                    continue;
                }

                searchRemainingMs[i] = Mathf.Max(0, searchRemainingMs[i] - deltaMs);
                BattleUnitHandle target = targets[i];
                bool hasTarget = IsValidEnemyTarget(unit, target);
                if (!hasTarget)
                {
                    target = BattleUnitHandle.Invalid;
                    targets[i] = target;
                }

                if (!hasTarget || searchRemainingMs[i] <= 0)
                {
                    if (TrySearchTarget(unit, out target))
                    {
                        targets[i] = target;
                        hasTarget = true;
                        searchRemainingMs[i] = HasTargetSearchIntervalMs;
                    }
                    else
                    {
                        targets[i] = BattleUnitHandle.Invalid;
                        searchRemainingMs[i] = NoTargetSearchIntervalMs;
                        StopMoving(unit);
                        continue;
                    }
                }

                if (hasTarget)
                {
                    MoveTowardTarget(unit, target, deltaSeconds);
                }
            }
        }

        private void TickCache(int deltaMs)
        {
            cacheClearRemainingMs -= deltaMs;
            if (cacheClearRemainingMs > 0)
            {
                return;
            }

            cacheClearRemainingMs = CacheClearIntervalMs;
            ClearTargetCache();
        }

        private bool TrySearchTarget(BattleUnitHandle unit, out BattleUnitHandle target)
        {
            target = BattleUnitHandle.Invalid;
            int camp = units.GetCamp(unit);
            if (camp < 0 || camp >= MaxCampCount)
            {
                return false;
            }

            Vector2 position = units.GetPosition(unit);
            if (!collisions.TryGetCellIndex(position, out int cellIndex))
            {
                return false;
            }

            int cacheIndex = CacheIndex(cellIndex, camp);
            BattleUnitHandle cachedTarget = cachedTargets[cacheIndex];
            if (IsValidEnemyTarget(unit, cachedTarget))
            {
                target = cachedTarget;
                return true;
            }

            cachedTargets[cacheIndex] = BattleUnitHandle.Invalid;
            Vector2 origin = collisions.GetCellCenter(cellIndex);
            if (!collisions.QueryNearestByCellRings(origin, EnemyOptions(camp), out int targetIndex))
            {
                return false;
            }

            BattleUnitHandle queriedTarget = collisions.GetUnitHandle(targetIndex);
            if (!IsValidEnemyTarget(unit, queriedTarget))
            {
                return false;
            }

            cachedTargets[cacheIndex] = queriedTarget;
            target = queriedTarget;
            return true;
        }

        private void MoveTowardTarget(BattleUnitHandle unit, BattleUnitHandle target, float deltaSeconds)
        {
            float basicAttackRange = skills.GetBasicAttackRange(unit);
            if (basicAttackRange <= 0f)
            {
                StopMoving(unit);
                return;
            }

            Vector2 position = units.GetPosition(unit);
            Vector2 targetPosition = units.GetPosition(target);
            Vector2 toTarget = targetPosition - position;
            float distance = toTarget.magnitude;
            float stopDistance = Mathf.Max(0f, basicAttackRange);
            if (distance <= stopDistance || distance <= 0.0001f)
            {
                StopMoving(unit);
                return;
            }

            float speed = Mathf.Max(0f, units.GetAttr(unit, AttributeType.Speed) * SpeedScale);
            if (speed <= 0f)
            {
                StopMoving(unit);
                return;
            }

            Vector2 direction = toTarget / distance;
            float moveDistance = Mathf.Min(speed * deltaSeconds, distance - stopDistance);
            if (moveDistance <= 0.0001f)
            {
                StopMoving(unit);
                return;
            }

            Vector2 nextPosition = position + direction * moveDistance;
            units.SetPosition(unit, nextPosition);
            StartMoving(unit, direction);
        }

        private void StartMoving(BattleUnitHandle unit, Vector2 direction)
        {
            if (!units.IsValid(unit))
            {
                return;
            }

            int index = unit.index;
            if (direction.x < -FlipEpsilon && !flipX[index])
            {
                flipX[index] = true;
                renderWorld?.SetUnitFlipX(units.GetRenderHandle(unit), true);
            }
            else if (direction.x > FlipEpsilon && flipX[index])
            {
                flipX[index] = false;
                renderWorld?.SetUnitFlipX(units.GetRenderHandle(unit), false);
            }

            if (!moving[index])
            {
                moving[index] = true;
                renderWorld?.PlayUnitWalk(units.GetRenderHandle(unit));
            }
        }

        private void StopMoving(BattleUnitHandle unit)
        {
            if (!units.IsValid(unit))
            {
                return;
            }

            int index = unit.index;
            if (moving[index])
            {
                moving[index] = false;
                renderWorld?.PlayUnitIdle(units.GetRenderHandle(unit));
            }
        }

        private bool IsValidEnemyTarget(BattleUnitHandle source, BattleUnitHandle target)
        {
            if (!units.IsAlive(source) || !units.IsAlive(target))
            {
                return false;
            }

            if (source.SameAs(target) || units.GetCamp(source) == units.GetCamp(target))
            {
                return false;
            }

            return (units.GetState(target) & BattleUnitStates.Selectable) != 0;
        }

        private BattleCollisionQueryOptions EnemyOptions(int camp)
        {
            int campMask = camp >= 0 && camp < MaxCampCount ? ~(1 << camp) : 0;
            return new BattleCollisionQueryOptions
            {
                campMask = campMask,
                stateMask = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layerMask = 0,
                maxHits = 0,
                sortByDistance = false
            };
        }

        private int CacheIndex(int cellIndex, int camp)
        {
            return cellIndex * MaxCampCount + camp;
        }

        private void ClearUnitState(int index)
        {
            if (index < 0 || index >= targets.Length)
            {
                return;
            }

            targets[index] = BattleUnitHandle.Invalid;
            searchRemainingMs[index] = 0;
            moving[index] = false;
            flipX[index] = false;
        }

        private void ClearTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = BattleUnitHandle.Invalid;
                searchRemainingMs[i] = 0;
            }
        }

        private void ClearTargetCache()
        {
            for (int i = 0; i < cachedTargets.Length; i++)
            {
                cachedTargets[i] = BattleUnitHandle.Invalid;
            }
        }
    }
}
