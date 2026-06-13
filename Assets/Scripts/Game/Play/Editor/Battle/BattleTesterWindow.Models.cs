using System.Collections.Generic;
using Game.Data.Configs;
using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private const string ScenarioAssetFolder = "Assets/Tests/BattleScenarios";
        private const string ScenarioJsonFolder = "Assets/Tests/BattleScenarios/Json";
        private const float LeftWidth = 390f;
        private const float RightWidth = 320f;
        private const float ToolbarHeight = 22f;
        private static readonly Color PlayerColor = new(0.25f, 0.55f, 1f, 1f);
        private static readonly Color EnemyColor = new(1f, 0.35f, 0.3f, 1f);

        [SerializeField] private BattleTesterScenario scenario;
        [SerializeField] private string scenarioName = "New Battle Scenario";
        [SerializeField] private int logicStepMs = 33;
        [SerializeField] private Vector2 gridMin = new(-10f, -10f);
        [SerializeField] private int gridWidth = 20;
        [SerializeField] private int gridHeight = 20;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private bool boundaryEnabled = true;
        [SerializeField] private float boundaryRectWidth = 9f;
        [SerializeField] private float boundaryRectHeight = 16f;
        [SerializeField] private Vector2 boundaryRectCenterOffset = Vector2.zero;
        [SerializeField] private Vector2 previewCameraCenter = Vector2.zero;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float defaultRunSeconds = 10f;
        [SerializeField] private bool useNullRenderWorld;
        [SerializeField] private BattleTesterSpawnMultiplierPreset spawnMultiplierPreset = BattleTesterSpawnMultiplierPreset.X1;
        [SerializeField] private int customSpawnMultiplier = 1;
        [SerializeField] private BattleTesterUnitEntry[] playerUnits = System.Array.Empty<BattleTesterUnitEntry>();
        [SerializeField] private BattleTesterUnitEntry[] enemyUnits = System.Array.Empty<BattleTesterUnitEntry>();
        [SerializeField] private int unitTab;
        [SerializeField] private int selectedTemplateSide = 1;
        [SerializeField] private int selectedTemplateIndex;
        [SerializeField] private int selectedUnitIndex;
        [SerializeField] private int manualSkillId = 2001;

        private readonly List<BattleRuntimeUnitSnapshot> unitStatus = new();
        private readonly List<string> eventLog = new();
        private readonly HashSet<string> attrFoldouts = new();
        private readonly Dictionary<string, string> unitConfigSearchTexts = new();
        private int[] unitConfigIds = System.Array.Empty<int>();
        private string[] unitConfigLabels = System.Array.Empty<string>();
        private string unitConfigCacheStatus = "Unit configs not loaded";
        private Tables tables;
        private BattleRuntimeDriver driver;
        private bool createdDriver;
        private bool playModeChanging;
        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private Vector2 statusScroll;
        private bool draggingTemplate;
        private bool panningPreview;
        private int draggingTemplateSide;
        private int draggingTemplateIndex;
        private Vector2 dragTemplateOffset;
        private Vector2 panStartMouse;
        private Vector2 panStartCenter;
        private string configStatus = "Not loaded";
        private string runtimeStatus = "Stopped";
        private float elapsedSeconds;

        private bool CanRun => EditorApplication.isPlaying && !playModeChanging;
        private bool IsRunning => driver != null && driver.IsRunning;
        private bool IsPaused => driver != null && driver.IsPaused;
    }
}
