# 01. 从 O(N) 暴力检测开始

空间划分不是第一步。第一步是先写一个最简单、最容易验证正确性的版本：遍历所有单位，逐个判断是否命中。

## 本章目标

实现一个圆形查询：

```text
给定查询圆 center/radius
遍历所有目标圆 targetPosition/targetRadius
返回所有相交目标
```

这个版本性能不是目标。它的价值是作为后续网格查询的正确性基准。

## 原理

两个圆相交，只需要判断圆心距离是否小于半径之和：

```text
distance <= radiusA + radiusB
```

实际代码里不要用 `Vector2.Distance`，因为它内部会开平方。比较平方距离即可：

```csharp
public static bool CircleHitsCircle(Vector2 center, float radius, Vector2 targetCenter, float targetRadius)
{
    float combinedRadius = Mathf.Max(0f, radius) + Mathf.Max(0f, targetRadius);
    return (targetCenter - center).sqrMagnitude <= combinedRadius * combinedRadius;
}
```

## 要实现什么

先准备一组最小目标数据：

```csharp
public struct SimpleTarget
{
    public int id;
    public Vector2 position;
    public float radius;
    public bool active;
}
```

暴力查询：

```csharp
public static int QueryCircleBruteForce(
    SimpleTarget[] targets,
    int targetCount,
    Vector2 center,
    float radius,
    int[] results)
{
    int count = 0;
    for (int i = 0; i < targetCount; i++)
    {
        SimpleTarget target = targets[i];
        if (!target.active)
        {
            continue;
        }

        if (!CircleHitsCircle(center, radius, target.position, target.radius))
        {
            continue;
        }

        results[count++] = target.id;
    }

    return count;
}
```

这里先不处理结果数组容量。正式版会在第 7 章统一设计 query buffer。

## 常见错误

- 用 `Distance` 做大量查询，导致不必要的 `sqrt`。
- 忘记加目标半径，只判断目标圆心是否在查询圆内。
- 查询函数里 `new List<int>()`，后续很难做到无 GC。
- 过早优化网格，导致形状数学错了也难定位。

## 验收标准

- 3 个目标中，查询圆能返回正确命中的 id。
- 目标圆心在查询圆外，但边缘相交时，也能命中。
- 关闭一个目标的 `active` 后，它不会出现在结果里。
- 后续所有优化版查询都能和这个暴力版本对比结果。
