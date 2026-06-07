using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BattleCollisionWorld : MonoBehaviour
{
    private readonly List<BakedSpineVitPlayer> targets = new List<BakedSpineVitPlayer>(256);
    private readonly List<BakedSpineVitPlayer> candidateResults = new List<BakedSpineVitPlayer>(128);
    private readonly Dictionary<Vector2Int, List<BakedSpineVitPlayer>> grid = new Dictionary<Vector2Int, List<BakedSpineVitPlayer>>(256);
    private readonly HashSet<BakedSpineVitPlayer> queryVisited = new HashSet<BakedSpineVitPlayer>();

    [Header("Grid")]
    [Tooltip("World units per grid cell.")]
    [SerializeField, Min(0.01f)] private float cellSize = 2f;
    [Tooltip("Grid width in cells.")]
    [SerializeField, Min(1)] private int gridWidth = 20;
    [Tooltip("Grid height in cells.")]
    [SerializeField, Min(1)] private int gridHeight = 12;
    [FormerlySerializedAs("defaultTargetRadius")]
    [Tooltip("Shared collision radius for all BakedSpineVitPlayer targets in this validation tool.")]
    [SerializeField, Min(0f)] private float targetRadius = 0.45f;

    [Header("Runtime")]
    [SerializeField] private bool scanTargetsOnEnable = true;
    [SerializeField] private bool rebuildGridEveryFrame = true;

    [Header("Gizmos")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private Color gridColor = new Color(0.2f, 0.85f, 1f, 0.45f);
    [SerializeField] private Color targetRadiusColor = new Color(1f, 0.85f, 0.1f, 0.45f);
    [SerializeField] private bool drawTargetRadius;

    private bool gridDirty = true;
    private int lastRebuildFrame = -1;

    public static BattleCollisionWorld Instance { get; private set; }
    public float CellSize => Mathf.Max(0.01f, cellSize);
    public int GridWidth => Mathf.Max(1, gridWidth);
    public int GridHeight => Mathf.Max(1, gridHeight);
    public float TargetRadius => Mathf.Max(0f, targetRadius);
    public float DefaultTargetRadius => TargetRadius;

    private void OnEnable()
    {
        Instance = this;
        if (scanTargetsOnEnable)
        {
            RefreshTargets();
        }

        MarkGridDirty();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        if (scanTargetsOnEnable)
        {
            RefreshTargets();
        }

        MarkGridDirty();
    }

    private void LateUpdate()
    {
        if (rebuildGridEveryFrame && lastRebuildFrame != Time.frameCount)
        {
            RebuildGrid();
        }
    }

    public static BattleCollisionWorld FindWorld()
    {
        if (Instance != null)
        {
            return Instance;
        }

#pragma warning disable CS0618
        Instance = FindObjectOfType<BattleCollisionWorld>();
#pragma warning restore CS0618
        return Instance;
    }

    [ContextMenu("Refresh Targets")]
    public void RefreshTargets()
    {
        targets.Clear();
#pragma warning disable CS0618
        BakedSpineVitPlayer[] foundTargets = FindObjectsOfType<BakedSpineVitPlayer>();
#pragma warning restore CS0618
        for (int i = 0; i < foundTargets.Length; i++)
        {
            Register(foundTargets[i]);
        }
    }

    public void Register(BakedSpineVitPlayer target)
    {
        if (target == null || targets.Contains(target))
        {
            return;
        }

        targets.Add(target);
        MarkGridDirty();
    }

    public void Unregister(BakedSpineVitPlayer target)
    {
        if (targets.Remove(target))
        {
            MarkGridDirty();
        }
    }

    public void MarkGridDirty()
    {
        gridDirty = true;
    }

    public void RebuildGrid()
    {
        ClearGridLists();

        float radius = TargetRadius;
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            BakedSpineVitPlayer target = targets[i];
            if (target == null)
            {
                targets.RemoveAt(i);
                continue;
            }

            if (!target.isActiveAndEnabled)
            {
                continue;
            }

            Vector2 center = target.transform.position;
            Rect targetAabb = BattleCollisionMath.CircleAabb(center, radius);
            InsertTarget(target, targetAabb);
        }

        gridDirty = false;
        lastRebuildFrame = Time.frameCount;
    }

    public void QueryCircle(Vector2 center, float radius, List<BakedSpineVitPlayer> results)
    {
        CollectCandidates(BattleCollisionMath.Expand(BattleCollisionMath.CircleAabb(center, radius), TargetRadius), results);
        NarrowCircle(center, radius, results);
    }

    public void QueryRect(Vector2 center, Vector2 size, float rotationDeg, List<BakedSpineVitPlayer> results)
    {
        CollectCandidates(BattleCollisionMath.Expand(BattleCollisionMath.RotatedRectAabb(center, size, rotationDeg), TargetRadius), results);
        NarrowRect(center, size, rotationDeg, results);
    }

    public void QuerySector(Vector2 center, Vector2 forward, float radius, float angleDeg, List<BakedSpineVitPlayer> results)
    {
        CollectCandidates(BattleCollisionMath.Expand(BattleCollisionMath.SectorAabb(center, forward, radius, angleDeg), TargetRadius), results);
        NarrowSector(center, forward, radius, angleDeg, results);
    }

    public void QueryCapsuleSegment(Vector2 start, Vector2 end, float width, List<BakedSpineVitPlayer> results)
    {
        CollectCandidates(BattleCollisionMath.Expand(BattleCollisionMath.CapsuleSegmentAabb(start, end, width), TargetRadius), results);
        NarrowCapsuleSegment(start, end, width, results);
    }

    private void CollectCandidates(Rect aabb, List<BakedSpineVitPlayer> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        queryVisited.Clear();
        EnsureGrid();

        Vector2Int minCell = WorldToCell(new Vector2(aabb.xMin, aabb.yMin));
        Vector2Int maxCell = WorldToCell(new Vector2(aabb.xMax, aabb.yMax));
        if (!ClampCellRange(ref minCell, ref maxCell))
        {
            return;
        }

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                if (!grid.TryGetValue(new Vector2Int(x, y), out List<BakedSpineVitPlayer> cellTargets))
                {
                    continue;
                }

                for (int i = 0; i < cellTargets.Count; i++)
                {
                    BakedSpineVitPlayer target = cellTargets[i];
                    if (target == null || !queryVisited.Add(target))
                    {
                        continue;
                    }

                    if (target.isActiveAndEnabled)
                    {
                        results.Add(target);
                    }
                }
            }
        }
    }

    private void NarrowCircle(Vector2 center, float radius, List<BakedSpineVitPlayer> results)
    {
        CopyResultsToCandidates(results);
        for (int i = 0; i < candidateResults.Count; i++)
        {
            BakedSpineVitPlayer target = candidateResults[i];
            if (target != null && BattleCollisionMath.CircleHitsCircle(center, radius, target.transform.position, TargetRadius))
            {
                results.Add(target);
            }
        }
    }

    private void NarrowRect(Vector2 center, Vector2 size, float rotationDeg, List<BakedSpineVitPlayer> results)
    {
        CopyResultsToCandidates(results);
        for (int i = 0; i < candidateResults.Count; i++)
        {
            BakedSpineVitPlayer target = candidateResults[i];
            if (target != null && BattleCollisionMath.RectHitsCircle(center, size, rotationDeg, target.transform.position, TargetRadius))
            {
                results.Add(target);
            }
        }
    }

    private void NarrowSector(Vector2 center, Vector2 forward, float radius, float angleDeg, List<BakedSpineVitPlayer> results)
    {
        CopyResultsToCandidates(results);
        for (int i = 0; i < candidateResults.Count; i++)
        {
            BakedSpineVitPlayer target = candidateResults[i];
            if (target != null && BattleCollisionMath.SectorHitsCircle(center, forward, radius, angleDeg, target.transform.position, TargetRadius))
            {
                results.Add(target);
            }
        }
    }

    private void NarrowCapsuleSegment(Vector2 start, Vector2 end, float width, List<BakedSpineVitPlayer> results)
    {
        CopyResultsToCandidates(results);
        for (int i = 0; i < candidateResults.Count; i++)
        {
            BakedSpineVitPlayer target = candidateResults[i];
            if (target != null && BattleCollisionMath.CapsuleSegmentHitsCircle(start, end, width, target.transform.position, TargetRadius))
            {
                results.Add(target);
            }
        }
    }

    private void CopyResultsToCandidates(List<BakedSpineVitPlayer> results)
    {
        candidateResults.Clear();
        candidateResults.AddRange(results);
        results.Clear();
    }

    private void EnsureGrid()
    {
        if (gridDirty || grid.Count == 0 || rebuildGridEveryFrame && lastRebuildFrame != Time.frameCount)
        {
            RebuildGrid();
        }
    }

    private void InsertTarget(BakedSpineVitPlayer target, Rect aabb)
    {
        Vector2Int minCell = WorldToCell(new Vector2(aabb.xMin, aabb.yMin));
        Vector2Int maxCell = WorldToCell(new Vector2(aabb.xMax, aabb.yMax));
        if (!ClampCellRange(ref minCell, ref maxCell))
        {
            return;
        }

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!grid.TryGetValue(cell, out List<BakedSpineVitPlayer> cellTargets))
                {
                    cellTargets = new List<BakedSpineVitPlayer>(8);
                    grid.Add(cell, cellTargets);
                }

                cellTargets.Add(target);
            }
        }
    }

    private Vector2Int WorldToCell(Vector2 position)
    {
        float safeCellSize = CellSize;
        Vector2 min = GridMin;
        Vector2 localPosition = position - min;
        return new Vector2Int(Mathf.FloorToInt(localPosition.x / safeCellSize), Mathf.FloorToInt(localPosition.y / safeCellSize));
    }

    private bool ClampCellRange(ref Vector2Int minCell, ref Vector2Int maxCell)
    {
        int maxX = GridWidth - 1;
        int maxY = GridHeight - 1;
        if (maxCell.x < 0 || maxCell.y < 0 || minCell.x > maxX || minCell.y > maxY)
        {
            return false;
        }

        minCell.x = Mathf.Clamp(minCell.x, 0, maxX);
        minCell.y = Mathf.Clamp(minCell.y, 0, maxY);
        maxCell.x = Mathf.Clamp(maxCell.x, 0, maxX);
        maxCell.y = Mathf.Clamp(maxCell.y, 0, maxY);
        return true;
    }

    private Vector2 GridMin
    {
        get
        {
            Vector3 position = transform.position;
            Vector2 center = new Vector2(position.x, position.y);
            return center - new Vector2(GridWidth * CellSize, GridHeight * CellSize) * 0.5f;
        }
    }

    private void ClearGridLists()
    {
        foreach (List<BakedSpineVitPlayer> cellTargets in grid.Values)
        {
            cellTargets.Clear();
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGrid && !drawTargetRadius)
        {
            return;
        }

        EnsureGrid();
        if (drawGrid)
        {
            Gizmos.color = gridColor;
            DrawGridGizmos();
        }

        if (drawTargetRadius)
        {
            Gizmos.color = targetRadiusColor;
            for (int i = 0; i < targets.Count; i++)
            {
                BakedSpineVitPlayer target = targets[i];
                if (target != null)
                {
                    DrawWireCircle(target.transform.position, TargetRadius, 24);
                }
            }
        }
    }

    private void DrawGridGizmos()
    {
        float safeCellSize = CellSize;
        int safeWidth = GridWidth;
        int safeHeight = GridHeight;
        Vector2 min = GridMin;
        float z = transform.position.z;

        for (int x = 0; x <= safeWidth; x++)
        {
            float worldX = min.x + x * safeCellSize;
            Gizmos.DrawLine(new Vector3(worldX, min.y, z), new Vector3(worldX, min.y + safeHeight * safeCellSize, z));
        }

        for (int y = 0; y <= safeHeight; y++)
        {
            float worldY = min.y + y * safeCellSize;
            Gizmos.DrawLine(new Vector3(min.x, worldY, z), new Vector3(min.x + safeWidth * safeCellSize, worldY, z));
        }
    }

    private static void DrawWireCircle(Vector3 center, float radius, int segments)
    {
        float safeRadius = Mathf.Max(0f, radius);
        int safeSegments = Mathf.Max(8, segments);
        Vector3 previous = center + new Vector3(safeRadius, 0f, 0f);
        for (int i = 1; i <= safeSegments; i++)
        {
            float angle = i / (float)safeSegments * Mathf.PI * 2f;
            Vector3 current = center + new Vector3(Mathf.Cos(angle) * safeRadius, Mathf.Sin(angle) * safeRadius, 0f);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }
}
