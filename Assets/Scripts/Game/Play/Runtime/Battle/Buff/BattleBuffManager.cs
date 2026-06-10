using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Battle.Buff
{
    public sealed class BattleBuffManager
    {
        private readonly BattleRuntimeData data;
        private readonly BattleUnitManager units;
        private readonly BattleEffectExecutor effects;
        private readonly int capacity;
        private readonly BattleUnitHandle[] owners;
        private readonly BattleUnitHandle[] sources;
        private readonly int[] buffIds;
        private readonly int[] stacks;
        private readonly int[] remainingMs;
        private readonly int[] tickRemainingMs;
        private readonly bool[] active;
        private readonly int[] freeStack;

        private int allocatedCount;
        private int freeCount;

        public BattleBuffManager(BattleRuntimeData data, BattleUnitManager units, BattleEffectExecutor effects, int capacity)
        {
            this.data = data;
            this.units = units;
            this.effects = effects;
            this.capacity = Mathf.Max(1, capacity);
            owners = new BattleUnitHandle[this.capacity];
            sources = new BattleUnitHandle[this.capacity];
            buffIds = new int[this.capacity];
            stacks = new int[this.capacity];
            remainingMs = new int[this.capacity];
            tickRemainingMs = new int[this.capacity];
            active = new bool[this.capacity];
            freeStack = new int[this.capacity];
        }

        public bool AddBuff(BattleUnitHandle source, BattleUnitHandle target, int buffId, int durationOverrideMs, int stack)
        {
            if (!units.IsAlive(target) || !data.TryGetBuff(buffId, out BattleBuffRuntimeData buff))
            {
                return false;
            }

            int existing = FindBuff(target, buffId);
            if (existing >= 0)
            {
                RefreshExisting(existing, source, buff, durationOverrideMs, stack);
                return true;
            }

            int index = Allocate();
            if (index < 0)
            {
                return false;
            }

            int safeStack = Mathf.Clamp(Mathf.Max(1, stack), 1, Mathf.Max(1, buff.maxStack));
            active[index] = true;
            owners[index] = target;
            sources[index] = source;
            buffIds[index] = buffId;
            stacks[index] = safeStack;
            remainingMs[index] = ResolveDuration(buff, durationOverrideMs);
            tickRemainingMs[index] = Mathf.Max(0, buff.tickMs);
            ApplyAttributeDelta(target, buff, safeStack);
            effects.ExecuteEffects(buff.beginEffects, source, target, units.GetPosition(target), Vector2.zero);
            return true;
        }

        public void RemoveUnitBuffs(BattleUnitHandle unit)
        {
            for (int i = 0; i < allocatedCount; i++)
            {
                if (active[i] && owners[i].index == unit.index && owners[i].generation == unit.generation)
                {
                    RemoveAt(i, executeEndEffects: false);
                }
            }
        }

        public void Tick(int deltaMs)
        {
            for (int i = 0; i < allocatedCount; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                if (!units.IsAlive(owners[i]))
                {
                    RemoveAt(i, executeEndEffects: false);
                    continue;
                }

                if (data.TryGetBuff(buffIds[i], out BattleBuffRuntimeData buff) && buff.tickMs > 0)
                {
                    tickRemainingMs[i] -= deltaMs;
                    while (tickRemainingMs[i] <= 0)
                    {
                        tickRemainingMs[i] += buff.tickMs;
                        effects.ExecuteEffects(buff.tickEffects, sources[i], owners[i], units.GetPosition(owners[i]), Vector2.zero);
                    }
                }

                remainingMs[i] -= deltaMs;
                if (remainingMs[i] <= 0)
                {
                    RemoveAt(i, executeEndEffects: true);
                }
            }
        }

        private void RefreshExisting(int index, BattleUnitHandle source, BattleBuffRuntimeData buff, int durationOverrideMs, int stack)
        {
            sources[index] = source;
            int oldStack = stacks[index];
            int addStack = Mathf.Max(1, stack);
            int maxStack = Mathf.Max(1, buff.maxStack);

            switch (buff.stackMode)
            {
                case ConfigBattle.BuffStackMode.Refresh:
                    remainingMs[index] = ResolveDuration(buff, durationOverrideMs);
                    break;
                case ConfigBattle.BuffStackMode.Stack:
                    stacks[index] = Mathf.Clamp(stacks[index] + addStack, 1, maxStack);
                    break;
                case ConfigBattle.BuffStackMode.RefreshAndStack:
                    remainingMs[index] = ResolveDuration(buff, durationOverrideMs);
                    stacks[index] = Mathf.Clamp(stacks[index] + addStack, 1, maxStack);
                    break;
                case ConfigBattle.BuffStackMode.Replace:
                    ApplyAttributeDelta(owners[index], buff, -oldStack);
                    stacks[index] = Mathf.Clamp(addStack, 1, maxStack);
                    remainingMs[index] = ResolveDuration(buff, durationOverrideMs);
                    ApplyAttributeDelta(owners[index], buff, stacks[index]);
                    return;
            }

            int deltaStack = stacks[index] - oldStack;
            if (deltaStack != 0)
            {
                ApplyAttributeDelta(owners[index], buff, deltaStack);
            }
        }

        private int FindBuff(BattleUnitHandle target, int buffId)
        {
            for (int i = 0; i < allocatedCount; i++)
            {
                if (active[i]
                    && buffIds[i] == buffId
                    && owners[i].index == target.index
                    && owners[i].generation == target.generation)
                {
                    return i;
                }
            }

            return -1;
        }

        private int Allocate()
        {
            if (freeCount > 0)
            {
                return freeStack[--freeCount];
            }

            if (allocatedCount >= capacity)
            {
                Debug.LogError($"[BattleBuff] Buff capacity exceeded: {capacity}");
                return -1;
            }

            return allocatedCount++;
        }

        private void RemoveAt(int index, bool executeEndEffects)
        {
            if (!active[index])
            {
                return;
            }

            BattleUnitHandle owner = owners[index];
            if (data.TryGetBuff(buffIds[index], out BattleBuffRuntimeData buff))
            {
                ApplyAttributeDelta(owner, buff, -stacks[index]);
                if (executeEndEffects && units.IsValid(owner))
                {
                    effects.ExecuteEffects(buff.endEffects, sources[index], owner, units.GetPosition(owner), Vector2.zero);
                }
            }

            active[index] = false;
            owners[index] = BattleUnitHandle.Invalid;
            sources[index] = BattleUnitHandle.Invalid;
            buffIds[index] = 0;
            stacks[index] = 0;
            remainingMs[index] = 0;
            tickRemainingMs[index] = 0;
            freeStack[freeCount++] = index;
        }

        private void ApplyAttributeDelta(BattleUnitHandle unit, BattleBuffRuntimeData buff, int stackDelta)
        {
            if (stackDelta == 0)
            {
                return;
            }

            for (int i = 0; i < buff.attrs.Length; i++)
            {
                units.AddModifierAttr(unit, buff.attrs[i].type, buff.attrs[i].value * stackDelta);
            }
        }

        private static int ResolveDuration(BattleBuffRuntimeData buff, int durationOverrideMs)
        {
            return durationOverrideMs > 0 ? durationOverrideMs : Mathf.Max(0, buff.durationMs);
        }
    }
}
