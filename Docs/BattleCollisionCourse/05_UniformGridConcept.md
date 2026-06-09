# 05. 固定网格概念

固定网格是最直接的空间划分方案。它把战斗区域切成很多 cell，每个目标根据自己的位置和半径插入覆盖到的 cell。

## 本章目标

理解这些参数：

```text
cellSize
gridWidth
gridHeight
GridMin
WorldToCell
```

## 网格范围

当前验证版用 `BattleCollisionWorld.transform.position` 作为网格中心：

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

这样设计方便调试。移动 World 物体就能移动整块网格。

正式版也可以保留这个概念，但不要依赖 Transform。正式版更适合存成纯数据：

```csharp
public Vector2 GridMin;
public float CellSize;
public int GridWidth;
public int GridHeight;
```

## 世界坐标转 cell

```csharp
private Vector2Int WorldToCell(Vector2 position)
{
    Vector2 localPosition = position - GridMin;
    return new Vector2Int(
        Mathf.FloorToInt(localPosition.x / CellSize),
        Mathf.FloorToInt(localPosition.y / CellSize));
}
```

`FloorToInt` 的含义是：

```text
0.0 ~ 0.999 -> cell 0
1.0 ~ 1.999 -> cell 1
```

## AABB 覆盖 cell

一个目标不是只插入一个 cell，而是根据目标圆 AABB 插入所有覆盖 cell：

```csharp
Vector2Int minCell = WorldToCell(new Vector2(aabb.xMin, aabb.yMin));
Vector2Int maxCell = WorldToCell(new Vector2(aabb.xMax, aabb.yMax));
```

然后遍历：

```csharp
for (int y = minCell.y; y <= maxCell.y; y++)
{
    for (int x = minCell.x; x <= maxCell.x; x++)
    {
        InsertToCell(x, y, targetIndex);
    }
}
```

## cellSize 怎么选

没有永远正确的 cellSize。

```text
cellSize 太大：每格目标多，候选多，narrow phase 变重
cellSize 太小：目标跨格多，插入和查询格子多
```

建议第一版：

```text
cellSize = 1 到 2 倍常规单位直径
```

如果目标半径是 `0.45`，常规单位直径约 `0.9`，可以从：

```text
cellSize = 1.5 或 2
```

开始调。

## 验收标准

- 给定坐标能算出正确 cell。
- AABB 覆盖多个 cell 时，所有 cell 都被插入。
- 超出网格范围的目标能被正确忽略或裁剪。
- 改 `cellSize` 后，候选数量变化符合预期。
