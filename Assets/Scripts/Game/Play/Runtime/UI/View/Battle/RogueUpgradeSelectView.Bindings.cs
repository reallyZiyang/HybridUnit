using TMPro;
using UnityEngine.UI;

namespace Game.Play.UI.View.Battle
{
    public partial class RogueUpgradeSelectView
    {
        protected Button m_BtnOption1;
        protected Button m_BtnOption2;
        protected Button m_BtnOption3;
        protected Image m_ImgOption1Icon;
        protected Image m_ImgOption2Icon;
        protected Image m_ImgOption3Icon;
        protected TextMeshProUGUI m_TxtOption1Title;
        protected TextMeshProUGUI m_TxtOption2Title;
        protected TextMeshProUGUI m_TxtOption3Title;
        protected TextMeshProUGUI m_TxtOption1Desc;
        protected TextMeshProUGUI m_TxtOption2Desc;
        protected TextMeshProUGUI m_TxtOption3Desc;

        protected override void OnInitBindings()
        {
            m_BtnOption1 = db.Q<Button>("BtnOption1");
            m_BtnOption2 = db.Q<Button>("BtnOption2");
            m_BtnOption3 = db.Q<Button>("BtnOption3");
            m_ImgOption1Icon = db.Q<Image>("ImgOption1Icon");
            m_ImgOption2Icon = db.Q<Image>("ImgOption2Icon");
            m_ImgOption3Icon = db.Q<Image>("ImgOption3Icon");
            m_TxtOption1Title = db.Q<TextMeshProUGUI>("TxtOption1Title");
            m_TxtOption2Title = db.Q<TextMeshProUGUI>("TxtOption2Title");
            m_TxtOption3Title = db.Q<TextMeshProUGUI>("TxtOption3Title");
            m_TxtOption1Desc = db.Q<TextMeshProUGUI>("TxtOption1Desc");
            m_TxtOption2Desc = db.Q<TextMeshProUGUI>("TxtOption2Desc");
            m_TxtOption3Desc = db.Q<TextMeshProUGUI>("TxtOption3Desc");
        }
    }
}
