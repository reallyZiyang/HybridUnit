using Cysharp.Threading.Tasks;
using Game.Play.Adapters;
using Game.Play.Battle.Rendering;
using Game.Play.Battle.Tester;
using UnityEngine;

namespace Game.Play.Debugging
{
    public sealed class BattleRuntimeDebugLauncher : MonoBehaviour
    {
        [SerializeField] private BattleTesterScenario scenario;
        [SerializeField] private bool useNullRenderWorld;
        [SerializeField] private bool autoLoadConfig = true;
        [SerializeField] private bool autoStart;
        [SerializeField] private bool stopOnDisable = true;
        [SerializeField] private bool dontDestroyOnLoad;
        [SerializeField] private Rect windowRect = new(12f, 260f, 320f, 170f);
        [SerializeField] private Vector2 buttonSize = new(135f, 44f);
        [SerializeField, Min(8)] private int fontSize = 14;

        private BattleRuntimeDriver driver;
        private bool createdDriver;
        private bool starting;
        private bool configLoaded;
        private string status = "Idle";
        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            if (autoStart)
            {
                StartBattleAsync().Forget();
            }
        }

        private void OnDisable()
        {
            if (stopOnDisable)
            {
                StopBattle();
            }
        }

        private void OnDestroy()
        {
            if (stopOnDisable)
            {
                StopBattle();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Battle Debug", windowStyle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            using (new GUIEnabledScope(!starting && !IsRunning))
            {
                if (GUILayout.Button("Start Battle", buttonStyle, GUILayout.Width(buttonSize.x), GUILayout.Height(buttonSize.y)))
                {
                    StartBattleAsync().Forget();
                }
            }

            using (new GUIEnabledScope(IsRunning || starting))
            {
                if (GUILayout.Button("Stop Battle", buttonStyle, GUILayout.Width(buttonSize.x), GUILayout.Height(buttonSize.y)))
                {
                    StopBattle();
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label(BuildStatusText(), labelStyle);
            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private async UniTaskVoid StartBattleAsync()
        {
            if (starting || IsRunning)
            {
                return;
            }

            starting = true;
            status = "Starting...";
            try
            {
                if (scenario == null)
                {
                    status = "Missing scenario";
                    return;
                }

                if (autoLoadConfig && API.Tables == null)
                {
                    status = "Loading configs...";
                    await API.InitConfig();
                }

                configLoaded = API.Tables != null;
                if (!configLoaded)
                {
                    status = "Config not loaded";
                    return;
                }

                StopBattle();
                BattleRuntimeDriver targetDriver = FindOrCreateDriver();
                if (targetDriver == null)
                {
                    status = "Driver unavailable";
                    return;
                }

                IBattleRenderWorld renderWorld = useNullRenderWorld || scenario.useNullRenderWorld
                    ? new NullBattleRenderWorld()
                    : new DrawMeshBattleRenderWorld();
                if (!targetDriver.StartBattle(API.Tables, scenario, renderWorld))
                {
                    status = "Start failed";
                    return;
                }

                driver = targetDriver;
                status = "Running";
            }
            finally
            {
                starting = false;
            }
        }

        private void StopBattle()
        {
            if (driver != null)
            {
                driver.StopBattle();
                if (createdDriver && driver.gameObject != null)
                {
                    Destroy(driver.gameObject);
                }
            }

            driver = null;
            createdDriver = false;
            if (!starting)
            {
                status = "Stopped";
            }
        }

        private BattleRuntimeDriver FindOrCreateDriver()
        {
            if (driver != null)
            {
                return driver;
            }

            driver = FindObjectOfType<BattleRuntimeDriver>();
            createdDriver = false;
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

        private string BuildStatusText()
        {
            int expandedUnits = BattleTesterScenarioRunner.CountExpandedUnits(scenario);
            if (!IsRunning)
            {
                return $"Status: {status}\nConfig: {(configLoaded || API.Tables != null ? "Loaded" : "Not loaded")}\nUnits: {expandedUnits}";
            }

            BattleRuntimeDriverSnapshot snapshot = driver.GetRuntimeSnapshot();
            CountAlive(snapshot, out int playerAlive, out int enemyAlive);
            return $"Status: {(driver.IsPaused ? "Paused" : "Running")}\nElapsed: {driver.ElapsedSeconds:0.0}s\nUnits: {expandedUnits}  Alive P/E: {playerAlive}/{enemyAlive}";
        }

        private void CountAlive(BattleRuntimeDriverSnapshot snapshot, out int players, out int enemies)
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

        private bool IsRunning => driver != null && driver.IsRunning;

        private void EnsureStyles()
        {
            if (labelStyle != null && labelStyle.fontSize == fontSize)
            {
                return;
            }

            windowStyle = new GUIStyle(GUI.skin.window)
            {
                fontSize = fontSize
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = Color.white }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize
            };
        }

        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool previous;

            public GUIEnabledScope(bool enabled)
            {
                previous = GUI.enabled;
                GUI.enabled = enabled;
            }

            public void Dispose()
            {
                GUI.enabled = previous;
            }
        }
    }
}
