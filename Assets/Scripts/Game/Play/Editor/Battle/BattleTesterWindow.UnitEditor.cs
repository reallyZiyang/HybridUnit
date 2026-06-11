using System;
using Game.Data.Configs.Attr;
using Game.Play.Adapters;
using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;
using ConfigBattle = Game.Data.Configs.Battle;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private void DrawUnitList(ref BattleTesterUnitEntry[] units, int defaultCamp, Color accent, string sideLabel)
        {
            EnsureUnitConfigOptions();
            units ??= Array.Empty<BattleTesterUnitEntry>();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{sideLabel} Templates ({units.Length})", EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(28f)))
            {
                AddUnit(ref units, defaultCamp, sideLabel);
            }

            EditorGUILayout.EndHorizontal();

            int duplicateIndex = -1;
            int deleteIndex = -1;
            for (int i = 0; i < units.Length; i++)
            {
                units[i] ??= CreateDefaultUnit(defaultCamp, $"{sideLabel} {i + 1}");
                DrawUnitRow(units[i], i, defaultCamp, accent, sideLabel, ref duplicateIndex, ref deleteIndex);
            }

            if (duplicateIndex >= 0)
            {
                DuplicateUnit(ref units, duplicateIndex);
            }

            if (deleteIndex >= 0)
            {
                DeleteUnit(ref units, deleteIndex);
            }
        }

        private void DrawUnitRow(
            BattleTesterUnitEntry unit,
            int index,
            int defaultCamp,
            Color accent,
            string sideLabel,
            ref int duplicateIndex,
            ref int deleteIndex)
        {
            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, accent, 0.12f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = oldBackground;

            EditorGUILayout.BeginHorizontal();
            unit.enabled = EditorGUILayout.Toggle(unit.enabled, GUILayout.Width(18f));
            if (GUILayout.Toggle(selectedTemplateSide == defaultCamp && selectedTemplateIndex == index, "S", EditorStyles.miniButton, GUILayout.Width(24f)))
            {
                selectedTemplateSide = defaultCamp;
                selectedTemplateIndex = index;
            }

            unit.label = EditorGUILayout.TextField(unit.label, GUILayout.MinWidth(90f));
            unit.spawnCount = Mathf.Max(1, DrawCompactIntField("Count", Mathf.Max(1, unit.spawnCount), 94f, 42f));
            if (GUILayout.Button("Dup", EditorStyles.miniButton, GUILayout.Width(36f)))
            {
                duplicateIndex = index;
            }

            if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(24f)))
            {
                deleteIndex = index;
            }

            EditorGUILayout.EndHorizontal();

            unit.camp = defaultCamp;
            DrawUnitConfigSelector(unit, $"{sideLabel}:{index}:unit");
            unit.position = EditorGUILayout.Vector2Field("Position", unit.position);
            EditorGUILayout.BeginHorizontal();
            float spacing = unit.spawnSpacing > 0f ? unit.spawnSpacing : GetDefaultTemplateSpacing();
            unit.spawnSpacing = Mathf.Max(0.01f, DrawCompactFloatField("Spacing", spacing, 132f, 58f));
            EditorGUILayout.LabelField("per generated unit", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            unit.overrideRadius = EditorGUILayout.ToggleLeft("Radius", unit.overrideRadius, GUILayout.Width(72f));
            using (new EditorGUI.DisabledScope(!unit.overrideRadius))
            {
                unit.radius = Mathf.Max(0f, EditorGUILayout.FloatField(unit.radius));
            }

            unit.overrideLayer = EditorGUILayout.ToggleLeft("Layer", unit.overrideLayer, GUILayout.Width(62f));
            using (new EditorGUI.DisabledScope(!unit.overrideLayer))
            {
                unit.layer = EditorGUILayout.IntField(unit.layer, GUILayout.Width(56f));
            }

            EditorGUILayout.EndHorizontal();
            unit.renderKey = EditorGUILayout.TextField("Render Key", unit.renderKey);
            DrawSkillArray(unit);
            DrawAttrArray(unit, $"{sideLabel}:{index}");
            EditorGUILayout.EndVertical();
        }

        private static int DrawCompactIntField(string label, int value, float width, float labelWidth)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = labelWidth;
                return EditorGUI.IntField(rect, label, value);
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        private static float DrawCompactFloatField(string label, float value, float width, float labelWidth)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = labelWidth;
                return EditorGUI.FloatField(rect, label, value);
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        private void DrawUnitConfigSelector(BattleTesterUnitEntry unit, string key)
        {
            EnsureUnitConfigOptions();
            NormalizeUnitCfgId(unit);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Unit", GUILayout.Width(52f));
            unitConfigSearchTexts.TryGetValue(key, out string searchText);
            searchText = EditorGUILayout.TextField(searchText ?? string.Empty, GUILayout.Width(110f));
            unitConfigSearchTexts[key] = searchText;

            if (unitConfigIds.Length == 0)
            {
                EditorGUILayout.LabelField(unitConfigCacheStatus, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            int[] filteredIndices = GetFilteredUnitConfigIndices(searchText);
            if (filteredIndices.Length == 0)
            {
                EditorGUILayout.LabelField($"No match. Current: {unit.unitCfgId}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            string[] labels = new string[filteredIndices.Length];
            int selected = 0;
            for (int i = 0; i < filteredIndices.Length; i++)
            {
                int optionIndex = filteredIndices[i];
                labels[i] = unitConfigLabels[optionIndex];
                if (unitConfigIds[optionIndex] == unit.unitCfgId)
                {
                    selected = i;
                }
            }

            int next = EditorGUILayout.Popup(selected, labels);
            next = Mathf.Clamp(next, 0, filteredIndices.Length - 1);
            unit.unitCfgId = unitConfigIds[filteredIndices[next]];
            EditorGUILayout.EndHorizontal();
        }

        private void EnsureUnitConfigOptions()
        {
            if (unitConfigIds.Length > 0)
            {
                return;
            }

            if (tables == null)
            {
                LoadConfig();
            }

            if (tables?.TbUnit?.DataList == null || tables.TbUnit.DataList.Count == 0)
            {
                unitConfigIds = Array.Empty<int>();
                unitConfigLabels = Array.Empty<string>();
                unitConfigCacheStatus = "No unit config";
                return;
            }

            System.Collections.Generic.List<ConfigBattle.UnitCfg> units = tables.TbUnit.DataList;
            unitConfigIds = new int[units.Count];
            unitConfigLabels = new string[units.Count];
            for (int i = 0; i < units.Count; i++)
            {
                ConfigBattle.UnitCfg cfg = units[i];
                unitConfigIds[i] = cfg.Id;
                unitConfigLabels[i] = $"{cfg.Id}  {cfg.Name}";
            }

            unitConfigCacheStatus = $"Loaded {unitConfigIds.Length} unit configs";
        }

        private int[] GetFilteredUnitConfigIndices(string searchText)
        {
            if (unitConfigIds.Length == 0)
            {
                return Array.Empty<int>();
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                int[] all = new int[unitConfigIds.Length];
                for (int i = 0; i < all.Length; i++)
                {
                    all[i] = i;
                }

                return all;
            }

            string filter = searchText.Trim();
            System.Collections.Generic.List<int> result = new();
            for (int i = 0; i < unitConfigLabels.Length; i++)
            {
                if (unitConfigLabels[i].IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(i);
                }
            }

            return result.ToArray();
        }

        private void NormalizeUnitCfgId(BattleTesterUnitEntry unit)
        {
            if (unit == null || unitConfigIds.Length == 0)
            {
                return;
            }

            for (int i = 0; i < unitConfigIds.Length; i++)
            {
                if (unitConfigIds[i] == unit.unitCfgId)
                {
                    return;
                }
            }

            unit.unitCfgId = unitConfigIds[0];
        }

        private int GetFirstUnitCfgId()
        {
            EnsureUnitConfigOptions();
            return unitConfigIds.Length > 0 ? unitConfigIds[0] : 1001;
        }

        private void DrawSkillArray(BattleTesterUnitEntry unit)
        {
            unit.skillIds ??= Array.Empty<int>();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Skills", GUILayout.Width(52f));
            int count = Mathf.Max(0, EditorGUILayout.IntField(unit.skillIds.Length, GUILayout.Width(42f)));
            if (count != unit.skillIds.Length)
            {
                Array.Resize(ref unit.skillIds, count);
            }

            for (int i = 0; i < unit.skillIds.Length; i++)
            {
                unit.skillIds[i] = EditorGUILayout.IntField(unit.skillIds[i], GUILayout.Width(58f));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAttrArray(BattleTesterUnitEntry unit, string key)
        {
            unit.attrs ??= Array.Empty<BattleTesterAttributeOverride>();
            bool expanded = attrFoldouts.Contains(key);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, $"Attrs ({unit.attrs.Length})", true);
            if (nextExpanded != expanded)
            {
                if (nextExpanded)
                {
                    attrFoldouts.Add(key);
                }
                else
                {
                    attrFoldouts.Remove(key);
                }
            }

            if (!nextExpanded)
            {
                return;
            }

            int deleteIndex = -1;
            EditorGUI.indentLevel++;
            for (int i = 0; i < unit.attrs.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                unit.attrs[i].type = (AttributeType)EditorGUILayout.EnumPopup(unit.attrs[i].type);
                unit.attrs[i].value = EditorGUILayout.LongField(unit.attrs[i].value);
                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(24f)))
                {
                    deleteIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("+ Add Attr", EditorStyles.miniButton))
            {
                Array.Resize(ref unit.attrs, unit.attrs.Length + 1);
                unit.attrs[^1] = new BattleTesterAttributeOverride { type = AttributeType.Hp, value = 100 };
            }

            if (deleteIndex >= 0)
            {
                DeleteAt(ref unit.attrs, deleteIndex);
            }

            EditorGUI.indentLevel--;
        }
    }
}
