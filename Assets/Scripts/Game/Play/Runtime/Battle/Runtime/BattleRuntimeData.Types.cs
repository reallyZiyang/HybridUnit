using Game.Data.Configs.Attr;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Runtime
{
    public readonly struct BattleEffectRef
    {
        public readonly ConfigBattle.EffectType type;
        public readonly int id;
        public readonly long value;

        public BattleEffectRef(ConfigBattle.EffectType type, int id, long value)
        {
            this.type = type;
            this.id = id;
            this.value = value;
        }
    }

    public readonly struct BattleUnitRuntimeData
    {
        public readonly int id;
        public readonly float radius;
        public readonly int camp;
        public readonly int layer;
        public readonly ConfigBattle.UnitFlag unitFlags;
        public readonly ConfigBattle.UnitRoleFlag roleFlags;
        public readonly string renderKey;
        public readonly BattleAttributeValue[] attrs;
        public readonly int[] defaultSkills;

        public BattleUnitRuntimeData(int id, float radius, int camp, int layer, ConfigBattle.UnitFlag unitFlags, ConfigBattle.UnitRoleFlag roleFlags, string renderKey, BattleAttributeValue[] attrs, int[] defaultSkills)
        {
            this.id = id;
            this.radius = radius;
            this.camp = camp;
            this.layer = layer;
            this.unitFlags = unitFlags;
            this.roleFlags = roleFlags;
            this.renderKey = renderKey;
            this.attrs = attrs;
            this.defaultSkills = defaultSkills;
        }
    }

    public readonly struct BattleSkillRuntimeData
    {
        public readonly int id;
        public readonly string actionName;
        public readonly int castPreMs;
        public readonly int castBackMs;
        public readonly int cooldownMs;
        public readonly float castRange;
        public readonly ConfigBattle.SkillTargetType targetType;
        public readonly ConfigBattle.TargetSelectType selectType;
        public readonly ConfigBattle.BattleShapeDesc shape;
        public readonly BattleEffectRef[] effects;

        public BattleSkillRuntimeData(int id, string actionName, int castPreMs, int castBackMs, int cooldownMs, float castRange, ConfigBattle.SkillTargetType targetType, ConfigBattle.TargetSelectType selectType, ConfigBattle.BattleShapeDesc shape, BattleEffectRef[] effects)
        {
            this.id = id;
            this.actionName = actionName;
            this.castPreMs = castPreMs;
            this.castBackMs = castBackMs;
            this.cooldownMs = cooldownMs;
            this.castRange = castRange;
            this.targetType = targetType;
            this.selectType = selectType;
            this.shape = shape;
            this.effects = effects;
        }
    }

    public readonly struct BattleBuffRuntimeData
    {
        public readonly int id;
        public readonly int durationMs;
        public readonly int maxStack;
        public readonly ConfigBattle.BuffStackMode stackMode;
        public readonly int tickMs;
        public readonly BattleAttributeValue[] attrs;
        public readonly BattleEffectRef[] tickEffects;
        public readonly BattleEffectRef[] beginEffects;
        public readonly BattleEffectRef[] endEffects;

        public BattleBuffRuntimeData(int id, int durationMs, int maxStack, ConfigBattle.BuffStackMode stackMode, int tickMs, BattleAttributeValue[] attrs, BattleEffectRef[] tickEffects, BattleEffectRef[] beginEffects, BattleEffectRef[] endEffects)
        {
            this.id = id;
            this.durationMs = durationMs;
            this.maxStack = maxStack;
            this.stackMode = stackMode;
            this.tickMs = tickMs;
            this.attrs = attrs;
            this.tickEffects = tickEffects;
            this.beginEffects = beginEffects;
            this.endEffects = endEffects;
        }
    }

    public readonly struct BattleDamageEffectRuntimeData
    {
        public readonly int id;
        public readonly AttributeType attr;
        public readonly long ratio;
        public readonly long fixedValue;
        public readonly int damageElement;
        public readonly bool canCrit;
        public readonly int hitCount;
        public readonly bool playHitReaction;

        public BattleDamageEffectRuntimeData(int id, AttributeType attr, long ratio, long fixedValue, int damageElement, bool canCrit, int hitCount, bool playHitReaction)
        {
            this.id = id;
            this.attr = attr;
            this.ratio = ratio;
            this.fixedValue = fixedValue;
            this.damageElement = damageElement;
            this.canCrit = canCrit;
            this.hitCount = hitCount;
            this.playHitReaction = playHitReaction;
        }
    }

    public readonly struct BattleHealEffectRuntimeData
    {
        public readonly int id;
        public readonly AttributeType attr;
        public readonly long ratio;
        public readonly long fixedValue;
        public readonly bool canCrit;

        public BattleHealEffectRuntimeData(int id, AttributeType attr, long ratio, long fixedValue, bool canCrit)
        {
            this.id = id;
            this.attr = attr;
            this.ratio = ratio;
            this.fixedValue = fixedValue;
            this.canCrit = canCrit;
        }
    }

    public readonly struct BattleAddBuffEffectRuntimeData
    {
        public readonly int id;
        public readonly int buffId;
        public readonly int durationOverrideMs;
        public readonly int stack;

        public BattleAddBuffEffectRuntimeData(int id, int buffId, int durationOverrideMs, int stack)
        {
            this.id = id;
            this.buffId = buffId;
            this.durationOverrideMs = durationOverrideMs;
            this.stack = stack;
        }
    }

    public readonly struct BattleProjectileRuntimeData
    {
        public readonly int id;
        public readonly string projectileKey;
        public readonly float speed;
        public readonly float radius;
        public readonly int lifetimeMs;
        public readonly int pierceCount;
        public readonly int hitIntervalMs;
        public readonly ConfigBattle.QueryQuality queryQuality;
        public readonly BattleEffectRef[] hitEffects;

        public BattleProjectileRuntimeData(int id, string projectileKey, float speed, float radius, int lifetimeMs, int pierceCount, int hitIntervalMs, ConfigBattle.QueryQuality queryQuality, BattleEffectRef[] hitEffects)
        {
            this.id = id;
            this.projectileKey = projectileKey;
            this.speed = speed;
            this.radius = radius;
            this.lifetimeMs = lifetimeMs;
            this.pierceCount = pierceCount;
            this.hitIntervalMs = hitIntervalMs;
            this.queryQuality = queryQuality;
            this.hitEffects = hitEffects;
        }
    }
}
