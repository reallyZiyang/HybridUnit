using Cysharp.Threading.Tasks;
using Game.Play.Battle.Tester;
using UniKit.Framework.Base;

namespace Game.Play.Systems.Level.Interface
{
    public interface ILevelSystem : ISystem, IUpdateSystem
    {
        bool IsRunning { get; }
        bool IsPaused { get; }
        float ElapsedSeconds { get; }
        UniTask StartLevelAsync(string scenarioKey = "TestBattleScenario");
        UniTask StartLevelAsync(BattleTesterScenario scenario);
        void StopLevel();
        void PauseLevel();
        void ResumeLevel();
        void StepBattle();
        bool CastSkill(int unitIndex, int skillId);
        BattleRuntimeDriverSnapshot GetRuntimeSnapshot();
    }
}
