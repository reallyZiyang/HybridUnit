using UnityEngine;
using UnityEngine.UI;


namespace Game.Play.UI.View.Common
{
    public partial class FullScreenMask
    {
        private Image m_ImgMask;

        protected override void OnInitBindings()
        {
            m_ImgMask = db.Q<Image>("ImgMask");
        }
    }
}