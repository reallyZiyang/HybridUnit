using Game.Kits.Bindable.Extensions;
using Game.Play.Systems.Level;
using Game.Play.Systems.Level.Command;
using Game.Play.Systems.Level.Model;
using UniKit.Framework.Base;
using UnityEngine;
using UnityEngine.UI;
using UniKit.UI.Core;

namespace Game.Play.UI.View.Result
{
    public partial class BattleResultView : UIView
    {
        protected override void OnInit()
        {
            LevelModel model = this.GetModel<LevelModel>();
            m_TxtResultState.BindText(model.BattleOutcome, FormatOutcome);
            m_BtnBackMain.SetOnClick(OnClickBackMain);
        }

        protected override void OnShow()
        {
        }

        protected override void OnHide()
        {
        }

        protected override void OnClose()
        {
        }

        private void OnClickBackMain()
        {
            Close();
            this.SendCommand(new OpenMainMenuCommand());
        }

        private static string FormatOutcome(BattleOutcome outcome)
        {
            return outcome == BattleOutcome.Victory ? "胜利" : "失败";
        }
    }
}
