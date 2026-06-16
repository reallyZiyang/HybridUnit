using System;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Systems.SkillEnhancement.Runtime
{
    public readonly struct SkillEnhancementChoice
    {
        public readonly int enhancementId;
        public readonly ConfigBattle.SkillEnhancementCfg config;
        public readonly int ownedStack;

        public SkillEnhancementChoice(ConfigBattle.SkillEnhancementCfg config, int ownedStack = 0)
        {
            this.enhancementId = config?.Id ?? 0;
            this.config = config;
            this.ownedStack = Math.Max(0, ownedStack);
        }
    }

    public readonly struct OwnedSkillEnhancement
    {
        public readonly int enhancementId;
        public readonly int stack;

        public OwnedSkillEnhancement(int enhancementId, int stack)
        {
            this.enhancementId = enhancementId;
            this.stack = Math.Max(1, stack);
        }
    }

    public static class BattleTesterRogueChoiceBridge
    {
        public static event Action<int> ChoiceApplied;

        public static void NotifyChoiceApplied(int enhancementId)
        {
            ChoiceApplied?.Invoke(enhancementId);
        }
    }
}
