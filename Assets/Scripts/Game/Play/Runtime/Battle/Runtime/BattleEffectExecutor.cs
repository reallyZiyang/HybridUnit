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

        public BattleEffectExecutor(BattleRuntimeData data, BattleUnitManager units, BattleCommandBuffer commands)
        {
            this.data = data;
            this.units = units;
            this.commands = commands;
        }

        public void ExecuteEffects(BattleEffectRef[] effects, BattleUnitHandle source, BattleUnitHandle target, Vector2 origin, Vector2 direction)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                ExecuteEffect(effects[i], source, target, origin, direction);
            }
        }

        private void ExecuteEffect(BattleEffectRef effect, BattleUnitHandle source, BattleUnitHandle target, Vector2 origin, Vector2 direction)
        {
            switch (effect.type)
            {
                case ConfigBattle.EffectType.Damage:
                    if (data.TryGetDamageEffect(effect.id, out BattleDamageEffectRuntimeData damage))
                    {
                        commands.AddDamage(source, target, CalculateValue(source, damage.attr, damage.ratio, damage.fixedValue, effect.value));
                    }
                    break;
                case ConfigBattle.EffectType.Heal:
                    if (data.TryGetHealEffect(effect.id, out BattleHealEffectRuntimeData heal))
                    {
                        commands.AddHeal(source, target, CalculateValue(source, heal.attr, heal.ratio, heal.fixedValue, effect.value));
                    }
                    break;
                case ConfigBattle.EffectType.AddBuff:
                    if (data.TryGetAddBuffEffect(effect.id, out BattleAddBuffEffectRuntimeData addBuff))
                    {
                        commands.AddBuff(source, target, addBuff.buffId, addBuff.durationOverrideMs, Mathf.Max(1, addBuff.stack));
                    }
                    break;
                case ConfigBattle.EffectType.Projectile:
                    if (data.TryGetProjectileEffect(effect.id, out _))
                    {
                        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
                        commands.SpawnProjectile(source, target, effect.id, origin, safeDirection);
                    }
                    break;
            }
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
