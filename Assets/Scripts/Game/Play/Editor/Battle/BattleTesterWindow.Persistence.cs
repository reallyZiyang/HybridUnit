using System;
using System.Collections.Generic;
using System.IO;
using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private void SaveCurrentAsset()
        {
            if (IsRunning)
            {
                AddEvent("Save skipped while running");
                return;
            }

            EnsureScenario();
            CopyWindowToScenario();
            if (!AssetDatabase.Contains(scenario))
            {
                SaveAsAsset();
                return;
            }

            MarkScenarioDirty();
            AddEvent("Scenario saved");
        }

        private void SaveAsAsset()
        {
            EnsureScenario();
            CopyWindowToScenario();
            EnsureFolder(ScenarioAssetFolder);
            string defaultName = string.IsNullOrEmpty(scenario.scenarioName) ? "BattleTesterScenario" : scenario.scenarioName;
            string path = EditorUtility.SaveFilePanelInProject("Save Battle Scenario", defaultName, "asset", "Choose save path", ScenarioAssetFolder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            BattleTesterScenario asset = Instantiate(scenario);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            scenario = asset;
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = scenario;
            AddEvent("Scenario asset created");
        }

        private void ExportJson()
        {
            if (IsRunning)
            {
                AddEvent("Export skipped while running");
                return;
            }

            EnsureScenario();
            CopyWindowToScenario();
            EnsureFolder(ScenarioJsonFolder);
            string defaultName = string.IsNullOrEmpty(scenario.scenarioName) ? "BattleTesterScenario" : scenario.scenarioName;
            string path = EditorUtility.SaveFilePanel("Export Battle Scenario JSON", ScenarioJsonFolder, defaultName, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, BattleTesterScenarioJsonUtility.ToJson(scenario, true));
            AssetDatabase.Refresh();
            AddEvent("Scenario JSON exported");
        }

        private void ImportJson()
        {
            if (IsRunning)
            {
                AddEvent("Import skipped while running");
                return;
            }

            string path = EditorUtility.OpenFilePanel("Import Battle Scenario JSON", ScenarioJsonFolder, "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            StopBattle();
            scenario = BattleTesterScenarioJsonUtility.FromJson(File.ReadAllText(path));
            scenario.name = Path.GetFileNameWithoutExtension(path);
            CopyScenarioToWindow();
            runtimeStatus = "Imported";
            AddEvent("Scenario JSON imported");
        }

        private void OnScenarioChanged()
        {
            StopBattle();
            if (scenario != null)
            {
                CopyScenarioToWindow();
            }
        }

        private void LoadFirstScenario()
        {
            string[] guids = AssetDatabase.FindAssets("t:BattleTesterScenario");
            if (guids.Length == 0)
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            scenario = AssetDatabase.LoadAssetAtPath<BattleTesterScenario>(path);
        }

        private void EnsureScenario()
        {
            if (scenario == null)
            {
                scenario = CreateInstance<BattleTesterScenario>();
            }
        }

        private void CreateWindowDefaults()
        {
            BattleTesterScenario defaults = CreateInstance<BattleTesterScenario>();
            scenarioName = defaults.scenarioName;
            logicStepMs = defaults.logicStepMs;
            gridMin = defaults.gridMin;
            gridWidth = defaults.gridWidth;
            gridHeight = defaults.gridHeight;
            cellSize = defaults.cellSize;
            autoStart = defaults.autoStart;
            defaultRunSeconds = defaults.defaultRunSeconds;
            useNullRenderWorld = defaults.useNullRenderWorld;
            spawnMultiplierPreset = defaults.spawnMultiplierPreset;
            customSpawnMultiplier = defaults.customSpawnMultiplier;
            playerUnits = CloneUnits(defaults.playerUnits);
            enemyUnits = CloneUnits(defaults.enemyUnits);
            DestroyImmediate(defaults);
            NormalizeUnitTemplates(playerUnits, 1);
            NormalizeUnitTemplates(enemyUnits, 2);
        }

        private void CopyScenarioToWindow()
        {
            scenarioName = scenario.scenarioName;
            logicStepMs = scenario.logicStepMs;
            gridMin = scenario.gridMin;
            gridWidth = scenario.gridWidth;
            gridHeight = scenario.gridHeight;
            cellSize = scenario.cellSize;
            autoStart = scenario.autoStart;
            defaultRunSeconds = scenario.defaultRunSeconds;
            useNullRenderWorld = scenario.useNullRenderWorld;
            spawnMultiplierPreset = scenario.spawnMultiplierPreset;
            customSpawnMultiplier = Mathf.Max(1, scenario.customSpawnMultiplier);

            if ((scenario.playerUnits == null || scenario.playerUnits.Length == 0)
                && (scenario.enemyUnits == null || scenario.enemyUnits.Length == 0)
                && scenario.units != null
                && scenario.units.Length > 0)
            {
                SplitLegacyUnits(scenario.units, out playerUnits, out enemyUnits);
            }
            else
            {
                playerUnits = CloneUnits(scenario.playerUnits);
                enemyUnits = CloneUnits(scenario.enemyUnits);
            }

            NormalizeUnitTemplates(playerUnits, 1);
            NormalizeUnitTemplates(enemyUnits, 2);
        }

        private void CopyWindowToScenario()
        {
            scenario.scenarioName = scenarioName;
            scenario.logicStepMs = Mathf.Max(1, logicStepMs);
            scenario.gridMin = gridMin;
            scenario.gridWidth = Mathf.Max(1, gridWidth);
            scenario.gridHeight = Mathf.Max(1, gridHeight);
            scenario.cellSize = Mathf.Max(0.01f, cellSize);
            scenario.autoStart = autoStart;
            scenario.defaultRunSeconds = Mathf.Max(0f, defaultRunSeconds);
            scenario.useNullRenderWorld = useNullRenderWorld;
            scenario.spawnMultiplierPreset = spawnMultiplierPreset;
            scenario.customSpawnMultiplier = Mathf.Max(1, customSpawnMultiplier);
            NormalizeCamp(playerUnits, 1);
            NormalizeCamp(enemyUnits, 2);
            NormalizeUnitTemplates(playerUnits, 1);
            NormalizeUnitTemplates(enemyUnits, 2);
            scenario.SetSideUnits(CloneUnits(playerUnits), CloneUnits(enemyUnits));
        }

        private void MarkScenarioDirty()
        {
            if (scenario == null)
            {
                return;
            }

            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
        }

        private static void SplitLegacyUnits(BattleTesterUnitEntry[] units, out BattleTesterUnitEntry[] players, out BattleTesterUnitEntry[] enemies)
        {
            List<BattleTesterUnitEntry> playerList = new();
            List<BattleTesterUnitEntry> enemyList = new();
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i]?.camp == 2)
                {
                    enemyList.Add(units[i]);
                }
                else
                {
                    playerList.Add(units[i]);
                }
            }

            players = CloneUnits(playerList.ToArray());
            enemies = CloneUnits(enemyList.ToArray());
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
