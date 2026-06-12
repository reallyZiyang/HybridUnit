using Game.Play.Battle.Collision;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Skill
{
    public sealed partial class BattleSkillManager
    {
        private enum CastPhase
        {
            Idle,
            WaitingTrigger,
            WaitingEnd
        }

        private readonly BattleRuntimeData data;
        private readonly BattleUnitManager units;
        private readonly BattleCollisionManager collisions;
        private readonly BattleCollisionQueryBuffer queryBuffer;
        private readonly BattleEffectExecutor effects;
        private readonly IBattleRenderWorld renderWorld;
        private readonly int unitCapacity;
        private readonly int slotsPerUnit;
        private readonly int[] skillIds;
        private readonly int[] cooldownMs;
        private readonly int[] phaseRemainingMs;
        private readonly int[] castDurationMs;
        private readonly CastPhase[] phases;
        private readonly BattleUnitHandle[] owners;
        private readonly BattleUnitHandle[] targets;
        private readonly bool[] active;

        public BattleSkillManager(
            BattleRuntimeData data,
            BattleUnitManager units,
            BattleCollisionManager collisions,
            BattleEffectExecutor effects,
            IBattleRenderWorld renderWorld,
            int unitCapacity,
            int slotsPerUnit,
            int queryCapacity)
        {
            this.data = data;
            this.units = units;
            this.collisions = collisions;
            this.effects = effects;
            this.renderWorld = renderWorld;
            this.unitCapacity = Mathf.Max(1, unitCapacity);
            this.slotsPerUnit = Mathf.Max(1, slotsPerUnit);
            int capacity = this.unitCapacity * this.slotsPerUnit;
            skillIds = new int[capacity];
            cooldownMs = new int[capacity];
            phaseRemainingMs = new int[capacity];
            castDurationMs = new int[capacity];
            phases = new CastPhase[capacity];
            owners = new BattleUnitHandle[capacity];
            targets = new BattleUnitHandle[capacity];
            active = new bool[capacity];
            queryBuffer = new BattleCollisionQueryBuffer(Mathf.Max(1, queryCapacity));
        }

        public int SlotsPerUnit => slotsPerUnit;

        public int GetSlotStart(BattleUnitHandle unit) => unit.index * slotsPerUnit;

        public bool IsUnitBusy(BattleUnitHandle unit)
        {
            if (!units.IsAlive(unit))
            {
                return false;
            }

            if (units.IsHitLocked(unit))
            {
                return true;
            }

            int start = GetSlotStart(unit);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                if (active[slot] && phases[slot] != CastPhase.Idle)
                {
                    return true;
                }
            }

            return false;
        }

        public void InterruptUnitCast(BattleUnitHandle unit)
        {
            if (!units.IsValid(unit))
            {
                return;
            }

            int start = GetSlotStart(unit);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                if (!active[slot] || phases[slot] == CastPhase.Idle)
                {
                    continue;
                }

                phases[slot] = CastPhase.Idle;
                phaseRemainingMs[slot] = 0;
                castDurationMs[slot] = 0;
                targets[slot] = BattleUnitHandle.Invalid;
            }
        }

        public float GetBasicAttackRange(BattleUnitHandle unit)
        {
            if (!units.IsAlive(unit))
            {
                return 0f;
            }

            int start = GetSlotStart(unit);
            if (!active[start] || !data.TryGetSkill(skillIds[start], out BattleSkillRuntimeData skill))
            {
                return 1f;
            }

            return ResolveCastRange(skill);
        }

        public void BindUnitSkills(BattleUnitHandle unit, int[] defaultSkills)
        {
            int start = GetSlotStart(unit);
            int count = Mathf.Min(slotsPerUnit, defaultSkills?.Length ?? 0);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                active[slot] = i < count;
                owners[slot] = unit;
                targets[slot] = BattleUnitHandle.Invalid;
                skillIds[slot] = i < count ? defaultSkills[i] : 0;
                cooldownMs[slot] = 0;
                phaseRemainingMs[slot] = 0;
                castDurationMs[slot] = 0;
                phases[slot] = CastPhase.Idle;
            }
        }

        public void ClearUnitSkills(BattleUnitHandle unit)
        {
            int start = GetSlotStart(unit);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                active[slot] = false;
                owners[slot] = BattleUnitHandle.Invalid;
                targets[slot] = BattleUnitHandle.Invalid;
                skillIds[slot] = 0;
                cooldownMs[slot] = 0;
                phaseRemainingMs[slot] = 0;
                castDurationMs[slot] = 0;
                phases[slot] = CastPhase.Idle;
            }
        }

        public bool TryCastSkill(BattleUnitHandle caster, int skillId)
        {
            if (!units.IsAlive(caster) || !data.TryGetSkill(skillId, out BattleSkillRuntimeData skill))
            {
                return false;
            }

            BattleUnitHandle target = SelectTarget(caster, skill);
            if (!target.IsValid)
            {
                return false;
            }

            int slot = FindSlot(caster, skillId);
            if (slot < 0)
            {
                return false;
            }

            StartCast(slot, skill, target);
            return true;
        }

        public void Tick(int deltaMs)
        {
            for (int unitIndex = 0; unitIndex < unitCapacity; unitIndex++)
            {
                if (!units.TryGetHandleByIndex(unitIndex, out BattleUnitHandle unit) || !units.IsAlive(unit))
                {
                    continue;
                }

                TickUnit(unit, deltaMs);
            }
        }

        private void TickUnit(BattleUnitHandle unit, int deltaMs)
        {
            int start = GetSlotStart(unit);
            if (units.IsHitLocked(unit))
            {
                TickCooldowns(start, deltaMs);
                return;
            }

            bool hasBusySkill = false;
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                if (!active[slot])
                {
                    continue;
                }

                if (cooldownMs[slot] > 0)
                {
                    cooldownMs[slot] = Mathf.Max(0, cooldownMs[slot] - deltaMs);
                }

                if (phases[slot] != CastPhase.Idle)
                {
                    hasBusySkill = true;
                    TickCast(slot, deltaMs);
                }
            }

            if (hasBusySkill)
            {
                return;
            }

            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                if (!active[slot] || cooldownMs[slot] > 0)
                {
                    continue;
                }

                if (!data.TryGetSkill(skillIds[slot], out BattleSkillRuntimeData skill))
                {
                    continue;
                }

                BattleUnitHandle target = SelectTarget(unit, skill);
                if (!target.IsValid)
                {
                    continue;
                }

                StartCast(slot, skill, target);
                return;
            }
        }

        private void TickCooldowns(int start, int deltaMs)
        {
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                if (active[slot] && cooldownMs[slot] > 0)
                {
                    cooldownMs[slot] = Mathf.Max(0, cooldownMs[slot] - deltaMs);
                }
            }
        }

        private void TickCast(int slot, int deltaMs)
        {
            phaseRemainingMs[slot] = Mathf.Max(0, phaseRemainingMs[slot] - deltaMs);
            if (phaseRemainingMs[slot] > 0 || !data.TryGetSkill(skillIds[slot], out BattleSkillRuntimeData skill))
            {
                return;
            }

            if (phases[slot] == CastPhase.WaitingTrigger)
            {
                FireSkill(owners[slot], targets[slot], skill);
                int castDurationMs = ResolveCastDurationMs(slot, skill);
                int remainingCastMs = Mathf.Max(0, castDurationMs - Mathf.Max(0, skill.castPreMs));
                if (remainingCastMs > 0)
                {
                    phases[slot] = CastPhase.WaitingEnd;
                    phaseRemainingMs[slot] = remainingCastMs;
                }
                else
                {
                    FinishCast(slot);
                }
            }
            else if (phases[slot] == CastPhase.WaitingEnd)
            {
                FinishCast(slot);
            }
        }

        private void StartCast(int slot, BattleSkillRuntimeData skill, BattleUnitHandle target)
        {
            cooldownMs[slot] = Mathf.Max(0, skill.cooldownMs);
            targets[slot] = target;
            int animationMs = renderWorld?.PlayUnitAction(units.GetRenderHandle(owners[slot]), skill.actionName) ?? 0;
            castDurationMs[slot] = Mathf.Max(animationMs, Mathf.Max(0, skill.castPreMs));
            phaseRemainingMs[slot] = castDurationMs[slot];

            if (skill.castPreMs <= 0)
            {
                FireSkill(owners[slot], target, skill);
                if (phaseRemainingMs[slot] > 0)
                {
                    phases[slot] = CastPhase.WaitingEnd;
                }
                else
                {
                    FinishCast(slot);
                }
            }
            else
            {
                phases[slot] = CastPhase.WaitingTrigger;
                phaseRemainingMs[slot] = skill.castPreMs;
            }
        }

        private int ResolveCastDurationMs(int slot, BattleSkillRuntimeData skill)
        {
            return Mathf.Max(castDurationMs[slot], Mathf.Max(0, skill.castPreMs));
        }

        private void FinishCast(int slot)
        {
            phases[slot] = CastPhase.Idle;
            phaseRemainingMs[slot] = 0;
            castDurationMs[slot] = 0;
            BattleUnitHandle owner = owners[slot];
            targets[slot] = BattleUnitHandle.Invalid;
            if (units.IsAlive(owner))
            {
                renderWorld?.PlayUnitIdle(units.GetRenderHandle(owner));
            }
        }

        private int FindSlot(BattleUnitHandle caster, int skillId)
        {
            int start = GetSlotStart(caster);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                if (active[slot] && skillIds[slot] == skillId && cooldownMs[slot] <= 0 && phases[slot] == CastPhase.Idle)
                {
                    return slot;
                }
            }

            return -1;
        }
    }
}
