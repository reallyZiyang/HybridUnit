using System.Collections.Generic;
using Game.Data.Configs.Sys;
using UniKit.Framework.Base;

namespace Game.Play.Systems.Common.Navigator.Data
{
    public class SystemData : AbstractModel
    {
        public readonly Dictionary<SystemType, SystemModule> modules = new();
    }
}