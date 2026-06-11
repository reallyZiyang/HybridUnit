using Game.Play.Battle.Collision;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Skill
{
    public sealed partial class BattleSkillManager
    {
        private void FireSkill(BattleUnitHandle caster, BattleUnitHandle target, BattleSkillRuntimeData skill)
        {
            if (!units.IsAlive(caster))
            {
                return;
            }

            Vector2 origin = units.GetPosition(caster);
            bool canUseTarget = units.IsValid(target) && (!target.SameAs(caster) || skill.targetType == ConfigBattle.SkillTargetType.Self);
            Vector2 direction = canUseTarget ? units.GetPosition(target) - origin : Vector2.right;
            if (skill.shape != null && skill.shape.ShapeType == ConfigBattle.SkillShapeType.Circle && skill.shape.Radius > 0f)
            {
                ExecuteAreaSkill(caster, skill, origin, direction);
                return;
            }

            if (canUseTarget && units.IsAlive(target))
            {
                effects.ExecuteEffects(skill.effects, caster, target, origin, direction);
            }
        }

        private void ExecuteAreaSkill(BattleUnitHandle caster, BattleSkillRuntimeData skill, Vector2 origin, Vector2 direction)
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
                    effects.ExecuteEffects(skill.effects, caster, target, origin, direction);
                }
            }
        }

        private BattleUnitHandle SelectTarget(BattleUnitHandle caster, BattleSkillRuntimeData skill)
        {
            if (skill.targetType == ConfigBattle.SkillTargetType.Self)
            {
                return caster;
            }

            float radius = skill.shape != null && skill.shape.Radius > 0f ? skill.shape.Radius : 100f;
            BattleCollisionShape shape = new()
            {
                type = BattleCollisionShapeType.Circle,
                center = units.GetPosition(caster),
                radius = radius
            };
            collisions.Query(shape, EnemyOptions(caster, 0, true), queryBuffer);
            for (int i = 0; i < queryBuffer.Count; i++)
            {
                BattleUnitHandle target = collisions.GetUnitHandle(queryBuffer.TargetIndices[i]);
                if (!target.SameAs(caster) && units.IsAlive(target))
                {
                    return target;
                }
            }

            return BattleUnitHandle.Invalid;
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
