# 12. 调试和压测

碰撞系统要同时满足两个目标：

```text
开发时容易看懂和调试
正式运行时足够快且无 GC
```

这两个目标不能混在同一条路径里。

## 本章目标

把调试路径和正式路径分开，并建立压测方法。

## 调试路径

调试路径可以使用：

```text
GameObject Detector
Scene Gizmos
命中变色
文字统计
Profiler Marker
```

这些工具服务验证，不进入正式高频逻辑。

例如：

```text
Detector MonoBehaviour
  -> 生成 BattleCollisionShape
  -> 调用 BattleCollisionManager.Query
  -> Debug 层让命中目标变色
```

## 正式路径

正式路径只做：

```text
单位数据更新
网格重建
批量查询
返回 target index
```

不要直接：

```text
AddComponent
GetComponent
FindObjectsOfType
new List
HashSet
Gizmos
SpineVitColorController
```

## 压测场景

建议建立三个固定压测：

```text
500 单位 + 100 抛射物
1000 单位 + 300 抛射物
2000 单位 + 500 抛射物
```

每个压测记录：

```text
targetCount
projectileCount
grid rebuild ms
query count
candidate count
narrow phase count
hit count
GC Alloc
```

## Profiler 关注点

重点看：

```text
BattleCollisionManager.RebuildGrid
BattleCollisionManager.Query
ProjectileManager.Tick
GC Alloc
```

如果 GC Alloc 不为 0，优先检查：

```text
隐式 foreach 装箱
LINQ
new List/HashSet
数组扩容
字符串拼接日志
Gizmos/Editor 代码是否在运行时路径
```

## 验收标准

- 关闭 Gizmos 后，正式查询无 GC Alloc。
- Profiler 中能区分网格重建和查询耗时。
- 压测数据可重复，方便比较优化前后。
- 调试变色和正式碰撞逻辑解耦。
