# 10. 抛射物和技能

正式战斗里，不应该每个抛射物都是一个 GameObject Detector。抛射物应该由 Manager 批量推进，再向碰撞 Manager 批量查询。

## 本章目标

理解三类常见检测：

```text
抛射物：capsule sweep
光束：细胶囊或 beam shape
范围技能：circle/sector/rect
```

## 抛射物为什么要 sweep

如果只检测当前帧位置，高速子弹可能直接跨过目标：

```text
上一帧在目标左边
下一帧在目标右边
当前点没有落在目标圆内
结果漏检
```

正确做法是使用上一帧位置到当前帧位置的胶囊线段：

```text
previousPosition -> currentPosition
width = projectileRadius * 2
```

## ProjectileManager 流程

```text
遍历 active projectiles
保存 previousPosition
根据 velocity 推进 currentPosition
生成 capsule segment shape
调用 CollisionManager.Query
处理命中、穿透、销毁
```

核心形状：

```csharp
BattleCollisionShape shape = new BattleCollisionShape
{
    type = BattleCollisionShapeType.CapsuleSegment,
    start = previousPosition,
    end = currentPosition,
    width = projectileRadius * 2f
};
```

## 穿透和 maxHits

穿透子弹可以设置：

```text
maxHits = pierceCount
sortByDistance = true
```

这样沿路径最近的目标先处理。

不需要排序的范围技能不要开启 `sortByDistance`。

## 光束

光束可以用细胶囊：

```text
start = firePoint
end = hitPoint
width = beamWidth
```

如果要做“线段末端快速从开火点移动到命中点”，表现层播放光束动画，碰撞层仍然只关心当前实际检测段。

## 范围技能

范围技能使用：

```text
Circle: 爆炸、治疗圈
Sector: 近战挥砍、扇形喷吐
Rect: 横扫、矩形路径伤害
```

范围技能通常低频触发，不需要每帧查询。持续范围技能可以按固定 tick 间隔查询，例如每 0.1 秒一次。

## 验收标准

- 高速子弹不会穿透目标。
- 穿透子弹按距离顺序处理命中。
- 光束能命中线段上的目标。
- 圆、扇形、矩形技能能复用同一套 `Query` 接口。
- ProjectileManager 批量处理，不给每个抛射物挂 Detector。
