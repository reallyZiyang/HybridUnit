using System;
using Game.Data.Configs.Sys;
using Game.Play.Systems.Common.Navigator.Data;

namespace Game.Play.Systems.Common.Navigator.Jumper
{
    public interface IJumper
    {
        bool IsJumped { get; }
        bool IsActivated { get; }
        Type ViewType { get; }

        void ParserConfig(NavigatorItem nav);
        void SetCallback(Action onComplete);
        void JumpToLocation();
        void JumpToLocationWithDelay(float delay);
    }
}