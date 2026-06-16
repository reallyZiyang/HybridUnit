using System;
using UniKit.Framework.Base;
using UniKit.UI;

namespace Game.Play.UI.View.Common.Loading
{
    public static class LoadingExtensions
    {
        private static LoadingView s_View;

        private static LoadingView View
        {
            get
            {
                if (!s_View)
                    s_View = UIManager.Instance.Get<LoadingView>();
                return s_View;
            }
        }

        public static void ShowMaskLoading(this IContextOwner _, string tips = null) => View.ShowMask(tips);
        public static void HideMaskLoading(this IContextOwner _) => View.HideMask();
        
        public static void ShowTimeoutMaskLoading(this IContextOwner _, string tips = null, float time = 1) => View.ShowTimeoutMask(tips, time);
        public static void HideTimeoutMaskLoading(this IContextOwner _) => View.HideTimeoutMask();

        public static void ShowLoading(this IContextOwner _) => View.SetProgressActive(true);
        public static void HideLoading(this IContextOwner _) => View.SetProgressActive(false);

        public static void SetProgress(this IContextOwner _, float progress, float duration = 0.25f,
            Action onComplete = null) => View.SetProgress(progress, duration, onComplete);
    }
}