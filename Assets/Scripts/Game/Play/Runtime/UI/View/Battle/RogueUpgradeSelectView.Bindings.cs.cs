using UnityEngine;
using UnityEngine.UI;
using UniKit.UI.Core.Wrappers;
using Game.Play.UI.View.Battle.Items;

namespace Game.Play.UI.View.Battle
{
    public partial class RogueUpgradeSelectView
    {
        private ListView m_ListView;
		private ItemRogueUpgradeSelection m_Template;

        protected override void OnInitBindings()
        {
            base.OnInitBindings();
			m_ListView = db.Q<ListView>("ListView");
			m_Template = db.As<ItemRogueUpgradeSelection>("Template");
        }
    }
}