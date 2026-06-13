using Game.Play.Battle.Collision;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Skill
{
    public sealed partial class BattleSkillManager
    {
        private static bool invalidCastRangeWarned;

        private void FireSkill(int slot, BattleUnitHandle caster, BattleUnitHandle target, BattleSkillRuntimeData skill, int baseSkillId)
        {
            if (!units.IsAlive(caster))
            {
                return;
            }

            BattleEffectContext context = BattleEffectContext.SkillDirect(baseSkillId, skill.id, GetLocalSlotIndex(slot));
            Vector2 origin = units.GetPosition(caster);
            bool canUseTarget = units.IsValid(target) && (!target.SameAs(caster) || skill.targetType == ConfigBattle.SkillTargetType.Self);
            Vector2 direction = canUseTarget ? units.GetPosition(target) - origin : Vector2.right;
            if (skill.shape != null && skill.shape.ShapeType == ConfigBattle.SkillShapeType.Circle && skill.shape.Radius > 0f)
            {
                ExecuteAreaSkill(caster, skill, origin, direction, context);
                return;
            }

            if (canUseTarget && units.IsAlive(target))
            {
                effects.ExecuteEffects(skill.effects, caster, target, origin, direction, context);
            }
        }

        private void ExecuteAreaSkill(BattleUnitHandle caster, BattleSkillRuntimeData skill, Vector2 origin, Vector2 direction, BattleEffectContext context)
        {
            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = origin + new Vector2(skill.shape.OffsetX, skill.shape.OffsetY),
                radius = skill.shape.Radius
            };
            collisions.Query(shape, EnemyOptions(caster, 0, false), queryBuffer);
            for (int i = 0; i < queryBuffer.Count; i++)
            {
                BattleUnitHandle target = collisions.GetUnitHandle(queryBuffer.TargetIndices[i]);
                if (target.SameAs(caster) && skill.targetType != ConfigBattle.SkillTargetType.Self)
                {
                    continue;
                }

                if (units.IsAlive(target))
                {
                    effects.ExecuteEffects(skill.effects, caster, target, origin, direction, context);
                }
            }
        }

        private BattleUnitHandle SelectTarget(BattleUnitHandle caster, int baseSkillId, BattleSkillRuntimeData skill, bool allowInterceptionFallback)
        {
            if (skill.targetType == ConfigBattle.SkillTargetType.Self)
            {
                return caster;
            }

            float radius = ResolveCastRange(caster, baseSkillId, skill);
            bool interceptLimited = IsInterceptLimitedSkill(skill);
            if (interceptLimited && TryGetReservedTargetInRange(caster, radius, out BattleUnitHandle reservedTarget))
            {
                return reservedTarget;
            }

            if (interceptLimited && !allowInterceptionFallback)
            {
                return BattleUnitHandle.Invalid;
            }

            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = units.GetPosition(caster),
                radius = radius
            };
            BattleCollisionQueryOptions options = EnemyOptions(caster, 0, true);
            IBattleCollisionTargetFilter targetFilter = null;
            if (interceptLimited && interceptionFilter != null)
            {
                interceptionFilter.Reset(caster);
                targetFilter = interceptionFilter;
            }

            if (skill.selectType == ConfigBattle.TargetSelectType.Nearest)
            {
                if (!collisions.QueryNearestCircle(shape.center, radius, options, targetFilter, out int nearestTargetIndex))
                {
                    return BattleUnitHandle.Invalid;
                }

                BattleUnitHandle nearestTarget = collisions.GetUnitHandle(nearestTargetIndex);
                if (nearestTarget.SameAs(caster) || !units.IsAlive(nearestTarget))
                {
                    return BattleUnitHandle.Invalid;
                }

                return !interceptLimited || interception.TryReserve(caster, nearestTarget)
                    ? nearestTarget
                    : BattleUnitHandle.Invalid;
            }

            collisions.Query(shape, options, queryBuffer);
            for (int i = 0; i < queryBuffer.Count; i++)
            {
                BattleUnitHandle target = collisions.GetUnitHandle(queryBuffer.TargetIndices[i]);
                if (!target.SameAs(caster)
                    && units.IsAlive(target)
                    && (!interceptLimited || interception.TryReserve(caster, target)))
                {
                    return target;
                }
            }

            return BattleUnitHandle.Invalid;
        }

        private bool TryGetReservedTargetInRange(BattleUnitHandle caster, float radius, out BattleUnitHandle target)
        {
            target = BattleUnitHandle.Invalid;
            if (interception == null)
            {
                return false;
            }

            BattleUnitHandle reservedTarget = interception.GetReservedTarget(caster);
            if (!units.IsAlive(reservedTarget))
            {
                return false;
            }

            if (!BattleCollisionMath.CircleHitsCircle(units.GetPosition(caster), radius, units.GetPosition(reservedTarget), units.GetRadius(reservedTarget)))
            {
                return false;
            }

            target = reservedTarget;
            return true;
        }

        private static bool IsInterceptLimitedSkill(BattleSkillRuntimeData skill)
        {
            if (skill.targetType == ConfigBattle.SkillTargetType.Self)
            {
                return false;
            }

            BattleEffectRef[] effects = skill.effects;
            for (int i = 0; i < (effects?.Length ?? 0); i++)
            {
                if (effects[i].type == ConfigBattle.EffectType.Projectile)
                {
                    return false;
                }
            }

            return true;
        }

        private float ResolveCastRange(BattleUnitHandle owner, int baseSkillId, BattleSkillRuntimeData skill)
        {
            float castRange;
            if (skill.castRange > 0f)
            {
                castRange = skill.castRange;
                return enhancements.ResolveCastRange(owner, baseSkillId, skill.id, castRange);
            }

            if (!invalidCastRangeWarned)
            {
                invalidCastRangeWarned = true;
                Debug.LogWarning($"[BattleSkill] Skill {skill.id} has invalid castRange {skill.castRange}. Fallback to 1.");
            }

            castRange = 1f;
            return enhancements.ResolveCastRange(owner, baseSkillId, skill.id, castRange);
        }

        private BattleCollisionQueryOptions EnemyOptions(BattleUnitHandle caster, int maxHits, bool sortByDistance)
        {
            int camp = units.GetCamp(caster);
            int campMask = camp >= 0 && camp < 32 ? ~(1 << camp) : 0;
            return new BattleCollisionQueryOptions
            {
                campMask = campMask,
                stateMask = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layerMask = 0,
                maxHits = maxHits,
                sortByDistance = sortByDistance
            };
        }
    }
}
