using System.Collections.Generic;
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

        [Test]
        public void BurnTick_DamagesWithoutHitReaction()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle player = battle.SpawnUnit(1001, Vector2.zero, 1);
            BattleUnitHandle enemy = battle.SpawnUnit(1101, new Vector2(3f, 0f), 2);

            Assert.IsTrue(battle.CastSkill(player, 2002));
            Tick(battle, 55);

            int enemyRenderHandle = battle.UnitManager.GetRenderHandle(enemy);
            Assert.Less(battle.UnitManager.GetHp(enemy), 240);
            Assert.AreEqual(1, renderWorld.GetHitCount(enemyRenderHandle));
            Assert.GreaterOrEqual(renderWorld.DamageTextCount, 2);
        }

        private static BattleRuntimeSystem CreateBattle(Tables tables, IBattleRenderWorld renderWorld = null)
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
                renderWorld: renderWorld ?? new NullBattleRenderWorld(),
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

        private sealed class RecordingRenderWorld : IBattleRenderWorld
        {
            private readonly Dictionary<int, int> hitCounts = new();
            private int nextHandle;

            public int DamageTextCount { get; private set; }
            public int HealTextCount { get; private set; }

            public int SpawnUnit(string renderKey, Vector2 position)
            {
                return ++nextHandle;
            }

            public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg)
            {
                return ++nextHandle;
            }

            public int PlayUnitAction(int renderHandle, string actionName) => 0;
            public void PlayUnitIdle(int renderHandle) { }

            public void PlayUnitHit(int renderHandle)
            {
                hitCounts.TryGetValue(renderHandle, out int count);
                hitCounts[renderHandle] = count + 1;
            }

            public void PlayUnitDead(int renderHandle) { }
            public void ShowDamageText(Vector2 worldPosition, long value) => DamageTextCount++;
            public void ShowHealText(Vector2 worldPosition, long value) => HealTextCount++;
            public void SetPaused(bool paused) { }
            public void SetPosition(int renderHandle, Vector2 position) { }
            public void SetRotation(int renderHandle, float angleDeg) { }
            public void SetVisible(int renderHandle, bool visible) { }
            public void Despawn(int renderHandle) { }
            public void Tick(float deltaTime) { }
            public void Clear() { }

            public int GetHitCount(int renderHandle)
            {
                return hitCounts.TryGetValue(renderHandle, out int count) ? count : 0;
            }
        }
    }
}
