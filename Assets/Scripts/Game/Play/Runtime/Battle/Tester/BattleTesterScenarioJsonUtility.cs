using System;
using Game.Data.Configs.Attr;
using UnityEngine;

namespace Game.Play.Battle.Tester
{
    public static class BattleTesterScenarioJsonUtility
    {
        private const int CurrentSchemaVersion = 4;

        public static string ToJson(BattleTesterScenario scenario, bool prettyPrint = true)
        {
            return JsonUtility.ToJson(ToJsonData(scenario), prettyPrint);
        }

        public static BattleTesterScenario FromJson(string json)
        {
            BattleTesterScenario scenario = ScriptableObject.CreateInstance<BattleTesterScenario>();
            ApplyJson(scenario, json);
            return scenario;
        }

        public static void ApplyJson(BattleTesterScenario scenario, string json)
        {
            BattleTesterScenarioJson data = JsonUtility.FromJson<BattleTesterScenarioJson>(json);
            ApplyJsonData(scenario, data);
        }

        private static BattleTesterScenarioJson ToJsonData(BattleTesterScenario scenario)
        {
            BattleTesterScenarioJson data = new()
            {
                schemaVersion = CurrentSchemaVersion,
                scenarioName = scenario.scenarioName,
                logicStepMs = scenario.logicStepMs,
                gridMinX = scenario.gridMin.x,
                gridMinY = scenario.gridMin.y,
                gridWidth = scenario.gridWidth,
                gridHeight = scenario.gridHeight,
                cellSize = scenario.cellSize,
                autoStart = scenario.autoStart,
                defaultRunSeconds = scenario.defaultRunSeconds,
                useNullRenderWorld = scenario.useNullRenderWorld,
                spawnMultiplierPreset = scenario.spawnMultiplierPreset.ToString(),
                customSpawnMultiplier = scenario.customSpawnMultiplier,
                playerUnits = ToJsonUnits(scenario.playerUnits),
                enemyUnits = ToJsonUnits(scenario.enemyUnits),
                units = ToJsonUnits(scenario.units)
            };
            return data;
        }

        private static BattleTesterUnitJson[] ToJsonUnits(BattleTesterUnitEntry[] units)
        {
            if (units == null || units.Length == 0)
            {
                return Array.Empty<BattleTesterUnitJson>();
            }

            BattleTesterUnitJson[] result = new BattleTesterUnitJson[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                BattleTesterUnitEntry unit = units[i] ?? new BattleTesterUnitEntry();
                result[i] = new BattleTesterUnitJson
                {
                    schemaVersion = CurrentSchemaVersion,
                    enabled = unit.enabled,
                    label = unit.label,
                    unitCfgId = unit.unitCfgId,
                    spawnCount = Mathf.Max(1, unit.spawnCount),
                    spawnSpacing = Mathf.Max(0f, unit.spawnSpacing),
                    camp = unit.camp,
                    positionX = unit.position.x,
                    positionY = unit.position.y,
                    overrideRadius = unit.overrideRadius,
                    radius = unit.radius,
                    overrideLayer = unit.overrideLayer,
                    layer = unit.layer,
                    renderKey = unit.renderKey,
                    skillIds = unit.skillIds ?? Array.Empty<int>(),
                    attrs = ToJsonAttrs(unit.attrs)
                };
            }

            return result;
        }

        private static BattleTesterAttributeJson[] ToJsonAttrs(BattleTesterAttributeOverride[] attrs)
        {
            if (attrs == null || attrs.Length == 0)
            {
                return Array.Empty<BattleTesterAttributeJson>();
            }

            BattleTesterAttributeJson[] result = new BattleTesterAttributeJson[attrs.Length];
            for (int i = 0; i < attrs.Length; i++)
            {
                result[i] = new BattleTesterAttributeJson
                {
                    type = attrs[i].type.ToString(),
                    value = attrs[i].value
                };
            }

            return result;
        }

        private static void ApplyJsonData(BattleTesterScenario scenario, BattleTesterScenarioJson data)
        {
            if (scenario == null || data == null)
            {
                return;
            }

            scenario.scenarioName = data.scenarioName;
            scenario.logicStepMs = data.logicStepMs;
            scenario.gridMin = new Vector2(data.gridMinX, data.gridMinY);
            scenario.gridWidth = data.gridWidth;
            scenario.gridHeight = data.gridHeight;
            scenario.cellSize = data.cellSize;
            scenario.autoStart = data.autoStart;
            scenario.defaultRunSeconds = data.defaultRunSeconds;
            scenario.useNullRenderWorld = data.useNullRenderWorld;
            scenario.spawnMultiplierPreset = Enum.TryParse(data.spawnMultiplierPreset, out BattleTesterSpawnMultiplierPreset preset)
                ? preset
                : BattleTesterSpawnMultiplierPreset.X1;
            scenario.customSpawnMultiplier = Mathf.Max(1, data.customSpawnMultiplier);
            BattleTesterUnitEntry[] players = FromJsonUnits(data.playerUnits);
            BattleTesterUnitEntry[] enemies = FromJsonUnits(data.enemyUnits);
            if ((players.Length == 0 && enemies.Length == 0) && data.units != null && data.units.Length > 0)
            {
                SplitLegacyUnits(FromJsonUnits(data.units), out players, out enemies);
            }

            scenario.SetSideUnits(players, enemies);
        }

        private static BattleTesterUnitEntry[] FromJsonUnits(BattleTesterUnitJson[] units)
        {
            if (units == null || units.Length == 0)
            {
                return Array.Empty<BattleTesterUnitEntry>();
            }

            BattleTesterUnitEntry[] result = new BattleTesterUnitEntry[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                BattleTesterUnitJson unit = units[i] ?? new BattleTesterUnitJson();
                result[i] = new BattleTesterUnitEntry
                {
                    enabled = unit.enabled || unit.schemaVersion <= 0,
                    label = unit.label,
                    unitCfgId = unit.unitCfgId,
                    spawnCount = unit.spawnCount > 0 ? unit.spawnCount : 1,
                    spawnSpacing = Mathf.Max(0f, unit.spawnSpacing),
                    camp = unit.camp,
                    position = new Vector2(unit.positionX, unit.positionY),
                    overrideRadius = unit.overrideRadius,
                    radius = unit.radius,
                    overrideLayer = unit.overrideLayer,
                    layer = unit.layer,
                    renderKey = unit.renderKey,
                    skillIds = unit.skillIds ?? Array.Empty<int>(),
                    attrs = FromJsonAttrs(unit.attrs)
                };
            }

            return result;
        }

        private static void SplitLegacyUnits(BattleTesterUnitEntry[] units, out BattleTesterUnitEntry[] players, out BattleTesterUnitEntry[] enemies)
        {
            int playerCount = 0;
            int enemyCount = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i]?.camp == 2)
                {
                    enemyCount++;
                }
                else
                {
                    playerCount++;
                }
            }

            players = new BattleTesterUnitEntry[playerCount];
            enemies = new BattleTesterUnitEntry[enemyCount];
            int p = 0;
            int e = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i]?.camp == 2)
                {
                    enemies[e++] = units[i];
                }
                else
                {
                    players[p++] = units[i];
                }
            }
        }

        private static BattleTesterAttributeOverride[] FromJsonAttrs(BattleTesterAttributeJson[] attrs)
        {
            if (attrs == null || attrs.Length == 0)
            {
                return Array.Empty<BattleTesterAttributeOverride>();
            }

            BattleTesterAttributeOverride[] result = new BattleTesterAttributeOverride[attrs.Length];
            for (int i = 0; i < attrs.Length; i++)
            {
                BattleTesterAttributeJson attr = attrs[i] ?? new BattleTesterAttributeJson();
                result[i] = new BattleTesterAttributeOverride
                {
                    type = Enum.TryParse(attr.type, out AttributeType type) ? type : AttributeType.Null,
                    value = attr.value
                };
            }

            return result;
        }
    }

    [Serializable]
    public sealed class BattleTesterScenarioJson
    {
        public int schemaVersion;
        public string scenarioName;
        public int logicStepMs;
        public float gridMinX;
        public float gridMinY;
        public int gridWidth;
        public int gridHeight;
        public float cellSize;
        public bool autoStart;
        public float defaultRunSeconds;
        public bool useNullRenderWorld;
        public string spawnMultiplierPreset;
        public int customSpawnMultiplier;
        public BattleTesterUnitJson[] playerUnits;
        public BattleTesterUnitJson[] enemyUnits;
        public BattleTesterUnitJson[] units;
    }

    [Serializable]
    public sealed class BattleTesterUnitJson
    {
        public int schemaVersion;
        public bool enabled;
        public string label;
        public int unitCfgId;
        public int spawnCount;
        public float spawnSpacing;
        public int camp;
        public float positionX;
        public float positionY;
        public bool overrideRadius;
        public float radius;
        public bool overrideLayer;
        public int layer;
        public string renderKey;
        public int[] skillIds;
        public BattleTesterAttributeJson[] attrs;
    }

    [Serializable]
    public sealed class BattleTesterAttributeJson
    {
        public string type;
        public long value;
    }
}
