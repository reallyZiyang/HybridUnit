using UniKit.UI;
using UnityEngine;

namespace Game.Play.Systems.Common.Navigator.Jumper.Impls
{
    public class JumpToUI : BaseJumper
    {
        public override bool IsActivated => UIManager.Instance.IsViewActive(ViewType.Name);
        protected override void Execute()
        {
            UIManager.Instance.Open(ViewType, _ => Complete());
        }
    }
}