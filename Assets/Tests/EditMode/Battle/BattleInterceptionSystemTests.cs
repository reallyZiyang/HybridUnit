using Game.Play.Battle.Interception;
using Game.Play.Battle.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattleInterceptionSystemTests
    {
        [Test]
        public void Interception_ResolveCandidatesKeepsNearestAttackersForOneTarget()
        {
            BattleUnitManager units = new(16);
            BattleInterceptionSystem interception = new(units, units.Capacity);
            BattleUnitHandle target = Spawn(units, 1);
            BattleUnitHandle nearAttacker = Spawn(units, 2);

            for (int i = 0; i < BattleInterceptionSystem.DefaultInterceptCapacity; i++)
            {
                Assert.IsTrue(interception.AddCandidate(Spawn(units, 2), target, 100f + i));
            }

            Assert.IsTrue(interception.AddCandidate(nearAttacker, target, 1f));
            interception.ResolveCandidates();

            Assert.AreEqual(target, interception.GetReservedTarget(nearAttacker));
            Assert.AreEqual(BattleInterceptionSystem.DefaultInterceptCapacity, interception.GetInterceptCount(target));
        }

        [Test]
        public void Interception_DifferentTargetsUseOwnCapacity()
        {
            BattleUnitManager units = new(8);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle firstTarget = Spawn(units, 1);
            BattleUnitHandle secondTarget = Spawn(units, 1);
            BattleUnitHandle firstNear = Spawn(units, 2);
            BattleUnitHandle firstFar = Spawn(units, 2);
            BattleUnitHandle secondNear = Spawn(units, 2);
            BattleUnitHandle secondFar = Spawn(units, 2);

            Assert.IsTrue(interception.AddCandidate(firstFar, firstTarget, 20f));
            Assert.IsTrue(interception.AddCandidate(firstNear, firstTarget, 1f));
            Assert.IsTrue(interception.AddCandidate(secondFar, secondTarget, 20f));
            Assert.IsTrue(interception.AddCandidate(secondNear, secondTarget, 1f));

            interception.ResolveCandidates();

            Assert.AreEqual(firstTarget, interception.GetReservedTarget(firstNear));
            Assert.AreEqual(BattleUnitHandle.Invalid, interception.GetReservedTarget(firstFar));
            Assert.AreEqual(secondTarget, interception.GetReservedTarget(secondNear));
            Assert.AreEqual(BattleUnitHandle.Invalid, interception.GetReservedTarget(secondFar));
            Assert.AreEqual(1, interception.GetInterceptCount(firstTarget));
            Assert.AreEqual(1, interception.GetInterceptCount(secondTarget));
        }

        [Test]
        public void Interception_EqualDistanceUsesAttackerIndex()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle target = Spawn(units, 1);
            BattleUnitHandle lowerIndex = Spawn(units, 2);
            BattleUnitHandle higherIndex = Spawn(units, 2);

            Assert.IsTrue(interception.AddCandidate(higherIndex, target, 1f));
            Assert.IsTrue(interception.AddCandidate(lowerIndex, target, 1f));

            interception.ResolveCandidates();

            Assert.AreEqual(target, interception.GetReservedTarget(lowerIndex));
            Assert.AreEqual(BattleUnitHandle.Invalid, interception.GetReservedTarget(higherIndex));
        }

        [Test]
        public void Interception_BeginTickClearsReservations()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new(units, units.Capacity);
            BattleUnitHandle target = Spawn(units, 1);

            Assert.IsTrue(interception.TryReserve(Spawn(units, 2), target));
            interception.BeginTick();

            Assert.AreEqual(0, interception.GetInterceptCount(target));
        }

        [Test]
        public void Interception_ActiveReservationUsesCapacityBeforeCandidates()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle target = Spawn(units, 1);
            BattleUnitHandle activeAttacker = Spawn(units, 2);
            BattleUnitHandle candidateAttacker = Spawn(units, 2);

            Assert.IsTrue(interception.TryReserve(activeAttacker, target));
            Assert.IsFalse(interception.AddCandidate(candidateAttacker, target, 0f));
            interception.ResolveCandidates();

            Assert.AreEqual(target, interception.GetReservedTarget(activeAttacker));
            Assert.AreEqual(BattleUnitHandle.Invalid, interception.GetReservedTarget(candidateAttacker));
            Assert.AreEqual(1, interception.GetInterceptCount(target));
        }

        [Test]
        public void Interception_ReservedAttackerCanSwitchToPendingTargetWithCapacity()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle oldTarget = Spawn(units, 1);
            BattleUnitHandle newTarget = Spawn(units, 1);
            BattleUnitHandle attacker = Spawn(units, 2);

            Assert.IsTrue(interception.TryReserve(attacker, oldTarget));
            Assert.IsTrue(interception.AddCandidate(attacker, newTarget, 0f));
            interception.ResolveCandidates();

            Assert.AreEqual(newTarget, interception.GetReservedTarget(attacker));
            Assert.AreEqual(0, interception.GetInterceptCount(oldTarget));
            Assert.AreEqual(1, interception.GetInterceptCount(newTarget));
        }

        [Test]
        public void Interception_FailedPendingSwitchKeepsCommittedReservation()
        {
            BattleUnitManager units = new(5);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle oldTarget = Spawn(units, 1);
            BattleUnitHandle newTarget = Spawn(units, 1);
            BattleUnitHandle committedAttacker = Spawn(units, 2);
            BattleUnitHandle nearerAttacker = Spawn(units, 2);

            Assert.IsTrue(interception.TryReserve(committedAttacker, oldTarget));
            Assert.IsTrue(interception.AddCandidate(committedAttacker, newTarget, 10f));
            Assert.IsTrue(interception.AddCandidate(nearerAttacker, newTarget, 1f));
            interception.ResolveCandidates();

            Assert.AreEqual(oldTarget, interception.GetReservedTarget(committedAttacker));
            Assert.AreEqual(newTarget, interception.GetReservedTarget(nearerAttacker));
            Assert.AreEqual(1, interception.GetInterceptCount(oldTarget));
            Assert.AreEqual(1, interception.GetInterceptCount(newTarget));
        }

        [Test]
        public void Interception_FullBlockRejectsNewCandidateButKeepsPreviousOwner()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle target = Spawn(units, 1);
            BattleUnitHandle owner = Spawn(units, 2);
            BattleUnitHandle blocked = Spawn(units, 2);

            Assert.IsTrue(interception.AddCandidate(owner, target, 0f));
            interception.ResolveCandidates();
            interception.BeginTick(33);

            Assert.IsTrue(interception.CanSubmitCandidate(owner, target));
            Assert.IsFalse(interception.CanSubmitCandidate(blocked, target));
            Assert.IsFalse(interception.AddCandidate(blocked, target, 0f));
        }

        [Test]
        public void Interception_FullBlockExpires()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle target = Spawn(units, 1);
            BattleUnitHandle owner = Spawn(units, 2);
            BattleUnitHandle newcomer = Spawn(units, 2);

            Assert.IsTrue(interception.AddCandidate(owner, target, 0f));
            interception.ResolveCandidates();
            interception.BeginTick(BattleInterceptionSystem.FullTargetBlockDurationMs);

            Assert.IsTrue(interception.CanSubmitCandidate(newcomer, target));
        }

        [Test]
        public void Interception_FullBlockRefreshesWhenTargetFillsAgain()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new CapacityOverrideInterceptionSystem(units, units.Capacity, 1);
            BattleUnitHandle target = Spawn(units, 1);
            BattleUnitHandle owner = Spawn(units, 2);
            BattleUnitHandle blocked = Spawn(units, 2);

            Assert.IsTrue(interception.AddCandidate(owner, target, 0f));
            interception.ResolveCandidates();
            interception.BeginTick(100);
            Assert.IsTrue(interception.AddCandidate(owner, target, 0f));
            interception.ResolveCandidates();
            interception.BeginTick(250);

            Assert.IsFalse(interception.CanSubmitCandidate(blocked, target));
        }

        [Test]
        public void Interception_DeadOrUnselectableTargetsCannotBeReserved()
        {
            BattleUnitManager units = new(4);
            BattleInterceptionSystem interception = new(units, units.Capacity);
            BattleUnitHandle attacker = Spawn(units, 2);
            BattleUnitHandle deadTarget = Spawn(units, 1);
            BattleUnitHandle unselectableTarget = Spawn(units, 1, BattleUnitStates.Alive);

            units.ApplyDamage(deadTarget, 100);

            Assert.IsFalse(interception.TryReserve(attacker, deadTarget));
            Assert.IsFalse(interception.TryReserve(attacker, unselectableTarget));
        }

        private static BattleUnitHandle Spawn(
            BattleUnitManager units,
            int camp,
            int state = BattleUnitStates.Alive | BattleUnitStates.Selectable)
        {
            BattleUnitHandle unit = units.SpawnUnit(new BattleUnitSpawnDesc
            {
                unitCfgId = 1,
                position = Vector2.zero,
                radius = 0.5f,
                camp = camp,
                state = state,
                hp = 100,
                renderHandle = -1
            });

            Assert.IsTrue(unit.IsValid);
            return unit;
        }

        private sealed class CapacityOverrideInterceptionSystem : BattleInterceptionSystem
        {
            private readonly int capacity;

            public CapacityOverrideInterceptionSystem(BattleUnitManager units, int unitCapacity, int capacity)
                : base(units, unitCapacity)
            {
                this.capacity = capacity;
            }

            public override int GetInterceptCapacity(BattleUnitHandle target)
            {
                return capacity;
            }
        }
    }
}
