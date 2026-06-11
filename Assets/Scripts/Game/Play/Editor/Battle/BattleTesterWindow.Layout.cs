using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth), GUILayout.ExpandHeight(true));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            using (new EditorGUI.DisabledScope(IsRunning))
            {
                DrawBattlefieldBox();
                DrawMultiplierBox();
                DrawUnitTemplatesBox();
            }

            if (!CanRun)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run battle.", MessageType.Info);
            }
            else if (IsRunning)
            {
                EditorGUILayout.HelpBox("Unit editing locked while running. Stop battle to edit templates.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawBattlefieldBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Battlefield Grid", EditorStyles.boldLabel);
            scenarioName = EditorGUILayout.TextField("Scenario Name", scenarioName);
            gridMin = EditorGUILayout.Vector2Field("Grid Min", gridMin);
            gridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", gridWidth));
            gridHeight = Mathf.Max(1, EditorGUILayout.IntField("Grid Height", gridHeight));
            cellSize = Mathf.Max(0.01f, EditorGUILayout.FloatField("Cell Size", cellSize));
            logicStepMs = Mathf.Max(1, EditorGUILayout.IntField("Logic Step Ms", logicStepMs));
            autoStart = EditorGUILayout.Toggle("Auto Start", autoStart);
            defaultRunSeconds = Mathf.Max(0f, EditorGUILayout.FloatField("Default Run Seconds", defaultRunSeconds));
            useNullRenderWorld = EditorGUILayout.Toggle("Logic Only", useNullRenderWorld);
            EditorGUILayout.EndVertical();
        }

        private void DrawMultiplierBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Spawn Multipliers", EditorStyles.boldLabel);
            string[] labels = { "x1", "x5", "x10", "Custom" };
            spawnMultiplierPreset = (BattleTesterSpawnMultiplierPreset)GUILayout.Toolbar((int)spawnMultiplierPreset, labels);
            if (spawnMultiplierPreset == BattleTesterSpawnMultiplierPreset.Custom)
            {
                customSpawnMultiplier = Mathf.Max(1, EditorGUILayout.IntField("Custom Multiplier", customSpawnMultiplier));
            }

            int templates = CountEnabledTemplates(playerUnits) + CountEnabledTemplates(enemyUnits);
            int expanded = templates * GetCurrentMultiplier();
            EditorGUILayout.LabelField("Preview Count", $"{templates} templates -> {expanded} units", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawUnitTemplatesBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Unit Templates", EditorStyles.boldLabel);
            unitTab = GUILayout.Toolbar(unitTab, new[] { "Player Units", "Enemy Units" });
            if (unitTab == 0)
            {
                DrawUnitList(ref playerUnits, 1, PlayerColor, "Player");
            }
            else
            {
                DrawUnitList(ref enemyUnits, 2, EnemyColor, "Enemy");
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightWidth), GUILayout.ExpandHeight(true));
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            DrawRuntimeBox();
            DrawRuntimeUnitList();
            DrawEventLog();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", runtimeStatus);
            EditorGUILayout.LabelField("Config", configStatus);
            EditorGUILayout.LabelField("Elapsed", $"{elapsedSeconds:0.00}s");
            EditorGUILayout.LabelField("Fixed Tick", $"{Mathf.Max(1, logicStepMs)} ms");
            EditorGUILayout.LabelField("Spawn Multiplier", $"x{GetCurrentMultiplier()}");
            CountAlive(out int players, out int enemies);
            EditorGUILayout.LabelField("Alive Player", players.ToString());
            EditorGUILayout.LabelField("Alive Enemy", enemies.ToString());
            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(!CanRun || !IsRunning))
            {
                selectedUnitIndex = EditorGUILayout.IntSlider("Selected Unit", selectedUnitIndex, 0, Mathf.Max(0, unitStatus.Count - 1));
                manualSkillId = EditorGUILayout.IntField("Manual Skill Id", manualSkillId);
                if (GUILayout.Button("Cast Manual Skill"))
                {
                    CastManualSkill();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRuntimeUnitList()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Runtime Units ({unitStatus.Count})", EditorStyles.boldLabel);
            statusScroll = EditorGUILayout.BeginScrollView(statusScroll, GUILayout.Height(260f));
            for (int i = 0; i < unitStatus.Count; i++)
            {
                BattleRuntimeUnitSnapshot status = unitStatus[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{status.index}. {status.label}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(status.valid ? "Alive" : "Dead", GUILayout.Width(48f));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"Handle {status.handle}  Camp {status.camp}  Cfg {status.unitCfgId}", EditorStyles.miniLabel);
                Rect bar = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
                float hp01 = status.hpMax > 0 ? Mathf.Clamp01(status.hp / (float)status.hpMax) : 0f;
                EditorGUI.ProgressBar(bar, hp01, $"{status.hp}/{status.hpMax}");
                EditorGUILayout.LabelField($"Pos {status.position}  Atk {status.atk}  State {status.state}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEventLog()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Event Log", EditorStyles.boldLabel);
            if (eventLog.Count == 0)
            {
                EditorGUILayout.LabelField("No events", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = Mathf.Max(0, eventLog.Count - 10); i < eventLog.Count; i++)
                {
                    EditorGUILayout.LabelField(eventLog[i], EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
