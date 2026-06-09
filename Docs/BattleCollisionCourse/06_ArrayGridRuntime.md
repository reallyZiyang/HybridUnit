# 06. 正式数组网格

验证版使用：

```text
Dictionary<Vector2Int, List<BakedSpineVitPlayer>>
```

它容易写、容易看，但不适合作为最终大量单位运行时。正式版用数组。

## 本章目标

实现 `BattleUniformGrid`：

```text
cellHeads[cellIndex]
nextInCell[targetIndex]
targetPositions[targetIndex]
targetRadii[targetIndex]
```

第一版每帧全量重建。不要一开始就做增量更新。

## 数据结构

```csharp
public sealed class BattleUniformGrid
{
    private readonly int[] cellHeads;
    private readonly int[] nextInCell;
    private readonly Vector2[] targetPositions;
    private readonly float[] targetRadii;

    private readonly int gridWidth;
    private readonly int gridHeight;
    private readonly float cellSize;
    private readonly Vector2 gridMin;
}
```

`cellHeads` 存每个 cell 的链表头：

```text
cellHeads[cellIndex] = targetIndex
```

`nextInCell` 存同一个 cell 里的下一个目标：

```text
nextInCell[targetIndex] = nextTargetIndex
```

空值用 `-1`。

## 清空网格

```csharp
for (int i = 0; i < cellHeads.Length; i++)
{
    cellHeads[i] = -1;
}
```

注意只清 `cellHeads`。`nextInCell` 会在插入时覆盖。

## 插入目标

```csharp
private void InsertToCell(int cellIndex, int targetIndex)
{
    nextInCell[targetIndex] = cellHeads[cellIndex];
    cellHeads[cellIndex] = targetIndex;
}
```

这是单向链表插入头部，复杂度 O(1)。

## 为什么第一版全量重建

全量重建流程：

```text
清空 cellHeads
遍历所有 active target
计算 target AABB
插入覆盖到的 cell
```

优点：

- 逻辑简单。
- 不需要记录旧 cell range。
- 单位移动、启用、禁用都不容易出错。
- 对移动端来说，数组线性遍历通常比复杂增量维护更稳定。

增量更新可以作为后续优化，不放在第一版。

## 验收标准

- 目标能插入正确 cell。
- 一个目标跨多个 cell 时，每个 cell 链表都能找到它。
- 清空后重新插入，不会残留旧目标。
- 1000 个目标重建时没有 GC Alloc。
