using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private void DrawPreviewPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Battlefield Preview", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(100f, 10000f, 100f, 10000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawBattlefieldPreview(rect);
            EditorGUILayout.EndVertical();
        }

        private void DrawBattlefieldPreview(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            Rect inner = new(rect.x + 20f, rect.y + 20f, rect.width - 40f, rect.height - 40f);
            float worldWidth = Mathf.Max(0.01f, gridWidth * cellSize);
            float worldHeight = Mathf.Max(0.01f, gridHeight * cellSize);
            float scale = Mathf.Min(inner.width / worldWidth, inner.height / worldHeight);
            Rect gridRect = new(
                inner.center.x - worldWidth * scale * 0.5f,
                inner.center.y - worldHeight * scale * 0.5f,
                worldWidth * scale,
                worldHeight * scale);

            EditorGUI.DrawRect(gridRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.14f);
            int xLines = Mathf.Min(gridWidth, 160);
            int yLines = Mathf.Min(gridHeight, 160);
            for (int i = 0; i <= xLines; i++)
            {
                float x = Mathf.Lerp(gridRect.xMin, gridRect.xMax, i / (float)Mathf.Max(1, xLines));
                Handles.DrawLine(new Vector3(x, gridRect.yMin), new Vector3(x, gridRect.yMax));
            }

            for (int i = 0; i <= yLines; i++)
            {
                float y = Mathf.Lerp(gridRect.yMin, gridRect.yMax, i / (float)Mathf.Max(1, yLines));
                Handles.DrawLine(new Vector3(gridRect.xMin, y), new Vector3(gridRect.xMax, y));
            }

            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(gridRect.xMin, gridRect.yMin),
                new Vector3(gridRect.xMax, gridRect.yMin),
                new Vector3(gridRect.xMax, gridRect.yMax),
                new Vector3(gridRect.xMin, gridRect.yMax),
                new Vector3(gridRect.xMin, gridRect.yMin));

            DrawPreviewUnits(playerUnits, 1, PlayerColor, gridRect, worldWidth, worldHeight);
            DrawPreviewUnits(enemyUnits, 2, EnemyColor, gridRect, worldWidth, worldHeight);
            Handles.color = oldColor;
            Handles.EndGUI();

            GUI.Label(new Rect(gridRect.xMin, gridRect.yMax + 3f, 220f, 18f), $"min {gridMin}  cell {cellSize:0.##}", EditorStyles.miniLabel);
            GUI.Label(new Rect(gridRect.xMax - 160f, gridRect.yMax + 3f, 160f, 18f), $"units {GetPreviewUnitCount()}", EditorStyles.miniLabel);
        }

        private void DrawPreviewUnits(BattleTesterUnitEntry[] units, int side, Color color, Rect gridRect, float worldWidth, float worldHeight)
        {
            if (units == null)
            {
                return;
            }

            int multiplier = GetCurrentMultiplier();
            float radius = multiplier >= 10 ? 3f : 4.5f;
            for (int i = 0; i < units.Length; i++)
            {
                BattleTesterUnitEntry unit = units[i];
                if (unit == null || !unit.enabled)
                {
                    continue;
                }

                bool selected = selectedTemplateSide == side && selectedTemplateIndex == i;
                for (int j = 0; j < multiplier; j++)
                {
                    Vector2 world = BattleTesterScenarioRunner.GetExpandedPosition(unit.position, j, multiplier, cellSize);
                    Vector2 gui = WorldToPreviewGui(world, gridRect, worldWidth, worldHeight);
                    if (!gridRect.Contains(gui))
                    {
                        continue;
                    }

                    Handles.color = selected ? Color.yellow : color;
                    Handles.DrawSolidDisc(gui, Vector3.forward, selected ? radius + 1.5f : radius);
                    Handles.color = color;
                    Handles.DrawSolidDisc(gui, Vector3.forward, radius);
                }
            }
        }

        private Vector2 WorldToPreviewGui(Vector2 world, Rect gridRect, float worldWidth, float worldHeight)
        {
            float x = gridRect.xMin + (world.x - gridMin.x) / worldWidth * gridRect.width;
            float y = gridRect.yMax - (world.y - gridMin.y) / worldHeight * gridRect.height;
            return new Vector2(x, y);
        }
    }
}
