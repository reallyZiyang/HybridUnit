using Game.Play.Battle.Unit;
using UnityEngine;

namespace Game.Play.Battle.Collision
{
    public sealed class BattleCollisionManager
    {
        private const float MinCellSize = 0.0001f;

        private readonly int capacity;
        private readonly Vector2 gridMin;
        private readonly int gridWidth;
        private readonly int gridHeight;
        private readonly float cellSize;
        private readonly float largeQueryCellRatio;

        private readonly Vector2[] positions;
        private readonly float[] radii;
        private readonly int[] camps;
        private readonly int[] states;
        private readonly int[] layers;
        private readonly int[] renderHandles;
        private readonly BattleUnitHandle[] unitHandles;
        private readonly bool[] active;
        private readonly int[] generations;
        private readonly int[] freeStack;

        private readonly int[] cellHeads;
        private readonly int[] linkTargets;
        private readonly int[] linkNext;
        private readonly int[] queryStamp;

        private int allocatedCount;
        private int activeCount;
        private int freeCount;
        private int linkCount;
        private int currentQueryId;
        private float maxTargetRadius;
        private bool gridDirty = true;
        private bool gridUsable = true;

        public BattleCollisionManager(
            int capacity,
            Vector2 gridMin,
            int gridWidth,
            int gridHeight,
            float cellSize,
            float largeQueryCellRatio = 0.35f,
            int maxGridLinks = 0)
        {
            this.capacity = Mathf.Max(1, capacity);
            this.gridMin = gridMin;
            this.gridWidth = Mathf.Max(1, gridWidth);
            this.gridHeight = Mathf.Max(1, gridHeight);
            this.cellSize = Mathf.Max(MinCellSize, cellSize);
            this.largeQueryCellRatio = Mathf.Clamp01(largeQueryCellRatio);

            int safeLinkCapacity = maxGridLinks > 0 ? maxGridLinks : this.capacity * 16;

            positions = new Vector2[this.capacity];
            radii = new float[this.capacity];
            camps = new int[this.capacity];
            states = new int[this.capacity];
            layers = new int[this.capacity];
            renderHandles = new int[this.capacity];
            unitHandles = new BattleUnitHandle[this.capacity];
            active = new bool[this.capacity];
            generations = new int[this.capacity];
            freeStack = new int[this.capacity];

            cellHeads = new int[this.gridWidth * this.gridHeight];
            linkTargets = new int[Mathf.Max(1, safeLinkCapacity)];
            linkNext = new int[Mathf.Max(1, safeLinkCapacity)];
            queryStamp = new int[this.capacity];

            ClearCellHeads();
        }

        public int Capacity => capacity;
        public int ActiveCount => activeCount;
        public bool IsGridDirty => gridDirty;

        public BattleCollisionTargetHandle RegisterTarget(
            Vector2 position,
            float radius,
            int camp,
            int state,
            int layer,
            int renderHandle,
            BattleUnitHandle unitHandle = default)
        {
            int index;
            if (freeCount > 0)
            {
                index = freeStack[--freeCount];
            }
            else
            {
                if (allocatedCount >= capacity)
                {
                    Debug.LogError($"[BattleCollision] Target capacity exceeded: {capacity}");
                    return BattleCollisionTargetHandle.Invalid;
                }

                index = allocatedCount++;
            }

            int generation = generations[index] + 1;
            generations[index] = generation > 0 ? generation : 1;
            positions[index] = position;
            radii[index] = Mathf.Max(0f, radius);
            camps[index] = camp;
            states[index] = state;
            layers[index] = layer;
            renderHandles[index] = renderHandle;
            unitHandles[index] = unitHandle;
            active[index] = true;
            activeCount++;
            gridDirty = true;

            return new BattleCollisionTargetHandle(index, generations[index]);
        }

        public bool UnregisterTarget(BattleCollisionTargetHandle target)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }

            int index = target.index;
            active[index] = false;
            unitHandles[index] = BattleUnitHandle.Invalid;
            freeStack[freeCount++] = index;
            activeCount--;
            gridDirty = true;
            return true;
        }

        public bool UpdateTargetPosition(BattleCollisionTargetHandle target, Vector2 position)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }

            positions[target.index] = position;
            gridDirty = true;
            return true;
        }

        public bool UpdateTargetRadius(BattleCollisionTargetHandle target, float radius)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }

            radii[target.index] = Mathf.Max(0f, radius);
            gridDirty = true;
            return true;
        }

        public bool UpdateTargetFilter(BattleCollisionTargetHandle target, int camp, int state, int layer)
        {
            if (!IsValidTarget(target))
            {
                return false;
            }

            camps[target.index] = camp;
            states[target.index] = state;
            layers[target.index] = layer;
            return true;
        }

        public bool IsValidTarget(BattleCollisionTargetHandle target)
        {
            return target.index >= 0
                && target.index < capacity
                && active[target.index]
                && generations[target.index] == target.generation;
        }

        public BattleUnitHandle GetUnitHandle(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= capacity || !active[targetIndex])
            {
                return BattleUnitHandle.Invalid;
            }

            return unitHandles[targetIndex];
        }

        public int GetRenderHandle(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= capacity || !active[targetIndex])
            {
                return -1;
            }

            return renderHandles[targetIndex];
        }

        public void RebuildGrid()
        {
            ClearCellHeads();
            linkCount = 0;
            maxTargetRadius = 0f;
            gridUsable = true;

            for (int i = 0; i < allocatedCount; i++)
            {
                if (!active[i])
                {
                    continue;
                }

                float radius = radii[i];
                if (radius > maxTargetRadius)
                {
                    maxTargetRadius = radius;
                }

                Rect targetAabb = BattleCollisionMath.CircleAabb(positions[i], radius);
                if (!TryGetClampedCellRange(targetAabb, out int minX, out int minY, out int maxX, out int maxY))
                {
                    continue;
                }

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!TryInsertToCell(CellIndex(x, y), i))
                        {
                            gridUsable = false;
                            gridDirty = false;
                            return;
                        }
                    }
                }
            }

            gridDirty = false;
        }

        public int Query(in BattleCollisionShape shape, in BattleCollisionQueryOptions options, BattleCollisionQueryBuffer buffer)
        {
            if (buffer == null)
            {
                Debug.LogError("[BattleCollision] Query buffer is null.");
                return 0;
            }

            buffer.Clear();
            if (activeCount == 0)
            {
                return 0;
            }

            if (gridDirty)
            {
                RebuildGrid();
            }

            Rect broadAabb = BattleCollisionMath.Expand(BattleCollisionMath.ShapeAabb(shape), maxTargetRadius);
            if (!gridUsable || ShouldFallback(broadAabb))
            {
                return QueryAllTargets(shape, options, buffer);
            }

            if (!TryGetClampedCellRange(broadAabb, out int minX, out int minY, out int maxX, out int maxY))
            {
                return 0;
            }

            BeginQueryStamp();
            Vector2 sortOrigin = BattleCollisionMath.SortOrigin(shape);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int link = cellHeads[CellIndex(x, y)];
                    while (link >= 0)
                    {
                        int targetIndex = linkTargets[link];
                        link = linkNext[link];

                        if (queryStamp[targetIndex] == currentQueryId)
                        {
                            continue;
                        }

                        queryStamp[targetIndex] = currentQueryId;
                        if (!TryAppendHit(targetIndex, shape, options, sortOrigin, buffer))
                        {
                            return FinishQuery(options, buffer);
                        }
                    }
                }
            }

            return FinishQuery(options, buffer);
        }

        public void QueryVisit(in BattleCollisionShape shape, in BattleCollisionQueryOptions options, IBattleCollisionQueryVisitor visitor)
        {
            if (visitor == null)
            {
                Debug.LogError("[BattleCollision] Query visitor is null.");
                return;
            }

            if (activeCount == 0)
            {
                return;
            }

            if (gridDirty)
            {
                RebuildGrid();
            }

            Rect broadAabb = BattleCollisionMath.Expand(BattleCollisionMath.ShapeAabb(shape), maxTargetRadius);
            if (!gridUsable || ShouldFallback(broadAabb))
            {
                VisitAllTargets(shape, options, visitor);
                return;
            }

            if (!TryGetClampedCellRange(broadAabb, out int minX, out int minY, out int maxX, out int maxY))
            {
                return;
            }

            BeginQueryStamp();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int link = cellHeads[CellIndex(x, y)];
                    while (link >= 0)
                    {
                        int targetIndex = linkTargets[link];
                        link = linkNext[link];

                        if (queryStamp[targetIndex] == currentQueryId)
                        {
                            continue;
                        }

                        queryStamp[targetIndex] = currentQueryId;
                        if (!TryVisitHit(targetIndex, shape, options, visitor))
                        {
                            return;
                        }
                    }
                }
            }
        }

        public bool QueryNearestCircle(Vector2 origin, float radius, in BattleCollisionQueryOptions options, out int targetIndex)
        {
            targetIndex = -1;
            if (activeCount == 0)
            {
                return false;
            }

            if (gridDirty)
            {
                RebuildGrid();
            }

            float safeRadius = Mathf.Max(0f, radius);
            if (!gridUsable || !TryGetCell(origin, out int centerX, out int centerY))
            {
                return QueryNearestCircleAll(origin, safeRadius, options, out targetIndex);
            }

            float searchRadius = safeRadius + maxTargetRadius;
            int maxRing = Mathf.CeilToInt(searchRadius / cellSize);
            float bestDistanceSqr = float.MaxValue;

            BeginQueryStamp();
            for (int ring = 0; ring <= maxRing; ring++)
            {
                VisitNearestRing(origin, safeRadius, options, centerX, centerY, ring, ref targetIndex, ref bestDistanceSqr);

                if (targetIndex >= 0 && ring < maxRing)
                {
                    float nextRingDistanceSqr = MinDistanceToRingSqr(origin, centerX, centerY, ring + 1);
                    if (nextRingDistanceSqr > bestDistanceSqr)
                    {
                        break;
                    }
                }
            }

            return targetIndex >= 0;
        }

        public int BruteForceQuery(in BattleCollisionShape shape, in BattleCollisionQueryOptions options, BattleCollisionQueryBuffer buffer)
        {
            if (buffer == null)
            {
                Debug.LogError("[BattleCollision] Query buffer is null.");
                return 0;
            }

            buffer.Clear();
            return QueryAllTargets(shape, options, buffer);
        }

        private void VisitAllTargets(in BattleCollisionShape shape, in BattleCollisionQueryOptions options, IBattleCollisionQueryVisitor visitor)
        {
            for (int i = 0; i < allocatedCount; i++)
            {
                if (!TryVisitHit(i, shape, options, visitor))
                {
                    return;
                }
            }
        }

        private int QueryAllTargets(in BattleCollisionShape shape, in BattleCollisionQueryOptions options, BattleCollisionQueryBuffer buffer)
        {
            Vector2 sortOrigin = BattleCollisionMath.SortOrigin(shape);
            for (int i = 0; i < allocatedCount; i++)
            {
                if (!TryAppendHit(i, shape, options, sortOrigin, buffer))
                {
                    return FinishQuery(options, buffer);
                }
            }

            return FinishQuery(options, buffer);
        }

        private bool TryAppendHit(
            int targetIndex,
            in BattleCollisionShape shape,
            in BattleCollisionQueryOptions options,
            Vector2 sortOrigin,
            BattleCollisionQueryBuffer buffer)
        {
            if (!PassFilter(targetIndex, options))
            {
                return true;
            }

            if (!BattleCollisionMath.ShapeHitsCircle(shape, positions[targetIndex], radii[targetIndex]))
            {
                return true;
            }

            float sortDistance = options.sortByDistance ? (positions[targetIndex] - sortOrigin).sqrMagnitude : 0f;
            if (!buffer.TryAdd(targetIndex, sortDistance))
            {
                return false;
            }

            return options.sortByDistance || options.maxHits <= 0 || buffer.Count < options.maxHits;
        }

        private bool TryVisitHit(
            int targetIndex,
            in BattleCollisionShape shape,
            in BattleCollisionQueryOptions options,
            IBattleCollisionQueryVisitor visitor)
        {
            if (!PassFilter(targetIndex, options))
            {
                return true;
            }

            if (!BattleCollisionMath.ShapeHitsCircle(shape, positions[targetIndex], radii[targetIndex]))
            {
                return true;
            }

            return visitor.Visit(targetIndex);
        }

        private bool QueryNearestCircleAll(Vector2 origin, float radius, in BattleCollisionQueryOptions options, out int targetIndex)
        {
            targetIndex = -1;
            float bestDistanceSqr = float.MaxValue;
            for (int i = 0; i < allocatedCount; i++)
            {
                if (!PassFilter(i, options))
                {
                    continue;
                }

                if (!BattleCollisionMath.CircleHitsCircle(origin, radius, positions[i], radii[i]))
                {
                    continue;
                }

                float distanceSqr = (positions[i] - origin).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    targetIndex = i;
                }
            }

            return targetIndex >= 0;
        }

        private int FinishQuery(in BattleCollisionQueryOptions options, BattleCollisionQueryBuffer buffer)
        {
            if (options.sortByDistance)
            {
                SortBufferByDistance(buffer);
                if (options.maxHits > 0 && buffer.Count > options.maxHits)
                {
                    buffer.Trim(options.maxHits);
                }
            }

            return buffer.Count;
        }

        private bool PassFilter(int targetIndex, in BattleCollisionQueryOptions options)
        {
            if (targetIndex < 0 || targetIndex >= capacity || !active[targetIndex])
            {
                return false;
            }

            if (options.campMask != 0 && !PassIndexMask(options.campMask, camps[targetIndex]))
            {
                return false;
            }

            if (options.stateMask != 0 && (options.stateMask & states[targetIndex]) == 0)
            {
                return false;
            }

            if (options.layerMask != 0 && !PassIndexMask(options.layerMask, layers[targetIndex]))
            {
                return false;
            }

            return true;
        }

        private static bool PassIndexMask(int mask, int index)
        {
            return index >= 0 && index < 32 && (mask & (1 << index)) != 0;
        }

        private bool ShouldFallback(Rect broadAabb)
        {
            if (!TryGetClampedCellRange(broadAabb, out int minX, out int minY, out int maxX, out int maxY))
            {
                return false;
            }

            int coveredCells = (maxX - minX + 1) * (maxY - minY + 1);
            int totalCells = gridWidth * gridHeight;
            return coveredCells > totalCells * largeQueryCellRatio;
        }

        private void BeginQueryStamp()
        {
            currentQueryId++;
            if (currentQueryId == int.MaxValue)
            {
                System.Array.Clear(queryStamp, 0, queryStamp.Length);
                currentQueryId = 1;
            }
        }

        private bool TryInsertToCell(int cellIndex, int targetIndex)
        {
            if (linkCount >= linkTargets.Length)
            {
                Debug.LogError($"[BattleCollision] Grid link capacity exceeded: {linkTargets.Length}. Query will fallback to full scan.");
                return false;
            }

            int linkIndex = linkCount++;
            linkTargets[linkIndex] = targetIndex;
            linkNext[linkIndex] = cellHeads[cellIndex];
            cellHeads[cellIndex] = linkIndex;
            return true;
        }

        private void ClearCellHeads()
        {
            for (int i = 0; i < cellHeads.Length; i++)
            {
                cellHeads[i] = -1;
            }
        }

        private bool TryGetClampedCellRange(Rect aabb, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = Mathf.FloorToInt((aabb.xMin - gridMin.x) / cellSize);
            minY = Mathf.FloorToInt((aabb.yMin - gridMin.y) / cellSize);
            maxX = Mathf.FloorToInt((aabb.xMax - gridMin.x) / cellSize);
            maxY = Mathf.FloorToInt((aabb.yMax - gridMin.y) / cellSize);

            if (maxX < 0 || maxY < 0 || minX >= gridWidth || minY >= gridHeight)
            {
                return false;
            }

            minX = Mathf.Clamp(minX, 0, gridWidth - 1);
            minY = Mathf.Clamp(minY, 0, gridHeight - 1);
            maxX = Mathf.Clamp(maxX, 0, gridWidth - 1);
            maxY = Mathf.Clamp(maxY, 0, gridHeight - 1);
            return minX <= maxX && minY <= maxY;
        }

        private bool TryGetCell(Vector2 position, out int x, out int y)
        {
            x = Mathf.FloorToInt((position.x - gridMin.x) / cellSize);
            y = Mathf.FloorToInt((position.y - gridMin.y) / cellSize);
            return x >= 0 && y >= 0 && x < gridWidth && y < gridHeight;
        }

        private void VisitNearestRing(
            Vector2 origin,
            float radius,
            in BattleCollisionQueryOptions options,
            int centerX,
            int centerY,
            int ring,
            ref int targetIndex,
            ref float bestDistanceSqr)
        {
            if (ring == 0)
            {
                if (centerX >= 0 && centerY >= 0 && centerX < gridWidth && centerY < gridHeight)
                {
                    VisitNearestCell(origin, radius, options, centerX, centerY, ref targetIndex, ref bestDistanceSqr);
                }

                return;
            }

            int minX = Mathf.Max(0, centerX - ring);
            int maxX = Mathf.Min(gridWidth - 1, centerX + ring);
            int minY = Mathf.Max(0, centerY - ring);
            int maxY = Mathf.Min(gridHeight - 1, centerY + ring);
            int topY = centerY + ring;
            int bottomY = centerY - ring;
            int leftX = centerX - ring;
            int rightX = centerX + ring;

            if (bottomY >= 0 && bottomY < gridHeight)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    VisitNearestCell(origin, radius, options, x, bottomY, ref targetIndex, ref bestDistanceSqr);
                }
            }

            if (topY != bottomY && topY >= 0 && topY < gridHeight)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    VisitNearestCell(origin, radius, options, x, topY, ref targetIndex, ref bestDistanceSqr);
                }
            }

            int innerMinY = Mathf.Max(minY, bottomY + 1);
            int innerMaxY = Mathf.Min(maxY, topY - 1);
            if (leftX >= 0 && leftX < gridWidth)
            {
                for (int y = innerMinY; y <= innerMaxY; y++)
                {
                    VisitNearestCell(origin, radius, options, leftX, y, ref targetIndex, ref bestDistanceSqr);
                }
            }

            if (rightX != leftX && rightX >= 0 && rightX < gridWidth)
            {
                for (int y = innerMinY; y <= innerMaxY; y++)
                {
                    VisitNearestCell(origin, radius, options, rightX, y, ref targetIndex, ref bestDistanceSqr);
                }
            }
        }

        private void VisitNearestCell(
            Vector2 origin,
            float radius,
            in BattleCollisionQueryOptions options,
            int cellX,
            int cellY,
            ref int targetIndex,
            ref float bestDistanceSqr)
        {
            int link = cellHeads[CellIndex(cellX, cellY)];
            while (link >= 0)
            {
                int candidate = linkTargets[link];
                link = linkNext[link];

                if (queryStamp[candidate] == currentQueryId)
                {
                    continue;
                }

                queryStamp[candidate] = currentQueryId;
                if (!PassFilter(candidate, options))
                {
                    continue;
                }

                if (!BattleCollisionMath.CircleHitsCircle(origin, radius, positions[candidate], radii[candidate]))
                {
                    continue;
                }

                float distanceSqr = (positions[candidate] - origin).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    targetIndex = candidate;
                }
            }
        }

        private float MinDistanceToRingSqr(Vector2 origin, int centerX, int centerY, int ring)
        {
            float best = float.MaxValue;
            MinDistanceToRingRow(origin, centerX, centerY, ring, ref best);
            return best;
        }

        private void MinDistanceToRingRow(Vector2 origin, int centerX, int centerY, int ring, ref float best)
        {
            if (ring == 0)
            {
                if (centerX >= 0 && centerY >= 0 && centerX < gridWidth && centerY < gridHeight)
                {
                    UpdateMinDistanceToCell(origin, centerX, centerY, ref best);
                }

                return;
            }

            int minX = Mathf.Max(0, centerX - ring);
            int maxX = Mathf.Min(gridWidth - 1, centerX + ring);
            int minY = Mathf.Max(0, centerY - ring);
            int maxY = Mathf.Min(gridHeight - 1, centerY + ring);
            int topY = centerY + ring;
            int bottomY = centerY - ring;
            int leftX = centerX - ring;
            int rightX = centerX + ring;

            if (bottomY >= 0 && bottomY < gridHeight)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    UpdateMinDistanceToCell(origin, x, bottomY, ref best);
                }
            }

            if (topY != bottomY && topY >= 0 && topY < gridHeight)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    UpdateMinDistanceToCell(origin, x, topY, ref best);
                }
            }

            int innerMinY = Mathf.Max(minY, bottomY + 1);
            int innerMaxY = Mathf.Min(maxY, topY - 1);
            if (leftX >= 0 && leftX < gridWidth)
            {
                for (int y = innerMinY; y <= innerMaxY; y++)
                {
                    UpdateMinDistanceToCell(origin, leftX, y, ref best);
                }
            }

            if (rightX != leftX && rightX >= 0 && rightX < gridWidth)
            {
                for (int y = innerMinY; y <= innerMaxY; y++)
                {
                    UpdateMinDistanceToCell(origin, rightX, y, ref best);
                }
            }
        }

        private void UpdateMinDistanceToCell(Vector2 origin, int x, int y, ref float best)
        {
            float minX = gridMin.x + x * cellSize;
            float minY = gridMin.y + y * cellSize;
            float maxX = minX + cellSize;
            float maxY = minY + cellSize;
            float dx = origin.x < minX ? minX - origin.x : origin.x > maxX ? origin.x - maxX : 0f;
            float dy = origin.y < minY ? minY - origin.y : origin.y > maxY ? origin.y - maxY : 0f;
            float distanceSqr = dx * dx + dy * dy;
            if (distanceSqr < best)
            {
                best = distanceSqr;
            }
        }

        private int CellIndex(int x, int y)
        {
            return y * gridWidth + x;
        }

        private static void SortBufferByDistance(BattleCollisionQueryBuffer buffer)
        {
            for (int i = 1; i < buffer.Count; i++)
            {
                int target = buffer.TargetIndices[i];
                float distance = buffer.SortDistances[i];
                int j = i - 1;

                while (j >= 0 && buffer.SortDistances[j] > distance)
                {
                    buffer.TargetIndices[j + 1] = buffer.TargetIndices[j];
                    buffer.SortDistances[j + 1] = buffer.SortDistances[j];
                    j--;
                }

                buffer.TargetIndices[j + 1] = target;
                buffer.SortDistances[j + 1] = distance;
            }
        }
    }
}
