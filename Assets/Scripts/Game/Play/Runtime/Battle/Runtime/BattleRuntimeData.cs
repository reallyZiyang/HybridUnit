using System.Collections.Generic;
using ConfigBattle = Game.Data.Configs.Battle;
using ConfigTables = Game.Data.Configs.Tables;

namespace Game.Play.Battle.Runtime
{
    public sealed class BattleRuntimeData
    {
        private readonly Dictionary<int, int> unitIndexById = new();
        private readonly Dictionary<int, int> skillIndexById = new();
        private readonly Dictionary<int, int> buffIndexById = new();
        private readonly Dictionary<int, int> damageEffectIndexById = new();
        private readonly Dictionary<int, int> healEffectIndexById = new();
        private readonly Dictionary<int, int> addBuffEffectIndexById = new();
        private readonly Dictionary<int, int> projectileEffectIndexById = new();

        public BattleUnitRuntimeData[] Units { get; private set; }
        public BattleSkillRuntimeData[] Skills { get; private set; }
        public BattleBuffRuntimeData[] Buffs { get; private set; }
        public BattleDamageEffectRuntimeData[] DamageEffects { get; private set; }
        public BattleHealEffectRuntimeData[] HealEffects { get; private set; }
        public BattleAddBuffEffectRuntimeData[] AddBuffEffects { get; private set; }
        public BattleProjectileRuntimeData[] ProjectileEffects { get; private set; }
        public int MaxDefaultSkillCount { get; private set; }

        public static BattleRuntimeData Build(ConfigTables tables)
        {
            BattleRuntimeData data = new();
            data.BuildUnits(tables);
            data.BuildSkills(tables);
            data.BuildBuffs(tables);
            data.BuildDamageEffects(tables);
            data.BuildHealEffects(tables);
            data.BuildAddBuffEffects(tables);
            data.BuildProjectileEffects(tables);
            return data;
        }

        public bool TryGetUnit(int id, out BattleUnitRuntimeData data)
        {
            if (unitIndexById.TryGetValue(id, out int index))
            {
                data = Units[index];
                return true;
            }

            data = default;
            return false;
        }

        public bool TryGetSkill(int id, out BattleSkillRuntimeData data)
        {
            if (skillIndexById.TryGetValue(id, out int index))
            {
                data = Skills[index];
                return true;
            }

            data = default;
            return false;
        }

        public bool TryGetBuff(int id, out BattleBuffRuntimeData data)
        {
            if (buffIndexById.TryGetValue(id, out int index))
            {
                data = Buffs[index];
                return true;
            }

            data = default;
            return false;
        }

        public bool TryGetDamageEffect(int id, out BattleDamageEffectRuntimeData data)
        {
            if (damageEffectIndexById.TryGetValue(id, out int index))
            {
                data = DamageEffects[index];
                return true;
            }

            data = default;
            return false;
        }

        public bool TryGetHealEffect(int id, out BattleHealEffectRuntimeData data)
        {
            if (healEffectIndexById.TryGetValue(id, out int index))
            {
                data = HealEffects[index];
                return true;
            }

            data = default;
            return false;
        }

        public bool TryGetAddBuffEffect(int id, out BattleAddBuffEffectRuntimeData data)
        {
            if (addBuffEffectIndexById.TryGetValue(id, out int index))
            {
                data = AddBuffEffects[index];
                return true;
            }

            data = default;
            return false;
        }

        public bool TryGetProjectileEffect(int id, out BattleProjectileRuntimeData data)
        {
            if (projectileEffectIndexById.TryGetValue(id, out int index))
            {
                data = ProjectileEffects[index];
                return true;
            }

            data = default;
            return false;
        }

        private void BuildUnits(ConfigTables tables)
        {
            List<ConfigBattle.UnitCfg> list = tables.TbUnit.DataList;
            Units = new BattleUnitRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.UnitCfg cfg = list[i];
                BattleAttributeValue[] attrs = new BattleAttributeValue[cfg.Attrs.Length];
                for (int j = 0; j < cfg.Attrs.Length; j++)
                {
                    attrs[j] = new BattleAttributeValue(cfg.Attrs[j].Id, cfg.Attrs[j].Value);
                }

                int[] skills = (int[])cfg.DefaultSkills.Clone();
                if (skills.Length > MaxDefaultSkillCount)
                {
                    MaxDefaultSkillCount = skills.Length;
                }

                Units[i] = new BattleUnitRuntimeData(cfg.Id, cfg.Radius, cfg.Camp, cfg.Layer, cfg.RenderKey, attrs, skills);
                unitIndexById[cfg.Id] = i;
            }
        }

        private void BuildSkills(ConfigTables tables)
        {
            List<ConfigBattle.SkillCfg> list = tables.TbSkill.DataList;
            Skills = new BattleSkillRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.SkillCfg cfg = list[i];
                Skills[i] = new BattleSkillRuntimeData(
                    cfg.Id,
                    cfg.ActionName,
                    cfg.CastPreMs,
                    cfg.CastBackMs,
                    cfg.CooldownMs,
                    cfg.TargetType,
                    cfg.SelectType,
                    cfg.Shape,
                    ToRuntimeEffects(cfg.Effects));
                skillIndexById[cfg.Id] = i;
            }
        }

        private void BuildBuffs(ConfigTables tables)
        {
            List<ConfigBattle.BuffCfg> list = tables.TbBuff.DataList;
            Buffs = new BattleBuffRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.BuffCfg cfg = list[i];
                BattleAttributeValue[] attrs = new BattleAttributeValue[cfg.Attrs.Length];
                for (int j = 0; j < cfg.Attrs.Length; j++)
                {
                    attrs[j] = new BattleAttributeValue(cfg.Attrs[j].Id, cfg.Attrs[j].Value);
                }

                Buffs[i] = new BattleBuffRuntimeData(
                    cfg.Id,
                    cfg.DurationMs,
                    cfg.MaxStack,
                    cfg.StackMode,
                    cfg.TickMs,
                    attrs,
                    ToRuntimeEffects(cfg.TickEffects),
                    ToRuntimeEffects(cfg.BeginEffects),
                    ToRuntimeEffects(cfg.EndEffects));
                buffIndexById[cfg.Id] = i;
            }
        }

        private void BuildDamageEffects(ConfigTables tables)
        {
            List<ConfigBattle.DamageEffectCfg> list = tables.TbDamageEffect.DataList;
            DamageEffects = new BattleDamageEffectRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.DamageEffectCfg cfg = list[i];
                DamageEffects[i] = new BattleDamageEffectRuntimeData(cfg.Id, cfg.Attr, cfg.Ratio, cfg.FixedValue, cfg.DamageElement, cfg.CanCrit, cfg.HitCount);
                damageEffectIndexById[cfg.Id] = i;
            }
        }

        private void BuildHealEffects(ConfigTables tables)
        {
            List<ConfigBattle.HealEffectCfg> list = tables.TbHealEffect.DataList;
            HealEffects = new BattleHealEffectRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.HealEffectCfg cfg = list[i];
                HealEffects[i] = new BattleHealEffectRuntimeData(cfg.Id, cfg.Attr, cfg.Ratio, cfg.FixedValue, cfg.CanCrit);
                healEffectIndexById[cfg.Id] = i;
            }
        }

        private void BuildAddBuffEffects(ConfigTables tables)
        {
            List<ConfigBattle.AddBuffEffectCfg> list = tables.TbAddBuffEffect.DataList;
            AddBuffEffects = new BattleAddBuffEffectRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.AddBuffEffectCfg cfg = list[i];
                AddBuffEffects[i] = new BattleAddBuffEffectRuntimeData(cfg.Id, cfg.BuffId, cfg.DurationOverrideMs, cfg.Stack);
                addBuffEffectIndexById[cfg.Id] = i;
            }
        }

        private void BuildProjectileEffects(ConfigTables tables)
        {
            List<ConfigBattle.ProjectileEffectCfg> list = tables.TbProjectileEffect.DataList;
            ProjectileEffects = new BattleProjectileRuntimeData[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ConfigBattle.ProjectileEffectCfg cfg = list[i];
                ProjectileEffects[i] = new BattleProjectileRuntimeData(
                    cfg.Id,
                    cfg.ProjectileKey,
                    cfg.Speed,
                    cfg.Radius,
                    cfg.LifetimeMs,
                    cfg.PierceCount,
                    cfg.HitIntervalMs,
                    ToRuntimeEffects(cfg.HitEffects));
                projectileEffectIndexById[cfg.Id] = i;
            }
        }

        private static BattleEffectRef[] ToRuntimeEffects(ConfigBattle.EffectRef[] refs)
        {
            BattleEffectRef[] effects = new BattleEffectRef[refs.Length];
            for (int i = 0; i < refs.Length; i++)
            {
                effects[i] = new BattleEffectRef(refs[i].Type, refs[i].Id, refs[i].Value);
            }

            return effects;
        }
    }
}
