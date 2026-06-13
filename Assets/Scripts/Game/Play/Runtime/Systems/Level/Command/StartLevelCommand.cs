using Cysharp.Threading.Tasks;
using Game.Play.Systems.Level.Interface;
using UniKit.Framework.Base;

namespace Game.Play.Systems.Level.Command
{
    public sealed class StartLevelCommand : AbstractCommand
    {
        private readonly string scenarioKey;

        public StartLevelCommand(string scenarioKey = "TestBattleScenario")
        {
            this.scenarioKey = scenarioKey;
        }

        protected override void OnExecute()
        {
            Context.GetSystem<ILevelSystem>().StartLevelAsync(scenarioKey).Forget();
        }
    }
}
