using UniKit.Framework.Base;
using UniKit.UI.Core;

namespace Game.Play.UI.Extensions
{
    public class UIContextNode : UINode, IContextOwner
    {
        public IContext Context
        {
            get => GameContext.Instance;
            set => throw new System.NotImplementedException();
        }
    }
}