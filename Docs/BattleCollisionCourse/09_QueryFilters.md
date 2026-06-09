# 09. 查询过滤

大量单位下，很多候选目标从业务上就不可能被命中。例如友军、死亡单位、空中单位。它们应该尽早过滤。

## 本章目标

实现查询过滤：

```text
campMask
stateMask
layerMask
maxHits
sortByDistance
```

## QueryOptions

```csharp
public struct BattleCollisionQueryOptions
{
    public int campMask;
    public int stateMask;
    public int layerMask;
    public int maxHits;
    public bool sortByDistance;
}
```

建议约定：

```text
mask = 0 表示不过滤
maxHits <= 0 表示不限制
```

## 过滤位置

过滤应该尽量放在 narrow phase 前：

```csharp
if (!PassFilter(targetIndex, options))
{
    continue;
}

if (!NarrowPhase(shape, targetIndex))
{
    continue;
}

buffer.TryAdd(targetIndex);
```

原因是阵营和状态判断比形状数学便宜。

## PassFilter

```csharp
private bool PassFilter(int targetIndex, in BattleCollisionQueryOptions options)
{
    if (!active[targetIndex])
    {
        return false;
    }

    if (options.campMask != 0 && (options.campMask & (1 << camps[targetIndex])) == 0)
    {
        return false;
    }

    if (options.stateMask != 0 && (options.stateMask & states[targetIndex]) == 0)
    {
        return false;
    }

    if (options.layerMask != 0 && (options.layerMask & (1 << layers[targetIndex])) == 0)
    {
        return false;
    }

    return true;
}
```

`state` 通常是位标记，例如：

```text
Alive
Dead
Invincible
Selectable
```

## maxHits

如果不排序，`maxHits` 可以在添加结果后立即停止：

```csharp
if (options.maxHits > 0 && buffer.Count >= options.maxHits)
{
    return buffer.Count;
}
```

如果需要 `sortByDistance`，必须先收集候选命中，再排序，再裁剪。排序会增加成本，不要默认开启。

## 验收标准

- 只命中敌方，不命中友方。
- 死亡或不可选中单位不会进入结果。
- 地面技能不命中空中单位。
- `maxHits = 1` 时只返回一个目标。
- `sortByDistance = true` 时最近目标排在前面。
