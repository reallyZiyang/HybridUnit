using System;
using Cysharp.Threading.Tasks;
using Game.Data.Configs.Sys;
using Game.Play.Systems.Common.Navigator.Data;
using UniKit.Framework.Base;
using UniKit.Framework.Timer;
using UniKit.UI.Core;

namespace Game.Play.Systems.Common.Navigator.Interface
{
    public interface INavigatorSystem : ISystem, ITimerOwner
    {
        SystemModule GetModule(SystemType type);
        void NavigateTo(SystemType type, Action callback = null, bool force = true);
        void UnlockAll();

        T Get<T>(SystemType type) where T : UIView;
        void Open(SystemType type, Action<UIView> complete = null);
        void Open(SystemType type, object data, Action<UIView> complete = null);
        void Close(SystemType type, bool recursion = true);
    }

    public class UnlockAllModuleCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetSystem<INavigatorSystem>().UnlockAll();
        }
    }
}