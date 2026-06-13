using Game.Play.Battle.Runtime;
using UniKit.Framework.Base;

namespace Game.Play.Systems.SkillEnhancement.Interface
{
    public interface ISkillEnhancementSystem : ISystem
    {
        void BeginBattle();
        void EndBattle();
        void RequestChoices();
        void ApplyChoice(int enhancementId);
        BattleSkillEnhancementContext GetBattleContext();
    }
}
