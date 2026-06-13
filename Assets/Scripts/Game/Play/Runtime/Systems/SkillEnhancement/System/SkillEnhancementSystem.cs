using System;
using Game.Play.Adapters;
using Game.Play.Battle.Runtime;
using Game.Play.Systems.SkillEnhancement.Interface;
using Game.Play.Systems.SkillEnhancement.Model;
using Game.Play.Systems.SkillEnhancement.Runtime;
using UniKit.Framework.Base;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Systems.SkillEnhancement.System
{
    public sealed class SkillEnhancementSystem : AbstractSystem, ISkillEnhancementSystem
    {
        private SkillEnhancementModel model;
        private BattleSkillEnhancementContext battleContext = BattleSkillEnhancementContext.Empty;

        protected override void OnInitialize()
        {
            model = Context.GetModel<SkillEnhancementModel>();
        }

        public void BeginBattle()
        {
            battleContext = new BattleSkillEnhancementContext();
            model.IsChoosing.Value = false;
            model.CurrentChoices.Value = Array.Empty<SkillEnhancementChoice>();
            model.OwnedEnhancements.Value = Array.Empty<OwnedSkillEnhancement>();
        }

        public void EndBattle()
        {
            battleContext = BattleSkillEnhancementContext.Empty;
            model.IsChoosing.Value = false;
            model.CurrentChoices.Value = Array.Empty<SkillEnhancementChoice>();
            model.OwnedEnhancements.Value = Array.Empty<OwnedSkillEnhancement>();
        }

        public void RequestChoices()
        {
            model.CurrentChoices.Value = BuildChoices();
            model.IsChoosing.Value = true;
        }

        public void ApplyChoice(int enhancementId)
        {
            if (TryGetConfig(enhancementId, out ConfigBattle.SkillEnhancementCfg config))
            {
                int stack = GetOwnedStack(enhancementId) + 1;
                int maxStack = Math.Max(1, config.MaxStack);
                if (stack <= maxStack)
                {
                    SetOwnedStack(enhancementId, stack);
                    battleContext.AddOrUpdate(config, stack);
                }
            }

            model.IsChoosing.Value = false;
            model.CurrentChoices.Value = Array.Empty<SkillEnhancementChoice>();
        }

        public BattleSkillEnhancementContext GetBattleContext()
        {
            return battleContext;
        }

        protected override void OnDispose()
        {
            EndBattle();
        }

        private SkillEnhancementChoice[] BuildChoices()
        {
            var table = API.Tables?.TbSkillEnhancement;
            if (table == null || table.DataList == null || table.DataList.Count == 0)
            {
                return Array.Empty<SkillEnhancementChoice>();
            }

            SkillEnhancementChoice[] choices = new SkillEnhancementChoice[Math.Min(3, table.DataList.Count)];
            int count = SkillEnhancementChoiceSelector.BuildChoices(
                table.DataList,
                model.OwnedEnhancements.Value,
                choices,
                choices.Length,
                Environment.TickCount);

            if (count == choices.Length)
            {
                return choices;
            }

            SkillEnhancementChoice[] trimmed = new SkillEnhancementChoice[count];
            Array.Copy(choices, trimmed, count);
            return trimmed;
        }

        private bool TryGetConfig(int enhancementId, out ConfigBattle.SkillEnhancementCfg config)
        {
            config = null;
            var table = API.Tables?.TbSkillEnhancement;
            if (table == null)
            {
                return false;
            }

            config = table.GetOrDefault(enhancementId);
            return config != null;
        }

        private int GetOwnedStack(int enhancementId)
        {
            OwnedSkillEnhancement[] owned = model.OwnedEnhancements.Value;
            for (int i = 0; i < (owned?.Length ?? 0); i++)
            {
                if (owned[i].enhancementId == enhancementId)
                {
                    return owned[i].stack;
                }
            }

            return 0;
        }

        private void SetOwnedStack(int enhancementId, int stack)
        {
            OwnedSkillEnhancement[] oldOwned = model.OwnedEnhancements.Value ?? Array.Empty<OwnedSkillEnhancement>();
            for (int i = 0; i < oldOwned.Length; i++)
            {
                if (oldOwned[i].enhancementId != enhancementId)
                {
                    continue;
                }

                OwnedSkillEnhancement[] updated = (OwnedSkillEnhancement[])oldOwned.Clone();
                updated[i] = new OwnedSkillEnhancement(enhancementId, stack);
                model.OwnedEnhancements.Value = updated;
                return;
            }

            OwnedSkillEnhancement[] appended = new OwnedSkillEnhancement[oldOwned.Length + 1];
            Array.Copy(oldOwned, appended, oldOwned.Length);
            appended[oldOwned.Length] = new OwnedSkillEnhancement(enhancementId, stack);
            model.OwnedEnhancements.Value = appended;
        }
    }
}
