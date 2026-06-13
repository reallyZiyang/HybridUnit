using Game.Play.Systems.SkillEnhancement.Interface;
using UniKit.Framework.Base;

namespace Game.Play.Systems.SkillEnhancement.Command
{
    public sealed class ClearSkillEnhancementCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            Context.GetSystem<ISkillEnhancementSystem>().EndBattle();
        }
    }
}
