using Game.Data.Configs;
using Game.Data.Configs.Attr;
using Game.Play.Adapters;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Tester;
using Game.Play.Battle.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Game.Play.Tests.Battle
{
    public sealed class BattleTesterScenarioTests
    {
        [Test]
        public void ScenarioJson_RoundTripsUnitOverrides()
        {
            BattleTesterScenario scenario = CreateScenario();
            scenario.spawnMultiplierPreset = BattleTesterSpawnMultiplierPreset.X10;
            scenario.customSpawnMultiplier = 13;

            string json = BattleTesterScenarioJsonUtility.ToJson(scenario);
            BattleTesterScenario restored = BattleTesterScenarioJsonUtility.FromJson(json);

            Assert.AreEqual(scenario.scenarioName, restored.scenarioName);
            Assert.AreEqual(1, restored.playerUnits.Length);
            Assert.AreEqual(1, restored.enemyUnits.Length);
            Assert.AreEqual(BattleTesterSpawnMultiplierPreset.X10, restored.spawnMultiplierPreset);
            Assert.AreEqual(13, restored.customSpawnMultiplier);
            Assert.AreEqual(999, restored.playerUnits[0].attrs[0].value);
            Assert.AreEqual(2002, restored.playerUnits[0].skillIds[0]);
            Assert.IsTrue(restored.playerUnits[0].enabled);
            Assert.AreEqual(new Vector2(1f, 0f), restored.enemyUnits[0].position);

            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(restored);
        }

        [Test]
        public void ScenarioRunner_AppliesUnitOverrides()
        {
            Tables tables = LoadTables();
            BattleTesterScenario scenario = CreateScenario();

            BattleTesterRunResult result = BattleTesterScenarioRunner.Start(tables, scenario, new NullBattleRenderWorld());
            BattleUnitHandle player = result.units[0];

            Assert.IsTrue(player.IsValid);
            Assert.AreEqual(999, result.battle.UnitManager.GetAttr(player, AttributeType.Atk));
            Assert.AreEqual(777, result.battle.UnitManager.GetHp(player));
            Assert.AreEqual(1, result.battle.UnitManager.GetSkillSlotCount(player));

            result.battle.DisposeBattle();
            Object.DestroyImmediate(scenario);
        }

        [Test]
        public void ScenarioRunner_ExpandsUnitsBySpawnMultiplier()
        {
            Tables tables = LoadTables();
            BattleTesterScenario scenario = CreateScenario();
            scenario.spawnMultiplierPreset = BattleTesterSpawnMultiplierPreset.X5;

            BattleTesterRunResult result = BattleTesterScenarioRunner.Start(tables, scenario, new NullBattleRenderWorld());

            Assert.AreEqual(10, result.units.Length);
            Assert.AreEqual(10, result.sources.Length);
            Assert.AreEqual(1001, result.sources[0].unitCfgId);
            Assert.AreNotEqual(result.sources[0].position, result.sources[1].position);

            result.battle.DisposeBattle();
            Object.DestroyImmediate(scenario);
        }

        [Test]
        public void ScenarioRunner_SkipsDisabledTemplates()
        {
            Tables tables = LoadTables();
            BattleTesterScenario scenario = CreateScenario();
            scenario.spawnMultiplierPreset = BattleTesterSpawnMultiplierPreset.X10;
            scenario.enemyUnits[0].enabled = false;

            BattleTesterRunResult result = BattleTesterScenarioRunner.Start(tables, scenario, new NullBattleRenderWorld());

            Assert.AreEqual(10, result.units.Length);
            for (int i = 0; i < result.sources.Length; i++)
            {
                Assert.AreEqual(1, result.sources[i].camp);
            }

            result.battle.DisposeBattle();
            Object.DestroyImmediate(scenario);
        }

        [Test]
        public void ScenarioRunner_TickCausesDamage()
        {
            Tables tables = LoadTables();
            BattleTesterScenario scenario = CreateScenario();
            scenario.playerUnits[0].skillIds = new[] { 2001 };

            BattleTesterRunResult result = BattleTesterScenarioRunner.Start(tables, scenario, new NullBattleRenderWorld());
            BattleUnitHandle enemy = result.units[1];

            for (int i = 0; i < 20; i++)
            {
                result.battle.OnUpdate(0.033f);
            }

            Assert.Less(result.battle.UnitManager.GetHp(enemy), 300);

            result.battle.DisposeBattle();
            Object.DestroyImmediate(scenario);
        }

        private static BattleTesterScenario CreateScenario()
        {
            BattleTesterScenario scenario = ScriptableObject.CreateInstance<BattleTesterScenario>();
            scenario.scenarioName = "RoundTrip";
            scenario.logicStepMs = 33;
            scenario.gridMin = new Vector2(-10f, -10f);
            scenario.gridWidth = 20;
            scenario.gridHeight = 20;
            scenario.cellSize = 1f;
            scenario.autoStart = true;
            scenario.useNullRenderWorld = true;
            scenario.SetSideUnits(
                new[]
            {
                new BattleTesterUnitEntry
                {
                    enabled = true,
                    label = "Player",
                    unitCfgId = 1001,
                    camp = 1,
                    position = Vector2.zero,
                    skillIds = new[] { 2002 },
                    attrs = new[]
                    {
                        new BattleTesterAttributeOverride { type = AttributeType.Atk, value = 999 },
                        new BattleTesterAttributeOverride { type = AttributeType.HpMax, value = 777 },
                        new BattleTesterAttributeOverride { type = AttributeType.Hp, value = 777 }
                    }
                }
            },
                new[]
            {
                new BattleTesterUnitEntry
                {
                    enabled = true,
                    label = "Enemy",
                    unitCfgId = 1101,
                    camp = 2,
                    position = new Vector2(1f, 0f)
                }
            });
            return scenario;
        }

        private static Tables LoadTables()
        {
            API.InitConfig().GetAwaiter().GetResult();
            return API.Tables;
        }
    }
}
