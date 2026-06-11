using System;
using Game.Data.Configs.Attr;
using Game.Play.Battle.Runtime;
using UnityEngine;

namespace Game.Play.Battle.Tester
{
    public enum BattleTesterSpawnMultiplierPreset
    {
        X1 = 0,
        X5 = 1,
        X10 = 2,
        Custom = 3
    }

    [CreateAssetMenu(menuName = "Game/Battle/Tester Scenario", fileName = "BattleTesterScenario")]
    public sealed class BattleTesterScenario : ScriptableObject
    {
        public string scenarioName = "New Battle Scenario";
        public int logicStepMs = 33;
        public Vector2 gridMin = new(-10f, -10f);
        public int gridWidth = 20;
        public int gridHeight = 20;
        public float cellSize = 1f;
        public bool autoStart = true;
        public float defaultRunSeconds = 10f;
        public bool useNullRenderWorld;
        public BattleTesterSpawnMultiplierPreset spawnMultiplierPreset = BattleTesterSpawnMultiplierPreset.X1;
        public int customSpawnMultiplier = 1;
        [Header("Player Side")]
        public BattleTesterUnitEntry[] playerUnits =
        {
            new()
            {
                enabled = true,
                label = "Player",
                unitCfgId = 1001,
                spawnCount = 1,
                spawnSpacing = 0.525f,
                camp = 1,
                position = new Vector2(-1f, 0f)
            }
        };

        [Header("Enemy Side")]
        public BattleTesterUnitEntry[] enemyUnits =
        {
            new()
            {
                enabled = true,
                label = "Enemy",
                unitCfgId = 1101,
                spawnCount = 1,
                spawnSpacing = 0.525f,
                camp = 2,
                position = new Vector2(1f, 0f)
            }
        };

        [HideInInspector]
        public BattleTesterUnitEntry[] units = Array.Empty<BattleTesterUnitEntry>();

        public int MaxSkillCount
        {
            get
            {
                int max = 0;
                BattleTesterUnitEntry[] allUnits = GetAllUnits();
                for (int i = 0; i < allUnits.Length; i++)
                {
                    int count = allUnits[i]?.skillIds?.Length ?? 0;
                    if (count > max)
                    {
                        max = count;
                    }
                }

                return max;
            }
        }

        public BattleTesterUnitEntry[] GetAllUnits()
        {
            if ((playerUnits == null || playerUnits.Length == 0)
                && (enemyUnits == null || enemyUnits.Length == 0)
                && units != null
                && units.Length > 0)
            {
                return units;
            }

            int playerCount = playerUnits?.Length ?? 0;
            int enemyCount = enemyUnits?.Length ?? 0;
            BattleTesterUnitEntry[] result = new BattleTesterUnitEntry[playerCount + enemyCount];
            for (int i = 0; i < playerCount; i++)
            {
                result[i] = playerUnits[i];
                if (result[i] != null && result[i].camp == 0)
                {
                    result[i].camp = 1;
                }
            }

            for (int i = 0; i < enemyCount; i++)
            {
                result[playerCount + i] = enemyUnits[i];
                if (result[playerCount + i] != null && result[playerCount + i].camp == 0)
                {
                    result[playerCount + i].camp = 2;
                }
            }

            return result;
        }

        public void SetSideUnits(BattleTesterUnitEntry[] players, BattleTesterUnitEntry[] enemies)
        {
            playerUnits = players ?? Array.Empty<BattleTesterUnitEntry>();
            enemyUnits = enemies ?? Array.Empty<BattleTesterUnitEntry>();
            units = Array.Empty<BattleTesterUnitEntry>();
        }
    }

    [Serializable]
    public sealed class BattleTesterUnitEntry
    {
        public bool enabled = true;
        public string label;
        public int unitCfgId;
        public int spawnCount = 1;
        public float spawnSpacing = 0.525f;
        public int camp;
        public Vector2 position;
        public bool overrideRadius;
        public float radius;
        public bool overrideLayer;
        public int layer;
        public string renderKey;
        public int[] skillIds = Array.Empty<int>();
        public BattleTesterAttributeOverride[] attrs = Array.Empty<BattleTesterAttributeOverride>();

        public BattleUnitSpawnOverrides ToSpawnOverrides()
        {
            return new BattleUnitSpawnOverrides(
                hasCamp: camp != 0,
                camp: camp,
                hasRadius: overrideRadius,
                radius: radius,
                hasLayer: overrideLayer,
                layer: layer,
                renderKey: renderKey,
                skillIds: skillIds,
                attrs: ToRuntimeAttrs(attrs));
        }

        private static BattleAttributeValue[] ToRuntimeAttrs(BattleTesterAttributeOverride[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<BattleAttributeValue>();
            }

            BattleAttributeValue[] result = new BattleAttributeValue[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = new BattleAttributeValue(source[i].type, source[i].value);
            }

            return result;
        }
    }

    [Serializable]
    public struct BattleTesterAttributeOverride
    {
        public AttributeType type;
        public long value;
    }
}
