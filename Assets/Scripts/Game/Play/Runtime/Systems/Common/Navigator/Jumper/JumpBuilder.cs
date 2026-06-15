using Game.Play.Systems.Common.Navigator.Data;
using Game.Play.Systems.Common.Navigator.Jumper.Impls;

namespace Game.Play.Systems.Common.Navigator.Jumper
{
    public static class JumpBuilder
    {
        public static IJumper Build(NavigatorItem nav)
        {
            if (nav == null)
                return null;

            IJumper jumper = nav.jumpType switch
            {
                JumpType.Open => new JumpToUI(),
                JumpType.URL => new JumpToPath(),
                _ => null
            };
            jumper?.ParserConfig(nav);
            return jumper;
        }
    }
}