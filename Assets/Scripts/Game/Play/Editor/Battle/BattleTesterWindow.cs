using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow : EditorWindow
    {
        [MenuItem("Game/Battle/Battle Tester", false, 120)]
        private static void OpenWindow()
        {
            BattleTesterWindow window = GetWindow<BattleTesterWindow>();
            window.titleContent = new GUIContent("Battle Tester");
            window.minSize = new Vector2(1120f, 680f);
            window.position = new Rect(window.position.x, window.position.y, 1220f, 760f);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
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

            RefreshDriverReference();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ClearRuntimeView();
        }

        private void Update()
        {
            RefreshDriverReference();
            if (IsRunning)
            {
                SampleStatus();
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            Rect content = new(0f, ToolbarHeight, position.width, position.height - ToolbarHeight);
            GUILayout.BeginArea(content);
            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawPreviewPanel();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(ToolbarHeight));
            using (new EditorGUI.DisabledScope(IsRunning))
            {
                EditorGUILayout.LabelField("Scenario", GUILayout.Width(54f));
                EditorGUI.BeginChangeCheck();
                scenario = (BattleTesterScenario)EditorGUILayout.ObjectField(scenario, typeof(BattleTesterScenario), false, GUILayout.Width(230f));
                if (EditorGUI.EndChangeCheck())
                {
                    OnScenarioChanged();
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    SaveCurrentAsset();
                }

                if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton, GUILayout.Width(86f)))
                {
                    ImportJson();
                }

                if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                {
                    ExportJson();
                }
            }

            GUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!CanRun || IsRunning))
            {
                if (GUILayout.Button("Start", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                {
                    StartBattle();
                }
            }

            using (new EditorGUI.DisabledScope(!CanRun || !IsRunning))
            {
                if (GUILayout.Button(IsPaused ? "Resume" : "Pause", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                {
                    TogglePause();
                }

                if (GUILayout.Button("Tick", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    StepTick();
                }

                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                {
                    StopBattle();
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(runtimeStatus, EditorStyles.miniLabel, GUILayout.Width(160f));
            EditorGUILayout.EndHorizontal();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                playModeChanging = true;
                ClearRuntimeView();
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playModeChanging = false;
                RefreshDriverReference();
            }

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                playModeChanging = true;
                StopBattle();
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                playModeChanging = false;
                ClearRuntimeView();
            }
        }

        private void RefreshDriverReference()
        {
            if (!CanRun)
            {
                if (driver != null && !EditorApplication.isPlaying)
                {
                    ClearRuntimeView();
                }

                return;
            }

            if (driver == null)
            {
                driver = FindObjectOfType<BattleRuntimeDriver>();
                createdDriver = IsTemporaryDriver(driver);
            }
        }
    }
}
