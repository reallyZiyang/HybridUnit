using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Runtime
{
    public readonly struct BattleSkillProperties
    {
        public static readonly BattleSkillProperties Empty = new(0, 0, 0);

        public readonly int projectileNumAdd;
        public readonly int attackSpeedBp;
        public readonly int cooldownReductionBp;

        public BattleSkillProperties(int projectileNumAdd, int attackSpeedBp, int cooldownReductionBp)
        {
            this.projectileNumAdd = projectileNumAdd;
            this.attackSpeedBp = attackSpeedBp;
            this.cooldownReductionBp = cooldownReductionBp;
        }
    }

    public readonly struct BattleProjectileProperties
    {
        public static readonly BattleProjectileProperties Empty = new(0, 0, 0);

        public readonly int replaceProjectileId;
        public readonly int pierceAdd;
        public readonly int hitAreaMilli;

        public BattleProjectileProperties(int replaceProjectileId, int pierceAdd, int hitAreaMilli)
        {
            this.replaceProjectileId = replaceProjectileId;
            this.pierceAdd = pierceAdd;
            this.hitAreaMilli = hitAreaMilli;
        }
    }

    public sealed class BattleSkillPropertyStore
    {
        private BattleSkillProperties[] properties;
        private bool[] dirty;
        private int unitCapacity;
        private int slotsPerUnit;

        public void Initialize(int unitCapacity, int slotsPerUnit)
        {
            this.unitCapacity = Mathf.Max(1, unitCapacity);
            this.slotsPerUnit = Mathf.Max(1, slotsPerUnit);
            int capacity = this.unitCapacity * this.slotsPerUnit;
            if (properties == null || properties.Length != capacity)
            {
                properties = new BattleSkillProperties[capacity];
                dirty = new bool[capacity];
            }

            MarkAllDirty();
        }

        public void Clear()
        {
            if (properties == null)
            {
                return;
            }

            System.Array.Clear(properties, 0, properties.Length);
            MarkAllDirty();
        }

        public void MarkAllDirty()
        {
            if (dirty == null)
            {
                return;
            }

            for (int i = 0; i < dirty.Length; i++)
            {
                dirty[i] = true;
            }
        }

        public bool TryGetCached(BattleUnitHandle owner, int localSlotIndex, out BattleSkillProperties value)
        {
            int index = GetIndex(owner, localSlotIndex);
            if (index < 0 || dirty[index])
            {
                value = BattleSkillProperties.Empty;
                return false;
            }

            value = properties[index];
            return true;
        }

        public void Set(BattleUnitHandle owner, int localSlotIndex, BattleSkillProperties value)
        {
            int index = GetIndex(owner, localSlotIndex);
            if (index < 0)
            {
                return;
            }

            properties[index] = value;
            dirty[index] = false;
        }

        private int GetIndex(BattleUnitHandle owner, int localSlotIndex)
        {
            if (!owner.IsValid || localSlotIndex < 0 || localSlotIndex >= slotsPerUnit || properties == null)
            {
                return -1;
            }

            int index = owner.index * slotsPerUnit + localSlotIndex;
            return index >= 0 && index < properties.Length ? index : -1;
        }
    }

    public sealed class BattleProjectilePropertyStore
    {
        private BattleProjectileProperties[] properties;
        private bool[] dirty;
        private int unitCapacity;
        private int slotsPerUnit;

        public void Initialize(int unitCapacity, int slotsPerUnit)
        {
            this.unitCapacity = Mathf.Max(1, unitCapacity);
            this.slotsPerUnit = Mathf.Max(1, slotsPerUnit);
            int capacity = this.unitCapacity * this.slotsPerUnit;
            if (properties == null || properties.Length != capacity)
            {
                properties = new BattleProjectileProperties[capacity];
                dirty = new bool[capacity];
            }

            MarkAllDirty();
        }

        public void Clear()
        {
            if (properties == null)
            {
                return;
            }

            System.Array.Clear(properties, 0, properties.Length);
            MarkAllDirty();
        }

        public void MarkAllDirty()
        {
            if (dirty == null)
            {
                return;
            }

            for (int i = 0; i < dirty.Length; i++)
            {
                dirty[i] = true;
            }
        }

        public bool TryGetCached(BattleUnitHandle owner, int localSlotIndex, out BattleProjectileProperties value)
        {
            int index = GetIndex(owner, localSlotIndex);
            if (index < 0 || dirty[index])
            {
                value = BattleProjectileProperties.Empty;
                return false;
            }

            value = properties[index];
            return true;
        }

        public void Set(BattleUnitHandle owner, int localSlotIndex, BattleProjectileProperties value)
        {
            int index = GetIndex(owner, localSlotIndex);
            if (index < 0)
            {
                return;
            }

            properties[index] = value;
            dirty[index] = false;
        }

        private int GetIndex(BattleUnitHandle owner, int localSlotIndex)
        {
            if (!owner.IsValid || localSlotIndex < 0 || localSlotIndex >= slotsPerUnit || properties == null)
            {
                return -1;
            }

            int index = owner.index * slotsPerUnit + localSlotIndex;
            return index >= 0 && index < properties.Length ? index : -1;
        }
    }
}
