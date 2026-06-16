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
        private const int TriggerEventTypeCapacity = 8;

        public static readonly BattleSkillEnhancementContext Empty = new();

        private RuntimeModifier[] modifiers = new RuntimeModifier[DefaultModifierCapacity];
        private readonly BattleSkillPropertyStore skillPropertyStore = new();
        private readonly BattleProjectilePropertyStore projectilePropertyStore = new();
        private readonly int[] triggerBucketCounts = new int[TriggerEventTypeCapacity];
        private int[] triggerBucketIndices = new int[DefaultModifierCapacity * TriggerEventTypeCapacity];
        private BattleUnitManager units;
        private int modifierCount;
        private int version;
        private bool triggerBucketsDirty = true;

        public int Version => version;
        public int ModifierCount => modifierCount;

        public void BindUnits(BattleUnitManager unitManager)
        {
            units = unitManager;
        }

        public void BindRuntime(BattleUnitManager unitManager, int unitCapacity, int slotsPerUnit)
        {
            units = unitManager;
            skillPropertyStore.Initialize(unitCapacity, slotsPerUnit);
            projectilePropertyStore.Initialize(unitCapacity, slotsPerUnit);
        }

        public void Clear()
        {
            for (int i = 0; i < modifierCount; i++)
            {
                if (modifiers[i].TargetType == ConfigBattle.ModifierTargetType.Unit)
                {
                    ApplyUnitModifierDelta(modifiers[i], -modifiers[i].Stack);
                }
            }

            modifierCount = 0;
            version++;
            skillPropertyStore.Clear();
            projectilePropertyStore.Clear();
            Array.Clear(triggerBucketCounts, 0, triggerBucketCounts.Length);
            triggerBucketsDirty = true;
        }

        public void AddOrUpdate(ConfigBattle.SkillEnhancementCfg config, int stack)
        {
            if (config == null || stack <= 0)
            {
                return;
            }

            int existing = IndexOfSource(ConfigBattle.ModifierSourceType.RogueEnhancement, config.Id);
            RuntimeModifier modifier = RuntimeModifier.FromConfig(config, stack);
            if (existing >= 0)
            {
                ApplyRemovedModifier(modifiers[existing]);
                modifiers[existing] = modifier;
            }
            else
            {
                EnsureModifierCapacity(modifierCount + 1);
                modifiers[modifierCount++] = modifier;
            }

            version++;
            ApplyAddedModifier(modifier);
        }

        public void AddSourceModifiers(
            ConfigBattle.ModifierSourceType sourceType,
            int sourceId,
            BattleUnitHandle target,
            ConfigBattle.BattleModifierRef[] refs,
            int stack)
        {
            RemoveSourceModifiers(sourceType, sourceId);
            if (refs == null || refs.Length == 0 || sourceType == ConfigBattle.ModifierSourceType.None || sourceId == 0 || stack <= 0)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < refs.Length; i++)
            {
                ConfigBattle.BattleModifierRef modifierRef = refs[i];
                if (modifierRef == null)
                {
                    continue;
                }

                RuntimeModifier modifier = RuntimeModifier.FromRef(sourceType, sourceId, target, modifierRef, stack);
                EnsureModifierCapacity(modifierCount + 1);
                modifiers[modifierCount++] = modifier;
                ApplyAddedModifier(modifier);
                changed = true;
            }

            if (changed)
            {
                version++;
            }
        }

        public void RemoveSourceModifiers(ConfigBattle.ModifierSourceType sourceType, int sourceId)
        {
            if (sourceType == ConfigBattle.ModifierSourceType.None || sourceId == 0)
            {
                return;
            }

            bool changed = false;
            int write = 0;
            for (int read = 0; read < modifierCount; read++)
            {
                RuntimeModifier modifier = modifiers[read];
                if (modifier.SourceType == sourceType && modifier.SourceId == sourceId)
                {
                    ApplyRemovedModifier(modifier);
                    changed = true;
                    continue;
                }

                if (write != read)
                {
                    modifiers[write] = modifier;
                }

                write++;
            }

            for (int i = write; i < modifierCount; i++)
            {
                modifiers[i] = default;
            }

            if (changed)
            {
                modifierCount = write;
                version++;
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
            BattleSkillProperties properties = ResolveSkillProperties(owner, localSlotIndex, baseSkillId, skillId);
            int attackSpeedBp = properties.attackSpeedBp;
            int cooldownReductionBp = properties.cooldownReductionBp;

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
            int attackSpeedBp = ResolveSkillProperties(owner, localSlotIndex, baseSkillId, skillId).attackSpeedBp;
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

        public void ExecuteTriggerEffects(
            ConfigBattle.TriggerEventType eventType,
            BattleUnitHandle source,
            BattleUnitHandle target,
            Vector2 origin,
            Vector2 direction,
            BattleEffectContext context,
            BattleEffectExecutor executor)
        {
            if (eventType == ConfigBattle.TriggerEventType.None || executor == null)
            {
                return;
            }

            EnsureTriggerBuckets();
            int eventIndex = (int)eventType;
            if (eventIndex <= 0 || eventIndex >= TriggerEventTypeCapacity)
            {
                return;
            }

            int bucketStart = eventIndex * modifiers.Length;
            int count = triggerBucketCounts[eventIndex];
            for (int i = 0; i < count; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[triggerBucketIndices[bucketStart + i]];
                if (modifier.MatchTrigger(source, context.slotIndex, context.baseSkillId, context.skillId, units))
                {
                    ExecuteTriggerModifier(modifier, source, target, origin, direction, context, executor);
                }
            }
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
            BattleProjectileProperties properties = ResolveProjectileProperties(source, context.slotIndex, context.baseSkillId, context.skillId);
            return properties.replaceProjectileId > 0 ? properties.replaceProjectileId : projectileId;
        }

        public int ResolveProjectileCount(BattleUnitHandle source, int projectileId, BattleEffectContext context, int baseCount)
        {
            BattleSkillProperties properties = ResolveSkillProperties(source, context.slotIndex, context.baseSkillId, context.skillId);
            return Mathf.Max(1, Mathf.Max(1, baseCount) + properties.projectileNumAdd);
        }

        public int ResolveProjectileLifetimeMs(BattleUnitHandle source, int projectileId, BattleEffectContext context, int lifetimeMs)
        {
            return Mathf.Max(1, lifetimeMs);
        }

        public int ResolveProjectilePierceCount(BattleUnitHandle source, int projectileId, BattleEffectContext context, int pierceCount)
        {
            BattleProjectileProperties properties = ResolveProjectileProperties(source, context.slotIndex, context.baseSkillId, context.skillId);
            return Mathf.Max(1, Mathf.Max(1, pierceCount) + properties.pierceAdd);
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
            BattleProjectileProperties properties = ResolveProjectileProperties(source, context.slotIndex, context.baseSkillId, context.skillId);
            return Mathf.Max(0f, properties.hitAreaMilli / 1000f);
        }

        public BattleSkillProperties ResolveSkillProperties(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            if (skillPropertyStore.TryGetCached(owner, localSlotIndex, out BattleSkillProperties properties))
            {
                return properties;
            }

            properties = BuildSkillProperties(owner, localSlotIndex, baseSkillId, skillId);
            skillPropertyStore.Set(owner, localSlotIndex, properties);
            return properties;
        }

        public BattleProjectileProperties ResolveProjectileProperties(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            if (projectilePropertyStore.TryGetCached(owner, localSlotIndex, out BattleProjectileProperties properties))
            {
                return properties;
            }

            properties = BuildProjectileProperties(owner, localSlotIndex, baseSkillId, skillId);
            projectilePropertyStore.Set(owner, localSlotIndex, properties);
            return properties;
        }

        public BattleSkillProperties GetDebugSkillProperties(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            return ResolveSkillProperties(owner, localSlotIndex, baseSkillId, skillId);
        }

        public BattleProjectileProperties GetDebugProjectileProperties(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            return ResolveProjectileProperties(owner, localSlotIndex, baseSkillId, skillId);
        }

        public int GetDebugTriggerBucketCount(ConfigBattle.TriggerEventType eventType)
        {
            EnsureTriggerBuckets();
            int eventIndex = (int)eventType;
            return eventIndex > 0 && eventIndex < TriggerEventTypeCapacity ? triggerBucketCounts[eventIndex] : 0;
        }

        private BattleSkillProperties BuildSkillProperties(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            int projectileNumAdd = 0;
            int attackSpeedBp = 0;
            int cooldownReductionBp = 0;
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.TargetType != ConfigBattle.ModifierTargetType.Skill
                    || !modifier.Match(owner, localSlotIndex, baseSkillId, skillId, units))
                {
                    continue;
                }

                int value = modifier.IntValue * modifier.Stack;
                switch (modifier.SkillType)
                {
                    case ConfigBattle.SkillModifierType.ProjectileNum:
                        projectileNumAdd += value;
                        break;
                    case ConfigBattle.SkillModifierType.AttackSpeed:
                        attackSpeedBp += value;
                        break;
                    case ConfigBattle.SkillModifierType.CooldownReduction:
                        cooldownReductionBp += value;
                        break;
                }
            }

            return new BattleSkillProperties(projectileNumAdd, attackSpeedBp, cooldownReductionBp);
        }

        private BattleProjectileProperties BuildProjectileProperties(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId)
        {
            int replaceProjectileId = 0;
            int pierceAdd = 0;
            int hitAreaMilli = 0;
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.TargetType != ConfigBattle.ModifierTargetType.Projectile
                    || !modifier.Match(owner, localSlotIndex, baseSkillId, skillId, units))
                {
                    continue;
                }

                int value = modifier.IntValue * modifier.Stack;
                switch (modifier.ProjectileType)
                {
                    case ConfigBattle.ProjectileModifierType.ReplaceProjectile:
                        if (modifier.IntValue > 0)
                        {
                            replaceProjectileId = modifier.IntValue;
                        }
                        break;
                    case ConfigBattle.ProjectileModifierType.ProjectilePierce:
                        pierceAdd += value;
                        break;
                    case ConfigBattle.ProjectileModifierType.ProjectileHitArea:
                        hitAreaMilli += value;
                        break;
                }
            }

            return new BattleProjectileProperties(replaceProjectileId, pierceAdd, hitAreaMilli);
        }

        private int IndexOfSource(ConfigBattle.ModifierSourceType sourceType, int sourceId)
        {
            for (int i = 0; i < modifierCount; i++)
            {
                if (modifiers[i].SourceType == sourceType && modifiers[i].SourceId == sourceId)
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
            Array.Resize(ref triggerBucketIndices, next * TriggerEventTypeCapacity);
            triggerBucketsDirty = true;
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

        private void EnsureTriggerBuckets()
        {
            if (!triggerBucketsDirty)
            {
                return;
            }

            Array.Clear(triggerBucketCounts, 0, triggerBucketCounts.Length);
            for (int i = 0; i < modifierCount; i++)
            {
                ref RuntimeModifier modifier = ref modifiers[i];
                if (modifier.TargetType != ConfigBattle.ModifierTargetType.Trigger)
                {
                    continue;
                }

                int eventIndex = (int)modifier.TriggerEventType;
                if (eventIndex <= 0 || eventIndex >= TriggerEventTypeCapacity)
                {
                    continue;
                }

                int count = triggerBucketCounts[eventIndex];
                if (count >= modifiers.Length)
                {
                    continue;
                }

                triggerBucketIndices[eventIndex * modifiers.Length + count] = i;
                triggerBucketCounts[eventIndex] = count + 1;
            }

            triggerBucketsDirty = false;
        }

        private void ExecuteTriggerModifier(
            RuntimeModifier modifier,
            BattleUnitHandle source,
            BattleUnitHandle target,
            Vector2 origin,
            Vector2 direction,
            BattleEffectContext context,
            BattleEffectExecutor executor)
        {
            int repeat = Mathf.Max(1, modifier.IntValue) * modifier.Stack;
            switch (modifier.TriggerType)
            {
                case ConfigBattle.TriggerModifierType.AddEffect:
                case ConfigBattle.TriggerModifierType.AddBuff:
                case ConfigBattle.TriggerModifierType.AddProjectile:
                    for (int i = 0; i < repeat; i++)
                    {
                        executor.ExecuteEffect(modifier.Effect, source, target, origin, direction, context);
                    }
                    break;
            }
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

        private void ApplyAddedModifier(RuntimeModifier modifier)
        {
            if (modifier.TargetType == ConfigBattle.ModifierTargetType.Unit)
            {
                ApplyUnitModifierDelta(modifier, modifier.Stack);
            }
            else if (modifier.TargetType == ConfigBattle.ModifierTargetType.Skill)
            {
                skillPropertyStore.MarkAllDirty();
            }
            else if (modifier.TargetType == ConfigBattle.ModifierTargetType.Projectile)
            {
                projectilePropertyStore.MarkAllDirty();
            }
            else if (modifier.TargetType == ConfigBattle.ModifierTargetType.Trigger)
            {
                triggerBucketsDirty = true;
            }
        }

        private void ApplyRemovedModifier(RuntimeModifier modifier)
        {
            if (modifier.TargetType == ConfigBattle.ModifierTargetType.Unit)
            {
                ApplyUnitModifierDelta(modifier, -modifier.Stack);
            }
            else if (modifier.TargetType == ConfigBattle.ModifierTargetType.Skill)
            {
                skillPropertyStore.MarkAllDirty();
            }
            else if (modifier.TargetType == ConfigBattle.ModifierTargetType.Projectile)
            {
                projectilePropertyStore.MarkAllDirty();
            }
            else if (modifier.TargetType == ConfigBattle.ModifierTargetType.Trigger)
            {
                triggerBucketsDirty = true;
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
            public readonly ConfigBattle.ModifierSourceType SourceType;
            public readonly int SourceId;
            public readonly BattleUnitHandle SpecificUnit;
            public readonly int Stack;
            public readonly ConfigBattle.ModifierCampType CampType;
            public readonly int RequiredUnitFlags;
            public readonly int ForbiddenUnitFlags;
            public readonly int RequiredRoleFlags;
            public readonly int ForbiddenRoleFlags;
            public readonly int[] UnitCfgIds;
            public readonly int SlotIndex;
            public readonly int[] SkillIds;
            public readonly ConfigBattle.ModifierTargetType TargetType;
            public readonly ConfigBattle.SkillModifierType SkillType;
            public readonly ConfigBattle.ProjectileModifierType ProjectileType;
            public readonly ConfigBattle.TriggerEventType TriggerEventType;
            public readonly ConfigBattle.TriggerModifierType TriggerType;
            public readonly BattleEffectRef Effect;
            public readonly int ModifierType;
            public readonly Game.Data.Configs.Attr.ValueType ValueType;
            public readonly int IntValue;

            private RuntimeModifier(
                int enhancementId,
                ConfigBattle.ModifierSourceType sourceType,
                int sourceId,
                BattleUnitHandle specificUnit,
                int stack,
                ConfigBattle.ModifierCampType campType,
                int requiredUnitFlags,
                int forbiddenUnitFlags,
                int requiredRoleFlags,
                int forbiddenRoleFlags,
                int[] unitCfgIds,
                int slotIndex,
                int[] skillIds,
                ConfigBattle.ModifierTargetType targetType,
                ConfigBattle.SkillModifierType skillType,
                ConfigBattle.ProjectileModifierType projectileType,
                ConfigBattle.TriggerEventType triggerEventType,
                ConfigBattle.TriggerModifierType triggerType,
                BattleEffectRef effect,
                int modifierType,
                Game.Data.Configs.Attr.ValueType valueType,
                int intValue)
            {
                EnhancementId = enhancementId;
                SourceType = sourceType;
                SourceId = sourceId;
                SpecificUnit = specificUnit;
                Stack = Mathf.Max(1, stack);
                CampType = campType;
                RequiredUnitFlags = requiredUnitFlags;
                ForbiddenUnitFlags = forbiddenUnitFlags;
                RequiredRoleFlags = requiredRoleFlags;
                ForbiddenRoleFlags = forbiddenRoleFlags;
                UnitCfgIds = unitCfgIds ?? Array.Empty<int>();
                SlotIndex = slotIndex;
                SkillIds = skillIds ?? Array.Empty<int>();
                TargetType = targetType;
                SkillType = skillType;
                ProjectileType = projectileType;
                TriggerEventType = triggerEventType;
                TriggerType = triggerType;
                Effect = effect;
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
                    ConfigBattle.ModifierSourceType.RogueEnhancement,
                    config.Id,
                    BattleUnitHandle.Invalid,
                    stack,
                    unitSelector?.CampType ?? ConfigBattle.ModifierCampType.Player,
                    (int)(unitSelector?.RequiredUnitFlags ?? ConfigBattle.UnitFlag.None),
                    (int)(unitSelector?.ForbiddenUnitFlags ?? ConfigBattle.UnitFlag.None),
                    (int)(unitSelector?.RequiredRoleFlags ?? ConfigBattle.UnitRoleFlag.None),
                    (int)(unitSelector?.ForbiddenRoleFlags ?? ConfigBattle.UnitRoleFlag.None),
                    unitSelector?.UnitCfgIds,
                    skillSelector?.SlotIndex ?? -1,
                    skillSelector?.SkillIds,
                    config.TargetType,
                    (ConfigBattle.SkillModifierType)config.ModifierType,
                    (ConfigBattle.ProjectileModifierType)config.ModifierType,
                    config.TriggerEventType,
                    (ConfigBattle.TriggerModifierType)config.ModifierType,
                    ToRuntimeEffect(config.Effect),
                    config.ModifierType,
                    value?.Type ?? Game.Data.Configs.Attr.ValueType.Null,
                    value?.IntValue ?? 0);
            }

            public static RuntimeModifier FromRef(
                ConfigBattle.ModifierSourceType sourceType,
                int sourceId,
                BattleUnitHandle specificUnit,
                ConfigBattle.BattleModifierRef modifierRef,
                int stack)
            {
                ConfigBattle.ModifierValue value = modifierRef.Value;
                return new RuntimeModifier(
                    0,
                    sourceType,
                    sourceId,
                    specificUnit,
                    stack,
                    ConfigBattle.ModifierCampType.Any,
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<int>(),
                    -1,
                    Array.Empty<int>(),
                    modifierRef.TargetType,
                    (ConfigBattle.SkillModifierType)modifierRef.ModifierType,
                    (ConfigBattle.ProjectileModifierType)modifierRef.ModifierType,
                    ConfigBattle.TriggerEventType.None,
                    (ConfigBattle.TriggerModifierType)modifierRef.ModifierType,
                    ToRuntimeEffect(modifierRef.Effect),
                    modifierRef.ModifierType,
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

                return MatchSkill(localSlotIndex, baseSkillId, skillId);
            }

            public bool MatchTrigger(BattleUnitHandle owner, int localSlotIndex, int baseSkillId, int skillId, BattleUnitManager units)
            {
                return TargetType == ConfigBattle.ModifierTargetType.Trigger
                    && MatchUnit(owner, units)
                    && MatchSkill(localSlotIndex, baseSkillId, skillId);
            }

            private bool MatchSkill(int localSlotIndex, int baseSkillId, int skillId)
            {
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
                if (SpecificUnit.IsValid)
                {
                    return units != null
                        && units.IsValid(owner)
                        && owner.index == SpecificUnit.index
                        && owner.generation == SpecificUnit.generation;
                }

                if (units == null || !units.IsValid(owner))
                {
                    return CampType == ConfigBattle.ModifierCampType.Any
                        && RequiredUnitFlags == 0
                        && RequiredRoleFlags == 0
                        && UnitCfgIds.Length == 0;
                }

                if (!MatchCamp(units.GetCamp(owner)))
                {
                    return false;
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

            private bool MatchCamp(int camp)
            {
                return CampType switch
                {
                    ConfigBattle.ModifierCampType.Player => camp == 1,
                    ConfigBattle.ModifierCampType.Enemy => camp == 2,
                    ConfigBattle.ModifierCampType.Any => true,
                    _ => false
                };
            }

            private static BattleEffectRef ToRuntimeEffect(ConfigBattle.EffectRef effect)
            {
                return effect == null
                    ? default
                    : new BattleEffectRef(effect.Type, effect.Id, effect.Value);
            }
        }
    }
}
