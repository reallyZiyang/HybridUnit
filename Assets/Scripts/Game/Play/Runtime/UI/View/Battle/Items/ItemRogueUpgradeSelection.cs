using Game.Play;
using Game.Play.Systems.SkillEnhancement.Command;
using Game.Play.Systems.SkillEnhancement.Runtime;
using Game.Play.UI.View.Battle;
using UniKit.UI.Core;

namespace Game.Play.UI.View.Battle.Items
{
    public partial class ItemRogueUpgradeSelection : UINode
    {
        private SkillEnhancementChoice choice;

        protected override void OnInit()
        {
        }

        public void SetData(SkillEnhancementChoice value)
        {
            choice = value;
            var config = choice.config;
            m_TxtTitle.text = config?.Name ?? string.Empty;
            m_TxtDesc.text = config?.Description ?? string.Empty;

            if (!string.IsNullOrEmpty(config?.IconKey))
            {
                m_ImgIcon.SetIcon(config.IconKey);
            }

            m_BtnSelect.SetOnClick(OnClickSelect);
        }

        private void OnClickSelect()
        {
            if (choice.enhancementId <= 0)
            {
                return;
            }

            GameContext.Instance.SendCommand(new PickSkillEnhancementCommand(choice.enhancementId));
            BattleTesterRogueChoiceBridge.NotifyChoiceApplied(choice.enhancementId);
            GetComponentInParent<RogueUpgradeSelectView>()?.Close();
        }
    }
}
