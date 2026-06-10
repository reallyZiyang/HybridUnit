using Game.Data.Configs;
using Game.Play.Adapters;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattleRuntimeSystemTests
    {
        [Test]
        public void RuntimeData_LoadsGeneratedBattleTables()
        {
            Tables tables = LoadTables();
            BattleRuntimeSystem battle = CreateBattle(tables);

            BattleUnitHandle player = battle.SpawnUnit(1001, Vector2.zero);

            Assert.IsTrue(player.IsValid);
            Assert.AreEqual(1000, battle.UnitManager.GetHp(player));
            Assert.AreEqual(2, battle.UnitManager.GetSkillSlotCount(player));
        }

        [Test]
        public void AutoSkill_AfterPrecastDamagesNearestEnemy()
        {
            Tables tables = LoadTables();
            BattleRuntimeSystem battle = CreateBattle(tables);
            battle.SpawnUnit(1001, Vector2.zero, 1);
            BattleUnitHandle enemy = battle.SpawnUnit(1101, new Vector2(0.8f, 0f), 2);

            Tick(battle, 11);

            Assert.Less(battle.UnitManager.GetHp(enemy), 300);
        }

        [Test]
        public void ProjectileSkill_HitsAndAppliesBurnTick()
        {
            Tables tables = LoadTables();
            BattleRuntimeSystem battle = CreateBattle(tables);
            BattleUnitHandle player = battle.SpawnUnit(1001, Vector2.zero, 1);
            BattleUnitHandle enemy = battle.SpawnUnit(1101, new Vector2(1f, 0f), 2);

            Assert.IsTrue(battle.CastSkill(player, 2002));
            Tick(battle, 55);

            Assert.Less(battle.UnitManager.GetHp(enemy), 240);
        }

        private static BattleRuntimeSystem CreateBattle(Tables tables)
        {
            BattleRuntimeSystem battle = new();
            battle.InitializeBattle(
                tables,
                unitCapacity: 16,
                projectileCapacity: 16,
                buffCapacity: 16,
                gridMin: new Vector2(-10f, -10f),
                gridWidth: 20,
                gridHeight: 20,
                cellSize: 1f,
                renderWorld: new NullBattleRenderWorld(),
                logicStepMs: 33);
            return battle;
        }

        private static void Tick(BattleRuntimeSystem battle, int count)
        {
            for (int i = 0; i < count; i++)
            {
                battle.OnUpdate(0.033f);
            }
        }

        private static Tables LoadTables()
        {
            API.InitConfig().GetAwaiter().GetResult();
            return API.Tables;
        }
    }
}
