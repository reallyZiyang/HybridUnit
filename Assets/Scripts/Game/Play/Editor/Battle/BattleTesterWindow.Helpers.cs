using System;
using Game.Data.Configs.Attr;
using Game.Play.Battle.Tester;
using UnityEngine;

namespace Game.Play.Editor.Battle
{
    public sealed partial class BattleTesterWindow
    {
        private int GetPreviewUnitCount()
        {
            return CountExpandedTemplates(playerUnits) + CountExpandedTemplates(enemyUnits);
        }

        private float GetDefaultTemplateSpacing()
        {
            return BattleTesterScenarioRunner.GetDefaultSpawnSpacing(cellSize);
        }

        private static int CountEnabledTemplates(BattleTesterUnitEntry[] units)
        {
            if (units == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountExpandedTemplates(BattleTesterUnitEntry[] units)
        {
            if (units == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].enabled)
                {
                    count += Mathf.Max(1, units[i].spawnCount);
                }
            }

            return count;
        }

        private void AddUnit(ref BattleTesterUnitEntry[] units, int defaultCamp, string sideLabel)
        {
            units ??= Array.Empty<BattleTesterUnitEntry>();
            Array.Resize(ref units, units.Length + 1);
            units[^1] = CreateDefaultUnit(defaultCamp, $"{sideLabel} {units.Length}");
            selectedTemplateSide = defaultCamp;
            selectedTemplateIndex = units.Length - 1;
        }

        private BattleTesterUnitEntry CreateDefaultUnit(int camp, string label)
        {
            return new BattleTesterUnitEntry
            {
                enabled = true,
                label = label,
                unitCfgId = GetFirstUnitCfgId(),
                spawnCount = 1,
                spawnSpacing = GetDefaultTemplateSpacing(),
                camp = camp,
                position = camp == 2 ? new Vector2(1f, 0f) : new Vector2(-1f, 0f),
                skillIds = Array.Empty<int>(),
                attrs = Array.Empty<BattleTesterAttributeOverride>()
            };
        }

        private void DuplicateUnit(ref BattleTesterUnitEntry[] units, int index)
        {
            if (units == null || index < 0 || index >= units.Length)
            {
                return;
            }

            Array.Resize(ref units, units.Length + 1);
            for (int i = units.Length - 1; i > index + 1; i--)
            {
                units[i] = units[i - 1];
            }

            BattleTesterUnitEntry copy = CloneUnit(units[index]);
            copy.label = string.IsNullOrEmpty(copy.label) ? "Copy" : $"{copy.label} Copy";
            copy.position += new Vector2(0.5f, 0f);
            units[index + 1] = copy;
            selectedTemplateSide = copy.camp == 2 ? 2 : 1;
            selectedTemplateIndex = index + 1;
        }

        private void DeleteUnit(ref BattleTesterUnitEntry[] units, int index)
        {
            DeleteAt(ref units, index);
            selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, Mathf.Max(0, (units?.Length ?? 0) - 1));
        }

        private static BattleTesterUnitEntry[] CloneUnits(BattleTesterUnitEntry[] units)
        {
            if (units == null || units.Length == 0)
            {
                return Array.Empty<BattleTesterUnitEntry>();
            }

            BattleTesterUnitEntry[] result = new BattleTesterUnitEntry[units.Length];
            for (int i = 0; i < units.Length; i++)
            {
                result[i] = units[i] == null ? null : CloneUnit(units[i]);
            }

            return result;
        }

        private static BattleTesterUnitEntry CloneUnit(BattleTesterUnitEntry unit)
        {
            return new BattleTesterUnitEntry
            {
                enabled = unit.enabled,
                label = unit.label,
                unitCfgId = unit.unitCfgId,
                spawnCount = Mathf.Max(1, unit.spawnCount),
                spawnSpacing = Mathf.Max(0f, unit.spawnSpacing),
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

        private static BattleTesterAttributeOverride[] CloneAttrs(BattleTesterAttributeOverride[] attrs)
        {
            if (attrs == null || attrs.Length == 0)
            {
                return Array.Empty<BattleTesterAttributeOverride>();
            }

            BattleTesterAttributeOverride[] result = new BattleTesterAttributeOverride[attrs.Length];
            Array.Copy(attrs, result, attrs.Length);
            return result;
        }

        private static void NormalizeCamp(BattleTesterUnitEntry[] units, int camp)
        {
            if (units == null)
            {
                return;
            }

            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null)
                {
                    units[i].camp = camp;
                    units[i].spawnCount = Mathf.Max(1, units[i].spawnCount);
                }
            }
        }

        private void NormalizeUnitTemplates(BattleTesterUnitEntry[] units, int camp)
        {
            EnsureUnitConfigOptions();
            if (units == null)
            {
                return;
            }

            for (int i = 0; i < units.Length; i++)
            {
                BattleTesterUnitEntry unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                unit.camp = camp;
                unit.spawnCount = Mathf.Max(1, unit.spawnCount);
                if (unit.spawnSpacing <= 0f)
                {
                    unit.spawnSpacing = GetDefaultTemplateSpacing();
                }

                NormalizeUnitCfgId(unit);
            }
        }

        private static void DeleteAt<T>(ref T[] array, int index)
        {
            if (array == null || index < 0 || index >= array.Length)
            {
                return;
            }

            for (int i = index; i < array.Length - 1; i++)
            {
                array[i] = array[i + 1];
            }

            Array.Resize(ref array, array.Length - 1);
        }
    }
}
