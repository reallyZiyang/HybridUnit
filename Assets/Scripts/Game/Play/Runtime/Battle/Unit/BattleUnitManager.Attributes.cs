using Game.Data.Configs.Attr;
using Game.Play.Battle.Runtime;
using UnityEngine;

namespace Game.Play.Battle.Unit
{
    public sealed partial class BattleUnitManager
    {
        public long GetAttr(BattleUnitHandle unit, AttributeType attr)
        {
            if (!IsValid(unit) || !BattleAttributeRegistry.TryGetIndex(attr, out int attrIndex))
            {
                return 0;
            }

            return finalAttrs[AttrOffset(unit.index, attrIndex)];
        }

        public bool SetBaseAttr(BattleUnitHandle unit, AttributeType attr, long value)
        {
            if (!IsValid(unit))
            {
                return false;
            }

            SetBaseAttrByIndex(unit.index, attr, value);
            if (attr == AttributeType.Hp)
            {
                hp[unit.index] = (int)System.Math.Max(0L, value);
            }

            return true;
        }

        public bool AddModifierAttr(BattleUnitHandle unit, AttributeType attr, long delta)
        {
            if (!IsValid(unit) || !BattleAttributeRegistry.TryGetIndex(attr, out int attrIndex))
            {
                return false;
            }

            int offset = AttrOffset(unit.index, attrIndex);
            modifierAttrs[offset] += delta;
            finalAttrs[offset] = baseAttrs[offset] + modifierAttrs[offset];
            return true;
        }

        public bool AddEndure(BattleUnitHandle unit, long delta)
        {
            if (!IsValid(unit) || !BattleAttributeRegistry.TryGetIndex(AttributeType.Endure, out int attrIndex))
            {
                return false;
            }

            int offset = AttrOffset(unit.index, attrIndex);
            long finalValue = baseAttrs[offset] + modifierAttrs[offset];
            long safeDelta = delta;
            if (safeDelta < 0 && finalValue + safeDelta < 0)
            {
                safeDelta = -finalValue;
            }

            modifierAttrs[offset] += safeDelta;
            finalAttrs[offset] = System.Math.Max(0L, baseAttrs[offset] + modifierAttrs[offset]);
            return true;
        }

        private void ClearAttributes(int index)
        {
            int offset = index * attrStride;
            System.Array.Clear(baseAttrs, offset, attrStride);
            System.Array.Clear(modifierAttrs, offset, attrStride);
            System.Array.Clear(finalAttrs, offset, attrStride);
        }

        private void ApplyInitialAttributes(int index, BattleAttributeValue[] attrs)
        {
            if (attrs == null)
            {
                return;
            }

            for (int i = 0; i < attrs.Length; i++)
            {
                SetBaseAttrByIndex(index, attrs[i].type, attrs[i].value);
            }
        }

        private int ResolveSpawnHp(int index, int descHp)
        {
            if (descHp > 0)
            {
                return descHp;
            }

            long attrHp = GetFinalAttrByIndex(index, AttributeType.Hp);
            if (attrHp > 0)
            {
                return (int)Mathf.Min(int.MaxValue, attrHp);
            }

            long hpMax = GetFinalAttrByIndex(index, AttributeType.HpMax);
            return hpMax > 0 ? (int)Mathf.Min(int.MaxValue, hpMax) : 1;
        }

        private void SetBaseAttrByIndex(int unitIndex, AttributeType attr, long value)
        {
            if (!BattleAttributeRegistry.TryGetIndex(attr, out int attrIndex))
            {
                return;
            }

            int offset = AttrOffset(unitIndex, attrIndex);
            baseAttrs[offset] = value;
            finalAttrs[offset] = baseAttrs[offset] + modifierAttrs[offset];
        }

        private long GetFinalAttrByIndex(int unitIndex, AttributeType attr)
        {
            if (!BattleAttributeRegistry.TryGetIndex(attr, out int attrIndex))
            {
                return 0;
            }

            return finalAttrs[AttrOffset(unitIndex, attrIndex)];
        }

        private int AttrOffset(int unitIndex, int attrIndex)
        {
            return unitIndex * attrStride + attrIndex;
        }
    }
}
