using Game.Play.Battle.Collision;
using Game.Play.Battle.Interception;
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
        private readonly BattleInterceptionSystem interception;
        private readonly BattleInterceptionTargetFilter interceptionFilter;
        private readonly BattleEffectExecutor effects;
        private readonly BattleSkillEnhancementContext enhancements;
        private readonly IBattleRenderWorld renderWorld;
        private readonly BattleUnitFacingController facing;
        private readonly int unitCapacity;
        private readonly int slotsPerUnit;
        private readonly int[] skillIds;
        private readonly int[] cooldownMs;
        private readonly int[] phaseRemainingMs;
        private readonly int[] castDurationMs;
        private readonly int[] resolvedCastPreMs;
        private readonly int[] resolvedCastBackMs;
        private readonly int[] resolvedCooldownMs;
        private readonly int[] endureRemainingMs;
        private readonly CastPhase[] phases;
        private readonly BattleUnitHandle[] owners;
        private readonly BattleUnitHandle[] targets;
        private readonly bool[] castEndureGranted;
        private readonly bool[] active;

        public BattleSkillManager(
            BattleRuntimeData data,
            BattleUnitManager units,
            BattleCollisionManager collisions,
            BattleInterceptionSystem interception,
            BattleEffectExecutor effects,
            BattleSkillEnhancementContext enhancements,
            IBattleRenderWorld renderWorld,
            BattleUnitFacingController facing,
            int unitCapacity,
            int slotsPerUnit,
            int queryCapacity)
        {
            this.data = data;
            this.units = units;
            this.collisions = collisions;
            this.interception = interception;
            this.effects = effects;
            this.enhancements = enhancements ?? BattleSkillEnhancementContext.Empty;
            this.renderWorld = renderWorld;
            this.facing = facing;
            this.unitCapacity = Mathf.Max(1, unitCapacity);
            this.slotsPerUnit = Mathf.Max(1, slotsPerUnit);
            int capacity = this.unitCapacity * this.slotsPerUnit;
            skillIds = new int[capacity];
            cooldownMs = new int[capacity];
            phaseRemainingMs = new int[capacity];
            castDurationMs = new int[capacity];
            resolvedCastPreMs = new int[capacity];
            resolvedCastBackMs = new int[capacity];
            resolvedCooldownMs = new int[capacity];
            endureRemainingMs = new int[capacity];
            phases = new CastPhase[capacity];
            owners = new BattleUnitHandle[capacity];
            targets = new BattleUnitHandle[capacity];
            castEndureGranted = new bool[capacity];
            active = new bool[capacity];
            queryBuffer = new BattleCollisionQueryBuffer(Mathf.Max(1, queryCapacity));
            interceptionFilter = interception != null ? new BattleInterceptionTargetFilter(collisions, interception) : null;
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

                ReleaseCastEndure(slot);
                phases[slot] = CastPhase.Idle;
                phaseRemainingMs[slot] = 0;
                castDurationMs[slot] = 0;
                endureRemainingMs[slot] = 0;
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
            if (!active[start] || !TryGetResolvedSkill(start, out BattleSkillRuntimeData skill, out int baseSkillId))
            {
                return 1f;
            }

            return ResolveCastRange(unit, baseSkillId, skill);
        }

        public bool IsBasicAttackInterceptLimited(BattleUnitHandle unit)
        {
            if (!units.IsAlive(unit))
            {
                return false;
            }

            int start = GetSlotStart(unit);
            return active[start]
                && TryGetResolvedSkill(start, out BattleSkillRuntimeData skill, out _)
                && IsInterceptLimitedSkill(skill);
        }

        public void ReserveActiveInterceptions()
        {
            if (interception == null)
            {
                return;
            }

            for (int slot = 0; slot < active.Length; slot++)
            {
                if (!active[slot] || phases[slot] == CastPhase.Idle)
                {
                    continue;
                }

                if (TryGetResolvedSkill(slot, out BattleSkillRuntimeData skill, out _) && IsInterceptLimitedSkill(skill))
                {
                    interception.TryReserve(owners[slot], targets[slot]);
                }
            }
        }

        public void BindUnitSkills(BattleUnitHandle unit, int[] defaultSkills)
        {
            int start = GetSlotStart(unit);
            int count = Mathf.Min(slotsPerUnit, defaultSkills?.Length ?? 0);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                ReleaseCastEndure(slot);
                active[slot] = i < count;
                owners[slot] = unit;
                targets[slot] = BattleUnitHandle.Invalid;
                skillIds[slot] = i < count ? defaultSkills[i] : 0;
                cooldownMs[slot] = 0;
                phaseRemainingMs[slot] = 0;
                castDurationMs[slot] = 0;
                resolvedCastPreMs[slot] = 0;
                resolvedCastBackMs[slot] = 0;
                resolvedCooldownMs[slot] = 0;
                endureRemainingMs[slot] = 0;
                phases[slot] = CastPhase.Idle;
            }
        }

        public void ClearUnitSkills(BattleUnitHandle unit)
        {
            int start = GetSlotStart(unit);
            for (int i = 0; i < slotsPerUnit; i++)
            {
                int slot = start + i;
                ReleaseCastEndure(slot);
                active[slot] = false;
                owners[slot] = BattleUnitHandle.Invalid;
                targets[slot] = BattleUnitHandle.Invalid;
                skillIds[slot] = 0;
                cooldownMs[slot] = 0;
                phaseRemainingMs[slot] = 0;
                castDurationMs[slot] = 0;
                resolvedCastPreMs[slot] = 0;
                resolvedCastBackMs[slot] = 0;
                resolvedCooldownMs[slot] = 0;
                endureRemainingMs[slot] = 0;
                phases[slot] = CastPhase.Idle;
            }
        }

        public bool TryCastSkill(BattleUnitHandle caster, int skillId)
        {
            if (!units.IsAlive(caster))
            {
                return false;
            }

            int slot = FindSlot(caster, skillId);
            if (slot < 0)
            {
                return false;
            }

            int localSlotIndex = GetLocalSlotIndex(slot);
            if (!data.TryGetSkill(enhancements.ResolveSkillId(caster, localSlotIndex, skillId), out BattleSkillRuntimeData skill))
            {
                return false;
            }

            BattleUnitHandle target = SelectTarget(caster, skillId, skill, true);
            if (!target.IsValid)
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

                if (!TryGetResolvedSkill(slot, out BattleSkillRuntimeData skill, out int baseSkillId))
                {
                    continue;
                }

                BattleUnitHandle target = SelectTarget(unit, baseSkillId, skill, false);
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
            TickCastEndure(slot, deltaMs);
            phaseRemainingMs[slot] = Mathf.Max(0, phaseRemainingMs[slot] - deltaMs);
            if (phaseRemainingMs[slot] > 0 || !TryGetResolvedSkill(slot, out BattleSkillRuntimeData skill, out int baseSkillId))
            {
                return;
            }

            if (phases[slot] == CastPhase.WaitingTrigger)
            {
                FireSkill(slot, owners[slot], targets[slot], skill, baseSkillId);
                int castDurationMs = ResolveCastDurationMs(slot, skill);
                int remainingCastMs = Mathf.Max(0, castDurationMs - resolvedCastPreMs[slot]);
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
            ReleaseCastEndure(slot);
            targets[slot] = target;
            facing?.FaceTarget(owners[slot], target);
            int baseSkillId = skillIds[slot];
            int localSlotIndex = GetLocalSlotIndex(slot);
            float animationSpeed = enhancements.ResolveAnimationSpeed(owners[slot], localSlotIndex, baseSkillId, skill.id);
            int animationMs = renderWorld?.PlayUnitAction(units.GetRenderHandle(owners[slot]), skill.actionName, animationSpeed) ?? 0;
            BattleResolvedSkillTiming timing = enhancements.ResolveTiming(
                owners[slot],
                localSlotIndex,
                baseSkillId,
                skill.id,
                skill.castPreMs,
                skill.castBackMs,
                skill.cooldownMs,
                animationMs);
            resolvedCastPreMs[slot] = timing.castPreMs;
            resolvedCastBackMs[slot] = timing.castBackMs;
            resolvedCooldownMs[slot] = timing.cooldownMs;
            castDurationMs[slot] = Mathf.Max(timing.animationMs, timing.castPreMs);
            phaseRemainingMs[slot] = castDurationMs[slot];
            GrantCastEndure(slot);

            if (timing.castPreMs <= 0)
            {
                FireSkill(slot, owners[slot], target, skill, skillIds[slot]);
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
                phaseRemainingMs[slot] = timing.castPreMs;
            }
        }

        private int ResolveCastDurationMs(int slot, BattleSkillRuntimeData skill)
        {
            return Mathf.Max(castDurationMs[slot], resolvedCastPreMs[slot]);
        }

        private void FinishCast(int slot)
        {
            ReleaseCastEndure(slot);
            if (TryGetResolvedSkill(slot, out BattleSkillRuntimeData skill, out int baseSkillId))
            {
                cooldownMs[slot] = resolvedCooldownMs[slot] > 0
                    ? resolvedCooldownMs[slot]
                    : enhancements.ResolveTiming(owners[slot], GetLocalSlotIndex(slot), baseSkillId, skill.id, skill.castPreMs, skill.castBackMs, skill.cooldownMs, 0).cooldownMs;
            }

            phases[slot] = CastPhase.Idle;
            phaseRemainingMs[slot] = 0;
            castDurationMs[slot] = 0;
            resolvedCastPreMs[slot] = 0;
            resolvedCastBackMs[slot] = 0;
            resolvedCooldownMs[slot] = 0;
            BattleUnitHandle owner = owners[slot];
            targets[slot] = BattleUnitHandle.Invalid;
            if (units.IsAlive(owner))
            {
                renderWorld?.PlayUnitIdle(units.GetRenderHandle(owner));
            }
        }

        private void GrantCastEndure(int slot)
        {
            int durationMs = resolvedCastPreMs[slot] + resolvedCastBackMs[slot];
            if (durationMs <= 0 || !units.AddEndure(owners[slot], 1))
            {
                endureRemainingMs[slot] = 0;
                castEndureGranted[slot] = false;
                return;
            }

            endureRemainingMs[slot] = durationMs;
            castEndureGranted[slot] = true;
        }

        private void TickCastEndure(int slot, int deltaMs)
        {
            if (!castEndureGranted[slot])
            {
                return;
            }

            endureRemainingMs[slot] = Mathf.Max(0, endureRemainingMs[slot] - Mathf.Max(0, deltaMs));
            if (endureRemainingMs[slot] <= 0)
            {
                ReleaseCastEndure(slot);
            }
        }

        private void ReleaseCastEndure(int slot)
        {
            endureRemainingMs[slot] = 0;
            if (!castEndureGranted[slot])
            {
                return;
            }

            castEndureGranted[slot] = false;
            units.AddEndure(owners[slot], -1);
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

        private bool TryGetResolvedSkill(int slot, out BattleSkillRuntimeData skill, out int baseSkillId)
        {
            baseSkillId = skillIds[slot];
            int resolvedSkillId = enhancements.ResolveSkillId(owners[slot], GetLocalSlotIndex(slot), baseSkillId);
            return data.TryGetSkill(resolvedSkillId, out skill);
        }

        private int GetLocalSlotIndex(int slot)
        {
            return slot >= 0 ? slot % slotsPerUnit : -1;
        }
    }
}
