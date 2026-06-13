using Game.Kits.Bindable.Core;
using UniKit.Framework.Base;

namespace Game.Play.Systems.Level.Model
{
    public sealed class LevelModel : AbstractModel
    {
        public Bindable<LevelFlowState> FlowState { get; } = new(LevelFlowState.None);
        public Bindable<BattleOutcome> BattleOutcome { get; } = new(global::Game.Play.Systems.Level.BattleOutcome.None);

        protected override void OnDispose()
        {
            FlowState.Value = LevelFlowState.None;
            BattleOutcome.Value = global::Game.Play.Systems.Level.BattleOutcome.None;
        }
    }
}
