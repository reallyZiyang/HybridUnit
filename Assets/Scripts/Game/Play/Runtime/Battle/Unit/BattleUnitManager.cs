using Game.Play.Battle.Collision;
using Game.Play.Battle.Runtime;
using Game.Data.Configs.Attr;
using UnityEngine;

namespace Game.Play.Battle.Unit
{
    public sealed partial class BattleUnitManager
    {
        public const float DefaultPushRadius = 0.25f;

        private readonly int capacity;
        private readonly int attrStride;
        private readonly Vector2[] positions;
        private readonly Vector2[] previousPositions;
        private readonly float[] radii;
        private readonly float[] pushRadii;
        private readonly int[] camps;
        private readonly int[] states;
        private readonly int[] layers;
        private readonly int[] unitCfgIds;
        private readonly int[] hp;
        private readonly int[] hitLockRemainingMs;
        private readonly bool[] canPushOthers;
        private readonly bool[] canBePushed;
        private readonly bool[] active;
        private readonly int[] generations;
        private readonly int[] renderHandles;
        private readonly int[] skillSlotStarts;
        private readonly int[] skillSlotCounts;
        private readonly BattleCollisionTargetHandle[] collisionTargetHandles;
        private readonly int[] freeStack;
        private readonly long[] baseAttrs;
        private readonly long[] modifierAttrs;
        private readonly long[] finalAttrs;

        private int allocatedCount;
        private int activeCount;
        private int freeCount;
        private float maxPushRadius;

        public BattleUnitManager(int capacity, int attrStride = 0)
        {
            this.capacity = Mathf.Max(1, capacity);
            this.attrStride = attrStride > 0 ? attrStride : BattleAttributeRegistry.Count;
            positions = new Vector2[this.capacity];
            previousPositions = new Vector2[this.capacity];
            radii = new float[this.capacity];
            pushRadii = new float[this.capacity];
            camps = new int[this.capacity];
            states = new int[this.capacity];
            layers = new int[this.capacity];
            unitCfgIds = new int[this.capacity];
            hp = new int[this.capacity];
            hitLockRemainingMs = new int[this.capacity];
            canPushOthers = new bool[this.capacity];
            canBePushed = new bool[this.capacity];
            active = new bool[this.capacity];
            generations = new int[this.capacity];
            renderHandles = new int[this.capacity];
            skillSlotStarts = new int[this.capacity];
            skillSlotCounts = new int[this.capacity];
            collisionTargetHandles = new BattleCollisionTargetHandle[this.capacity];
            freeStack = new int[this.capacity];
            baseAttrs = new long[this.capacity * this.attrStride];
            modifierAttrs = new long[this.capacity * this.attrStride];
            finalAttrs = new long[this.capacity * this.attrStride];

            for (int i = 0; i < collisionTargetHandles.Length; i++)
            {
                collisionTargetHandles[i] = BattleCollisionTargetHandle.Invalid;
                renderHandles[i] = -1;
                skillSlotStarts[i] = -1;
            }
        }

        public int Capacity => capacity;
        public int ActiveCount => activeCount;
        public int AllocatedCount => allocatedCount;
        public int AttributeStride => attrStride;

        public BattleUnitHandle SpawnUnit(in BattleUnitSpawnDesc desc)
        {
            int index;
            if (freeCount > 0)
            {
                index = freeStack[--freeCount];
            }
            else
            {
                if (allocatedCount >= capacity)
                {
                    Debug.LogError($"[BattleUnit] Unit capacity exceeded: {capacity}");
                    return BattleUnitHandle.Invalid;
                }

                index = allocatedCount++;
            }

            int generation = generations[index] + 1;
            generations[index] = generation > 0 ? generation : 1;
            unitCfgIds[index] = desc.unitCfgId;
            positions[index] = desc.position;
            previousPositions[index] = desc.position;
            radii[index] = Mathf.Max(0f, desc.radius);
            camps[index] = desc.camp;
            states[index] = desc.state;
            layers[index] = desc.layer;
            renderHandles[index] = desc.renderHandle;
            skillSlotStarts[index] = desc.skillSlotStart;
            skillSlotCounts[index] = Mathf.Max(0, desc.skillSlotCount);
            collisionTargetHandles[index] = BattleCollisionTargetHandle.Invalid;
            ClearAttributes(index);
            ApplyInitialAttributes(index, desc.attrs);
            hp[index] = ResolveSpawnHp(index, desc.hp);
            hitLockRemainingMs[index] = 0;
            SetBaseAttrByIndex(index, AttributeType.Hp, hp[index]);
            SetPushPropertiesByIndex(
                index,
                desc.hasPushRadius ? desc.pushRadius : DefaultPushRadius,
                desc.hasCanPushOthers ? desc.canPushOthers : true,
                desc.hasCanBePushed ? desc.canBePushed : true);
            active[index] = true;
            activeCount++;

            return new BattleUnitHandle(index, generations[index]);
        }

        public bool DespawnUnit(BattleUnitHandle unit, BattleCollisionManager collisionManager = null)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            int index = unit.index;
            UnregisterCollisionTarget(index, collisionManager);
            active[index] = false;
            unitCfgIds[index] = 0;
            hp[index] = 0;
            hitLockRemainingMs[index] = 0;
            pushRadii[index] = 0f;
            canPushOthers[index] = false;
            canBePushed[index] = false;
            states[index] = 0;
            renderHandles[index] = -1;
            skillSlotStarts[index] = -1;
            skillSlotCounts[index] = 0;
            ClearAttributes(index);
            RecalculateMaxPushRadius();
            freeStack[freeCount++] = index;
            activeCount--;
            return true;
        }

        public bool IsValid(BattleUnitHandle unit)
        {
            return unit.index >= 0
                && unit.index < capacity
                && active[unit.index]
                && generations[unit.index] == unit.generation;
        }

        public bool IsAlive(BattleUnitHandle unit)
        {
            return IsValid(unit)
                && hp[unit.index] > 0
                && (states[unit.index] & BattleUnitStates.Dead) == 0;
        }

        public bool SetPosition(BattleUnitHandle unit, Vector2 position)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            positions[unit.index] = position;
            return true;
        }

        public void TickHitLocks(int deltaMs)
        {
            int safeDeltaMs = Mathf.Max(0, deltaMs);
            if (safeDeltaMs <= 0)
            {
                return;
            }

            for (int i = 0; i < allocatedCount; i++)
            {
                if (active[i] && hitLockRemainingMs[i] > 0)
                {
                    hitLockRemainingMs[i] = Mathf.Max(0, hitLockRemainingMs[i] - safeDeltaMs);
                }
            }
        }

        public bool ApplyHitLock(BattleUnitHandle unit, int durationMs)
        {
            if (!IsAlive(unit))
            {
                return false;
            }

            hitLockRemainingMs[unit.index] = Mathf.Max(hitLockRemainingMs[unit.index], Mathf.Max(1, durationMs));
            return true;
        }

        public bool IsHitLocked(BattleUnitHandle unit)
        {
            return IsAlive(unit) && hitLockRemainingMs[unit.index] > 0;
        }

        public bool HasEndure(BattleUnitHandle unit)
        {
            return IsAlive(unit) && GetAttr(unit, AttributeType.Endure) > 0;
        }

        public void CapturePreviousPositions()
        {
            for (int i = 0; i < allocatedCount; i++)
            {
                if (active[i])
                {
                    previousPositions[i] = positions[i];
                }
            }
        }

        public bool SetRenderHandle(BattleUnitHandle unit, int renderHandle)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            renderHandles[unit.index] = renderHandle;
            return true;
        }

        public bool SetSkillSlots(BattleUnitHandle unit, int slotStart, int slotCount)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            skillSlotStarts[unit.index] = slotStart;
            skillSlotCounts[unit.index] = Mathf.Max(0, slotCount);
            return true;
        }

        public bool SetFilter(BattleUnitHandle unit, int camp, int state, int layer)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            int index = unit.index;
            camps[index] = camp;
            states[index] = state;
            layers[index] = layer;
            return true;
        }

        public bool ApplyDamage(BattleUnitHandle unit, long damage, BattleCollisionManager collisionManager = null)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            if (damage <= 0 || (states[unit.index] & BattleUnitStates.Invincible) != 0)
            {
                return true;
            }

            int index = unit.index;
            hp[index] = (int)System.Math.Max(0L, hp[index] - damage);
            SetBaseAttrByIndex(index, AttributeType.Hp, hp[index]);
            if (hp[index] == 0)
            {
                MarkDead(index, collisionManager);
            }

            return true;
        }

        public bool ApplyHeal(BattleUnitHandle unit, long heal)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            if (heal <= 0)
            {
                return true;
            }

            int index = unit.index;
            long hpMax = GetAttr(unit, AttributeType.HpMax);
            long nextHp = hp[index] + heal;
            if (hpMax > 0)
            {
                nextHp = System.Math.Min(nextHp, hpMax);
            }

            hp[index] = (int)System.Math.Max(0L, nextHp);
            SetBaseAttrByIndex(index, AttributeType.Hp, hp[index]);
            return true;
        }

        public Vector2 GetPosition(BattleUnitHandle unit)
        {
            return IsValid(unit) ? positions[unit.index] : default;
        }

        public Vector2 GetInterpolatedPosition(BattleUnitHandle unit, float alpha)
        {
            return IsValid(unit) ? Vector2.Lerp(previousPositions[unit.index], positions[unit.index], Mathf.Clamp01(alpha)) : default;
        }

        public bool HasMovedSincePreviousCapture(BattleUnitHandle unit, float epsilon = 0.0001f)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            float safeEpsilon = Mathf.Max(0f, epsilon);
            return (positions[unit.index] - previousPositions[unit.index]).sqrMagnitude > safeEpsilon * safeEpsilon;
        }

        public float GetRadius(BattleUnitHandle unit)
        {
            return IsValid(unit) ? radii[unit.index] : 0f;
        }

        public int GetHp(BattleUnitHandle unit)
        {
            return IsValid(unit) ? hp[unit.index] : 0;
        }

        public int GetCamp(BattleUnitHandle unit)
        {
            return IsValid(unit) ? camps[unit.index] : 0;
        }

        public int GetLayer(BattleUnitHandle unit)
        {
            return IsValid(unit) ? layers[unit.index] : 0;
        }

        public int GetState(BattleUnitHandle unit)
        {
            return IsValid(unit) ? states[unit.index] : 0;
        }

        public int GetUnitCfgId(BattleUnitHandle unit)
        {
            return IsValid(unit) ? unitCfgIds[unit.index] : 0;
        }

        public int GetRenderHandle(BattleUnitHandle unit)
        {
            return IsValid(unit) ? renderHandles[unit.index] : -1;
        }

        public int GetSkillSlotStart(BattleUnitHandle unit)
        {
            return IsValid(unit) ? skillSlotStarts[unit.index] : -1;
        }

        public int GetSkillSlotCount(BattleUnitHandle unit)
        {
            return IsValid(unit) ? skillSlotCounts[unit.index] : 0;
        }

        public bool TryGetHandleByIndex(int index, out BattleUnitHandle unit)
        {
            if (index >= 0 && index < capacity && active[index])
            {
                unit = new BattleUnitHandle(index, generations[index]);
                return true;
            }

            unit = BattleUnitHandle.Invalid;
            return false;
        }

        public BattleCollisionTargetHandle GetCollisionTargetHandle(BattleUnitHandle unit)
        {
            return IsValid(unit) ? collisionTargetHandles[unit.index] : BattleCollisionTargetHandle.Invalid;
        }

        private void MarkDead(int index, BattleCollisionManager collisionManager)
        {
            states[index] = (states[index] | BattleUnitStates.Dead) & ~BattleUnitStates.Alive;
            UnregisterCollisionTarget(index, collisionManager);
        }
    }
}
