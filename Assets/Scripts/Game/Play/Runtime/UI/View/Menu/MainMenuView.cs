using Game.Play.Systems.Level.Command;
using UniKit.Framework.Base;
using UnityEngine.UI;
using UniKit.UI.Core;

namespace Game.Play.UI.View.Menu
{
    public partial class MainMenuView : UIView
    {
        protected override void OnInit()
        {
            m_BtnStart.SetOnClick(OnClickStart);
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

        private void OnClickStart()
        {
            Close();
            this.SendCommand(new StartLevelCommand());
        }
    }
}
