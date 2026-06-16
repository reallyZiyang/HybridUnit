using System;
using Game.Play.Systems.SkillEnhancement.Query;
using Game.Play.Systems.SkillEnhancement.Runtime;
using Game.Play.UI.View.Battle.Items;
using Game.Play.UI.View.Common;

namespace Game.Play.UI.View.Battle
{
    public partial class RogueUpgradeSelectView : FullScreenMask
    {
        protected override void OnInit()
        {
            m_ListView.SetRenderer<SkillEnhancementChoice, ItemRogueUpgradeSelection>(
                (_, choice, item) => item.SetData(choice));
        }

        protected override void OnShow()
        {
            SkillEnhancementChoice[] choices = Context != null
                ? Context.SendQuery(new GetSkillEnhancementChoicesQuery())
                : Array.Empty<SkillEnhancementChoice>();
            m_ListView.Reload(choices);
        }

        protected override void OnHide()
        {
        }

        protected override void OnClose()
        {
        }
    }
}
