using Game.Play.Battle.Unit;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Play.Battle.Interception
{
    public class BattleInterceptionSystem
    {
        public const int DefaultInterceptCapacity = 10;
        public const int FullTargetBlockDurationMs = 300;

        private struct InterceptCandidate
        {
            public BattleUnitHandle attacker;
            public BattleUnitHandle target;
            public float distanceSqr;
        }

        private sealed class CandidateComparer : IComparer<InterceptCandidate>
        {
            public int Compare(InterceptCandidate x, InterceptCandidate y)
            {
                int targetCompare = x.target.index.CompareTo(y.target.index);
                if (targetCompare != 0)
                {
                    return targetCompare;
                }

                int distanceCompare = x.distanceSqr.CompareTo(y.distanceSqr);
                return distanceCompare != 0 ? distanceCompare : x.attacker.index.CompareTo(y.attacker.index);
            }
        }

        private static readonly CandidateComparer CandidateSortComparer = new();

        private readonly BattleUnitManager units;
        private readonly BattleUnitHandle[] reservedTargets;
        private readonly BattleUnitHandle[] previousReservedTargets;
        private readonly int[] interceptCounts;
        private readonly int[] fullBlockRemainingMs;
        private readonly int[] fullBlockGenerations;
        private readonly InterceptCandidate[] candidates;
        private int candidateCount;

        public BattleInterceptionSystem(BattleUnitManager units, int unitCapacity)
        {
            this.units = units;
            int capacity = Mathf.Max(1, unitCapacity);
            reservedTargets = new BattleUnitHandle[capacity];
            previousReservedTargets = new BattleUnitHandle[capacity];
            interceptCounts = new int[capacity];
            fullBlockRemainingMs = new int[capacity];
            fullBlockGenerations = new int[capacity];
            candidates = new InterceptCandidate[capacity];
            for (int i = 0; i < capacity; i++)
            {
                reservedTargets[i] = BattleUnitHandle.Invalid;
                previousReservedTargets[i] = BattleUnitHandle.Invalid;
            }

            BeginTick(0);
        }

        public void BeginTick(int deltaMs = 0)
        {
            int safeDeltaMs = Mathf.Max(0, deltaMs);
            for (int i = 0; i < reservedTargets.Length; i++)
            {
                previousReservedTargets[i] = reservedTargets[i];
                reservedTargets[i] = BattleUnitHandle.Invalid;
                interceptCounts[i] = 0;
                if (fullBlockRemainingMs[i] > 0)
                {
                    fullBlockRemainingMs[i] = Mathf.Max(0, fullBlockRemainingMs[i] - safeDeltaMs);
                }
            }

            candidateCount = 0;
        }

        public BattleUnitHandle GetReservedTarget(BattleUnitHandle attacker)
        {
            if (!IsKnownUnit(attacker))
            {
                return BattleUnitHandle.Invalid;
            }

            BattleUnitHandle target = reservedTargets[attacker.index];
            return IsValidTarget(attacker, target) ? target : BattleUnitHandle.Invalid;
        }

        public bool CanReserve(BattleUnitHandle attacker, BattleUnitHandle target)
        {
            if (!IsKnownUnit(attacker) || !IsKnownUnit(target) || !IsValidTarget(attacker, target))
            {
                return false;
            }

            BattleUnitHandle reservedTarget = reservedTargets[attacker.index];
            if (reservedTarget.SameAs(target))
            {
                return true;
            }

            int capacity = GetInterceptCapacity(target);
            return capacity > 0 && interceptCounts[target.index] < capacity;
        }

        public bool CanSubmitCandidate(BattleUnitHandle attacker, BattleUnitHandle target)
        {
            if (!IsKnownUnit(attacker) || !IsKnownUnit(target) || !IsValidTarget(attacker, target))
            {
                return false;
            }

            if (!IsFullBlocked(target))
            {
                return true;
            }

            BattleUnitHandle reservedTarget = reservedTargets[attacker.index];
            if (reservedTarget.SameAs(target))
            {
                return true;
            }

            BattleUnitHandle previousReservedTarget = previousReservedTargets[attacker.index];
            return previousReservedTarget.SameAs(target);
        }

        public bool TryReserve(BattleUnitHandle attacker, BattleUnitHandle target)
        {
            if (!CanReserve(attacker, target))
            {
                return false;
            }

            BattleUnitHandle reservedTarget = reservedTargets[attacker.index];
            if (reservedTarget.SameAs(target))
            {
                return true;
            }

            ReleaseReservation(attacker);
            reservedTargets[attacker.index] = target;
            interceptCounts[target.index]++;
            RefreshFullBlock(target);
            return true;
        }

        public bool AddCandidate(BattleUnitHandle attacker, BattleUnitHandle target, float distanceSqr)
        {
            if (candidateCount >= candidates.Length
                || !CanSubmitCandidate(attacker, target))
            {
                return false;
            }

            candidates[candidateCount++] = new InterceptCandidate
            {
                attacker = attacker,
                target = target,
                distanceSqr = Mathf.Max(0f, distanceSqr)
            };
            return true;
        }

        public void ResolveCandidates()
        {
            if (candidateCount <= 0)
            {
                RefreshFullBlocks();
                return;
            }

            Array.Sort(candidates, 0, candidateCount, CandidateSortComparer);
            for (int i = 0; i < candidateCount; i++)
            {
                InterceptCandidate candidate = candidates[i];
                TryReserve(candidate.attacker, candidate.target);
            }

            RefreshFullBlocks();
        }

        public int GetInterceptCount(BattleUnitHandle target)
        {
            return IsKnownUnit(target) ? interceptCounts[target.index] : 0;
        }

        public virtual int GetInterceptCapacity(BattleUnitHandle target)
        {
            return units.IsAlive(target) ? DefaultInterceptCapacity : 0;
        }

        private void ReleaseReservation(BattleUnitHandle attacker)
        {
            if (!IsKnownUnit(attacker))
            {
                return;
            }

            BattleUnitHandle reservedTarget = reservedTargets[attacker.index];
            if (IsKnownUnit(reservedTarget) && interceptCounts[reservedTarget.index] > 0)
            {
                interceptCounts[reservedTarget.index]--;
            }

            reservedTargets[attacker.index] = BattleUnitHandle.Invalid;
        }

        private bool IsValidTarget(BattleUnitHandle attacker, BattleUnitHandle target)
        {
            return units.IsAlive(attacker)
                && units.IsAlive(target)
                && !attacker.SameAs(target)
                && units.GetCamp(attacker) != units.GetCamp(target)
                && (units.GetState(target) & BattleUnitStates.Selectable) != 0;
        }

        private bool IsKnownUnit(BattleUnitHandle unit)
        {
            return unit.index >= 0 && unit.index < reservedTargets.Length && units.IsValid(unit);
        }

        private void RefreshFullBlocks()
        {
            for (int i = 0; i < interceptCounts.Length; i++)
            {
                if (interceptCounts[i] <= 0 || !units.TryGetHandleByIndex(i, out BattleUnitHandle target))
                {
                    continue;
                }

                int capacity = GetInterceptCapacity(target);
                if (capacity > 0 && interceptCounts[i] >= capacity)
                {
                    RefreshFullBlock(target);
                }
            }
        }

        private void RefreshFullBlock(BattleUnitHandle target)
        {
            if (!IsKnownUnit(target))
            {
                return;
            }

            int capacity = GetInterceptCapacity(target);
            if (capacity > 0 && interceptCounts[target.index] >= capacity)
            {
                fullBlockRemainingMs[target.index] = FullTargetBlockDurationMs;
                fullBlockGenerations[target.index] = target.generation;
            }
        }

        private bool IsFullBlocked(BattleUnitHandle target)
        {
            return IsKnownUnit(target)
                && fullBlockRemainingMs[target.index] > 0
                && fullBlockGenerations[target.index] == target.generation;
        }
    }
}
