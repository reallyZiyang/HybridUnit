using Game.Play.Battle.Collision;
using Game.Play.Battle.Push;
using Game.Play.Battle.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattlePushSystemTests
    {
        [Test]
        public void Push_SameCampOverlappingPushableUnits_BothMove()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle a = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle b = Spawn(units, collisions, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.Greater(units.GetPosition(a).sqrMagnitude, 0f);
            Assert.Greater(units.GetPosition(b).sqrMagnitude, 0f);
        }

        [Test]
        public void Push_MovingUnitWithoutPushOthersOverlapsFriendlyStaticUnit_OnlyMovesMovingUnit()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle staticUnit = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle mover = Spawn(units, collisions, new Vector2(0.5f, 0f), canPushOthers: false);
            units.CapturePreviousPositions();
            units.SetPosition(mover, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(staticUnit));
            Assert.Greater(units.GetPosition(mover).sqrMagnitude, 0f);
        }

        [Test]
        public void Push_MovingUnitPushesFriendlyStaticUnit()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle staticUnit = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle mover = Spawn(units, collisions, new Vector2(0.5f, 0f));
            units.CapturePreviousPositions();
            units.SetPosition(mover, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.Greater(units.GetPosition(staticUnit).sqrMagnitude, 0f);
            Assert.Greater(units.GetPosition(mover).sqrMagnitude, 0f);
        }

        [Test]
        public void Push_DifferentCampOverlappingUnits_DoNotMove()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle staticEnemy = Spawn(units, collisions, Vector2.zero, camp: 2);
            BattleUnitHandle mover = Spawn(units, collisions, new Vector2(0.5f, 0f), camp: 1);
            units.CapturePreviousPositions();
            units.SetPosition(mover, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(staticEnemy));
            Assert.AreEqual(Vector2.zero, units.GetPosition(mover));
        }

        [Test]
        public void Push_MovingUnitDoesNotPushEnduringFriendlyUnit()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle enduringUnit = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle mover = Spawn(units, collisions, new Vector2(0.5f, 0f));
            units.AddEndure(enduringUnit, 1);
            units.CapturePreviousPositions();
            units.SetPosition(mover, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(enduringUnit));
            Assert.Greater(units.GetPosition(mover).sqrMagnitude, 0f);
        }

        [Test]
        public void Push_TwoEnduringFriendlyUnits_DoNotMove()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle a = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle b = Spawn(units, collisions, Vector2.zero);
            units.AddEndure(a, 1);
            units.AddEndure(b, 1);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(a));
            Assert.AreEqual(Vector2.zero, units.GetPosition(b));
        }

        [Test]
        public void Push_MovingUnitCanPushHitLockedFriendlyUnitWithoutEndure()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle hitLockedUnit = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle mover = Spawn(units, collisions, new Vector2(0.5f, 0f));
            units.ApplyHitLock(hitLockedUnit, 1000);
            units.CapturePreviousPositions();
            units.SetPosition(mover, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.Greater(units.GetPosition(hitLockedUnit).sqrMagnitude, 0f);
            Assert.Greater(units.GetPosition(mover).sqrMagnitude, 0f);
        }

        [Test]
        public void Endure_AddEndure_DoesNotGoBelowZero()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle unit = Spawn(units, collisions, Vector2.zero);

            units.AddEndure(unit, 1);
            units.AddEndure(unit, -5);

            Assert.IsFalse(units.HasEndure(unit));
        }

        [Test]
        public void Push_StaticEnemyAndFriendlyUnits_DoNotMove()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle friendly = Spawn(units, collisions, Vector2.zero, camp: 1);
            BattleUnitHandle enemy = Spawn(units, collisions, Vector2.zero, camp: 2);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(friendly));
            Assert.AreEqual(Vector2.zero, units.GetPosition(enemy));
        }

        [Test]
        public void Push_MovingEnemyAndFriendlyUnits_DoNotMove()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle friendly = Spawn(units, collisions, new Vector2(-0.5f, 0f), camp: 1);
            BattleUnitHandle enemy = Spawn(units, collisions, new Vector2(0.5f, 0f), camp: 2);
            units.CapturePreviousPositions();
            Vector2 friendlyPosition = new(-0.05f, 0f);
            Vector2 enemyPosition = new(0.05f, 0f);
            units.SetPosition(friendly, friendlyPosition);
            units.SetPosition(enemy, enemyPosition);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(friendlyPosition, units.GetPosition(friendly));
            Assert.AreEqual(enemyPosition, units.GetPosition(enemy));
        }

        [Test]
        public void Push_TwoUnpushableUnits_DoesNotMoveEither()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle a = Spawn(units, collisions, Vector2.zero, canBePushed: false);
            BattleUnitHandle b = Spawn(units, collisions, Vector2.zero, canBePushed: false);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(a));
            Assert.AreEqual(Vector2.zero, units.GetPosition(b));
        }

        [Test]
        public void Push_DeadUnit_DoesNotPushAliveUnit()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle dead = Spawn(units, collisions, Vector2.zero);
            BattleUnitHandle alive = Spawn(units, collisions, Vector2.zero);
            units.ApplyDamage(dead, 100, collisions);
            Sync(units, collisions);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.AreEqual(Vector2.zero, units.GetPosition(alive));
        }

        [Test]
        public void Push_Offset_IsClampedPerTick()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle a = Spawn(units, collisions, new Vector2(-0.5f, 0f));
            BattleUnitHandle b = Spawn(units, collisions, new Vector2(0.5f, 0f));
            units.CapturePreviousPositions();
            units.SetPosition(a, Vector2.zero);
            units.SetPosition(b, Vector2.zero);
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Assert.LessOrEqual(units.GetPosition(a).magnitude, BattlePushSystem.MaxPushDistancePerTick + 0.0001f);
            Assert.LessOrEqual(units.GetPosition(b).magnitude, BattlePushSystem.MaxPushDistancePerTick + 0.0001f);
        }

        [Test]
        public void Push_HeadOnOverlap_AddsSidewaysSlide()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle a = Spawn(units, collisions, new Vector2(-0.5f, 0f));
            BattleUnitHandle b = Spawn(units, collisions, new Vector2(0.5f, 0f));
            units.CapturePreviousPositions();
            units.SetPosition(a, new Vector2(-0.05f, 0f));
            units.SetPosition(b, new Vector2(0.05f, 0f));
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Vector2 aOffset = units.GetPosition(a) - new Vector2(-0.05f, 0f);
            Vector2 bOffset = units.GetPosition(b) - new Vector2(0.05f, 0f);
            Assert.Greater(Mathf.Abs(aOffset.y), 0.0001f);
            Assert.Greater(Mathf.Abs(bOffset.y), 0.0001f);
            Assert.Less(aOffset.y * bOffset.y, 0f);
        }

        [Test]
        public void Push_HeadOnOverlapWithUnpushableUnit_MovableUnitSlidesSideways()
        {
            BattleUnitManager units = CreateUnits(out BattleCollisionManager collisions);
            BattleUnitHandle fixedUnit = Spawn(units, collisions, new Vector2(-0.05f, 0f), canBePushed: false);
            BattleUnitHandle movable = Spawn(units, collisions, new Vector2(0.5f, 0f));
            units.CapturePreviousPositions();
            units.SetPosition(movable, new Vector2(0.05f, 0f));
            BattlePushSystem push = CreatePush(units, collisions);

            push.Tick();

            Vector2 movableOffset = units.GetPosition(movable) - new Vector2(0.05f, 0f);
            Assert.AreEqual(new Vector2(-0.05f, 0f), units.GetPosition(fixedUnit));
            Assert.Greater(Mathf.Abs(movableOffset.y), 0.0001f);
        }

        [Test]
        public void PushOffset_TangentScaleZero_UsesOnlyNormal()
        {
            Vector2 offset = BattlePushSystem.ResolvePushOffset(Vector2.right, 1f, 1, 2, 0f);

            Assert.AreEqual(1f, offset.x, 0.0001f);
            Assert.AreEqual(0f, offset.y, 0.0001f);
        }

        [Test]
        public void PushOffset_TangentScaleOne_UsesOnlyTangent()
        {
            Vector2 offset = BattlePushSystem.ResolvePushOffset(Vector2.right, 1f, 1, 2, 1f);

            Assert.AreEqual(0f, offset.x, 0.0001f);
            Assert.AreEqual(1f, Mathf.Abs(offset.y), 0.0001f);
        }

        [Test]
        public void PushOffset_LargerTangentScale_ReducesNormal()
        {
            Vector2 lowTangent = BattlePushSystem.ResolvePushOffset(Vector2.right, 1f, 1, 2, 0.25f);
            Vector2 highTangent = BattlePushSystem.ResolvePushOffset(Vector2.right, 1f, 1, 2, 0.75f);

            Assert.Greater(Mathf.Abs(highTangent.y), Mathf.Abs(lowTangent.y));
            Assert.Less(highTangent.x, lowTangent.x);
            Assert.AreEqual(1f, lowTangent.x + Mathf.Abs(lowTangent.y), 0.0001f);
            Assert.AreEqual(1f, highTangent.x + Mathf.Abs(highTangent.y), 0.0001f);
        }

        private static BattleUnitManager CreateUnits(out BattleCollisionManager collisions)
        {
            BattleUnitManager units = new(8);
            collisions = new BattleCollisionManager(8, new Vector2(-5f, -5f), 10, 10, 1f);
            return units;
        }

        private static BattlePushSystem CreatePush(
            BattleUnitManager units,
            BattleCollisionManager collisions)
        {
            Sync(units, collisions);
            return new BattlePushSystem(units, collisions, null, units.Capacity, units.Capacity);
        }

        private static BattleUnitHandle Spawn(
            BattleUnitManager units,
            BattleCollisionManager collisions,
            Vector2 position,
            bool canBePushed = true,
            int camp = 1,
            bool canPushOthers = true)
        {
            BattleUnitHandle unit = units.SpawnUnit(new BattleUnitSpawnDesc
            {
                unitCfgId = 1,
                position = position,
                radius = 0.1f,
                camp = camp,
                state = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layer = 0,
                hp = 10,
                renderHandle = -1,
                hasCanPushOthers = true,
                canPushOthers = canPushOthers,
                hasCanBePushed = true,
                canBePushed = canBePushed
            });

            Assert.IsTrue(unit.IsValid);
            Assert.IsTrue(units.RegisterCollisionTarget(unit, collisions));
            return unit;
        }

        private static void Sync(BattleUnitManager units, BattleCollisionManager collisions)
        {
            units.SyncCollisionTargets(collisions);
            collisions.RebuildGrid();
        }
    }
}
