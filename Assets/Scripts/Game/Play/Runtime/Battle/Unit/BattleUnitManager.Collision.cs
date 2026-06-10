using Game.Play.Battle.Collision;

namespace Game.Play.Battle.Unit
{
    public sealed partial class BattleUnitManager
    {
        public bool RegisterCollisionTarget(BattleUnitHandle unit, BattleCollisionManager collisionManager)
        {
            if (!IsValid(unit) || collisionManager == null)
            {
                return false;
            }

            int index = unit.index;
            BattleCollisionTargetHandle existing = collisionTargetHandles[index];
            if (collisionManager.IsValidTarget(existing))
            {
                return SyncCollisionTarget(index, collisionManager);
            }

            BattleCollisionTargetHandle target = collisionManager.RegisterTarget(
                positions[index],
                radii[index],
                camps[index],
                states[index],
                layers[index],
                renderHandles[index],
                unit);

            if (!target.IsValid)
            {
                return false;
            }

            collisionTargetHandles[index] = target;
            return true;
        }

        public bool UnregisterCollisionTarget(BattleUnitHandle unit, BattleCollisionManager collisionManager)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            return UnregisterCollisionTarget(unit.index, collisionManager);
        }

        public void SyncCollisionTargets(BattleCollisionManager collisionManager)
        {
            if (collisionManager == null)
            {
                return;
            }

            for (int i = 0; i < allocatedCount; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                BattleCollisionTargetHandle target = collisionTargetHandles[i];
                if (!collisionManager.IsValidTarget(target))
                {
                    continue;
                }

                SyncCollisionTarget(i, collisionManager);
            }
        }

        private bool SyncCollisionTarget(int index, BattleCollisionManager collisionManager)
        {
            BattleCollisionTargetHandle target = collisionTargetHandles[index];
            bool positionUpdated = collisionManager.UpdateTargetPosition(target, positions[index]);
            bool radiusUpdated = collisionManager.UpdateTargetRadius(target, radii[index]);
            bool filterUpdated = collisionManager.UpdateTargetFilter(target, camps[index], states[index], layers[index]);
            return positionUpdated && radiusUpdated && filterUpdated;
        }

        private bool UnregisterCollisionTarget(int index, BattleCollisionManager collisionManager)
        {
            BattleCollisionTargetHandle target = collisionTargetHandles[index];

            if (collisionManager == null || !target.IsValid)
            {
                return false;
            }

            bool unregistered = collisionManager.UnregisterTarget(target);
            if (unregistered)
            {
                collisionTargetHandles[index] = BattleCollisionTargetHandle.Invalid;
            }

            return unregistered;
        }
    }
}
