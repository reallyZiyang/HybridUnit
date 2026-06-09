# 07. 无 GC 查询 Buffer

正式碰撞查询不能每次创建 `List` 或 `HashSet`。这一章实现复用结果数组和 stamp 去重。

## 本章目标

实现：

```text
BattleCollisionQueryBuffer
queryStamp[targetIndex]
currentQueryId
```

用它们替代：

```text
new List<int>()
HashSet<int>
```

## 查询结果 Buffer

```csharp
public sealed class BattleCollisionQueryBuffer
{
    public int Count;
    public int[] TargetIndices;

    public BattleCollisionQueryBuffer(int capacity)
    {
        TargetIndices = new int[Mathf.Max(1, capacity)];
        Count = 0;
    }

    public void Clear()
    {
        Count = 0;
    }

    public bool TryAdd(int targetIndex)
    {
        if (Count >= TargetIndices.Length)
        {
            return false;
        }

        TargetIndices[Count++] = targetIndex;
        return true;
    }
}
```

第一版容量满了可以直接停止添加。后续如果业务需要，可以提供显式扩容，但不要在每帧查询里隐式扩容。

## Stamp 去重

目标跨多个 cell 时，同一次查询可能重复访问同一个 target。验证版用 `HashSet`，正式版用 stamp。

```csharp
private readonly int[] queryStamp;
private int currentQueryId;
```

每次查询开始：

```csharp
currentQueryId++;
if (currentQueryId == int.MaxValue)
{
    Array.Clear(queryStamp, 0, queryStamp.Length);
    currentQueryId = 1;
}
```

访问目标：

```csharp
if (queryStamp[targetIndex] == currentQueryId)
{
    continue;
}

queryStamp[targetIndex] = currentQueryId;
```

## 查询流程

```text
buffer.Clear()
currentQueryId++
遍历 AABB 覆盖 cell
遍历 cell 链表
stamp 去重
过滤 active
narrow phase
写入 buffer
```

## 常见错误

- 忘记处理 `currentQueryId` 溢出。
- buffer 满了还继续写，导致数组越界。
- 查询函数内部 `new BattleCollisionQueryBuffer`。
- 查询返回 `IEnumerable`，隐藏分配和迭代器成本。

## 验收标准

- 同一个目标跨多个 cell 时，只出现一次。
- 查询 1000 次没有 GC Alloc。
- buffer 容量不足时不会崩溃。
- Profiler 中查询阶段没有 `HashSet` 或 `List` 扩容。
