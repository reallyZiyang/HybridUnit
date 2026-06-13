using Game.Play.Systems.SkillEnhancement.Interface;
using UniKit.Framework.Base;

namespace Game.Play.Systems.SkillEnhancement.Command
{
    public sealed class PickSkillEnhancementCommand : AbstractCommand
    {
        private readonly int enhancementId;

        public PickSkillEnhancementCommand(int enhancementId)
        {
            this.enhancementId = enhancementId;
        }

        protected override void OnExecute()
        {
            Context.GetSystem<ISkillEnhancementSystem>().ApplyChoice(enhancementId);
        }
    }
}
