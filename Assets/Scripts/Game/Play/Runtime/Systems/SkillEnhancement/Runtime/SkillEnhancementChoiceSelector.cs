using System;
using System.Collections.Generic;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Systems.SkillEnhancement.Runtime
{
    public static class SkillEnhancementChoiceSelector
    {
        public static int BuildChoices(
            IList<ConfigBattle.SkillEnhancementCfg> configs,
            OwnedSkillEnhancement[] owned,
            SkillEnhancementChoice[] output,
            int maxChoices,
            int seed)
        {
            if (configs == null || output == null || maxChoices <= 0)
            {
                return 0;
            }

            int capacity = Math.Min(maxChoices, output.Length);
            int count = 0;
            Random random = new(seed);
            while (count < capacity)
            {
                int totalWeight = CalculateTotalWeight(configs, owned, output, count);
                if (totalWeight <= 0)
                {
                    break;
                }

                int roll = random.Next(totalWeight);
                for (int i = 0; i < configs.Count; i++)
                {
                    ConfigBattle.SkillEnhancementCfg config = configs[i];
                    if (!IsSelectable(config, configs, owned, output, count))
                    {
                        continue;
                    }

                    roll -= Math.Max(0, config.Weight);
                    if (roll >= 0)
                    {
                        continue;
                    }

                    output[count++] = new SkillEnhancementChoice(config, GetOwnedStack(owned, config.Id));
                    break;
                }
            }

            return count;
        }

        public static bool IsSelectable(
            ConfigBattle.SkillEnhancementCfg config,
            IList<ConfigBattle.SkillEnhancementCfg> configs,
            OwnedSkillEnhancement[] owned,
            SkillEnhancementChoice[] selected,
            int selectedCount)
        {
            if (config == null || config.Weight <= 0 || IsSelected(selected, selectedCount, config.Id))
            {
                return false;
            }

            if (GetOwnedStack(owned, config.Id) >= Math.Max(1, config.MaxStack))
            {
                return false;
            }

            if (!HasAllRequired(owned, config.RequireEnhancementIds) || HasAnyOwned(owned, config.ExcludeEnhancementIds))
            {
                return false;
            }

            return !HasConflictingTags(config, configs, owned, selected, selectedCount);
        }

        private static int CalculateTotalWeight(
            IList<ConfigBattle.SkillEnhancementCfg> configs,
            OwnedSkillEnhancement[] owned,
            SkillEnhancementChoice[] selected,
            int selectedCount)
        {
            int total = 0;
            for (int i = 0; i < configs.Count; i++)
            {
                ConfigBattle.SkillEnhancementCfg config = configs[i];
                if (IsSelectable(config, configs, owned, selected, selectedCount))
                {
                    total += Math.Max(0, config.Weight);
                }
            }

            return total;
        }

        private static bool HasAllRequired(OwnedSkillEnhancement[] owned, int[] requiredIds)
        {
            for (int i = 0; i < (requiredIds?.Length ?? 0); i++)
            {
                if (GetOwnedStack(owned, requiredIds[i]) <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasAnyOwned(OwnedSkillEnhancement[] owned, int[] ids)
        {
            for (int i = 0; i < (ids?.Length ?? 0); i++)
            {
                if (GetOwnedStack(owned, ids[i]) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasConflictingTags(
            ConfigBattle.SkillEnhancementCfg config,
            IList<ConfigBattle.SkillEnhancementCfg> configs,
            OwnedSkillEnhancement[] owned,
            SkillEnhancementChoice[] selected,
            int selectedCount)
        {
            for (int i = 0; i < (owned?.Length ?? 0); i++)
            {
                if (owned[i].stack <= 0)
                {
                    continue;
                }

                ConfigBattle.SkillEnhancementCfg ownedConfig = FindConfig(configs, owned[i].enhancementId);
                if (ownedConfig != null && HasTagConflict(config, ownedConfig))
                {
                    return true;
                }
            }

            for (int i = 0; i < selectedCount; i++)
            {
                ConfigBattle.SkillEnhancementCfg selectedConfig = selected[i].config;
                if (selectedConfig != null && HasTagConflict(config, selectedConfig))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTagConflict(ConfigBattle.SkillEnhancementCfg a, ConfigBattle.SkillEnhancementCfg b)
        {
            return HasTagOverlap(a.ConflictTags, b.Tags) || HasTagOverlap(a.Tags, b.ConflictTags);
        }

        private static bool HasTagOverlap(string[] left, string[] right)
        {
            for (int i = 0; i < (left?.Length ?? 0); i++)
            {
                string candidate = left[i];
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                for (int j = 0; j < (right?.Length ?? 0); j++)
                {
                    if (candidate == right[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSelected(SkillEnhancementChoice[] selected, int selectedCount, int enhancementId)
        {
            for (int i = 0; i < selectedCount; i++)
            {
                if (selected[i].enhancementId == enhancementId)
                {
                    return true;
                }
            }

            return false;
        }

        private static ConfigBattle.SkillEnhancementCfg FindConfig(IList<ConfigBattle.SkillEnhancementCfg> configs, int enhancementId)
        {
            if (configs == null)
            {
                return null;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                ConfigBattle.SkillEnhancementCfg config = configs[i];
                if (config != null && config.Id == enhancementId)
                {
                    return config;
                }
            }

            return null;
        }

        private static int GetOwnedStack(OwnedSkillEnhancement[] owned, int enhancementId)
        {
            for (int i = 0; i < (owned?.Length ?? 0); i++)
            {
                if (owned[i].enhancementId == enhancementId)
                {
                    return owned[i].stack;
                }
            }

            return 0;
        }
    }
}
