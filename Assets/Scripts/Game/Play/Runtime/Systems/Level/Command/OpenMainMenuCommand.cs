using Game.Data.Configs.Sys;
using Game.Play.Systems.Common.Navigator;
using Game.Play.Systems.Level.Model;
using Game.Play.UI.View.Menu;
using UniKit.Framework.Base;
using UniKit.UI;

namespace Game.Play.Systems.Level.Command
{
    public sealed class OpenMainMenuCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            LevelModel model = Context.GetModel<LevelModel>();
            model.FlowState.Value = LevelFlowState.MainMenu;
            this.Open(SystemType.Main);
        }
    }
}
