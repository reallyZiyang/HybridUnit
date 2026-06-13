using Game.Play.Systems.Level.Model;
using UniKit.Framework.Base;

namespace Game.Play.Systems.Level.Query
{
    public sealed class GetBattleResultQuery : AbstractQuery<BattleOutcome>
    {
        protected override BattleOutcome OnDo()
        {
            return Context.GetModel<LevelModel>().BattleOutcome.Value;
        }
    }
}
