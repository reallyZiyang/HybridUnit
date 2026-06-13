using Cysharp.Threading.Tasks;
using UniKit.Framework.Base;

namespace Game.Play.Systems.Level.Interface
{
    public interface ILevelSystem : ISystem, IUpdateSystem
    {
        bool IsRunning { get; }
        UniTask StartLevelAsync(string scenarioKey = "TestBattleScenario");
        void StopLevel();
    }
}
