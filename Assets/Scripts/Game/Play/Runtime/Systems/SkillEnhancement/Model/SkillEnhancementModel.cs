using System;
using Game.Kits.Bindable.Core;
using Game.Play.Systems.SkillEnhancement.Runtime;
using UniKit.Framework.Base;

namespace Game.Play.Systems.SkillEnhancement.Model
{
    public sealed class SkillEnhancementModel : AbstractModel
    {
        public Bindable<bool> IsChoosing { get; } = new(false);
        public Bindable<SkillEnhancementChoice[]> CurrentChoices { get; } = new(Array.Empty<SkillEnhancementChoice>());
        public Bindable<OwnedSkillEnhancement[]> OwnedEnhancements { get; } = new(Array.Empty<OwnedSkillEnhancement>());

        protected override void OnDispose()
        {
            IsChoosing.Value = false;
            CurrentChoices.Value = Array.Empty<SkillEnhancementChoice>();
            OwnedEnhancements.Value = Array.Empty<OwnedSkillEnhancement>();
        }
    }
}
