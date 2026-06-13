using UnityEngine;
using UnityEngine.UI;

namespace Game.Play.UI.View.Menu
{
    public partial class MainMenuView
    {
        protected Button m_BtnStart;

        protected override void OnInitBindings()
        {
            m_BtnStart = db.Q<Button>("BtnStart");
        }
    }
}
