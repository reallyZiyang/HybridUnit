using Game.Data.Configs;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Runtime;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.System;
using UnityEngine;

namespace Game.Play.Battle.Tester
{
    public static class BattleTesterScenarioRunner
    {
        public static BattleTesterRunResult Start(Tables tables, BattleTesterScenario scenario, IBattleRenderWorld renderWorld, BattleSkillEnhancementContext skillEnhancementContext = null)
        {
            if (scenario == null)
            {
                return default;
            }

            BattleTesterUnitEntry[] units = ExpandUnits(scenario);
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
                skillSlotsPerUnit: scenario.MaxSkillCount,
                boundaryConfig: scenario.GetBoundaryConfig(),
                skillEnhancementContext: skillEnhancementContext);

            BattleUnitHandle[] handles = new BattleUnitHandle[unitCount];
            for (int i = 0; i < unitCount; i++)
            {
                BattleTesterUnitEntry unit = units[i];
                handles[i] = unit == null
                    ? BattleUnitHandle.Invalid
                    : battle.SpawnUnit(unit.unitCfgId, unit.position, unit.ToSpawnOverrides());
            }

            return new BattleTesterRunResult(battle, handles, units);
        }

        public static int GetSpawnMultiplier(BattleTesterScenario scenario)
        {
            if (scenario == null)
            {
                return 1;
            }

            return scenario.spawnMultiplierPreset switch
            {
                BattleTesterSpawnMultiplierPreset.X5 => 5,
                BattleTesterSpawnMultiplierPreset.X10 => 10,
                BattleTesterSpawnMultiplierPreset.Custom => Mathf.Max(1, scenario.customSpawnMultiplier),
                _ => 1
            };
        }

        public static int CountExpandedUnits(BattleTesterScenario scenario)
        {
            if (scenario == null)
            {
                return 0;
            }

            BattleTesterUnitEntry[] units = scenario.GetAllUnits();
            int count = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].enabled)
                {
                    count += GetTemplateSpawnCount(units[i], scenario);
                }
            }

            return count;
        }

        public static BattleTesterUnitEntry[] ExpandUnits(BattleTesterScenario scenario)
        {
            if (scenario == null)
            {
                return System.Array.Empty<BattleTesterUnitEntry>();
            }

            BattleTesterUnitEntry[] templates = scenario.GetAllUnits();
            BattleTesterUnitEntry[] expanded = new BattleTesterUnitEntry[CountExpandedUnits(scenario)];
            int cursor = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                BattleTesterUnitEntry template = templates[i];
                if (template == null || !template.enabled)
                {
                    continue;
                }

                int spawnCount = GetTemplateSpawnCount(template, scenario);
                float spawnSpacing = GetTemplateSpawnSpacing(template, scenario.cellSize);
                for (int j = 0; j < spawnCount; j++)
                {
                    BattleTesterUnitEntry unit = CloneUnit(template);
                    unit.position = GetExpandedPositionBySpacing(template.position, j, spawnCount, spawnSpacing);
                    expanded[cursor++] = unit;
                }
            }

            return expanded;
        }

        public static Vector2 GetExpandedPosition(Vector2 origin, int index, int count, float cellSize)
        {
            return GetExpandedPositionBySpacing(origin, index, count, GetDefaultSpawnSpacing(cellSize));
        }

        public static Vector2 GetExpandedPositionBySpacing(Vector2 origin, int index, int count, float spacing)
        {
            if (count <= 1)
            {
                return origin;
            }

            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)columns);
            int x = index % columns;
            int y = index / columns;
            spacing = Mathf.Max(0.01f, spacing);
            Vector2 centerOffset = new((columns - 1) * 0.5f, (rows - 1) * 0.5f);
            return origin + new Vector2((x - centerOffset.x) * spacing, (y - centerOffset.y) * spacing);
        }

        public static float GetDefaultSpawnSpacing(float cellSize)
        {
            return Mathf.Max(0.25f, Mathf.Max(0.01f, cellSize) * 0.35f) * 1.5f;
        }

        public static float GetTemplateSpawnSpacing(BattleTesterUnitEntry unit, float cellSize)
        {
            if (unit != null && unit.spawnSpacing > 0f)
            {
                return Mathf.Max(0.01f, unit.spawnSpacing);
            }

            return GetDefaultSpawnSpacing(cellSize);
        }

        private static BattleTesterUnitEntry CloneUnit(BattleTesterUnitEntry unit)
        {
            return new BattleTesterUnitEntry
            {
                enabled = unit.enabled,
                label = unit.label,
                unitCfgId = unit.unitCfgId,
                spawnCount = Mathf.Max(1, unit.spawnCount),
                spawnSpacing = Mathf.Max(0f, unit.spawnSpacing),
                camp = unit.camp,
                position = unit.position,
                overrideRadius = unit.overrideRadius,
                radius = unit.radius,
                overrideLayer = unit.overrideLayer,
                layer = unit.layer,
                renderKey = unit.renderKey,
                skillIds = unit.skillIds != null ? (int[])unit.skillIds.Clone() : System.Array.Empty<int>(),
                attrs = CloneAttrs(unit.attrs)
            };
        }

        private static int GetTemplateSpawnCount(BattleTesterUnitEntry unit, BattleTesterScenario scenario)
        {
            if (unit == null)
            {
                return 0;
            }

            if (unit.spawnCount > 0)
            {
                return Mathf.Max(1, unit.spawnCount);
            }

            return GetSpawnMultiplier(scenario);
        }

        private static BattleTesterAttributeOverride[] CloneAttrs(BattleTesterAttributeOverride[] attrs)
        {
            if (attrs == null || attrs.Length == 0)
            {
                return System.Array.Empty<BattleTesterAttributeOverride>();
            }

            BattleTesterAttributeOverride[] result = new BattleTesterAttributeOverride[attrs.Length];
            System.Array.Copy(attrs, result, attrs.Length);
            return result;
        }
    }

    public readonly struct BattleTesterRunResult
    {
        public readonly BattleRuntimeSystem battle;
        public readonly BattleUnitHandle[] units;
        public readonly BattleTesterUnitEntry[] sources;

        public BattleTesterRunResult(BattleRuntimeSystem battle, BattleUnitHandle[] units, BattleTesterUnitEntry[] sources)
        {
            this.battle = battle;
            this.units = units;
            this.sources = sources;
        }
    }
}
