using System;
using Game.Data.Configs.Attr;
using Game.Play.Battle.Tester;
using UnityEditor;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private void DrawUnitList(ref BattleTesterUnitEntry[] units, int defaultCamp, Color accent, string sideLabel)
        {
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
            unit.unitCfgId = EditorGUILayout.IntField(unit.unitCfgId, GUILayout.Width(70f));
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
            unit.position = EditorGUILayout.Vector2Field("Position", unit.position);
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
