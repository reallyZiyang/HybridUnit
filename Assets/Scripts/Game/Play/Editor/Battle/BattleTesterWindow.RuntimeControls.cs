using System;
using Game.Data.Configs;
using Game.Play.Adapters;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private void LoadConfig()
        {
            API.InitConfig().GetAwaiter().GetResult();
            tables = API.Tables;
            unitConfigIds = Array.Empty<int>();
            unitConfigLabels = Array.Empty<string>();
            unitConfigCacheStatus = "Unit configs not loaded";
            configStatus = tables == null ? "Load failed" : "Loaded";
            AddEvent(configStatus);
        }

        private void StartBattle()
        {
            if (!CanRun)
            {
                AddEvent("Enter Play Mode to run battle");
                return;
            }

            EnsureScenario();
            CopyWindowToScenario();
            if (tables == null)
            {
                LoadConfig();
            }

            StopBattle();
            BattleRuntimeDriver targetDriver = FindOrCreateDriver();
            if (targetDriver == null)
            {
                runtimeStatus = "No driver";
                AddEvent("Battle driver unavailable");
                return;
            }

            IBattleRenderWorld renderWorld = useNullRenderWorld
                ? new NullBattleRenderWorld()
                : new DrawMeshBattleRenderWorld();
            if (!targetDriver.StartBattle(tables, scenario, renderWorld))
            {
                runtimeStatus = "Start failed";
                AddEvent("Start battle failed");
                return;
            }

            driver = targetDriver;
            selectedUnitIndex = 0;
            SampleStatus();
            runtimeStatus = driver.IsPaused ? "Started, paused" : "Running";
            AddEvent($"Start battle: {unitStatus.Count} units");
        }

        private void TogglePause()
        {
            if (!CanRun || driver == null || !driver.IsRunning)
            {
                return;
            }

            if (driver.IsPaused)
            {
                driver.Resume();
                runtimeStatus = "Running";
            }
            else
            {
                driver.Pause();
                runtimeStatus = "Paused";
            }

            AddEvent(runtimeStatus);
        }

        private void StepTick()
        {
            if (!CanRun || driver == null || !driver.IsRunning)
            {
                return;
            }

            driver.StepTick();
            SampleStatus();
            runtimeStatus = driver.IsPaused ? "Paused" : "Running";
            AddEvent($"Tick +{Mathf.Max(1, logicStepMs)}ms");
            Repaint();
        }

        private void CastManualSkill()
        {
            if (!CanRun || driver == null || !driver.IsRunning)
            {
                return;
            }

            if (driver.CastSkill(selectedUnitIndex, manualSkillId))
            {
                AddEvent($"Cast skill {manualSkillId} by unit {selectedUnitIndex}");
            }

            SampleStatus();
        }

        private void StopBattle()
        {
            bool hadBattle = driver != null && driver.IsRunning;
            if (driver != null)
            {
                driver.StopBattle();
                if (createdDriver && driver.gameObject != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(driver.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(driver.gameObject);
                    }
                }
            }

            ClearRuntimeView();
            if (hadBattle)
            {
                AddEvent("Stop battle");
            }
        }

        private BattleRuntimeDriver FindOrCreateDriver()
        {
            if (!CanRun)
            {
                return null;
            }

            if (driver != null)
            {
                return driver;
            }

            driver = FindObjectOfType<BattleRuntimeDriver>();
            createdDriver = IsTemporaryDriver(driver);
            if (driver != null)
            {
                return driver;
            }

            GameObject go = new("Battle Runtime Driver")
            {
                hideFlags = HideFlags.DontSave
            };
            driver = go.AddComponent<BattleRuntimeDriver>();
            createdDriver = true;
            return driver;
        }

        private static bool IsTemporaryDriver(BattleRuntimeDriver targetDriver)
        {
            return targetDriver != null
                && targetDriver.gameObject != null
                && targetDriver.gameObject.name == "Battle Runtime Driver"
                && (targetDriver.gameObject.hideFlags & HideFlags.DontSave) != 0;
        }

        private void SampleStatus()
        {
            unitStatus.Clear();
            if (driver == null)
            {
                elapsedSeconds = 0f;
                runtimeStatus = CanRun ? "Stopped" : "Edit only";
                return;
            }

            BattleRuntimeDriverSnapshot snapshot = driver.GetRuntimeSnapshot();
            elapsedSeconds = snapshot.elapsedSeconds;
            for (int i = 0; i < snapshot.units.Length; i++)
            {
                unitStatus.Add(snapshot.units[i]);
            }

            runtimeStatus = snapshot.isRunning
                ? snapshot.isPaused ? "Paused" : "Running"
                : CanRun ? "Stopped" : "Edit only";
        }

        private void ClearRuntimeView()
        {
            driver = null;
            createdDriver = false;
            elapsedSeconds = 0f;
            runtimeStatus = CanRun ? "Stopped" : "Edit only";
            unitStatus.Clear();
        }

        private void CountAlive(out int players, out int enemies)
        {
            players = 0;
            enemies = 0;
            for (int i = 0; i < unitStatus.Count; i++)
            {
                if (!unitStatus[i].valid)
                {
                    continue;
                }

                if (unitStatus[i].camp == 2)
                {
                    enemies++;
                }
                else
                {
                    players++;
                }
            }
        }

        private void AddEvent(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            eventLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (eventLog.Count > 64)
            {
                eventLog.RemoveAt(0);
            }
        }
    }
}
