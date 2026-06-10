# 13. 最终接入

这一章把碰撞系统接到正式战斗流程里。重点不是再写数学，而是分层。

## 本章目标

形成最终运行链路：

```text
BattleUnitManager 管理单位数据
BattleCollisionManager 维护可命中目标和网格
ProjectileManager / SkillManager 发起查询
CombatSystem 处理伤害和状态
RenderManager 播放 Spine、特效、飘字、HUD
```

## UnitManager 是正式接入中心

正式战斗里，单位不是一个 `GameObject`，而是一组业务数据：

```text
unit handle
position
radius
camp
state
layer
hp
renderHandle
collisionTargetId
```

所以最终应该由 `BattleUnitManager` 统一管理单位生命周期：

```text
SpawnUnit
MoveUnit
ApplyDamage
Death
DespawnUnit
Recycle index
```

`BattleCollisionManager` 不应该自己扫描场景，也不应该保存完整单位业务数据。它只保存查询需要的最小目标数据：

```text
position
radius
camp
state
layer
unitIndex 或 unitHandle
```

## 碰撞层职责

碰撞层只负责：

```text
注册 collision target
注销 collision target
更新 target 位置和过滤数据
维护空间网格
执行形状查询
返回 target index 或 unit handle
```

碰撞层不负责：

```text
扣血
飘字
特效
受击变色
播放动画
音效
```

## UnitManager 和 CollisionManager 同步

建议第一版采用批量同步，而不是每个单位移动时立即重建网格：

```text
UnitManager 更新 positions[]
UnitManager 标记 moved/dirty
每帧固定时机 SyncCollisionTargets
CollisionManager 更新 targetPositions[]
CollisionManager 全量重建数组网格
```

同步示例：

```csharp
public void SyncCollisionTargets(BattleCollisionManager collisionManager)
{
    for (int i = 0; i < unitCount; i++)
    {
        if (!active[i])
        {
            continue;
        }

        int targetId = collisionTargetIds[i];
        collisionManager.UpdateTargetPosition(targetId, positions[i]);
        collisionManager.UpdateTargetFilter(targetId, camps[i], states[i], layers[i]);
    }
}
```

第一版可以全量同步，逻辑最稳。后续单位数量更大时，再加 dirty list：

```text
movedUnitIndices[]
filterDirtyUnitIndices[]
```

## 战斗逻辑消费查询结果

技能或抛射物查询：

```csharp
int hitCount = collisionManager.Query(shape, options, queryBuffer);
for (int i = 0; i < hitCount; i++)
{
    int targetIndex = queryBuffer.TargetIndices[i];
    BattleUnitHandle unit = collisionManager.GetUnitHandle(targetIndex);
    combatSystem.ApplyDamage(skillId, attacker, unit);
}
```

推荐最终映射：

```text
collision target index
  -> unit handle
  -> UnitManager hp/state/renderHandle
```

这样技能系统不需要知道 Spine 对象，也不需要知道 HUD 对象。

## 表现层消费战斗事件

伤害结算后，表现层处理：

```text
Spine VIT 受击颜色
BakedSequence 命中特效
FloatText 飘字
HUD 血条变化
```

这些系统可以继续走各自的 Manager：

```text
SpineVitManager
BakedSequenceManager
FloatTextManager
HudMeshManager
```

`BattleUnitManager` 只保存表现句柄：

```text
unitIndex -> spineHandle
unitIndex -> hpBarHandle
unitIndex -> nameHandle
```

表现播放时通过 handle 找到对应渲染数据，不让碰撞层直接引用表现类。

## 验证版如何保留

当前 `BattleCollisionWorld + Detector` 可以保留为调试包装：

```text
编辑器拖形状
生成 BattleCollisionShape
调用正式 BattleCollisionManager.Query
显示 Gizmos 和命中颜色
```

这样调试体验保留，但正式运行时不走 GameObject Detector。

## 迁移顺序

建议按这个顺序迁移：

```text
1. 保留验证版，新增 BattleUnitManager 数据结构
2. UnitManager Spawn 时注册 Collision target
3. UnitManager Move 后批量同步 Collision target
4. 用同一组单位对比 brute force 和 Manager 查询结果
5. ProjectileManager 改用 CollisionManager 查询
6. SkillManager 改用 CollisionManager 查询
7. CombatSystem 只消费 unit handle
8. RenderManager 只消费战斗事件和 render handle
9. GameObject Detector 退为 debug only
```

不要一次性替换所有战斗逻辑。先让新旧查询并行对比，确认结果一致。

## 验收标准

- UnitManager 能 Spawn/Despawn/Move/Death 单位。
- CollisionManager 查询结果能映射回有效 unit handle。
- 死亡单位不再被技能命中。
- ProjectileManager 和 SkillManager 都能通过 `BattleCollisionManager.Query` 获取命中。
- 表现层只通过 render handle 播放 Spine、特效、飘字。
- Debug Detector 可以继续显示形状和命中。
- 关闭 Debug 后，正式路径无每帧 GC。
