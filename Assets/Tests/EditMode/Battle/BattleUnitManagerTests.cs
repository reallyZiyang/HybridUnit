using Game.Play.Battle.Collision;
using Game.Play.Battle.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattleUnitManagerTests
    {
        [Test]
        public void Spawn_ManyUnitsCreatesValidHandles()
        {
            BattleUnitManager units = new(1000);
            for (int i = 0; i < 1000; i++)
            {
                BattleUnitHandle handle = units.SpawnUnit(CreateDesc(new Vector2(i, 0f)));
                Assert.IsTrue(units.IsValid(handle));
                Assert.IsTrue(units.IsAlive(handle));
            }

            Assert.AreEqual(1000, units.ActiveCount);
        }

        [Test]
        public void Despawn_InvalidatesOldHandleAfterIndexReuse()
        {
            BattleUnitManager units = new(1);
            BattleUnitHandle first = units.SpawnUnit(CreateDesc(Vector2.zero));
            Assert.IsTrue(units.DespawnUnit(first));

            BattleUnitHandle second = units.SpawnUnit(CreateDesc(Vector2.one));
            Assert.AreEqual(first.index, second.index);
            Assert.AreNotEqual(first.generation, second.generation);
            Assert.IsFalse(units.IsValid(first));
            Assert.IsTrue(units.IsValid(second));
            Assert.IsFalse(units.SetPosition(first, new Vector2(9f, 9f)));
            Assert.AreEqual(Vector2.one, units.GetPosition(second));
        }

        [Test]
        public void SyncCollisionTargets_UpdatesQueryPosition()
        {
            BattleUnitManager units = new(4);
            BattleCollisionManager collisions = CreateCollisionManager();
            BattleUnitHandle unit = units.SpawnUnit(CreateDesc(Vector2.zero));
            Assert.IsTrue(units.RegisterCollisionTarget(unit, collisions));

            Assert.IsTrue(units.SetPosition(unit, new Vector2(3f, 0f)));
            units.SyncCollisionTargets(collisions);
            collisions.RebuildGrid();

            BattleCollisionQueryBuffer buffer = new(4);
            int count = collisions.Query(new BattleCollisionShape
            {
                type = BattleCollisionShapeType.Circle,
                center = new Vector2(3f, 0f),
                radius = 0.5f
            }, default, buffer);

            Assert.AreEqual(1, count);
            Assert.AreEqual(unit.index, collisions.GetUnitHandle(buffer.TargetIndices[0]).index);
        }

        [Test]
        public void ApplyDamageToZero_UnregistersCollisionTarget()
        {
            BattleUnitManager units = new(4);
            BattleCollisionManager collisions = CreateCollisionManager();
            BattleUnitHandle unit = units.SpawnUnit(CreateDesc(Vector2.zero, hp: 10));
            Assert.IsTrue(units.RegisterCollisionTarget(unit, collisions));

            Assert.IsTrue(units.ApplyDamage(unit, 10, collisions));
            Assert.IsFalse(units.IsAlive(unit));

            BattleCollisionQueryBuffer buffer = new(4);
            int count = collisions.Query(new BattleCollisionShape
            {
                type = BattleCollisionShapeType.Circle,
                center = Vector2.zero,
                radius = 1f
            }, default, buffer);

            Assert.AreEqual(0, count);
        }

        private static BattleUnitSpawnDesc CreateDesc(Vector2 position, int hp = 100)
        {
            return new BattleUnitSpawnDesc
            {
                position = position,
                radius = 0.2f,
                camp = 1,
                state = BattleUnitStates.Alive | BattleUnitStates.Selectable,
                layer = 0,
                hp = hp,
                renderHandle = 7
            };
        }

        private static BattleCollisionManager CreateCollisionManager()
        {
            return new BattleCollisionManager(16, new Vector2(-5f, -5f), 10, 10, 1f);
        }
    }
}
