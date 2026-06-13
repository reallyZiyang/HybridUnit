using Game.Play.Battle.Tester;
using Game.Play.Battle.Runtime;
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
            Vector2 visibleWorldSize = GetPreviewVisibleWorldSize(inner);
            float worldWidth = visibleWorldSize.x;
            float worldHeight = visibleWorldSize.y;
            float scale = inner.width / worldWidth;
            Rect gridRect = inner;
            Rect worldRect = GetPreviewWorldRect(visibleWorldSize);

            EditorGUI.DrawRect(gridRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            Handles.BeginGUI();
            Color oldColor = Handles.color;
            DrawBoundaryPreview(gridRect, worldWidth, worldHeight);
            DrawPreviewGrid(gridRect, worldRect, worldWidth, worldHeight);
            DrawPreviewOrigin(gridRect, worldWidth, worldHeight);

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
            GUI.Label(new Rect(gridRect.xMin, gridRect.yMax + 3f, 260f, 18f), $"center {previewCameraCenter}  cell {cellSize:0.##}", EditorStyles.miniLabel);
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
            Rect worldRect = GetPreviewWorldRect(new Vector2(worldWidth, worldHeight));
            float x = gridRect.xMin + (world.x - worldRect.xMin) / worldWidth * gridRect.width;
            float y = gridRect.yMax - (world.y - worldRect.yMin) / worldHeight * gridRect.height;
            return new Vector2(x, y);
        }

        private Vector2 PreviewGuiToWorld(Vector2 gui, Rect gridRect, float worldWidth, float worldHeight)
        {
            Rect worldRect = GetPreviewWorldRect(new Vector2(worldWidth, worldHeight));
            float x = worldRect.xMin + (gui.x - gridRect.xMin) / gridRect.width * worldWidth;
            float y = worldRect.yMin + (gridRect.yMax - gui.y) / gridRect.height * worldHeight;
            return new Vector2(x, y);
        }

        private void HandlePreviewTemplateDrag(Rect gridRect, float worldWidth, float worldHeight)
        {
            Event evt = Event.current;
            HandlePreviewPan(evt, gridRect, worldWidth, worldHeight);
            if (panningPreview)
            {
                return;
            }

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
                            dragging.position = ClampPreviewWorld(PreviewGuiToWorld(gui, gridRect, worldWidth, worldHeight));
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

        private Vector2 GetPreviewVisibleWorldSize(Rect viewRect)
        {
            float aspect = Mathf.Max(0.01f, viewRect.width / Mathf.Max(1f, viewRect.height));
            float baseWidth = boundaryEnabled ? Mathf.Max(0.01f, boundaryRectWidth) : Mathf.Max(0.01f, gridWidth * cellSize);
            float baseHeight = boundaryEnabled ? Mathf.Max(0.01f, boundaryRectHeight) : Mathf.Max(0.01f, gridHeight * cellSize);
            float baseAspect = baseWidth / baseHeight;
            if (baseAspect > aspect)
            {
                return new Vector2(baseWidth, baseWidth / aspect);
            }

            return new Vector2(baseHeight * aspect, baseHeight);
        }

        private Rect GetPreviewWorldRect(Vector2 visibleWorldSize)
        {
            return new Rect(
                previewCameraCenter.x - visibleWorldSize.x * 0.5f,
                previewCameraCenter.y - visibleWorldSize.y * 0.5f,
                visibleWorldSize.x,
                visibleWorldSize.y);
        }

        private void DrawPreviewGrid(Rect gridRect, Rect worldRect, float worldWidth, float worldHeight)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.14f);
            float step = Mathf.Max(0.01f, cellSize);
            int firstX = Mathf.FloorToInt(worldRect.xMin / step);
            int lastX = Mathf.CeilToInt(worldRect.xMax / step);
            for (int i = firstX; i <= lastX; i++)
            {
                float xWorld = i * step;
                float x = gridRect.xMin + (xWorld - worldRect.xMin) / worldWidth * gridRect.width;
                Handles.DrawLine(new Vector3(x, gridRect.yMin), new Vector3(x, gridRect.yMax));
            }

            int firstY = Mathf.FloorToInt(worldRect.yMin / step);
            int lastY = Mathf.CeilToInt(worldRect.yMax / step);
            for (int i = firstY; i <= lastY; i++)
            {
                float yWorld = i * step;
                float y = gridRect.yMax - (yWorld - worldRect.yMin) / worldHeight * gridRect.height;
                Handles.DrawLine(new Vector3(gridRect.xMin, y), new Vector3(gridRect.xMax, y));
            }
        }

        private void DrawPreviewOrigin(Rect gridRect, float worldWidth, float worldHeight)
        {
            Vector2 origin = WorldToPreviewGui(Vector2.zero, gridRect, worldWidth, worldHeight);
            if (!gridRect.Contains(origin))
            {
                return;
            }

            Handles.color = new Color(1f, 0.95f, 0.25f, 0.85f);
            Handles.DrawAAPolyLine(2f, new Vector3(gridRect.xMin, origin.y), new Vector3(gridRect.xMax, origin.y));
            Handles.DrawAAPolyLine(2f, new Vector3(origin.x, gridRect.yMin), new Vector3(origin.x, gridRect.yMax));
            Handles.DrawSolidDisc(origin, Vector3.forward, 3f);
        }

        private void HandlePreviewPan(Event evt, Rect gridRect, float worldWidth, float worldHeight)
        {
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if ((evt.button == 1 || evt.button == 2) && gridRect.Contains(evt.mousePosition))
                    {
                        panningPreview = true;
                        panStartMouse = evt.mousePosition;
                        panStartCenter = previewCameraCenter;
                        evt.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (panningPreview && (evt.button == 1 || evt.button == 2))
                    {
                        Vector2 deltaGui = evt.mousePosition - panStartMouse;
                        previewCameraCenter = panStartCenter + new Vector2(
                            -deltaGui.x / gridRect.width * worldWidth,
                            deltaGui.y / gridRect.height * worldHeight);
                        Repaint();
                        evt.Use();
                    }

                    break;
                case EventType.MouseUp:
                case EventType.Ignore:
                    if (panningPreview)
                    {
                        panningPreview = false;
                        evt.Use();
                    }

                    break;
            }
        }

        private void DrawBoundaryPreview(Rect gridRect, float worldWidth, float worldHeight)
        {
            BattlefieldBoundaryConfig config = GetPreviewBoundaryConfig();
            if (!BattlefieldBoundary.IsEnabled(config))
            {
                return;
            }

            Color fill = BattlefieldBoundary.FillColor;
            Rect rect = BattlefieldBoundary.GetRect(config);
            Vector2 minGui = WorldToPreviewGui(new Vector2(rect.xMin, rect.yMin), gridRect, worldWidth, worldHeight);
            Vector2 maxGui = WorldToPreviewGui(new Vector2(rect.xMax, rect.yMax), gridRect, worldWidth, worldHeight);
            Rect boundaryGui = Rect.MinMaxRect(
                Mathf.Min(minGui.x, maxGui.x),
                Mathf.Min(minGui.y, maxGui.y),
                Mathf.Max(minGui.x, maxGui.x),
                Mathf.Max(minGui.y, maxGui.y));
            Rect clipped = Rect.MinMaxRect(
                Mathf.Max(boundaryGui.xMin, gridRect.xMin),
                Mathf.Max(boundaryGui.yMin, gridRect.yMin),
                Mathf.Min(boundaryGui.xMax, gridRect.xMax),
                Mathf.Min(boundaryGui.yMax, gridRect.yMax));
            if (clipped.width > 0f && clipped.height > 0f)
            {
                EditorGUI.DrawRect(clipped, fill);
            }
        }

        private Vector2 ClampPreviewWorld(Vector2 world)
        {
            BattlefieldBoundaryConfig config = GetPreviewBoundaryConfig();
            return BattlefieldBoundary.IsEnabled(config)
                ? BattlefieldBoundary.Clamp(world, config)
                : ClampWorldToGrid(world);
        }

        private BattlefieldBoundaryConfig GetPreviewBoundaryConfig()
        {
            return new BattlefieldBoundaryConfig
            {
                enabled = boundaryEnabled,
                rectWidth = Mathf.Max(0f, boundaryRectWidth),
                rectHeight = Mathf.Max(0f, boundaryRectHeight),
                rectCenterOffset = boundaryRectCenterOffset
            };
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
