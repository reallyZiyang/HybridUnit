using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Projectile
{
    public sealed partial class BattleProjectileManager
    {
        private bool CanHit(int projectileIndex, BattleUnitHandle target, int hitIntervalMs)
        {
            if (hitIntervalMs <= 0)
            {
                return FindHitRecord(projectileIndex, target) < 0;
            }

            int record = FindHitRecord(projectileIndex, target);
            return record < 0 || hitCooldownMs[record] <= 0;
        }

        private void SetHitCooldown(int projectileIndex, BattleUnitHandle target, int hitIntervalMs)
        {
            int record = FindHitRecord(projectileIndex, target);
            if (record < 0)
            {
                record = FindFreeHitRecord(projectileIndex);
            }

            if (record < 0)
            {
                return;
            }

            hitUnitIndices[record] = target.index;
            hitUnitGenerations[record] = target.generation;
            hitCooldownMs[record] = Mathf.Max(0, hitIntervalMs);
        }

        private int FindHitRecord(int projectileIndex, BattleUnitHandle target)
        {
            int start = projectileIndex * hitRecordStride;
            for (int i = 0; i < hitRecordStride; i++)
            {
                int record = start + i;
                if (hitUnitIndices[record] == target.index && hitUnitGenerations[record] == target.generation)
                {
                    return record;
                }
            }

            return -1;
        }

        private int FindFreeHitRecord(int projectileIndex)
        {
            int start = projectileIndex * hitRecordStride;
            for (int i = 0; i < hitRecordStride; i++)
            {
                int record = start + i;
                if (hitUnitIndices[record] < 0)
                {
                    return record;
                }
            }

            return -1;
        }

        private void TickHitCooldowns(int projectileIndex, int deltaMs)
        {
            int start = projectileIndex * hitRecordStride;
            for (int i = 0; i < hitRecordStride; i++)
            {
                int record = start + i;
                if (hitUnitIndices[record] >= 0 && hitCooldownMs[record] > 0)
                {
                    hitCooldownMs[record] = Mathf.Max(0, hitCooldownMs[record] - deltaMs);
                }
            }
        }

        private void ClearHitRecords(int projectileIndex)
        {
            int start = projectileIndex * hitRecordStride;
            for (int i = 0; i < hitRecordStride; i++)
            {
                int record = start + i;
                hitUnitIndices[record] = -1;
                hitUnitGenerations[record] = 0;
                hitCooldownMs[record] = 0;
            }
        }
    }
}
