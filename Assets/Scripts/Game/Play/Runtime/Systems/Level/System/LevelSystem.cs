using Cysharp.Threading.Tasks;
using Game.Data.Configs.Sys;
using Game.Play.Adapters;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Tester;
using Game.Play.Systems.Common.Navigator;
using Game.Play.Systems.Level.Interface;
using Game.Play.Systems.Level.Model;
using Game.Play.Systems.SkillEnhancement.Interface;
using Game.Play.UI.View.Menu;
using Game.Play.UI.View.Result;
using UniKit.Framework.Base;
using UniKit.UI;
using UnityEngine;

namespace Game.Play.Systems.Level.System
{
    public sealed class LevelSystem : AbstractSystem, ILevelSystem
    {
        private LevelModel model;
        private BattleRuntimeDriver driver;
        private BattleTesterScenario runtimeScenario;
        private ISkillEnhancementSystem skillEnhancementSystem;
        private bool createdDriver;
        private bool starting;
        private bool ending;

        public bool IsRunning => driver != null && driver.IsRunning;
        public bool IsPaused => driver != null && driver.IsPaused;
        public float ElapsedSeconds => driver != null ? driver.ElapsedSeconds : 0f;

        protected override void OnInitialize()
        {
            model = Context.GetModel<LevelModel>();
            skillEnhancementSystem = Context.GetSystem<ISkillEnhancementSystem>();
        }

        public async UniTask StartLevelAsync(string scenarioKey = "TestBattleScenario")
        {
            if (!BeginStart())
            {
                return;
            }

            try
            {
                await EnsureConfigAsync();

                BattleTesterScenario scenario = await API.Assets.LoadAssetAsync<BattleTesterScenario>(scenarioKey);
                if (scenario == null)
                {
                    Debug.LogError($"[LevelSystem] Battle scenario not found: {scenarioKey}");
                    ReturnToMainMenu();
                    return;
                }

                StartScenario(scenario, scenarioKey, false);
            }
            finally
            {
                starting = false;
            }
        }

        public async UniTask StartLevelAsync(BattleTesterScenario scenario)
        {
            if (!BeginStart())
            {
                return;
            }

            try
            {
                await EnsureConfigAsync();
                if (scenario == null)
                {
                    Debug.LogError("[LevelSystem] Battle scenario is null.");
                    ReturnToMainMenu();
                    return;
                }

                StartScenario(scenario, scenario.scenarioName, true);
            }
            finally
            {
                starting = false;
            }
        }

        public void StopLevel()
        {
            if (driver != null)
            {
                driver.StopBattle();

                if (createdDriver && driver.gameObject != null)
                {
                    Object.Destroy(driver.gameObject);
                }
            }

            driver = null;
            createdDriver = false;
            DestroyRuntimeScenario();
            ending = false;
            skillEnhancementSystem?.EndBattle();
        }

        public void PauseLevel()
        {
            driver?.Pause();
        }

        public void ResumeLevel()
        {
            driver?.Resume();
        }

        public void StepBattle()
        {
            driver?.StepTick();
        }

        public bool CastSkill(int unitIndex, int skillId)
        {
            return driver != null && driver.CastSkill(unitIndex, skillId);
        }

        public BattleRuntimeDriverSnapshot GetRuntimeSnapshot()
        {
            return driver != null
                ? driver.GetRuntimeSnapshot()
                : new BattleRuntimeDriverSnapshot(false, false, 0f, null);
        }

        public void OnUpdate(float deltaTime)
        {
            if (!IsRunning || ending)
            {
                return;
            }

            BattleRuntimeDriverSnapshot snapshot = driver.GetRuntimeSnapshot();
            if (!snapshot.isRunning || snapshot.units.Length == 0)
            {
                return;
            }

            CountAlive(snapshot, out int playerAlive, out int enemyAlive);
            if (playerAlive > 0 && enemyAlive > 0)
            {
                return;
            }

            FinishBattle(enemyAlive <= 0 ? BattleOutcome.Victory : BattleOutcome.Defeat);
        }

        protected override void OnDispose()
        {
            StopLevel();
        }

        private bool BeginStart()
        {
            if (starting || IsRunning)
            {
                return false;
            }

            starting = true;
            ending = false;
            model.FlowState.Value = LevelFlowState.LoadingBattle;
            model.BattleOutcome.Value = BattleOutcome.None;
            return true;
        }

        private async UniTask EnsureConfigAsync()
        {
            if (API.Tables == null)
            {
                await API.InitConfig();
            }
        }

        private void StartScenario(BattleTesterScenario sourceScenario, string scenarioLabel, bool cloneScenario)
        {
            StopLevel();

            BattleTesterScenario scenario = sourceScenario;
            if (cloneScenario)
            {
                runtimeScenario = Object.Instantiate(sourceScenario);
                runtimeScenario.hideFlags = HideFlags.DontSave;
                scenario = runtimeScenario;
            }

            driver = CreateDriver();
            skillEnhancementSystem.BeginBattle();

            IBattleRenderWorld renderWorld = scenario.useNullRenderWorld
                ? new NullBattleRenderWorld()
                : new DrawMeshBattleRenderWorld();

            if (!driver.StartBattle(API.Tables, scenario, renderWorld, skillEnhancementSystem.GetBattleContext()))
            {
                Debug.LogError($"[LevelSystem] Start battle failed: {scenarioLabel}");
                ReturnToMainMenu();
                return;
            }

            this.NavigateTo(SystemType.Battle, () =>
            {
                model.FlowState.Value = LevelFlowState.BattleRunning;
            });
        }

        private BattleRuntimeDriver CreateDriver()
        {
            GameObject go = new("Level Battle Runtime Driver")
            {
                hideFlags = HideFlags.DontSave
            };

            createdDriver = true;
            return go.AddComponent<BattleRuntimeDriver>();
        }

        private void DestroyRuntimeScenario()
        {
            if (runtimeScenario == null)
            {
                return;
            }

            Object.Destroy(runtimeScenario);
            runtimeScenario = null;
        }

        private void FinishBattle(BattleOutcome outcome)
        {
            ending = true;
            model.BattleOutcome.Value = outcome;
            model.FlowState.Value = LevelFlowState.BattleFinished;
            StopLevel();
            this.Open(SystemType.BattleResult);
        }

        private void ReturnToMainMenu()
        {
            StopLevel();
            model.FlowState.Value = LevelFlowState.MainMenu;
            this.NavigateTo(SystemType.Main);
        }

        private static void CountAlive(BattleRuntimeDriverSnapshot snapshot, out int players, out int enemies)
        {
            players = 0;
            enemies = 0;

            for (int i = 0; i < snapshot.units.Length; i++)
            {
                BattleRuntimeUnitSnapshot unit = snapshot.units[i];
                if (!unit.valid || unit.hp <= 0)
                {
                    continue;
                }

                if (unit.camp == 2)
                {
                    enemies++;
                }
                else
                {
                    players++;
                }
            }
        }
    }
}
