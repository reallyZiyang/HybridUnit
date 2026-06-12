using UnityEngine;

namespace Game.Play.Battle.Unit
{
    public sealed partial class BattleUnitManager
    {
        public float MaxPushRadius => maxPushRadius;

        public float GetPushRadius(BattleUnitHandle unit)
        {
            return IsValid(unit) ? pushRadii[unit.index] : 0f;
        }

        public bool CanPushOthers(BattleUnitHandle unit)
        {
            return IsAlive(unit) && canPushOthers[unit.index] && pushRadii[unit.index] > 0f;
        }

        public bool CanBePushed(BattleUnitHandle unit)
        {
            return IsAlive(unit) && canBePushed[unit.index] && pushRadii[unit.index] > 0f;
        }

        public bool SetPushProperties(BattleUnitHandle unit, float pushRadius, bool pushesOthers, bool isPushable)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            SetPushPropertiesByIndex(unit.index, pushRadius, pushesOthers, isPushable);
            return true;
        }

        private void SetPushPropertiesByIndex(int index, float pushRadius, bool pushesOthers, bool isPushable)
        {
            float previousMax = maxPushRadius;
            float previousRadius = pushRadii[index];
            float safeRadius = Mathf.Max(0f, pushRadius);
            pushRadii[index] = safeRadius;
            canPushOthers[index] = pushesOthers;
            canBePushed[index] = isPushable;

            if (safeRadius > maxPushRadius)
            {
                maxPushRadius = safeRadius;
            }
            else if (Mathf.Approximately(previousRadius, previousMax) && safeRadius < previousRadius)
            {
                RecalculateMaxPushRadius();
            }
        }

        private void RecalculateMaxPushRadius()
        {
            float max = 0f;
            for (int i = 0; i < allocatedCount; i++)
            {
                if (active[i] && pushRadii[i] > max)
                {
                    max = pushRadii[i];
                }
            }

            maxPushRadius = max;
        }
    }
}
