using UnityEngine;

namespace Game.Play.Battle.Collision
{
    public sealed class BattleCollisionQueryBuffer
    {
        public int Count { get; private set; }
        public int[] TargetIndices { get; }

        internal float[] SortDistances { get; }

        public BattleCollisionQueryBuffer(int capacity)
        {
            int safeCapacity = Mathf.Max(1, capacity);
            TargetIndices = new int[safeCapacity];
            SortDistances = new float[safeCapacity];
        }

        public void Clear()
        {
            Count = 0;
        }

        public bool TryAdd(int targetIndex, float sortDistance = 0f)
        {
            if (Count >= TargetIndices.Length)
            {
                return false;
            }

            TargetIndices[Count] = targetIndex;
            SortDistances[Count] = sortDistance;
            Count++;
            return true;
        }

        internal void Trim(int count)
        {
            Count = Mathf.Clamp(count, 0, Count);
        }
    }
}
