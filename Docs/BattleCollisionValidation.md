# Battle Collision Validation

本文档说明当前测试用 2D 战斗碰撞检测工具的设计思路和核心实现。它的目标不是替代完整战斗系统，而是先验证“同屏大量 Spine 单位 + 多形状检测 + Scene 视图可调试”的性能路径。

相关代码：

```text
Assets/Scripts/Core/Runtime/Battle/Collision/
  BattleCollisionWorld.cs
  BattleCollisionDetectorBase.cs
  BattleCollisionMath.cs
  CircleCollisionDetector.cs
  RectCollisionDetector.cs
  SectorCollisionDetector.cs
  CapsuleSegmentCollisionDetector.cs
  SpineVitColorController.cs

Assets/Scripts/Core/Runtime/Rendering/SpineVit/BakedSpineVitPlayer.cs
```

## 1. 为什么不用 Physics2D

对标同屏大量单位和抛射物时，高频碰撞通常不是“少量刚体之间的真实物理”，而是大量规则化查询：

- 圆形范围技能。
- 矩形范围技能。
- 扇形近战或技能。
- 有宽度的抛射物扫掠线段。

这些查询只需要回答“哪些单位在范围内”，不需要刚体解算、接触点、碰撞回调和物理材质。因此当前方案采用自研 2D 逻辑检测：

```text
形状 AABB
  -> 空间网格 broad phase 找候选单位
  -> 几何 narrow phase 精确判断
  -> 命中单位变色，离开后恢复
```

第一版目标只检测 `BakedSpineVitPlayer`，并用统一半径代表单位碰撞范围。

## 2. World：固定范围空间网格

`BattleCollisionWorld` 是碰撞世界。它维护目标列表和空间哈希网格。

当前网格参数：

```csharp
[Header("Grid")]
[SerializeField, Min(0.01f)] private float cellSize = 2f;
[SerializeField, Min(1)] private int gridWidth = 20;
[SerializeField, Min(1)] private int gridHeight = 12;
[SerializeField, Min(0f)] private float targetRadius = 0.45f;
```

含义：

- `cellSize`：每个格子的世界尺寸。
- `gridWidth`：横向格子数量。
- `gridHeight`：纵向格子数量。
- `targetRadius`：所有 Spine 目标统一半径。

网格以 `BattleCollisionWorld.transform.position` 为中心：

```csharp
private Vector2 GridMin
{
    get
    {
        Vector3 position = transform.position;
        Vector2 center = new Vector2(position.x, position.y);
        return center - new Vector2(GridWidth * CellSize, GridHeight * CellSize) * 0.5f;
    }
}
```

这样做的原因是调试直观：移动 World 物体就能移动整块碰撞网格，Scene 视图也能看到固定长宽的网格范围。

## 3. 目标如何进网格

World 启用时会扫描场景里的 `BakedSpineVitPlayer`：

```csharp
[ContextMenu("Refresh Targets")]
public void RefreshTargets()
{
    targets.Clear();
    BakedSpineVitPlayer[] foundTargets = FindObjectsOfType<BakedSpineVitPlayer>();
    for (int i = 0; i < foundTargets.Length; i++)
    {
        Register(foundTargets[i]);
    }
}
```

运行时每帧或查询前重建网格。第一版选择“每帧清空并重建”，牺牲一点 CPU，换取逻辑稳定和调试简单：

```csharp
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
```

每个目标是一个圆形代理，所以先用 `targetRadius` 生成 AABB，再把目标插入覆盖到的 cell：

```csharp
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
```

`ClampCellRange` 会把查询或目标插入限制在固定网格范围内，超出网格的对象不参与检测。这让 Scene 视图里看到的网格范围和实际查询范围一致。

## 4. Broad Phase：先找候选，再精确判断

以圆形检测为例，外部调用：

```csharp
public void QueryCircle(Vector2 center, float radius, List<BakedSpineVitPlayer> results)
{
    CollectCandidates(BattleCollisionMath.Expand(BattleCollisionMath.CircleAabb(center, radius), TargetRadius), results);
    NarrowCircle(center, radius, results);
}
```

这里有两个关键点：

- 查询形状先转成 AABB。
- AABB 会额外扩展 `TargetRadius`，避免目标圆心在形状外但圆边缘相交时被漏掉。

候选收集只访问 AABB 覆盖到的 cell：

```csharp
private void CollectCandidates(Rect aabb, List<BakedSpineVitPlayer> results)
{
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
```

`queryVisited` 用来去重。因为一个目标圆可能跨多个 cell，同一次查询里不能重复命中。

## 5. Narrow Phase：不同形状的数学检测

### 圆形

圆形检测就是两个圆的距离判断：

```csharp
public static bool CircleHitsCircle(Vector2 center, float radius, Vector2 targetCenter, float targetRadius)
{
    float combinedRadius = Mathf.Max(0f, radius) + Mathf.Max(0f, targetRadius);
    return (targetCenter - center).sqrMagnitude <= combinedRadius * combinedRadius;
}
```

这里用平方距离，避免 `sqrt`。

### 旋转矩形

矩形检测先把目标点转到矩形本地坐标，再算点到 AABB 的距离：

```csharp
public static bool RectHitsCircle(Vector2 center, Vector2 size, float rotationDeg, Vector2 targetCenter, float targetRadius)
{
    Vector2 halfSize = new Vector2(Mathf.Max(0f, size.x) * 0.5f, Mathf.Max(0f, size.y) * 0.5f);
    Quaternion inverseRotation = Quaternion.Inverse(Quaternion.Euler(0f, 0f, rotationDeg));
    Vector2 localPoint = inverseRotation * (targetCenter - center);
    float dx = Mathf.Max(Mathf.Abs(localPoint.x) - halfSize.x, 0f);
    float dy = Mathf.Max(Mathf.Abs(localPoint.y) - halfSize.y, 0f);
    return dx * dx + dy * dy <= targetRadius * targetRadius;
}
```

如果目标圆心在矩形内，`dx/dy` 都是 0，必定命中。若在外部，则判断圆到矩形边界的最短距离。

### 扇形

扇形先做半径判断，再做角度判断：

```csharp
public static bool SectorHitsCircle(Vector2 center, Vector2 forward, float radius, float angleDeg, Vector2 targetCenter, float targetRadius)
{
    Vector2 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
    Vector2 toTarget = targetCenter - center;
    float distance = toTarget.magnitude;
    float safeRadius = Mathf.Max(0f, radius);
    float safeTargetRadius = Mathf.Max(0f, targetRadius);
    if (distance > safeRadius + safeTargetRadius)
    {
        return false;
    }

    if (distance <= safeTargetRadius)
    {
        return true;
    }

    float halfAngle = Mathf.Clamp(angleDeg * 0.5f, 0f, 180f);
    float targetAngleAllowance = Mathf.Asin(Mathf.Clamp01(safeTargetRadius / Mathf.Max(0.0001f, distance))) * Mathf.Rad2Deg;
    return Vector2.Angle(safeForward, toTarget) <= halfAngle + targetAngleAllowance;
}
```

`targetAngleAllowance` 是为了考虑目标圆半径。否则目标圆边缘已经碰到扇形边界，但圆心略在角度外时会被漏判。

### 有宽度线段

有宽度的两点线段本质是 capsule。先求目标圆心到线段的最近距离，再和半径比较：

```csharp
public static bool CapsuleSegmentHitsCircle(Vector2 start, Vector2 end, float width, Vector2 targetCenter, float targetRadius)
{
    float radius = Mathf.Max(0f, width) * 0.5f + Mathf.Max(0f, targetRadius);
    float distanceSqr = DistancePointToSegmentSqr(targetCenter, start, end);
    return distanceSqr <= radius * radius;
}
```

这类检测适合高速抛射物：用上一帧位置到当前帧位置作为线段，可以避免子弹一帧跨过单位导致穿透。

## 6. Detector：挂在 GameObject 上的形状脚本

所有形状脚本都继承 `BattleCollisionDetectorBase`。基类负责：

- 每帧检测。
- 保存当前命中和上帧命中。
- 离开范围时恢复颜色。
- Scene 视图绘制 Gizmos。

核心流程：

```csharp
[ContextMenu("Detect Once")]
public void Detect()
{
    BattleCollisionWorld world = BattleCollisionWorld.FindWorld();
    if (world == null)
    {
        return;
    }

    Query(world, queryResults);
    ApplyHits(queryResults);
}
```

具体形状只需要实现 `Query` 和 `DrawShapeGizmos`。例如圆形检测器：

```csharp
public sealed class CircleCollisionDetector : BattleCollisionDetectorBase
{
    [SerializeField, Min(0f)] private float radius = 1f;

    protected override void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results)
    {
        world.QueryCircle(Position2D(transform), radius, results);
    }

    protected override void DrawShapeGizmos()
    {
        DrawWireCircle(Position2D(transform), radius, transform.position.z);
    }
}
```

矩形使用 `transform.eulerAngles.z` 作为旋转；扇形使用 `transform.right` 作为朝向；有宽度线段使用 `localStart/localEnd` 经 `TransformPoint` 转到世界坐标。

## 7. 命中颜色和恢复

检测器的颜色逻辑在 `ApplyHits`：

```csharp
private void ApplyHits(List<BakedSpineVitPlayer> hits)
{
    currentHits.Clear();
    currentHitSet.Clear();
    for (int i = 0; i < hits.Count; i++)
    {
        BakedSpineVitPlayer player = hits[i];
        if (player != null && currentHitSet.Add(player))
        {
            currentHits.Add(player);
        }
    }

    for (int i = 0; i < previousHits.Count; i++)
    {
        BakedSpineVitPlayer player = previousHits[i];
        if (player != null && !currentHitSet.Contains(player))
        {
            SpineVitColorController controller = SpineVitColorController.GetOrAdd(player);
            if (controller != null)
            {
                controller.RestoreOriginalColor();
            }
        }
    }

    for (int i = 0; i < currentHits.Count; i++)
    {
        SpineVitColorController controller = SpineVitColorController.GetOrAdd(currentHits[i]);
        if (controller != null)
        {
            controller.SetColor(hitColor);
        }
    }

    previousHits.Clear();
    previousHits.AddRange(currentHits);
}
```

规则：

- 当前帧命中的目标设置为检测器 `hitColor`。
- 上帧命中但当前帧未命中的目标恢复原色。
- 多个检测器同时命中同一个目标时，后执行的检测器颜色覆盖前一个。

`SpineVitColorController` 会缓存原始颜色：

```csharp
private void CaptureOriginalColor()
{
    if (hasOriginalColor || player == null)
    {
        return;
    }

    originalColor = player.InstanceColor;
    hasOriginalColor = true;
}
```

颜色最终通过 `BakedSpineVitPlayer.SetInstanceColor` 写入 MPB：

```csharp
public void SetInstanceColor(Color instanceColor)
{
    color = instanceColor;
    ApplyFrame(previewFrame);
}
```

这样不会复制材质，仍然沿用 Spine VIT 当前的 `_InstanceColor` 实例参数。

## 8. Scene 视图调试

World 负责画网格：

```csharp
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
```

Detector 负责画自己的形状。命中时用 `hitColor`，未命中时用普通 gizmo 颜色：

```csharp
private void OnDrawGizmos()
{
    if (!drawGizmos)
    {
        return;
    }

    Gizmos.color = HasHits ? hitColor : gizmoColor;
    DrawShapeGizmos();
}
```

这样调试时能同时看到：

- 世界网格覆盖范围。
- 每个检测器的真实形状。
- 当前是否有命中。
- 被命中的 Spine 颜色变化。

## 9. 当前限制和后续方向

当前限制：

- 只检测 `BakedSpineVitPlayer`。
- 所有目标使用统一半径。
- 不包含阵营、伤害、穿透次数、命中冷却。
- 不做 Physics2D 碰撞回调。
- 固定使用 XY 平面。

后续可以扩展：

- 抽 `BattleHitTarget`，让非 Spine 单位也能注册。
- 每个目标独立半径或 capsule。
- 检测器增加阵营过滤、最大命中数、命中间隔。
- 抛射物 Manager 批量更新并调用 `QueryCapsuleSegment`。
- 将 World 的目标注册改为显式注册，避免运行时扫描。
