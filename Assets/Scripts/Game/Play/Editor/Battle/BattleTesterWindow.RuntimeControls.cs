using System;
using Game.Data.Configs.Sys;
using Game.Play.Adapters;
using Game.Play.Battle.Tester;
using Game.Play.Systems.Common.Navigator.Interface;
using Game.Play.Systems.Level.Interface;
using Game.Play.Systems.SkillEnhancement.Command;
using Game.Play.Systems.SkillEnhancement.Runtime;
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

        private async void StartBattle()
        {
            if (!CanRun)
            {
                AddEvent("Enter Play Mode to run battle");
                return;
            }

            ILevelSystem levelSystem = TryGetLevelSystem(true);
            if (levelSystem == null)
            {
                runtimeStatus = "Level system unavailable";
                AddEvent("Level system unavailable");
                return;
            }

            EnsureScenario();
            CopyWindowToScenario();

            levelStartPending = true;
            runtimeStatus = "Starting";
            AddEvent("Start level battle");
            try
            {
                await levelSystem.StartLevelAsync(scenario);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                runtimeStatus = "Start failed";
                AddEvent("Start battle failed");
                return;
            }
            finally
            {
                levelStartPending = false;
            }

            selectedUnitIndex = 0;
            SampleStatus();
            if (levelSystem.IsRunning)
            {
                runtimeStatus = levelSystem.IsPaused ? "Started, paused" : "Running";
                AddEvent($"Start battle: {unitStatus.Count} units");
            }
            else
            {
                runtimeStatus = "Start failed";
            }

            Repaint();
        }

        private void TogglePause()
        {
            ILevelSystem levelSystem = TryGetLevelSystem();
            if (!CanRun || levelSystem == null || !levelSystem.IsRunning)
            {
                return;
            }

            if (levelSystem.IsPaused)
            {
                levelSystem.ResumeLevel();
                runtimeStatus = "Running";
            }
            else
            {
                levelSystem.PauseLevel();
                runtimeStatus = "Paused";
            }

            AddEvent(runtimeStatus);
        }

        private void StepTick()
        {
            ILevelSystem levelSystem = TryGetLevelSystem();
            if (!CanRun || levelSystem == null || !levelSystem.IsRunning)
            {
                return;
            }

            levelSystem.StepBattle();
            SampleStatus();
            runtimeStatus = levelSystem.IsPaused ? "Paused" : "Running";
            AddEvent($"Tick +{Mathf.Max(1, logicStepMs)}ms");
            Repaint();
        }

        private void CastManualSkill()
        {
            ILevelSystem levelSystem = TryGetLevelSystem();
            if (!CanRun || levelSystem == null || !levelSystem.IsRunning)
            {
                return;
            }

            if (levelSystem.CastSkill(selectedUnitIndex, manualSkillId))
            {
                AddEvent($"Cast skill {manualSkillId} by unit {selectedUnitIndex}");
            }

            SampleStatus();
        }

        private void StopBattle()
        {
            ILevelSystem levelSystem = TryGetLevelSystem();
            bool hadBattle = levelSystem != null && levelSystem.IsRunning;
            levelSystem?.StopLevel();
            UnregisterRogueChoiceCallback();
            levelStartPending = false;
            rogueChoiceAwaitingSelection = false;
            ClearRuntimeView();
            if (hadBattle)
            {
                AddEvent("Stop battle");
            }
        }

        private void SampleStatus()
        {
            unitStatus.Clear();
            ILevelSystem levelSystem = TryGetLevelSystem();
            if (levelSystem == null)
            {
                elapsedSeconds = 0f;
                runtimeStatus = CanRun ? "Stopped" : "Edit only";
                return;
            }

            BattleRuntimeDriverSnapshot snapshot = levelSystem.GetRuntimeSnapshot();
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

        private void OpenRogueChoice()
        {
            ILevelSystem levelSystem = TryGetLevelSystem();
            if (!CanRun || levelSystem == null || !levelSystem.IsRunning)
            {
                return;
            }

            GameContext context = GameContext.Instance;
            if (!context.Initialized)
            {
                AddEvent("Game context not initialized");
                return;
            }

            rogueChoiceWasPaused = levelSystem.IsPaused;
            rogueChoiceAwaitingSelection = true;
            UnregisterRogueChoiceCallback();
            BattleTesterRogueChoiceBridge.ChoiceApplied += OnRogueChoiceApplied;

            if (!levelSystem.IsPaused)
            {
                levelSystem.PauseLevel();
            }

            context.SendCommand(new OpenSkillEnhancementChoiceCommand());
            context.GetSystem<INavigatorSystem>().NavigateTo(SystemType.RougueChoosen);
            runtimeStatus = "Choosing enhancement";
            AddEvent("Open rogue choice");
        }

        private ILevelSystem TryGetLevelSystem(bool logError = false)
        {
            GameContext context = GameContext.Instance;
            if (!context.Initialized)
            {
                return null;
            }

            try
            {
                return context.GetSystem<ILevelSystem>();
            }
            catch (Exception e)
            {
                if (logError)
                {
                    Debug.LogError(e);
                }

                return null;
            }
        }

        private void OnRogueChoiceApplied(int enhancementId)
        {
            UnregisterRogueChoiceCallback();
            if (!rogueChoiceAwaitingSelection)
            {
                return;
            }

            rogueChoiceAwaitingSelection = false;
            ILevelSystem levelSystem = TryGetLevelSystem();
            if (levelSystem != null && levelSystem.IsRunning && !rogueChoiceWasPaused)
            {
                levelSystem.ResumeLevel();
            }

            SampleStatus();
            AddEvent($"Pick enhancement {enhancementId}");
            Repaint();
        }

        private void UnregisterRogueChoiceCallback()
        {
            BattleTesterRogueChoiceBridge.ChoiceApplied -= OnRogueChoiceApplied;
        }
    }
}
