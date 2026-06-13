using System;
using Game.Data.Configs.Attr;
using Game.Play.Battle.Unit;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Runtime
{
    public enum BattleEffectSourceType
    {
        None,
        SkillDirect,
        ProjectileHit,
        BuffBegin,
        BuffTick,
        BuffEnd,
        PassiveTrigger
    }

    public readonly struct BattleEffectContext
    {
        public static readonly BattleEffectContext None = new(BattleEffectSourceType.None, 0, 0, -1, 0, 0);

        public readonly BattleEffectSourceType sourceType;
        public readonly int baseSkillId;
        public readonly int skillId;
        public readonly int slotIndex;
        public readonly int projectileId;
        public readonly int buffId;

        public BattleEffectContext(BattleEffectSourceType sourceType, int baseSkillId, int skillId, int slotIndex, int projectileId, int buffId)
        {
            this.sourceType = sourceType;
            this.baseSkillId = baseSkillId;
            this.skillId = skillId;
            this.slotIndex = slotIndex;
            this.projectileId = projectileId;
            this.buffId = buffId;
        }

        public static BattleEffectContext SkillDirect(int baseSkillId, int skillId, int slotIndex)
            => new(BattleEffectSourceType.SkillDirect, baseSkillId, skillId, slotIndex, 0, 0);

        public BattleEffectContext AsProjectileHit(int projectileId)
            => new(BattleEffectSourceType.ProjectileHit, baseSkillId, skillId, slotIndex, projectileId, buffId);

        public BattleEffectContext AsBuffBegin(int buffId)
            => new(BattleEffectSourceType.BuffBegin, baseSkillId, skillId, slotIndex, projectileId, buffId);

        public BattleEffectContext AsBuffTick(int buffId)
            => new(BattleEffectSourceType.BuffTick, baseSkillId, skillId, slotIndex, projectileId, buffId);

        public BattleEffectContext AsBuffEnd(int buffId)
            => new(BattleEffectSourceType.BuffEnd, baseSkillId, skillId, slotIndex, projectileId, buffId);
    }

    public readonly struct BattleResolvedSkillTiming
    {
        public readonly int castPreMs;
        public readonly int castBackMs;
        public readonly int cooldownMs;
        public readonly int animationMs;
        public readonly float animationSpeed;

        public BattleResolvedSkillTiming(int castPreMs, int castBackMs, int cooldownMs, int animationMs, float animationSpeed)
        {
            this.castPreMs = Mathf.Max(0, castPreMs);
            this.castBackMs = Mathf.Max(0, castBackMs);
            this.cooldownMs = Mathf.Max(0, cooldownMs);
            this.animationMs = Mathf.Max(0, animationMs);
            this.animationSpeed = Mathf.Max(0.01f, animationSpeed);
        }
    }

    public sealed class BattleSkillEnhancementContext
    {
        private const int BasisPoint = 10000;
        private const int DefaultModifierCapacity = 32;

        public static readonly BattleSkillEnhancementContext Empty = new();

        private RuntimeModifier[] modifiers = new RuntimeModifier[DefaultModifierCapacity];
        private BattleUnitManager units;
        private int modifierCount;
        private int version;

        public int Version => version;

        public void BindUnits(BattleUnitManager unitManager)
        {
            units = unitManager;
        }

        public void Clear()
        {
            modifierCount = 0;
            version++;
        }

        public void AddOrUpdate(ConfigBattle.SkillEnhancementCfg config, int stack)
        {
            if (config == null || stack <= 0)
            {
                return;
            }

            int existing = IndexOf(config.Id);
            RuntimeModifier modifier = RuntimeModifier.FromConfig(config, stack);
            int oldStack = existing >= 0 ? modifiers[existing].Stack : 0;
            if (existing >= 0)
            {
                modifiers[existing] = modifier;
            }
            else
            {
                EnsureModifierCapacity(modifierCount + 1);
                modifiers[modifierCount++] = modifier;
            }

            version++;
            if (modifier.TargetType == ConfigBattle.ModifierTargetType.Unit)
            {
                ApplyUnitModifierDelta(modifier, modifier.Stack - oldStack);
            }
        }

        public int ResolveSkillId(BattleUnitHandle owner, int localSlotIndex, int skillId)
        {
            return skillId;
        }

        public int ResolveSkillId(BattleUnitHandle owner, int skillId)
        {
            return ResolveSkillId(owner, -1, skillId);
        }

        public BattleResolvedSkillTiming ResolveTiming(
            BattleUnitHandle owner,
            int localSlotIndex,
            int baseSkillId,
            int skillId,
            int castPreMs,
            int castBackMs,
            int cooldownMs,
            int animationMs)
        {
            int attackSpeedBp = 0;
            int cooldownReductionBp = 0;
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (!modifier.Match(owner, localSlotIndex, baseSkillId, skillId, units))
                {
                    continue;
                }

                int value = modifier.IntValue * modifier.Stack;
                switch (modifier.SkillType)
                {
                    case ConfigBattle.SkillModifierType.AttackSpeed:
                        attackSpeedBp += value;
                        break;
                    case ConfigBattle.SkillModifierType.CooldownReduction:
                        cooldownReductionBp += value;
                        break;
                }
            }

            int resolvedCastPreMs = ApplyAttackSpeed(castPreMs, attackSpeedBp);
            int resolvedCastBackMs = ApplyAttackSpeed(castBackMs, attackSpeedBp);
            int resolvedCooldownMs = ApplyAttackSpeed(cooldownMs, attackSpeedBp);
            resolvedCooldownMs = ApplyCooldownReduction(resolvedCooldownMs, cooldownReductionBp);
            int resolvedAnimationMs = ApplyAttackSpeed(animationMs, attackSpeedBp);
            float animationSpeed = (BasisPoint + Mathf.Max(0, attackSpeedBp)) / (float)BasisPoint;
            return new BattleResolvedSkillTiming(resolvedCastPreMs, resolvedCastBackMs, resolvedCooldownMs, resolvedAnimationMs, animationSpeed);
        }

        public float ResolveAnimationSpeed(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            int attackSpeedBp = 0;
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.SkillType == ConfigBattle.SkillModifierType.AttackSpeed
                    && modifier.Match(owner, localSlotIndex, baseSkillId, skillId, units))
                {
                    attackSpeedBp += modifier.IntValue * modifier.Stack;
                }
            }

            return (BasisPoint + Mathf.Max(0, attackSpeedBp)) / (float)BasisPoint;
        }

        public int ResolveCooldownMs(BattleUnitHandle owner, int baseSkillId, int skillId, int cooldownMs)
        {
            return ResolveTiming(owner, -1, baseSkillId, skillId, 0, 0, cooldownMs, 0).cooldownMs;
        }

        public float ResolveCastRange(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId, float castRange)
        {
            return Mathf.Max(0f, castRange);
        }

        public float ResolveCastRange(BattleUnitHandle owner, int baseSkillId, int skillId, float castRange)
        {
            return ResolveCastRange(owner, -1, baseSkillId, skillId, castRange);
        }

        public BattleEffectRef[] ResolveEffects(BattleEffectRef[] effects, BattleEffectContext context)
        {
            return effects ?? Array.Empty<BattleEffectRef>();
        }

        public long ResolveEffectValue(long value, BattleEffectRef effect, BattleEffectContext context)
        {
            return value;
        }

        public void ApplyUnitModifiers(BattleUnitHandle unit)
        {
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.TargetType == ConfigBattle.ModifierTargetType.Unit && modifier.MatchUnit(unit, units))
                {
                    ApplyUnitModifier(unit, modifier, modifier.Stack);
                }
            }
        }

        public int ResolveProjectileId(BattleUnitHandle source, int projectileId, BattleEffectContext context)
        {
            int resolvedProjectileId = projectileId;
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.SkillType != ConfigBattle.SkillModifierType.ReplaceProjectile
                    || !modifier.Match(source, context.slotIndex, context.baseSkillId, context.skillId, units))
                {
                    continue;
                }

                if (modifier.IntValue > 0)
                {
                    resolvedProjectileId = modifier.IntValue;
                }
            }

            return resolvedProjectileId;
        }

        public int ResolveProjectileCount(BattleUnitHandle source, int projectileId, BattleEffectContext context, int baseCount)
        {
            int count = Mathf.Max(1, baseCount);
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.SkillType == ConfigBattle.SkillModifierType.ProjectileNum
                    && modifier.Match(source, context.slotIndex, context.baseSkillId, context.skillId, units))
                {
                    count += modifier.IntValue * modifier.Stack;
                }
            }

            return Mathf.Max(1, count);
        }

        public int ResolveProjectileLifetimeMs(BattleUnitHandle source, int projectileId, BattleEffectContext context, int lifetimeMs)
        {
            return Mathf.Max(1, lifetimeMs);
        }

        public int ResolveProjectilePierceCount(BattleUnitHandle source, int projectileId, BattleEffectContext context, int pierceCount)
        {
            int resolved = Mathf.Max(1, pierceCount);
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.SkillType == ConfigBattle.SkillModifierType.ProjectilePierce
                    && modifier.Match(source, context.slotIndex, context.baseSkillId, context.skillId, units))
                {
                    resolved += modifier.IntValue * modifier.Stack;
                }
            }

            return Mathf.Max(1, resolved);
        }

        public float ResolveProjectileSpeed(BattleUnitHandle source, int projectileId, BattleEffectContext context, float speed)
        {
            return Mathf.Max(0f, speed);
        }

        public float ResolveProjectileRadius(BattleUnitHandle source, int projectileId, BattleEffectContext context, float radius)
        {
            return Mathf.Max(0f, radius);
        }

        public int ResolveProjectileHitIntervalMs(BattleUnitHandle source, int projectileId, BattleEffectContext context, int hitIntervalMs)
        {
            return Mathf.Max(0, hitIntervalMs);
        }

        public float ResolveProjectileHitAreaRadius(BattleUnitHandle source, int projectileId, BattleEffectContext context)
        {
            int radiusMilli = 0;
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.SkillType == ConfigBattle.SkillModifierType.ProjectileHitArea
                    && modifier.Match(source, context.slotIndex, context.baseSkillId, context.skillId, units))
                {
                    radiusMilli += modifier.IntValue * modifier.Stack;
                }
            }

            return Mathf.Max(0f, radiusMilli / 1000f);
        }

        private int IndexOf(int enhancementId)
        {
            for (int i = 0; i < modifierCount; i++)
            {
                if (modifiers[i].EnhancementId == enhancementId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureModifierCapacity(int capacity)
        {
            if (modifiers.Length >= capacity)
            {
                return;
            }

            int next = Mathf.Max(capacity, modifiers.Length * 2);
            Array.Resize(ref modifiers, next);
        }

        private static int ApplyAttackSpeed(int valueMs, int attackSpeedBp)
        {
            if (valueMs <= 0)
            {
                return 0;
            }

            int denominator = BasisPoint + Mathf.Max(0, attackSpeedBp);
            return Mathf.Max(1, (int)((long)valueMs * BasisPoint / denominator));
        }

        private static int ApplyCooldownReduction(int valueMs, int cooldownReductionBp)
        {
            if (valueMs <= 0)
            {
                return 0;
            }

            int multiplier = Mathf.Max(0, BasisPoint - Mathf.Max(0, cooldownReductionBp));
            return Mathf.Max(0, (int)((long)valueMs * multiplier / BasisPoint));
        }

        private void ApplyUnitModifierDelta(RuntimeModifier modifier, int stackDelta)
        {
            if (units == null || stackDelta == 0)
            {
                return;
            }

            for (int i = 0; i < units.AllocatedCount; i++)
            {
                if (!units.TryGetHandleByIndex(i, out BattleUnitHandle unit) || !modifier.MatchUnit(unit, units))
                {
                    continue;
                }

                ApplyUnitModifier(unit, modifier, stackDelta);
            }
        }

        private void ApplyUnitModifier(BattleUnitHandle unit, RuntimeModifier modifier, int stackDelta)
        {
            AttributeType attr = (AttributeType)modifier.ModifierType;
            if (attr == AttributeType.Null)
            {
                return;
            }

            long delta = modifier.ValueType == Game.Data.Configs.Attr.ValueType.RatioBp
                ? units.GetBaseAttr(unit, attr) * modifier.IntValue / BasisPoint
                : modifier.IntValue;
            if (delta != 0L)
            {
                units.AddModifierAttr(unit, attr, delta * stackDelta);
            }
        }

        private readonly struct RuntimeModifier
        {
            public readonly int EnhancementId;
            public readonly int Stack;
            public readonly int RequiredUnitFlags;
            public readonly int ForbiddenUnitFlags;
            public readonly int RequiredRoleFlags;
            public readonly int ForbiddenRoleFlags;
            public readonly int[] UnitCfgIds;
            public readonly int SlotIndex;
            public readonly int[] SkillIds;
            public readonly ConfigBattle.ModifierTargetType TargetType;
            public readonly ConfigBattle.SkillModifierType SkillType;
            public readonly int ModifierType;
            public readonly Game.Data.Configs.Attr.ValueType ValueType;
            public readonly int IntValue;

            private RuntimeModifier(
                int enhancementId,
                int stack,
                int requiredUnitFlags,
                int forbiddenUnitFlags,
                int requiredRoleFlags,
                int forbiddenRoleFlags,
                int[] unitCfgIds,
                int slotIndex,
                int[] skillIds,
                ConfigBattle.ModifierTargetType targetType,
                ConfigBattle.SkillModifierType skillType,
                int modifierType,
                Game.Data.Configs.Attr.ValueType valueType,
                int intValue)
            {
                EnhancementId = enhancementId;
                Stack = Mathf.Max(1, stack);
                RequiredUnitFlags = requiredUnitFlags;
                ForbiddenUnitFlags = forbiddenUnitFlags;
                RequiredRoleFlags = requiredRoleFlags;
                ForbiddenRoleFlags = forbiddenRoleFlags;
                UnitCfgIds = unitCfgIds ?? Array.Empty<int>();
                SlotIndex = slotIndex;
                SkillIds = skillIds ?? Array.Empty<int>();
                TargetType = targetType;
                SkillType = skillType;
                ModifierType = modifierType;
                ValueType = valueType;
                IntValue = intValue;
            }

            public static RuntimeModifier FromConfig(ConfigBattle.SkillEnhancementCfg config, int stack)
            {
                ConfigBattle.UnitSelector unitSelector = config.UnitSelector;
                ConfigBattle.SkillSelector skillSelector = config.SkillSelector;
                ConfigBattle.ModifierValue value = config.Value;
                return new RuntimeModifier(
                    config.Id,
                    stack,
                    (int)(unitSelector?.RequiredUnitFlags ?? ConfigBattle.UnitFlag.None),
                    (int)(unitSelector?.ForbiddenUnitFlags ?? ConfigBattle.UnitFlag.None),
                    (int)(unitSelector?.RequiredRoleFlags ?? ConfigBattle.UnitRoleFlag.None),
                    (int)(unitSelector?.ForbiddenRoleFlags ?? ConfigBattle.UnitRoleFlag.None),
                    unitSelector?.UnitCfgIds,
                    skillSelector?.SlotIndex ?? -1,
                    skillSelector?.SkillIds,
                    config.TargetType,
                    (ConfigBattle.SkillModifierType)config.ModifierType,
                    config.ModifierType,
                    value?.Type ?? Game.Data.Configs.Attr.ValueType.Null,
                    value?.IntValue ?? 0);
            }

            public bool Match(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId, BattleUnitManager units)
            {
                if (TargetType != ConfigBattle.ModifierTargetType.Skill && TargetType != ConfigBattle.ModifierTargetType.Projectile)
                {
                    return false;
                }

                if (!MatchUnit(owner, units))
                {
                    return false;
                }

                if (SlotIndex >= 0 && localSlotIndex >= 0 && SlotIndex != localSlotIndex)
                {
                    return false;
                }

                if (SkillIds.Length <= 0)
                {
                    return true;
                }

                for (int i = 0; i < SkillIds.Length; i++)
                {
                    int candidate = SkillIds[i];
                    if (candidate == baseSkillId || candidate == skillId)
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool MatchUnit(BattleUnitHandle owner, BattleUnitManager units)
            {
                if (units == null || !units.IsValid(owner))
                {
                    return RequiredUnitFlags == 0 && RequiredRoleFlags == 0 && UnitCfgIds.Length == 0;
                }

                int unitFlags = units.GetUnitFlags(owner);
                int roleFlags = units.GetRoleFlags(owner);
                if ((unitFlags & RequiredUnitFlags) != RequiredUnitFlags)
                {
                    return false;
                }

                if ((unitFlags & ForbiddenUnitFlags) != 0)
                {
                    return false;
                }

                if ((roleFlags & RequiredRoleFlags) != RequiredRoleFlags)
                {
                    return false;
                }

                if ((roleFlags & ForbiddenRoleFlags) != 0)
                {
                    return false;
                }

                if (UnitCfgIds.Length <= 0)
                {
                    return true;
                }

                int unitCfgId = units.GetUnitCfgId(owner);
                for (int i = 0; i < UnitCfgIds.Length; i++)
                {
                    if (UnitCfgIds[i] == unitCfgId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
