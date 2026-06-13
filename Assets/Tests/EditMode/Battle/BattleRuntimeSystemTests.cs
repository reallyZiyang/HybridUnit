using System.Collections.Generic;
using Game.Data.Configs;
using Game.Data.Configs.Attr;
using Game.Play.Adapters;
using Game.Play.Battle.Interception;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
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

        [Test]
        public void AI_MovingUnitPlaysWalkAndFlipsLeft()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle mover = battle.SpawnUnit(1001, new Vector2(3f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001 }));
            battle.SpawnUnit(1101, Vector2.zero, new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));

            battle.OnUpdate(0.033f);

            int moverRenderHandle = battle.UnitManager.GetRenderHandle(mover);
            Assert.Less(battle.UnitManager.GetPosition(mover).x, 3f);
            Assert.Greater(renderWorld.GetWalkCount(moverRenderHandle), 0);
            Assert.IsTrue(renderWorld.GetFlipX(moverRenderHandle));
        }

        [Test]
        public void AI_StopsByFirstSkillRangeNotOtherSkillRange()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle mover = battle.SpawnUnit(1001, new Vector2(2f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001, 2002 }));
            battle.SpawnUnit(1101, Vector2.zero, new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));

            battle.OnUpdate(0.033f);

            Assert.Less(battle.UnitManager.GetPosition(mover).x, 2f);
        }

        [Test]
        public void HitLock_PreventsMovement()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle mover = battle.SpawnUnit(1001, new Vector2(3f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001 }));
            battle.SpawnUnit(1101, Vector2.zero, new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));

            battle.UnitManager.ApplyHitLock(mover, 300);
            battle.OnUpdate(0.033f);

            int moverRenderHandle = battle.UnitManager.GetRenderHandle(mover);
            Assert.AreEqual(3f, battle.UnitManager.GetPosition(mover).x);
            Assert.AreEqual(0, renderWorld.GetWalkCount(moverRenderHandle));
        }

        [Test]
        public void Endure_PreventsHitReactionButStillTakesDamage()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            battle.SpawnUnit(1001, Vector2.zero, new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001 }));
            BattleUnitHandle enemy = battle.SpawnUnit(
                1101,
                new Vector2(0.8f, 0f),
                new BattleUnitSpawnOverrides(
                    hasCamp: true,
                    camp: 2,
                    skillIds: new int[0],
                    attrs: new[] { new BattleAttributeValue(AttributeType.Endure, 1) }));

            Tick(battle, 11);

            int enemyRenderHandle = battle.UnitManager.GetRenderHandle(enemy);
            Assert.Less(battle.UnitManager.GetHp(enemy), 300);
            Assert.AreEqual(0, renderWorld.GetHitCount(enemyRenderHandle));
            Assert.IsFalse(battle.UnitManager.IsHitLocked(enemy));
        }

        [Test]
        public void SkillCooldown_StartsAfterCastFinishes()
        {
            Tables tables = LoadTables();
            BattleRuntimeSystem battle = CreateBattle(tables);
            BattleUnitHandle caster = battle.SpawnUnit(
                1002,
                Vector2.zero,
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001 }));
            BattleUnitHandle enemy = battle.SpawnUnit(
                2001,
                new Vector2(0.8f, 0f),
                new BattleUnitSpawnOverrides(
                    hasCamp: true,
                    camp: 2,
                    skillIds: new int[0],
                    attrs: new[]
                    {
                        new BattleAttributeValue(AttributeType.HpMax, 1000),
                        new BattleAttributeValue(AttributeType.Hp, 1000)
                    }));

            Assert.IsTrue(battle.CastSkill(caster, 2001));
            Tick(battle, 31);
            int hpAfterFirstHit = battle.UnitManager.GetHp(enemy);

            Tick(battle, 30);

            Assert.AreEqual(hpAfterFirstHit, battle.UnitManager.GetHp(enemy));

            Tick(battle, 32);

            Assert.Less(battle.UnitManager.GetHp(enemy), hpAfterFirstHit);
        }

        [Test]
        public void SkillCast_EndureUsesPreAndBackWindowDuringLongAnimation()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new()
            {
                ActionDurationMs = 3000
            };
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle caster = battle.SpawnUnit(
                1002,
                Vector2.zero,
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001 }));
            BattleUnitHandle enemy = battle.SpawnUnit(
                2001,
                new Vector2(0.8f, 0f),
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));
            int enemyHp = battle.UnitManager.GetHp(enemy);

            Assert.IsTrue(battle.CastSkill(caster, 2001));
            Assert.AreEqual(1, battle.UnitManager.GetAttr(caster, AttributeType.Endure));

            Tick(battle, 30);

            Assert.AreEqual(1, battle.UnitManager.GetAttr(caster, AttributeType.Endure));
            Assert.AreEqual(enemyHp, battle.UnitManager.GetHp(enemy));

            Tick(battle, 1);

            Assert.AreEqual(1, battle.UnitManager.GetAttr(caster, AttributeType.Endure));
            Assert.Less(battle.UnitManager.GetHp(enemy), enemyHp);

            Tick(battle, 6);

            Assert.AreEqual(0, battle.UnitManager.GetAttr(caster, AttributeType.Endure));
            Assert.IsFalse(battle.CastSkill(caster, 2001));
        }

        [Test]
        public void SkillCast_FinishCastReleasesEndureBeforeBackWindowEnds()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new()
            {
                ActionDurationMs = 300
            };
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle caster = battle.SpawnUnit(
                1002,
                Vector2.zero,
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new[] { 2001 }));
            BattleUnitHandle enemy = battle.SpawnUnit(
                2001,
                new Vector2(0.8f, 0f),
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));
            int enemyHp = battle.UnitManager.GetHp(enemy);

            Assert.IsTrue(battle.CastSkill(caster, 2001));
            Tick(battle, 31);

            Assert.Less(battle.UnitManager.GetHp(enemy), enemyHp);
            Assert.AreEqual(0, battle.UnitManager.GetAttr(caster, AttributeType.Endure));
        }

        [Test]
        public void Push_AIMovingUnitPushesSameCampUnit()
        {
            Tables tables = LoadTables();
            BattleRuntimeSystem battle = CreateBattle(tables);
            BattleUnitHandle staticUnit = battle.SpawnUnit(
                1001,
                Vector2.zero,
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new int[0]));
            BattleUnitHandle mover = battle.SpawnUnit(
                1001,
                new Vector2(0.3f, 0f),
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new int[0]));
            battle.SpawnUnit(2001, new Vector2(-0.8f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));

            battle.OnUpdate(0.033f);

            Assert.Greater(battle.UnitManager.GetPosition(staticUnit).sqrMagnitude, 0f);
            Assert.Greater(Vector2.Distance(battle.UnitManager.GetPosition(staticUnit), battle.UnitManager.GetPosition(mover)), 0.3f);
        }

        [Test]
        public void Push_CastingUnit_IsNotMovedWhenMovingUnitAvoidsIt()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new()
            {
                ActionDurationMs = 300
            };
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle caster = battle.SpawnUnit(1001, Vector2.zero, new BattleUnitSpawnOverrides(hasCamp: true, camp: 1));
            BattleUnitHandle mover = battle.SpawnUnit(
                1001,
                new Vector2(0.2f, 0f),
                new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new int[0]));
            battle.SpawnUnit(2001, new Vector2(-0.8f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new int[0]));

            Assert.IsTrue(battle.CastSkill(caster, 2001));
            battle.OnUpdate(0.033f);

            Assert.AreEqual(Vector2.zero, battle.UnitManager.GetPosition(caster));
            Assert.Greater(battle.UnitManager.GetPosition(mover).x, 0.12f);
            Assert.Less(battle.UnitManager.GetPosition(mover).x, 0.2f);
        }

        [Test]
        public void Boundary_ClampsSpawnPosition()
        {
            Tables tables = LoadTables();
            BattlefieldBoundaryConfig boundary = new()
            {
                enabled = true,
                rectWidth = 2f,
                rectHeight = 4f,
                rectCenterOffset = Vector2.zero
            };
            BattleRuntimeSystem battle = CreateBattle(tables, boundaryConfig: boundary);

            BattleUnitHandle unit = battle.SpawnUnit(1001, new Vector2(5f, 0f), 1);

            Assert.IsTrue(BattlefieldBoundary.Contains(battle.UnitManager.GetPosition(unit), boundary));
        }

        [Test]
        public void Boundary_ClampsAfterMovementAndPush()
        {
            Tables tables = LoadTables();
            BattlefieldBoundaryConfig boundary = new()
            {
                enabled = true,
                rectWidth = 2f,
                rectHeight = 4f,
                rectCenterOffset = Vector2.zero
            };
            BattleRuntimeSystem battle = CreateBattle(tables, boundaryConfig: boundary);
            BattleUnitHandle a = battle.SpawnUnit(1001, new Vector2(0.9f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new int[0]));
            BattleUnitHandle b = battle.SpawnUnit(1001, new Vector2(1.1f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 1, skillIds: new int[0]));

            battle.OnUpdate(0.033f);

            Assert.IsTrue(BattlefieldBoundary.Contains(battle.UnitManager.GetPosition(a), boundary));
            Assert.IsTrue(BattlefieldBoundary.Contains(battle.UnitManager.GetPosition(b), boundary));
        }

        [Test]
        public void Interception_MeleeAttackersAreLimitedToDefaultCapacity()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle target = SpawnDurableUnit(battle, 1002, Vector2.zero, 1, new int[0]);

            for (int i = 0; i < BattleInterceptionSystem.DefaultInterceptCapacity + 1; i++)
            {
                battle.SpawnUnit(2001, new Vector2(0.6f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2001 }));
            }

            Tick(battle, 31);

            Assert.AreEqual(BattleInterceptionSystem.DefaultInterceptCapacity, renderWorld.GetHitCount(battle.UnitManager.GetRenderHandle(target)));
        }

        [Test]
        public void Interception_NearMeleeGetsSlotEvenWhenSpawnedAfterFarMelee()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle target = SpawnDurableUnit(battle, 1002, Vector2.zero, 1, new int[0]);

            for (int i = 0; i < BattleInterceptionSystem.DefaultInterceptCapacity; i++)
            {
                battle.SpawnUnit(2001, new Vector2(0.6f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2001 }));
            }

            battle.SpawnUnit(
                2001,
                new Vector2(0.1f, 0f),
                new BattleUnitSpawnOverrides(
                    hasCamp: true,
                    camp: 2,
                    skillIds: new[] { 2001 },
                    attrs: new[] { new BattleAttributeValue(AttributeType.Atk, 1000) }));
            int hpBefore = battle.UnitManager.GetHp(target);

            Tick(battle, 31);

            Assert.LessOrEqual(battle.UnitManager.GetHp(target), hpBefore - 1000);
        }

        [Test]
        public void Interception_UnassignedMeleeStopsWhenCapacityIsFull()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            SpawnDurableUnit(battle, 1002, Vector2.zero, 1, new int[0]);

            for (int i = 0; i < BattleInterceptionSystem.DefaultInterceptCapacity; i++)
            {
                battle.SpawnUnit(2001, new Vector2(1f, i * 0.05f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2001 }));
            }

            BattleUnitHandle overflow = battle.SpawnUnit(2001, new Vector2(2f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2001 }));

            battle.OnUpdate(0.033f);

            Assert.AreEqual(0, renderWorld.GetWalkCount(battle.UnitManager.GetRenderHandle(overflow)));
        }

        [Test]
        public void Interception_UnassignedMeleeSkipsBlockedTargetAfterRetryInterval()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            SpawnDurableUnit(battle, 1002, Vector2.zero, 1, new int[0]);
            SpawnDurableUnit(battle, 1002, new Vector2(8f, 0f), 1, new int[0]);

            for (int i = 0; i < BattleInterceptionSystem.DefaultInterceptCapacity; i++)
            {
                battle.SpawnUnit(2001, new Vector2(0.6f, i * 0.05f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2001 }));
            }

            BattleUnitHandle overflow = battle.SpawnUnit(2001, new Vector2(1.2f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2001 }));
            int overflowRenderHandle = battle.UnitManager.GetRenderHandle(overflow);

            battle.OnUpdate(0.033f);
            Assert.AreEqual(0, renderWorld.GetWalkCount(overflowRenderHandle));

            Tick(battle, 9);
            Assert.Greater(renderWorld.GetWalkCount(overflowRenderHandle), 0);

            int walkCountAfterRetarget = renderWorld.GetWalkCount(overflowRenderHandle);
            Tick(battle, 70);

            Assert.AreEqual(walkCountAfterRetarget, renderWorld.GetWalkCount(overflowRenderHandle));
        }

        [Test]
        public void Interception_RangedProjectileAttackersIgnoreCapacity()
        {
            Tables tables = LoadTables();
            RecordingRenderWorld renderWorld = new();
            BattleRuntimeSystem battle = CreateBattle(tables, renderWorld);
            BattleUnitHandle target = SpawnDurableUnit(battle, 1002, Vector2.zero, 1, new int[0]);

            for (int i = 0; i < BattleInterceptionSystem.DefaultInterceptCapacity + 1; i++)
            {
                battle.SpawnUnit(1001, new Vector2(1f, 0f), new BattleUnitSpawnOverrides(hasCamp: true, camp: 2, skillIds: new[] { 2002 }));
            }

            Tick(battle, 55);

            Assert.AreEqual(BattleInterceptionSystem.DefaultInterceptCapacity + 1, renderWorld.GetHitCount(battle.UnitManager.GetRenderHandle(target)));
        }

        private static BattleRuntimeSystem CreateBattle(
            Tables tables,
            IBattleRenderWorld renderWorld = null,
            BattlefieldBoundaryConfig boundaryConfig = default)
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
                logicStepMs: 33,
                boundaryConfig: boundaryConfig);
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
            private readonly Dictionary<int, int> walkCounts = new();
            private readonly Dictionary<int, bool> flipX = new();
            private int nextHandle;

            public int DamageTextCount { get; private set; }
            public int HealTextCount { get; private set; }
            public int ActionDurationMs { get; set; }

            public int SpawnUnit(string renderKey, Vector2 position)
            {
                return ++nextHandle;
            }

            public int SpawnProjectile(string projectileKey, Vector2 position, float angleDeg)
            {
                return ++nextHandle;
            }

            public int PlayUnitAction(int renderHandle, string actionName) => ActionDurationMs;
            public void PlayUnitIdle(int renderHandle) { }

            public void PlayUnitWalk(int renderHandle)
            {
                walkCounts.TryGetValue(renderHandle, out int count);
                walkCounts[renderHandle] = count + 1;
            }

            public int PlayUnitHit(int renderHandle)
            {
                hitCounts.TryGetValue(renderHandle, out int count);
                hitCounts[renderHandle] = count + 1;
                return 300;
            }

            public void PlayUnitDead(int renderHandle) { }
            public void ShowDamageText(Vector2 worldPosition, long value) => DamageTextCount++;
            public void ShowHealText(Vector2 worldPosition, long value) => HealTextCount++;
            public void SetPaused(bool paused) { }
            public void SetSortingGrid(float gridMinY, float cellSize) { }
            public void SetBattlefieldBoundary(BattlefieldBoundaryConfig config) { }
            public void SetPosition(int renderHandle, Vector2 position) { }
            public void SetRotation(int renderHandle, float angleDeg) { }
            public void SetUnitFlipX(int renderHandle, bool value) => flipX[renderHandle] = value;
            public void SetVisible(int renderHandle, bool visible) { }
            public void Despawn(int renderHandle) { }
            public void Tick(float deltaTime) { }
            public void Clear() { }

            public int GetHitCount(int renderHandle)
            {
                return hitCounts.TryGetValue(renderHandle, out int count) ? count : 0;
            }

            public int GetWalkCount(int renderHandle)
            {
                return walkCounts.TryGetValue(renderHandle, out int count) ? count : 0;
            }

            public bool GetFlipX(int renderHandle)
            {
                return flipX.TryGetValue(renderHandle, out bool value) && value;
            }
        }

        private static BattleUnitHandle SpawnDurableUnit(
            BattleRuntimeSystem battle,
            int unitCfgId,
            Vector2 position,
            int camp,
            int[] skillIds)
        {
            return battle.SpawnUnit(
                unitCfgId,
                position,
                new BattleUnitSpawnOverrides(
                    hasCamp: true,
                    camp: camp,
                    skillIds: skillIds,
                    attrs: new[]
                    {
                        new BattleAttributeValue(AttributeType.HpMax, 10000),
                        new BattleAttributeValue(AttributeType.Hp, 10000)
                    }));
        }
    }
}
