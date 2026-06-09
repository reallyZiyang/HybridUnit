# 13. 最终接入

这一章把碰撞系统接到正式战斗流程里。重点不是再写数学，而是分层。

## 本章目标

形成最终运行链路：

```text
UnitSystem 更新单位数据
BattleCollisionManager 维护目标和网格
ProjectileManager / SkillManager 发起查询
CombatSystem 处理伤害
RenderSystem 播放表现
```

## 碰撞层职责

碰撞层只负责：

```text
注册目标
更新目标位置
维护网格
执行形状查询
返回 target index
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

## 战斗逻辑消费结果

```csharp
int hitCount = collisionManager.Query(shape, options, queryBuffer);
for (int i = 0; i < hitCount; i++)
{
    int targetIndex = queryBuffer.TargetIndices[i];
    combatSystem.ApplyDamage(skillId, attackerId, targetIndex);
}
```

`targetIndex` 可以再映射到业务单位 id：

```text
targetIndex -> unitId -> hp/state/renderHandle
```

## 表现层消费结果

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
1. 保留验证版，新增正式 Manager
2. 用同一组目标对比 brute force 和 Manager 查询结果
3. ProjectileManager 改用 Manager 查询
4. SkillManager 改用 Manager 查询
5. 表现层只消费战斗事件
6. GameObject Detector 退为 debug only
```

不要一次性替换所有战斗逻辑。先让新旧查询并行对比，确认结果一致。

## 验收标准

- 正式碰撞层不引用 Spine、飘字、特效类。
- ProjectileManager 和 SkillManager 都能通过 `BattleCollisionManager.Query` 获取命中。
- 新旧查询在测试场景中结果一致。
- Debug Detector 可以继续显示形状和命中。
- 关闭 Debug 后，正式路径无每帧 GC。
