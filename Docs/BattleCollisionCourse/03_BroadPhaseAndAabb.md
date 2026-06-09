# 03. Broad Phase 和 AABB

暴力检测会对每个目标都做精确形状判断。空间划分的第一步是把检测拆成两层：

```text
Broad Phase: 快速找候选
Narrow Phase: 精确判断
```

## 本章目标

理解为什么所有查询形状都先转成 AABB，并且为什么 AABB 要扩展目标半径。

## AABB 是什么

AABB 是轴对齐包围盒：

```text
xMin, yMin, xMax, yMax
```

它不旋转，计算便宜，适合用于粗筛。

圆形 AABB：

```csharp
public static Rect CircleAabb(Vector2 center, float radius)
{
    float safeRadius = Mathf.Max(0f, radius);
    Vector2 size = Vector2.one * (safeRadius * 2f);
    return new Rect(center - Vector2.one * safeRadius, size);
}
```

胶囊线段 AABB：

```csharp
public static Rect CapsuleSegmentAabb(Vector2 start, Vector2 end, float width)
{
    float radius = Mathf.Max(0f, width) * 0.5f;
    float minX = Mathf.Min(start.x, end.x) - radius;
    float minY = Mathf.Min(start.y, end.y) - radius;
    float maxX = Mathf.Max(start.x, end.x) + radius;
    float maxY = Mathf.Max(start.y, end.y) + radius;
    return Rect.MinMaxRect(minX, minY, maxX, maxY);
}
```

## 为什么要 Expand targetRadius

假设查询圆半径是 2，目标半径是 0.5。目标圆心可能在查询 AABB 外，但目标边缘已经碰到查询形状。

所以 broad phase 使用：

```csharp
public static Rect Expand(Rect rect, float amount)
{
    float safeAmount = Mathf.Max(0f, amount);
    return Rect.MinMaxRect(
        rect.xMin - safeAmount,
        rect.yMin - safeAmount,
        rect.xMax + safeAmount,
        rect.yMax + safeAmount);
}
```

查询时：

```csharp
Rect queryAabb = BattleCollisionMath.CircleAabb(center, radius);
Rect broadAabb = BattleCollisionMath.Expand(queryAabb, targetRadius);
```

## BattleCollisionShape

正式版不要为每种技能写不同查询入口，可以统一形状数据：

```csharp
public enum BattleCollisionShapeType
{
    Circle,
    Rect,
    Sector,
    CapsuleSegment
}

public struct BattleCollisionShape
{
    public BattleCollisionShapeType type;
    public Vector2 center;
    public Vector2 direction;
    public Vector2 size;
    public Vector2 start;
    public Vector2 end;
    public float radius;
    public float angleDeg;
    public float width;
}
```

`BattleCollisionShape` 的职责是描述一次查询，不负责保存结果。

## 验收标准

- 每种形状都能生成 AABB。
- AABB 扩展目标半径后，边缘相交目标不会漏掉。
- broad phase 可以多返回候选，但不能少返回。
- narrow phase 负责剔除 AABB 内但形状外的目标。
