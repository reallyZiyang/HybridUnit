# 04. GameObject 可视化验证版

正式版最终会走 Manager 和数组，但直接从数组网格开始很难调试。当前项目已有一套 GameObject 验证工具，适合作为学习和调参入口。

## 本章目标

理解当前验证版的职责：

```text
BattleCollisionWorld 维护目标和网格
Detector GameObject 描述一个形状
Scene Gizmos 显示网格和形状
命中时让 Spine 变色
```

它不是最终性能方案。

## 当前结构

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

## World 职责

`BattleCollisionWorld` 做三件事：

```text
扫描 BakedSpineVitPlayer
每帧或 dirty 时重建网格
提供 QueryCircle/QueryRect/QuerySector/QueryCapsuleSegment
```

核心流程：

```csharp
public void QueryCircle(Vector2 center, float radius, List<BakedSpineVitPlayer> results)
{
    CollectCandidates(BattleCollisionMath.Expand(BattleCollisionMath.CircleAabb(center, radius), TargetRadius), results);
    NarrowCircle(center, radius, results);
}
```

这已经体现了正式系统的核心思想：

```text
AABB 找候选
形状数学精确判断
```

## Detector 职责

每个 Detector 都是调试包装：

```csharp
protected override void Query(BattleCollisionWorld world, List<BakedSpineVitPlayer> results)
{
    world.QueryCircle(Position2D(transform), radius, results);
}
```

它适合 Scene 视图拖动、旋转、调参数。正式战斗里不应该每个技能或抛射物都挂一个 Detector。

## 命中变色为什么只能用于调试

验证版会调用：

```csharp
SpineVitColorController.GetOrAdd(player).SetColor(hitColor);
```

这对验证很直观，但正式碰撞层不能依赖表现层。正式版应该只输出 target index：

```text
CollisionManager -> target index
CombatSystem -> 伤害结算
RenderSystem -> 受击颜色、特效、飘字
```

## 验收标准

- Scene 视图能看到网格。
- 能拖动圆、矩形、扇形、胶囊检测器。
- 命中目标变色，离开后恢复。
- 关闭 Gizmos 后，验证工具不影响正式性能判断。
- 明确知道这套 GameObject 工具只是调试层。
