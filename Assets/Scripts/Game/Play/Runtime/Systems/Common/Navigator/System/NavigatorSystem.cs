using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Data.Configs.Sys;
using Game.Play.Adapters;
using Game.Play.Base.Attributes;
using Game.Play.Systems.Common.Navigator.Data;
using Game.Play.Systems.Common.Navigator.Interface;
using UniKit.Framework.Base;
using UniKit.Framework.Timer;
using UniKit.UI;
using UniKit.UI.Core;
using UnityEngine;

namespace Game.Play.Systems.Common.Navigator.System
{
    [Order(0)]
    public partial class NavigatorSystem : AbstractSystem, INavigatorSystem, IInitializableAsync
    {
        private SystemData m_Data;

        public async UniTask InitAsync()
        {
            await InitModules();
        }

        private async UniTask InitModules()
        {
            m_Data = this.GetModel<SystemData>();

            var navData = await API.Assets.LoadAssetAsync<NavigatorData>("UI_Navigator");
            var navDic = new Dictionary<SystemType, NavigatorItem>();
            navData.items.ForEach(m =>
            {
                var sys = (SystemType)m.id;
                if (!navDic.TryAdd(sys, m))
                    Debug.LogError($"重复系统id:{m.id} {sys}");
            });

            var modules = m_Data.modules;
            foreach (var module in API.Tables.TbSystem.DataList.Select(cfg =>
                         new SystemModule(cfg, navDic.GetValueOrDefault(cfg.Id))))
            {
                modules.TryAdd(module.Type, module);
            }

            foreach (var item in modules.Values)
            {
                if (item.Nav == null)
                {
                    Debug.LogError("Navigator data not found for system type: " + item.Type);
                    continue;
                }

                if (item.Nav.parent <= 0)
                    continue;

                var parentType = (SystemType)item.Nav.parent;
                if (!modules.TryGetValue(parentType, out var parentItem))
                    continue;

                item.Parent = parentItem;
                parentItem.Children.Add(item);
            }
        }

        public SystemModule GetModule(SystemType type)
        {
            if (m_Data.modules.TryGetValue(type, out var item))
            {
                return item;
            }

            Debug.LogError($"System module [{type}] not found.");
            return null;
        }

        public async UniTask<bool> ReceiveReward(SystemType type)
        {
            var data = this.GetModel<SystemData>();
            if (!data.modules.TryGetValue(type, out var module))
                return false;

            if (module.Received)
                return false;

            //var req = new GetFuncOpenReward.request { id = (long)type };
            //var rsp = await this.SendAsync<GetFuncOpenReward.request, GetFuncOpenReward.response>(req);
            //if (rsp.errCode > 0)
            //{
            //    this.ShowErrorCode(rsp.errCode);
            //    return false;
            //}

            module.Received.Value = true;
            return true;
        }

        public void NavigateTo(SystemType type, Action callback = null, bool force = true)
        {
            if (m_Data.modules.TryGetValue(type, out var item))
            {
                DoJump(item, callback, force);
            }
            else
            {
                Debug.LogError($"System type {type} not found.");
            }
        }

        private void DoJump(SystemModule module, Action callback = null, bool force = true)
        {
            var parents = GetParentAndCloseModules(module);
            if (force)
                DoForceJump(module, parents, callback);
            else
                DoSoftJump(module);
        }

        private void DoSoftJump(SystemModule module)
        {
            //var id = module.Cfg.JumpId;
            //if (id > 0)
            //    this.SendCommand(new PlayNavTutorialCommand(id));
        }

        private void DoForceJump(SystemModule module, Stack<SystemModule> parents, Action callback = null)
        {
            var showLoading = parents.Count > 0 && !parents.Peek().Jumper.IsJumped;

            Timer timeout = null;
            if (showLoading)
            {
                // this.ShowLoadingProgress(0.5f);
                timeout = this.RegisterTimer(3, OnJumped);
            }

            API.Fork(Next);

            return;

            void Next()
            {
                if (parents.Count == 0)
                {
                    JumpTo(module, OnJumped);
                }
                else
                {
                    JumpTo(parents.Pop(), Next);
                }
            }

            void JumpTo(SystemModule target, Action onCompleted)
            {
                var jumper = target.Jumper;
                if (jumper == null)
                {
                    onCompleted?.Invoke();
                    Debug.LogWarning($"Jumper for system module [{target.Type}] is not set.");
                    return;
                }

                if (jumper.IsJumped && jumper.IsActivated)
                {
                    onCompleted?.Invoke();
                }
                else
                {
                    Debug.Log("Jumping to system module: " + target.Type);
                    jumper.SetCallback(() =>
                    {
                        onCompleted?.Invoke();
                        target.OnOpen?.Invoke();
                    });
                    jumper.JumpToLocationWithDelay(.2f);
                }
            }

            void OnJumped()
            {
                callback?.Invoke();
                callback = null;

                if (showLoading)
                {
                    timeout?.Cancel();
                    timeout = null;
                    // this.SetLoadingProgress(1, this.HideLoading);
                }
            }
        }

        private static Stack<SystemModule> GetParentAndCloseModules(SystemModule module)
        {
            var views = new HashSet<string>();
            var parents = new Stack<SystemModule>();

            var m = module.Parent;
            while (m is { NeedsToJump: true })
            {
                views.Add(m.Jumper.ViewType.Name);
                if (!m.Nav.skippable)
                    parents.Push(m);
                m = m.Parent;
            }

            UIManager.Instance.CloseAll(name => !views.Contains(name));

            return parents;
        }

        public void UnlockAll()
        {
            foreach (var module in m_Data.modules)
            {
                module.Value.Unlocked.Value = true;
            }
        }

        public T Get<T>(SystemType type) where T : UIView
        {
            var module = GetModule(type);
            if (module == null)
            {
                Debug.LogError($"Module {type} not found");
                return null;
            }
            else
            {
                return (T)UIManager.Instance.Get(module.Jumper.ViewType.Name);
            }
        }

        public void Open(SystemType type, Action<UIView> complete = null)
        {
            Open(type, null, complete);
        }

        public void Open(SystemType type, object data, Action<UIView> complete = null)
        {
            var module = GetModule(type);
            if (module == null)
            {
                Debug.LogError($"Module {type} not found");
                return;
            }

            if (module.Jumper == null)
            {
                Debug.LogError("Jumper not found for module: " + type);
                return;
            }

            UIManager.Instance.Open(module.Jumper.ViewType, data, view =>
            {
                complete?.Invoke(view);
                module.OnOpen?.Invoke();
            });
        }

        public void Close(SystemType type, bool recursion = true)
        {
            var module = GetModule(type);
            if (module == null)
            {
                Debug.LogError($"Module {type} not found");
            }
            else
            {
                if (recursion)
                {
                    module.Children.ForEach(childModule =>
                    {
                        if (!string.IsNullOrEmpty(childModule.Jumper?.ViewType?.Name) &&
                            UIManager.Instance.IsViewActive(childModule.Jumper.ViewType.Name))
                            Close(childModule.Type);
                    });
                }

                UIManager.Instance.Close(module.Jumper.ViewType.Name);
            }
        }
    }
}