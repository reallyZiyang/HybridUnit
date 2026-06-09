# 02. 形状数学

这一章只做 narrow phase，也就是“候选目标已经找到了，如何精确判断是否命中”。

## 本章目标

实现四种目标圆检测：

```text
圆 vs 目标圆
旋转矩形 vs 目标圆
扇形 vs 目标圆
胶囊线段 vs 目标圆
```

所有单位第一版都用圆代理，这样目标形状统一，查询形状可以多样化。

## 圆形

圆形检测是最基础版本：

```csharp
public static bool CircleHitsCircle(Vector2 center, float radius, Vector2 targetCenter, float targetRadius)
{
    float combinedRadius = Mathf.Max(0f, radius) + Mathf.Max(0f, targetRadius);
    return (targetCenter - center).sqrMagnitude <= combinedRadius * combinedRadius;
}
```

关键点：

- 使用平方距离。
- 查询半径和目标半径相加。

## 旋转矩形

做法是把目标点转换到矩形本地坐标，再计算点到本地 AABB 的距离。

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

关键点：

- 不是旋转矩形去贴目标，而是把目标点反向旋转回矩形局部空间。
- 点在矩形内部时 `dx/dy` 为 0。

## 扇形

扇形检测先判断距离，再判断角度。

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

`targetAngleAllowance` 的作用是把目标半径考虑进去。否则目标圆边缘已经进入扇形，但圆心在角度外时会漏检。

## 胶囊线段

胶囊线段适合子弹 sweep、光束、路径伤害。

```csharp
public static bool CapsuleSegmentHitsCircle(Vector2 start, Vector2 end, float width, Vector2 targetCenter, float targetRadius)
{
    float radius = Mathf.Max(0f, width) * 0.5f + Mathf.Max(0f, targetRadius);
    float distanceSqr = DistancePointToSegmentSqr(targetCenter, start, end);
    return distanceSqr <= radius * radius;
}

private static float DistancePointToSegmentSqr(Vector2 point, Vector2 start, Vector2 end)
{
    Vector2 segment = end - start;
    float lengthSqr = segment.sqrMagnitude;
    if (lengthSqr <= 0.000001f)
    {
        return (point - start).sqrMagnitude;
    }

    float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
    Vector2 closest = start + segment * t;
    return (point - closest).sqrMagnitude;
}
```

关键点：

- 子弹半径用 `width * 0.5f`。
- 目标半径加到胶囊半径里。
- 线段长度接近 0 时退化成圆。

## 验收标准

- 圆形、矩形、扇形、胶囊线段都能命中目标圆边缘。
- 旋转矩形旋转 45 度后，命中范围和可视化一致。
- 扇形背后目标不命中，角度边缘目标不漏检。
- 胶囊线段宽度外目标不命中，端点圆范围内目标命中。
