# 00. 空间划分课程总览

这套课程的目标是从零实现一套适合 2D 战斗的空间划分碰撞系统。最终结果不是依赖 `Physics2D`，也不是每个抛射物挂一个检测脚本，而是一个数据化的 `BattleCollisionManager`：

```text
单位数据注册到 Manager
Manager 每帧维护空间网格
技能和抛射物向 Manager 发起形状查询
Manager 返回 target index
战斗逻辑处理伤害
表现层播放 Spine 受击色、特效、飘字
```

## 本章目标

理解最终要做成什么，以及为什么要按课程逐步实现。

最终能力：

- 支持大量单位。
- 支持圆、矩形、扇形、胶囊线段查询。
- 支持抛射物 sweep 检测，避免高速穿透。
- 支持阵营、状态、地面/空中层级过滤。
- 支持无 GC 查询。
- 支持大范围技能 fallback。
- 支持 Scene 视图调试，但正式运行路径不依赖 Gizmos 或 GameObject Detector。

## 为什么不用 Physics2D

战斗技能检测通常不是严格物理模拟，而是规则化查询：

```text
圆形范围技能
矩形范围技能
扇形近战技能
有宽度的子弹飞行路径
光束路径
```

这些查询只需要回答“哪些目标被命中”，不需要刚体、摩擦、反弹、接触点、碰撞回调。大量单位和抛射物下，自己维护数据结构更容易做到稳定无 GC，也更容易和 Manager 化渲染系统配合。

## 课程路线

```text
O(N) 暴力检测
  -> 形状数学
  -> AABB broad phase
  -> GameObject 可视化验证
  -> 固定网格概念
  -> 数组网格正式实现
  -> 无 GC 查询 buffer
  -> 目标注册和过滤
  -> 抛射物和技能批量查询
  -> 大范围 fallback
  -> 调试和压测
  -> 最终接入战斗和表现层
```

## 当前项目关系

当前项目已有验证版：

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
```

这套代码适合看网格、拖拽形状、验证命中颜色。课程前半段会利用它解释概念。后半段会把正式运行时改成数组 Manager，不把 `SpineVitColorController`、Gizmos、Detector GameObject 放进正式路径。

## 最终接口目标

课程最终会落到这组接口：

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

public struct BattleCollisionQueryOptions
{
    public int campMask;
    public int stateMask;
    public int layerMask;
    public int maxHits;
    public bool sortByDistance;
}

public sealed class BattleCollisionQueryBuffer
{
    public int Count;
    public int[] TargetIndices;
}

public sealed class BattleCollisionManager
{
    public int RegisterTarget(Vector2 position, float radius, int camp, int state, int layer, int renderHandle);
    public void UnregisterTarget(int targetId);
    public void UpdateTargetPosition(int targetId, Vector2 position);
    public int Query(in BattleCollisionShape shape, in BattleCollisionQueryOptions options, BattleCollisionQueryBuffer buffer);
}
```

## 验收标准

学完整套课程后，应该能做到：

- 用暴力检测验证数组网格查询结果正确。
- 同一个目标跨多个 cell 时不会重复命中。
- 查询阶段没有每帧 GC。
- 高速子弹不会穿透目标。
- 大范围技能不会因为扫大量 cell 反而更慢。
- 碰撞层不直接依赖 Spine、飘字、特效或伤害结算。
