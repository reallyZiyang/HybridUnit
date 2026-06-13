using Game.Data.Configs.Attr;
using Game.Play.Battle.Unit;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Runtime
{
    public sealed class BattleEffectExecutor
    {
        private readonly BattleRuntimeData data;
        private readonly BattleUnitManager units;
        private readonly BattleCommandBuffer commands;
        private readonly BattleSkillEnhancementContext enhancements;

        public BattleEffectExecutor(BattleRuntimeData data, BattleUnitManager units, BattleCommandBuffer commands, BattleSkillEnhancementContext enhancements = null)
        {
            this.data = data;
            this.units = units;
            this.commands = commands;
            this.enhancements = enhancements ?? BattleSkillEnhancementContext.Empty;
        }

        public void ExecuteEffects(BattleEffectRef[] effects, BattleUnitHandle source, BattleUnitHandle target, Vector2 origin, Vector2 direction)
        {
            ExecuteEffects(effects, source, target, origin, direction, BattleEffectContext.None);
        }

        public void ExecuteEffects(BattleEffectRef[] effects, BattleUnitHandle source, BattleUnitHandle target, Vector2 origin, Vector2 direction, BattleEffectContext context)
        {
            BattleEffectRef[] resolvedEffects = enhancements.ResolveEffects(effects, context);
            if (resolvedEffects == null)
            {
                return;
            }

            for (int i = 0; i < resolvedEffects.Length; i++)
            {
                ExecuteEffect(resolvedEffects[i], source, target, origin, direction, context);
            }

            enhancements.ExecuteTriggerEffects(ToTriggerEventType(context.sourceType), source, target, origin, direction, context, this);
        }

        public void ExecuteEffect(BattleEffectRef effect, BattleUnitHandle source, BattleUnitHandle target, Vector2 origin, Vector2 direction, BattleEffectContext context)
        {
            switch (effect.type)
            {
                case ConfigBattle.EffectType.Damage:
                    if (data.TryGetDamageEffect(effect.id, out BattleDamageEffectRuntimeData damage))
                    {
                        long value = CalculateValue(source, damage.attr, damage.ratio, damage.fixedValue, effect.value);
                        commands.AddDamage(source, target, enhancements.ResolveEffectValue(value, effect, context), damage.playHitReaction, context);
                    }
                    break;
                case ConfigBattle.EffectType.Heal:
                    if (data.TryGetHealEffect(effect.id, out BattleHealEffectRuntimeData heal))
                    {
                        long value = CalculateValue(source, heal.attr, heal.ratio, heal.fixedValue, effect.value);
                        commands.AddHeal(source, target, enhancements.ResolveEffectValue(value, effect, context), context);
                    }
                    break;
                case ConfigBattle.EffectType.AddBuff:
                    if (data.TryGetAddBuffEffect(effect.id, out BattleAddBuffEffectRuntimeData addBuff))
                    {
                        commands.AddBuff(source, target, addBuff.buffId, addBuff.durationOverrideMs, Mathf.Max(1, addBuff.stack), context);
                    }
                    break;
                case ConfigBattle.EffectType.Projectile:
                    int projectileId = enhancements.ResolveProjectileId(source, effect.id, context);
                    if (data.TryGetProjectileEffect(projectileId, out _))
                    {
                        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
                        int projectileCount = enhancements.ResolveProjectileCount(source, projectileId, context, 1);
                        SpawnProjectiles(source, target, projectileId, origin, safeDirection, context, projectileCount);
                    }
                    break;
            }
        }

        private static ConfigBattle.TriggerEventType ToTriggerEventType(BattleEffectSourceType sourceType)
        {
            switch (sourceType)
            {
                case BattleEffectSourceType.SkillDirect:
                    return ConfigBattle.TriggerEventType.OnSkillCast;
                case BattleEffectSourceType.ProjectileHit:
                    return ConfigBattle.TriggerEventType.OnProjectileHit;
                case BattleEffectSourceType.BuffTick:
                    return ConfigBattle.TriggerEventType.OnBuffTick;
                default:
                    return ConfigBattle.TriggerEventType.None;
            }
        }

        private void SpawnProjectiles(BattleUnitHandle source, BattleUnitHandle target, int projectileId, Vector2 origin, Vector2 direction, BattleEffectContext context, int count)
        {
            int safeCount = Mathf.Max(1, count);
            if (safeCount == 1)
            {
                commands.SpawnProjectile(source, target, projectileId, origin, direction, context);
                return;
            }

            const float totalSpreadDeg = 12f;
            float startDeg = -totalSpreadDeg * 0.5f;
            float stepDeg = safeCount > 1 ? totalSpreadDeg / (safeCount - 1) : 0f;
            for (int i = 0; i < safeCount; i++)
            {
                float angleDeg = startDeg + stepDeg * i;
                commands.SpawnProjectile(source, target, projectileId, origin, Rotate(direction, angleDeg), context);
            }
        }

        private static Vector2 Rotate(Vector2 direction, float angleDeg)
        {
            float radians = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cos - direction.y * sin,
                direction.x * sin + direction.y * cos).normalized;
        }

        private long CalculateValue(BattleUnitHandle source, AttributeType attr, long ratio, long fixedValue, long effectValue)
        {
            long attrValue = attr == AttributeType.Null ? 0 : units.GetAttr(source, attr);
            long value = fixedValue + attrValue * ratio / 10000L;
            if (effectValue > 0)
            {
                value = value * effectValue / 10000L;
            }

            return value;
        }
    }
}
