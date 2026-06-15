using System;
using System.Reflection;
using Game.Play.Adapters;
using Game.Play.Systems.Common.Navigator.Data;
using UniKit.UI;

namespace Game.Play.Systems.Common.Navigator.Jumper
{
    public abstract class BaseJumper : IJumper
    {
        private Action m_OnComplete;

        public bool IsJumped => ViewType == null || UIManager.Instance.IsViewActive(ViewType.Name);
        public Type ViewType { get; private set; }
        public virtual bool IsActivated { get; set; }

        public void ParserConfig(NavigatorItem config)
        {
            if (string.IsNullOrEmpty(config.type)) return;
            ViewType = Assembly.GetExecutingAssembly().GetType($"Game.Play.UI.View.{config.type}");
        }

        public void SetCallback(Action onComplete)
        {
            m_OnComplete = onComplete;
        }

        public void JumpToLocation()
        {
            Execute();
        }

        public void JumpToLocationWithDelay(float delay)
        {
            API.DelayInvoke(delay, Execute);
        }

        protected abstract void Execute();

        protected void Complete()
        {
            m_OnComplete?.Invoke();
            m_OnComplete = null;
        }
    }
}