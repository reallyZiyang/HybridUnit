using System;
using Game.Data.Configs.Sys;
using Game.Play.Adapters;
using Game.Play.Systems.Common.Navigator.Data;
using Game.Play.Systems.Common.Navigator.Interface;
using UniKit.Framework.Base;
using UniKit.UI;
using UniKit.UI.Core;

namespace Game.Play.Systems.Common.Navigator
{
    public static class NavigatorExtensions
    {
        public static void NavigateTo(this IContextOwner target, SystemType type, Action callback = null, bool force = true)
        {
            target.GetSystem<INavigatorSystem>().NavigateTo(type, callback, force);
        }

        public static SystemModule GetModule(this IContextOwner target, SystemType type)
        {
            return target.GetSystem<INavigatorSystem>().GetModule(type);
        }

        public static T GetView<T>(this IContextOwner target) where T : UIView
        {
            return UIManager.Instance.Get<T>();
        }

        public static void Open(this IContextOwner target, SystemType type, Action<UIView> complete = null)
        {
            target.GetSystem<INavigatorSystem>().Open(type, complete);
        }

        public static void Open(this IContextOwner target, SystemType type, object data, Action<UIView> complete = null)
        {
            target.GetSystem<INavigatorSystem>().Open(type, data, complete);
        }

        public static void Close(this IContextOwner target, SystemType type, bool recursion = true)
        {
            target.GetSystem<INavigatorSystem>().Close(type, recursion);
        }

        public static void ShowUnlockTips(this IContextOwner target, int type, int value)
        {
            //target.ShowTips(GetUnlockTips(type, value));
        }

        public static string GetUnlockTips(this SystemCfg cfg)
        {
            //foreach (var args in cfg.UnlockCondition)
            //{
            //    var tips = GetUnlockTips(args[0], args[1]);
            //    if (!string.IsNullOrEmpty(tips))
            //        return tips;
            //}

            return string.Empty;
        }

        private enum ConditionType
        {
            None,
            MainTask,
            PlayerLevel,
            OpenDays,
        }

        private static string GetUnlockTips(int type, int value)
        {
            var cond = (ConditionType)type;

            switch (cond)
            {
                case ConditionType.PlayerLevel:
                case ConditionType.OpenDays:
                    return API.UI.LocalizeText($"Cond.Type_{type}", value);
                case ConditionType.None:
                default:
                    break;
            }

            return string.Empty;
        }
    }
}