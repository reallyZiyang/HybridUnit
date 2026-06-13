using System;
using Game.Play.Systems.SkillEnhancement.Model;
using Game.Play.Systems.SkillEnhancement.Runtime;
using UniKit.Framework.Base;

namespace Game.Play.Systems.SkillEnhancement.Query
{
    public sealed class GetSkillEnhancementChoicesQuery : AbstractQuery<SkillEnhancementChoice[]>
    {
        protected override SkillEnhancementChoice[] OnDo()
        {
            SkillEnhancementChoice[] choices = Context.GetModel<SkillEnhancementModel>().CurrentChoices.Value;
            return choices != null ? (SkillEnhancementChoice[])choices.Clone() : Array.Empty<SkillEnhancementChoice>();
        }
    }
}
