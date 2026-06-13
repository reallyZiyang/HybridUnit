using TMPro;
using UnityEngine.UI;

namespace Game.Play.UI.View.Result
{
    public partial class BattleResultView
    {
        protected Button m_BtnBackMain;
        protected TextMeshProUGUI m_TxtResultState;
        protected TextMeshProUGUI m_TxtTimeValue;
        protected TextMeshProUGUI m_TxtKillsValue;
        protected TextMeshProUGUI m_TxtLevelValue;
        protected TextMeshProUGUI m_TxtCoreValue;
        protected TextMeshProUGUI m_TxtDamageValue;
        protected TextMeshProUGUI m_TxtExpValue;
        protected Image m_ImgRangerBarFill;
        protected Image m_ImgKnightBarFill;
        protected Image m_ImgAxeBarFill;

        protected override void OnInitBindings()
        {
            m_BtnBackMain = db.Q<Button>("BtnBackMain");
            m_TxtResultState = db.Q<TextMeshProUGUI>("TxtResultState");
            m_TxtTimeValue = db.Q<TextMeshProUGUI>("TxtTimeValue");
            m_TxtKillsValue = db.Q<TextMeshProUGUI>("TxtKillsValue");
            m_TxtLevelValue = db.Q<TextMeshProUGUI>("TxtLevelValue");
            m_TxtCoreValue = db.Q<TextMeshProUGUI>("TxtCoreValue");
            m_TxtDamageValue = db.Q<TextMeshProUGUI>("TxtDamageValue");
            m_TxtExpValue = db.Q<TextMeshProUGUI>("TxtExpValue");
            m_ImgRangerBarFill = db.Q<Image>("ImgRangerBarFill");
            m_ImgKnightBarFill = db.Q<Image>("ImgKnightBarFill");
            m_ImgAxeBarFill = db.Q<Image>("ImgAxeBarFill");
        }
    }
}
