using Game.Play.Battle.Collision;
using Game.Play.Battle.Unit;

namespace Game.Play.Battle.Interception
{
    public sealed class BattleInterceptionTargetFilter : IBattleCollisionTargetFilter
    {
        private readonly BattleCollisionManager collisions;
        private readonly BattleInterceptionSystem interception;
        private BattleUnitHandle attacker;
        private bool useCandidateFilter;

        public BattleInterceptionTargetFilter(BattleCollisionManager collisions, BattleInterceptionSystem interception)
        {
            this.collisions = collisions;
            this.interception = interception;
            attacker = BattleUnitHandle.Invalid;
        }

        public void Reset(BattleUnitHandle attacker, bool useCandidateFilter = false)
        {
            this.attacker = attacker;
            this.useCandidateFilter = useCandidateFilter;
        }

        public bool Accept(int targetIndex)
        {
            if (interception == null)
            {
                return true;
            }

            BattleUnitHandle target = collisions.GetUnitHandle(targetIndex);
            return useCandidateFilter
                ? interception.CanSubmitCandidate(attacker, target)
                : interception.CanReserve(attacker, target);
        }
    }
}
