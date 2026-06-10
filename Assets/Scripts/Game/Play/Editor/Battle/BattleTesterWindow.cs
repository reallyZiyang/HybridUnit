using System;
using System.Collections.Generic;
using System.IO;
using Game.Data.Configs;
using Game.Data.Configs.Attr;
using Game.Play.Adapters;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Tester;
using Game.Play.Battle.Unit;
using Game.Play.Systems.Battle.System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed class BattleTesterWindow : OdinEditorWindow
    {
        private const string ScenarioAssetFolder = "Assets/Tests/BattleScenarios";
        private const string ScenarioJsonFolder = "Assets/Tests/BattleScenarios/Json";

        [MenuItem("Game/Battle/Battle Tester", false, 120)]
        private static void OpenWindow()
        {
            BattleTesterWindow window = GetWindow<BattleTesterWindow>();
            window.titleContent = new GUIContent("Battle Tester");
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(980, 760);
        }

        [BoxGroup("Scenario")]
        [LabelText("Scenario Asset")]
        [OnValueChanged(nameof(OnScenarioChanged))]
        public BattleTesterScenario scenario;

        [BoxGroup("Scenario")]
        [LabelText("Scenario Name")]
        public string scenarioName = "New Battle Scenario";

        [BoxGroup("Settings")]
        [LabelText("Logic Step Ms")]
        public int logicStepMs = 33;

        [BoxGroup("Settings")]
        [LabelText("Grid Min")]
        public Vector2 gridMin = new(-10f, -10f);

        [BoxGroup("Settings")]
        [LabelText("Grid Width")]
        public int gridWidth = 20;

        [BoxGroup("Settings")]
        [LabelText("Grid Height")]
        public int gridHeight = 20;

        [BoxGroup("Settings")]
        [LabelText("Cell Size")]
        public float cellSize = 1f;

        [BoxGroup("Settings")]
        [LabelText("Auto Start")]
        public bool autoStart = true;

        [BoxGroup("Settings")]
        [LabelText("Default Run Seconds")]
        public float defaultRunSeconds = 10f;

        [BoxGroup("Settings")]
        [LabelText("Logic Only")]
        public bool useNullRenderWorld;

        [TabGroup("Units", "Player Units")]
        [TableList]
        public BattleTesterUnitEntry[] playerUnits = Array.Empty<BattleTesterUnitEntry>();

        [TabGroup("Units", "Enemy Units")]
        [TableList]
        public BattleTesterUnitEntry[] enemyUnits = Array.Empty<BattleTesterUnitEntry>();

        [BoxGroup("Runtime")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Config Status")]
        private string configStatus = "Not loaded";

        [BoxGroup("Runtime")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Runtime Status")]
        private string runtimeStatus = "Stopped";

        [BoxGroup("Runtime")]
        [LabelText("Selected Unit Index")]
        public int selectedUnitIndex;

        [BoxGroup("Runtime")]
        [LabelText("Manual Skill Id")]
        public int manualSkillId = 2001;

        [BoxGroup("Status")]
        [TableList(IsReadOnly = true)]
        public List<BattleTesterUnitStatus> unitStatus = new();

        private Tables tables;
        private BattleRuntimeSystem battle;
        private BattleUnitHandle[] spawnedUnits = Array.Empty<BattleUnitHandle>();
        private BattleTesterUnitEntry[] spawnedSources = Array.Empty<BattleTesterUnitEntry>();
        private double lastEditorTime;
        private float elapsedSeconds;
        private bool isPaused;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (scenario == null)
            {
                LoadFirstScenario();
            }

            if (scenario == null)
            {
                CreateWindowDefaults();
            }
            else
            {
                CopyScenarioToWindow();
            }
        }

        private void Update()
        {
            if (battle == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Max(0f, (float)(now - lastEditorTime));
            lastEditorTime = now;

            if (!isPaused)
            {
                battle.OnUpdate(deltaTime);
                elapsedSeconds += deltaTime;
                SampleStatus();
                Repaint();
            }
        }

        [BoxGroup("Scenario")]
        [Button("New Default Setup")]
        private void NewDefaultSetup()
        {
            StopBattle();
            scenario = null;
            CreateWindowDefaults();
        }

        [BoxGroup("Scenario")]
        [Button("Apply Window To Scenario")]
        private void ApplyWindowToScenarioButton()
        {
            EnsureScenario();
            CopyWindowToScenario();
            MarkScenarioDirty();
        }

        [BoxGroup("Scenario")]
        [Button("Save As Asset")]
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
        }

        [BoxGroup("Scenario")]
        [Button("Save Current Asset")]
        private void SaveCurrentAsset()
        {
            EnsureScenario();
            CopyWindowToScenario();
            if (!AssetDatabase.Contains(scenario))
            {
                SaveAsAsset();
                return;
            }

            MarkScenarioDirty();
        }

        [BoxGroup("Scenario")]
        [Button("Export JSON")]
        private void ExportJson()
        {
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
        }

        [BoxGroup("Scenario")]
        [Button("Import JSON")]
        private void ImportJson()
        {
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
        }

        [BoxGroup("Runtime")]
        [Button("Load Config")]
        private void LoadConfig()
        {
            API.InitConfig().GetAwaiter().GetResult();
            tables = API.Tables;
            configStatus = tables == null ? "Load failed" : "Loaded";
        }

        [BoxGroup("Runtime")]
        [Button("Start Battle", ButtonSizes.Large)]
        private void StartBattle()
        {
            EnsureScenario();
            CopyWindowToScenario();
            if (tables == null)
            {
                LoadConfig();
            }

            StopBattle();
            IBattleRenderWorld renderWorld = useNullRenderWorld
                ? new NullBattleRenderWorld()
                : new GameObjectBattleRenderWorld();
            BattleTesterRunResult result = BattleTesterScenarioRunner.Start(tables, scenario, renderWorld);
            battle = result.battle;
            spawnedUnits = result.units ?? Array.Empty<BattleUnitHandle>();
            spawnedSources = scenario.GetAllUnits();
            isPaused = !autoStart;
            battle?.SetPaused(isPaused);
            elapsedSeconds = 0f;
            lastEditorTime = EditorApplication.timeSinceStartup;
            SampleStatus();
            runtimeStatus = isPaused ? "Started, paused" : "Running";
        }

        [BoxGroup("Runtime")]
        [Button("Pause / Resume")]
        private void TogglePause()
        {
            if (battle == null)
            {
                return;
            }

            isPaused = !isPaused;
            battle.SetPaused(isPaused);
            lastEditorTime = EditorApplication.timeSinceStartup;
            runtimeStatus = isPaused ? "Paused" : "Running";
        }

        [BoxGroup("Runtime")]
        [Button("Step Tick")]
        private void StepTick()
        {
            if (battle == null)
            {
                return;
            }

            float step = Mathf.Max(1, logicStepMs) / 1000f;
            TickUnpaused(step);
            elapsedSeconds += step;
            SampleStatus();
            Repaint();
        }

        [BoxGroup("Runtime")]
        [Button("Run 1 Second")]
        private void RunOneSecond()
        {
            RunSeconds(1f);
        }

        [BoxGroup("Runtime")]
        [Button("Run Default Seconds")]
        private void RunDefaultSeconds()
        {
            RunSeconds(Mathf.Max(0f, defaultRunSeconds));
        }

        [BoxGroup("Runtime")]
        [Button("Cast Manual Skill")]
        private void CastManualSkill()
        {
            if (battle == null || selectedUnitIndex < 0 || selectedUnitIndex >= spawnedUnits.Length)
            {
                return;
            }

            battle.CastSkill(spawnedUnits[selectedUnitIndex], manualSkillId);
            SampleStatus();
        }

        [BoxGroup("Runtime")]
        [Button("Stop")]
        private void StopBattle()
        {
            battle?.DisposeBattle();
            battle = null;
            spawnedUnits = Array.Empty<BattleUnitHandle>();
            spawnedSources = Array.Empty<BattleTesterUnitEntry>();
            isPaused = false;
            elapsedSeconds = 0f;
            runtimeStatus = "Stopped";
            unitStatus.Clear();
        }

        [BoxGroup("Runtime")]
        [Button("Clear Scene")]
        private void ClearScene()
        {
            StopBattle();
        }

        private void RunSeconds(float seconds)
        {
            if (battle == null || seconds <= 0f)
            {
                return;
            }

            float step = Mathf.Max(1, logicStepMs) / 1000f;
            int count = Mathf.CeilToInt(seconds / step);
            for (int i = 0; i < count; i++)
            {
                TickUnpaused(step);
            }

            elapsedSeconds += seconds;
            SampleStatus();
            Repaint();
        }

        private void SampleStatus()
        {
            unitStatus.Clear();
            if (battle?.UnitManager == null)
            {
                return;
            }

            for (int i = 0; i < spawnedUnits.Length; i++)
            {
                BattleUnitHandle handle = spawnedUnits[i];
                BattleTesterUnitEntry source = i < spawnedSources.Length ? spawnedSources[i] : null;
                bool valid = battle.UnitManager.IsValid(handle);
                unitStatus.Add(new BattleTesterUnitStatus
                {
                    index = i,
                    label = source?.label,
                    handle = $"{handle.index}:{handle.generation}",
                    valid = valid,
                    unitCfgId = valid ? battle.UnitManager.GetUnitCfgId(handle) : source?.unitCfgId ?? 0,
                    camp = valid ? battle.UnitManager.GetCamp(handle) : source?.camp ?? 0,
                    position = valid ? battle.UnitManager.GetPosition(handle) : default,
                    hp = valid ? battle.UnitManager.GetHp(handle) : 0,
                    hpMax = valid ? battle.UnitManager.GetAttr(handle, AttributeType.HpMax) : 0,
                    atk = valid ? battle.UnitManager.GetAttr(handle, AttributeType.Atk) : 0,
                    state = valid ? battle.UnitManager.GetState(handle) : 0,
                    elapsedSeconds = elapsedSeconds
                });
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

        private void TickUnpaused(float deltaTime)
        {
            bool restorePaused = battle.IsPaused;
            if (restorePaused)
            {
                battle.SetPaused(false);
            }

            battle.OnUpdate(deltaTime);

            if (restorePaused)
            {
                battle.SetPaused(true);
            }
        }

        private void OnScenarioChanged()
        {
            StopBattle();
            if (scenario != null)
            {
                CopyScenarioToWindow();
            }
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
            playerUnits = CloneUnits(defaults.playerUnits);
            enemyUnits = CloneUnits(defaults.enemyUnits);
            DestroyImmediate(defaults);
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
            NormalizeCamp(playerUnits, 1);
            NormalizeCamp(enemyUnits, 2);
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

        private static BattleTesterUnitEntry[] CloneUnits(BattleTesterUnitEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<BattleTesterUnitEntry>();
            }

            BattleTesterUnitEntry[] result = new BattleTesterUnitEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                BattleTesterUnitEntry unit = source[i] ?? new BattleTesterUnitEntry();
                result[i] = new BattleTesterUnitEntry
                {
                    label = unit.label,
                    unitCfgId = unit.unitCfgId,
                    camp = unit.camp,
                    position = unit.position,
                    overrideRadius = unit.overrideRadius,
                    radius = unit.radius,
                    overrideLayer = unit.overrideLayer,
                    layer = unit.layer,
                    renderKey = unit.renderKey,
                    skillIds = unit.skillIds != null ? (int[])unit.skillIds.Clone() : Array.Empty<int>(),
                    attrs = CloneAttrs(unit.attrs)
                };
            }

            return result;
        }

        private static BattleTesterAttributeOverride[] CloneAttrs(BattleTesterAttributeOverride[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<BattleTesterAttributeOverride>();
            }

            BattleTesterAttributeOverride[] result = new BattleTesterAttributeOverride[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }

        private static void NormalizeCamp(BattleTesterUnitEntry[] units, int defaultCamp)
        {
            if (units == null)
            {
                return;
            }

            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].camp == 0)
                {
                    units[i].camp = defaultCamp;
                }
            }
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

    [Serializable]
    public sealed class BattleTesterUnitStatus
    {
        [ReadOnly] public int index;
        [ReadOnly] public string label;
        [ReadOnly] public string handle;
        [ReadOnly] public bool valid;
        [ReadOnly] public int unitCfgId;
        [ReadOnly] public int camp;
        [ReadOnly] public Vector2 position;
        [ReadOnly] public int hp;
        [ReadOnly] public long hpMax;
        [ReadOnly] public long atk;
        [ReadOnly] public int state;
        [ReadOnly] public float elapsedSeconds;
    }
}
