using Game.Data.Configs;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.System;
using UnityEngine;

namespace Game.Play.Battle.Tester
{
    public static class BattleTesterScenarioRunner
    {
        public static BattleTesterRunResult Start(Tables tables, BattleTesterScenario scenario, IBattleRenderWorld renderWorld)
        {
            if (scenario == null)
            {
                return default;
            }

            BattleTesterUnitEntry[] units = scenario.GetAllUnits();
            int unitCount = units.Length;
            int unitCapacity = Mathf.Max(16, unitCount + 4);
            BattleRuntimeSystem battle = new();
            battle.InitializeBattle(
                tables,
                unitCapacity,
                projectileCapacity: Mathf.Max(16, unitCapacity * 2),
                buffCapacity: Mathf.Max(16, unitCapacity * 4),
                gridMin: scenario.gridMin,
                gridWidth: Mathf.Max(1, scenario.gridWidth),
                gridHeight: Mathf.Max(1, scenario.gridHeight),
                cellSize: Mathf.Max(0.01f, scenario.cellSize),
                renderWorld: renderWorld,
                logicStepMs: Mathf.Max(1, scenario.logicStepMs),
                skillSlotsPerUnit: scenario.MaxSkillCount);

            BattleUnitHandle[] handles = new BattleUnitHandle[unitCount];
            for (int i = 0; i < unitCount; i++)
            {
                BattleTesterUnitEntry unit = units[i];
                handles[i] = unit == null
                    ? BattleUnitHandle.Invalid
                    : battle.SpawnUnit(unit.unitCfgId, unit.position, unit.ToSpawnOverrides());
            }

            return new BattleTesterRunResult(battle, handles);
        }
    }

    public readonly struct BattleTesterRunResult
    {
        public readonly BattleRuntimeSystem battle;
        public readonly BattleUnitHandle[] units;

        public BattleTesterRunResult(BattleRuntimeSystem battle, BattleUnitHandle[] units)
        {
            this.battle = battle;
            this.units = units;
        }
    }
}
