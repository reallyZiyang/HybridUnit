using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine;

namespace Game.Play.UI.View.Common.Loading
{
    public partial class LoadingView
    {
        private TextMeshProUGUI m_TxtVersion;
		private TextMeshProUGUI m_TxtPackage;
		private Button m_BtnTips;
		private TextMeshProUGUI m_TxtTips;
		private Slider m_SldValue;
		private Image m_ImgLoading;
		private TextMeshProUGUI m_TxtMask;
		private RectTransform m_Progress;
		private RectTransform m_Mask;

        protected override void OnInitBindings()
        {
            m_TxtVersion = db.Q<TextMeshProUGUI>("TxtVersion");
			m_TxtPackage = db.Q<TextMeshProUGUI>("TxtPackage");
			m_BtnTips = db.Q<Button>("BtnTips");
			m_TxtTips = db.Q<TextMeshProUGUI>("TxtTips");
			m_SldValue = db.Q<Slider>("SldValue");
			m_ImgLoading = db.Q<Image>("ImgLoading");
			m_TxtMask = db.Q<TextMeshProUGUI>("TxtMask");
			m_Progress = db.Q<RectTransform>("Progress");
			m_Mask = db.Q<RectTransform>("Mask");
        }
    }
}