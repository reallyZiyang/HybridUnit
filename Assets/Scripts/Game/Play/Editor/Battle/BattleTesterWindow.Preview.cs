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

            DrawPreviewUnits(playerUnits, 1, PlayerColor, gridRect, worldWidth, worldHeight, scale);
            DrawPreviewUnits(enemyUnits, 2, EnemyColor, gridRect, worldWidth, worldHeight, scale);
            Handles.color = oldColor;
            Handles.EndGUI();

            HandlePreviewTemplateDrag(gridRect, worldWidth, worldHeight);
            GUI.Label(new Rect(gridRect.xMin, gridRect.yMax + 3f, 220f, 18f), $"min {gridMin}  cell {cellSize:0.##}", EditorStyles.miniLabel);
            GUI.Label(new Rect(gridRect.xMax - 160f, gridRect.yMax + 3f, 160f, 18f), $"units {GetPreviewUnitCount()}", EditorStyles.miniLabel);
        }

        private void DrawPreviewUnits(BattleTesterUnitEntry[] units, int side, Color color, Rect gridRect, float worldWidth, float worldHeight, float scale)
        {
            if (units == null)
            {
                return;
            }

            for (int i = 0; i < units.Length; i++)
            {
                BattleTesterUnitEntry unit = units[i];
                if (unit == null || !unit.enabled)
                {
                    continue;
                }

                int spawnCount = Mathf.Max(1, unit.spawnCount);
                float spacing = BattleTesterScenarioRunner.GetTemplateSpawnSpacing(unit, cellSize);
                float unitRadius = ResolvePreviewUnitRadius(unit);
                float guiRadius = unitRadius * scale;
                float centerRadius = spawnCount >= 10 ? 2f : 2.5f;
                bool selected = selectedTemplateSide == side && selectedTemplateIndex == i;
                for (int j = 0; j < spawnCount; j++)
                {
                    Vector2 world = BattleTesterScenarioRunner.GetExpandedPositionBySpacing(unit.position, j, spawnCount, spacing);
                    Vector2 gui = WorldToPreviewGui(world, gridRect, worldWidth, worldHeight);
                    if (!CircleIntersectsRect(gui, Mathf.Max(guiRadius, centerRadius), gridRect))
                    {
                        continue;
                    }

                    Color fill = color;
                    fill.a = selected ? 0.32f : 0.18f;
                    Handles.color = fill;
                    Handles.DrawSolidDisc(gui, Vector3.forward, Mathf.Max(0f, guiRadius));

                    Handles.color = selected ? Color.yellow : color;
                    Handles.DrawWireDisc(gui, Vector3.forward, Mathf.Max(0f, guiRadius));

                    Handles.color = selected ? Color.yellow : color;
                    Handles.DrawSolidDisc(gui, Vector3.forward, centerRadius);
                }

                if (selected)
                {
                    Vector2 centerGui = WorldToPreviewGui(unit.position, gridRect, worldWidth, worldHeight);
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(centerGui, Vector3.forward, 9f);
                    Handles.DrawLine(centerGui + Vector2.left * 6f, centerGui + Vector2.right * 6f);
                    Handles.DrawLine(centerGui + Vector2.down * 6f, centerGui + Vector2.up * 6f);
                }
            }
        }

        private float ResolvePreviewUnitRadius(BattleTesterUnitEntry unit)
        {
            if (unit == null)
            {
                return GetFallbackPreviewUnitRadius();
            }

            if (unit.overrideRadius)
            {
                return Mathf.Max(0f, unit.radius);
            }

            if (tables?.TbUnit?.DataList != null)
            {
                for (int i = 0; i < tables.TbUnit.DataList.Count; i++)
                {
                    if (tables.TbUnit.DataList[i].Id == unit.unitCfgId)
                    {
                        return Mathf.Max(0f, tables.TbUnit.DataList[i].Radius);
                    }
                }
            }

            return GetFallbackPreviewUnitRadius();
        }

        private float GetFallbackPreviewUnitRadius()
        {
            return Mathf.Max(0.01f, cellSize * 0.25f);
        }

        private static bool CircleIntersectsRect(Vector2 center, float radius, Rect rect)
        {
            return center.x + radius >= rect.xMin
                && center.x - radius <= rect.xMax
                && center.y + radius >= rect.yMin
                && center.y - radius <= rect.yMax;
        }

        private Vector2 WorldToPreviewGui(Vector2 world, Rect gridRect, float worldWidth, float worldHeight)
        {
            float x = gridRect.xMin + (world.x - gridMin.x) / worldWidth * gridRect.width;
            float y = gridRect.yMax - (world.y - gridMin.y) / worldHeight * gridRect.height;
            return new Vector2(x, y);
        }

        private Vector2 PreviewGuiToWorld(Vector2 gui, Rect gridRect, float worldWidth, float worldHeight)
        {
            float x = gridMin.x + (gui.x - gridRect.xMin) / gridRect.width * worldWidth;
            float y = gridMin.y + (gridRect.yMax - gui.y) / gridRect.height * worldHeight;
            return new Vector2(x, y);
        }

        private void HandlePreviewTemplateDrag(Rect gridRect, float worldWidth, float worldHeight)
        {
            if (IsRunning)
            {
                draggingTemplate = false;
                return;
            }

            BattleTesterUnitEntry selected = GetSelectedTemplate();
            if (selected == null)
            {
                draggingTemplate = false;
                return;
            }

            Event evt = Event.current;
            Vector2 centerGui = WorldToPreviewGui(selected.position, gridRect, worldWidth, worldHeight);
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && gridRect.Contains(evt.mousePosition) && Vector2.Distance(evt.mousePosition, centerGui) <= 14f)
                    {
                        draggingTemplate = true;
                        draggingTemplateSide = selectedTemplateSide;
                        draggingTemplateIndex = selectedTemplateIndex;
                        dragTemplateOffset = evt.mousePosition - centerGui;
                        evt.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (draggingTemplate && evt.button == 0)
                    {
                        BattleTesterUnitEntry dragging = GetTemplate(draggingTemplateSide, draggingTemplateIndex);
                        if (dragging != null)
                        {
                            Vector2 gui = evt.mousePosition - dragTemplateOffset;
                            dragging.position = ClampWorldToGrid(PreviewGuiToWorld(gui, gridRect, worldWidth, worldHeight));
                            GUI.changed = true;
                            Repaint();
                        }

                        evt.Use();
                    }

                    break;

                case EventType.MouseUp:
                case EventType.Ignore:
                    if (draggingTemplate)
                    {
                        draggingTemplate = false;
                        evt.Use();
                    }

                    break;
            }
        }

        private Vector2 ClampWorldToGrid(Vector2 world)
        {
            float maxX = gridMin.x + Mathf.Max(0.01f, gridWidth * cellSize);
            float maxY = gridMin.y + Mathf.Max(0.01f, gridHeight * cellSize);
            world.x = Mathf.Clamp(world.x, gridMin.x, maxX);
            world.y = Mathf.Clamp(world.y, gridMin.y, maxY);
            return world;
        }

        private BattleTesterUnitEntry GetSelectedTemplate()
        {
            return GetTemplate(selectedTemplateSide, selectedTemplateIndex);
        }

        private BattleTesterUnitEntry GetTemplate(int side, int index)
        {
            BattleTesterUnitEntry[] units = side == 2 ? enemyUnits : playerUnits;
            if (units == null || index < 0 || index >= units.Length)
            {
                return null;
            }

            return units[index];
        }
    }
}
