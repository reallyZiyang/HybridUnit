using System;
using System.Collections.Generic;
using Game.Data.Configs.Sys;
using Game.Kits.Bindable.Core;
using Game.Play.Systems.Common.Navigator.Jumper;

namespace Game.Play.Systems.Common.Navigator.Data
{
    public class SystemModule
    {
        public Action OnOpen { get; set; }
        public Bindable<bool> Unlocked { get; } = new();
        public Bindable<bool> Received { get; } = new();

        public SystemModule Parent { get; set; }
        public List<SystemModule> Children { get; set; } = new();
        public SystemCfg Cfg { get; }
        public NavigatorItem Nav { get; }
        public SystemType Type => Cfg.Id;
        public IJumper Jumper { get; private set; }

        public bool NeedsToJump => Jumper != null;

        public SystemModule(SystemCfg cfg, NavigatorItem nav)
        {
            Cfg = cfg;
            Nav = nav;
            Unlocked.Value = cfg.UnlockCondition.Length == 0;
            Jumper = JumpBuilder.Build(nav);
        }
    }
}