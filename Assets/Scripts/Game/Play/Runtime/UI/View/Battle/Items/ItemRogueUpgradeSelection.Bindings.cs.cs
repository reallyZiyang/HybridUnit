using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Play.UI.View.Battle.Items
{
    public partial class ItemRogueUpgradeSelection
    {
        private TextMeshProUGUI m_TxtTitle;
		private Image m_ImgIcon;
		private TextMeshProUGUI m_TxtDesc;
		private Button m_BtnSelect;

        protected override void OnInitBindings()
        {
            m_TxtTitle = db.Q<TextMeshProUGUI>("TxtTitle");
			m_ImgIcon = db.Q<Image>("ImgIcon");
			m_TxtDesc = db.Q<TextMeshProUGUI>("TxtDesc");
			m_BtnSelect = db.Q<Button>("BtnSelect");
        }
    }
}