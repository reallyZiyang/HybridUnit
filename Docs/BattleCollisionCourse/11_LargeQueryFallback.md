# 11. 大范围查询 Fallback

空间划分不是所有情况下都更快。全屏技能或超大范围技能会覆盖大量 cell，这时走网格反而可能多做去重和 cell 遍历。

## 本章目标

实现大范围查询 fallback：

```text
当查询覆盖 cell 数超过阈值
直接遍历 active targets
```

## 为什么需要 fallback

普通查询：

```text
覆盖少量 cell
候选远少于全体单位
网格收益明显
```

大范围查询：

```text
覆盖大半地图
几乎所有单位都是候选
还要遍历大量 cell 和做 stamp 去重
```

这时直接遍历 active targets 更简单。

## 触发条件

先计算查询 AABB 覆盖 cell 数：

```csharp
int coveredCells = (maxCell.x - minCell.x + 1) * (maxCell.y - minCell.y + 1);
int totalCells = gridWidth * gridHeight;
```

建议第一版阈值：

```text
coveredCells > totalCells * 0.35
```

或者：

```text
coveredCells > 128
```

两者取更小的限制也可以。课程第一版建议用比例阈值，方便不同网格尺寸复用。

## Fallback 流程

```text
buffer.Clear()
遍历所有 targetIndex
过滤 active/camp/state/layer
narrow phase
写入 buffer
maxHits 处理
```

注意 fallback 仍然要走相同的 narrow phase 和 query options。

## 什么时候不要 fallback

抛射物 sweep 通常覆盖 cell 不大，不需要 fallback。  
fallback 主要服务：

```text
全屏技能
超大圆形技能
超长光束
大范围治疗
```

## 验收标准

- 大圆技能触发 fallback。
- 小范围技能仍走网格。
- fallback 查询结果和暴力检测一致。
- 大范围查询耗时不明显高于直接遍历 active targets。
