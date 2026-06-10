# 大规模单位与空间划分方案思路总览

这份文档是空间划分课程的“思路版”。它不按每章代码实现展开，而是把整套课程背后的设计逻辑串起来：为什么要从暴力检测开始，为什么要引入空间网格，为什么最终还需要 UnitManager，以及如何让碰撞、抛射物、技能、渲染表现各自保持清晰边界。

对应详细课程：

```text
Docs/BattleCollisionCourse/
```

## 1. 最终要解决的问题

目标不是单纯写一个碰撞检测函数，而是支撑这种战斗场景：

```text
同屏大量单位
大量抛射物
频繁范围技能
Spine VIT 角色
序列帧特效
飘字、血条、名字、HUD
移动端和微信小游戏
```

如果继续使用传统 GameObject 思路：

```text
每个单位一个完整 MonoBehaviour 组合
每个抛射物一个 GameObject
每个技能一个 Detector
每帧 Physics/Collider/Trigger 回调
每个表现一个 Renderer
```

数量上来后，瓶颈会同时出现在：

- 单位生命周期管理。
- Transform 和 MonoBehaviour Update。
- 碰撞检测候选过多。
- Renderer 和材质状态切换。
- GC 和临时集合分配。
- 调试逻辑和正式逻辑混在一起。

所以最终方向是 Manager 化和数据化：

```text
BattleUnitManager
  管单位数据和生命周期

BattleCollisionManager
  管可命中目标和空间查询

ProjectileManager / SkillManager
  管抛射物推进和技能查询

CombatSystem
  管伤害、状态、规则

RenderManager
  管 Spine、特效、飘字、HUD 表现
```

## 2. 核心分层原则

最重要的原则是：每一层只保存自己需要的数据。

### UnitManager

单位层保存完整业务数据：

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

它知道一个单位是否存在、是否死亡、属于哪个阵营、当前位置在哪里。

### CollisionManager

碰撞层只保存查询需要的最小数据：

```text
position
radius
camp
state
layer
unit handle 或 unit index
```

它不扣血，不播放动画，不生成飘字，也不引用 Spine 对象。

### Projectile / Skill

技能和抛射物层只负责发起查询：

```text
生成查询形状
设置过滤条件
读取命中结果
交给 CombatSystem 结算
```

### RenderManager

表现层只消费事件：

```text
受击变色
命中特效
飘字
血条变化
死亡动画
```

碰撞层不直接调用表现层。这样后续 Spine VIT、序列帧、MeshPlayer、UI HUD 怎么优化，都不会反向污染碰撞系统。

## 3. 为什么从暴力检测开始

空间划分是优化，不是正确性的来源。

第一步应该先写最简单的 O(N) 检测：

```text
遍历所有目标
用形状数学判断是否命中
返回命中列表
```

这个版本性能差，但它有两个价值：

- 逻辑最容易理解。
- 可以作为后续网格查询的正确性基准。

后续数组网格、去重、过滤、fallback 都应该和暴力检测对比结果。否则一开始就写复杂网格，很难判断 bug 是来自数学、网格、去重还是过滤。

## 4. 形状检测的统一思路

所有单位第一版都可以用圆代理。

查询形状可以多样：

```text
圆形：爆炸、治疗圈、范围技能
矩形：横扫、矩形区域、路径伤害
扇形：近战挥砍、喷吐、朝向技能
胶囊线段：子弹 sweep、光束、轨迹检测
```

统一成：

```text
查询形状 vs 目标圆
```

这样目标侧非常简单，只需要：

```text
position + radius
```

技能侧复杂度集中在查询形状里。

这一层叫 narrow phase，也就是精确判断。它只负责“候选目标到底有没有命中”。

## 5. Broad Phase 和 Narrow Phase

如果每个技能都对所有单位做 narrow phase，单位越多越慢。

所以要拆成两步：

```text
Broad Phase
  快速找附近候选

Narrow Phase
  对候选做精确形状判断
```

Broad phase 不要求完全精准，只要求不能漏。它可以多返回候选，让 narrow phase 再剔除。

典型流程：

```text
查询形状生成 AABB
AABB 扩展目标半径
空间网格找覆盖 cell
收集候选目标
精确形状判断
输出命中结果
```

为什么 AABB 要扩展目标半径：

```text
目标圆心可能在查询形状 AABB 外
但目标圆边缘已经碰到查询形状
如果不扩展，就可能漏检
```

## 6. 固定网格为什么适合第一版

固定网格是最适合第一版正式实现的空间划分方案。

它的思路是：

```text
把战斗区域切成固定大小的 cell
目标根据自己的 AABB 插入覆盖到的 cell
查询时只访问形状 AABB 覆盖到的 cell
```

优点：

- 概念直观。
- 插入和查询都简单。
- 适合 2D 平面战斗。
- 容易做 Scene Gizmos 调试。
- 数组化后性能稳定。

不建议第一版就做四叉树、BVH 或复杂动态结构。它们在目标分布极不均匀时可能有优势，但实现、调试和更新成本都更高。

## 7. cellSize 的取舍

`cellSize` 是空间划分效果的核心参数。

```text
cellSize 太大
  每个 cell 里目标多
  候选多
  narrow phase 变重

cellSize 太小
  目标跨多个 cell
  插入成本增加
  查询覆盖 cell 数增加
```

第一版建议：

```text
cellSize = 1 到 2 倍常规单位直径
```

如果单位半径约 `0.45`，单位直径约 `0.9`，可以从 `1.5` 或 `2` 开始。

调参时不要只看帧率，要看：

```text
平均候选数
平均 narrow phase 次数
每帧网格重建耗时
每帧查询次数
GC Alloc
```

## 8. 为什么正式版要从 Dictionary 改成数组

验证版可以用：

```text
Dictionary<Vector2Int, List<Target>>
HashSet<Target>
List<Target>
```

因为它容易写、容易调试。

正式版不建议这样做，因为：

- 哈希开销不稳定。
- List/HashSet 可能扩容。
- 数据不连续，缓存不友好。
- 移动端和小游戏平台更容易出现抖动。

正式版更适合：

```text
cellHeads[cellIndex]
nextInCell[targetIndex]
positions[targetIndex]
radii[targetIndex]
queryStamp[targetIndex]
```

用数组模拟 cell 链表。这样每帧重建网格只是线性清空和线性插入，成本更可控。

## 9. 为什么第一版全量重建网格

很多人会第一时间想做增量更新：

```text
单位跨 cell 才更新它所在的 cell
```

这个方向是对的，但不适合第一版。

第一版全量重建：

```text
清空 cellHeads
遍历所有 active target
重新插入网格
```

优点：

- 逻辑简单。
- 不需要记录旧 cell range。
- 目标启用、禁用、死亡、移动都不容易出错。
- 更适合先验证正确性。

只有当全量重建在目标规模下确实成为瓶颈时，再考虑增量更新。

## 10. 无 GC 查询的关键：buffer 和 stamp

正式查询不能每次创建集合。

结果应该写入复用 buffer：

```text
BattleCollisionQueryBuffer
  Count
  TargetIndices[]
```

目标跨多个 cell 时，同一个目标可能被访问多次。验证版可以用 HashSet 去重，正式版用 stamp：

```text
queryStamp[targetIndex]
currentQueryId
```

每次查询递增 `currentQueryId`，目标第一次访问时写 stamp。后续同一次查询再遇到它，就跳过。

这个模式比 HashSet 更适合大量查询：

- 没有哈希成本。
- 没有扩容。
- 数据连续。
- 查询路径稳定。

## 11. 过滤要尽早做

很多候选目标从业务上根本不能被命中：

```text
友军
死亡单位
无敌单位
不可选中单位
空中单位
建筑单位
```

这些判断比形状数学便宜，应该尽量放在 narrow phase 前。

查询参数可以包含：

```text
campMask
stateMask
layerMask
maxHits
sortByDistance
```

流程：

```text
候选目标
  -> active 判断
  -> camp/state/layer 过滤
  -> narrow phase
  -> 写入结果
```

排序和 `maxHits` 要谨慎。最近目标、穿透目标、最先命中目标这些需求会增加成本，不应该默认开启。

## 12. 抛射物必须用 sweep

高速抛射物不能只检测当前帧位置。

如果只检测当前点：

```text
上一帧在目标左边
下一帧在目标右边
当前点没有落在目标圆内
结果漏检
```

正确思路是 sweep：

```text
previousPosition -> currentPosition
width = projectileRadius * 2
```

也就是用胶囊线段检测子弹这一帧扫过的路径。

这套思路还能复用到：

```text
激光
光束
冲刺撞击
路径伤害
近似射线
```

抛射物也不应该每个挂一个 Detector。正式版应该由 `ProjectileManager` 批量推进和批量查询。

## 13. 大范围技能不一定适合走网格

空间划分的收益来自“只看附近少量 cell”。

如果技能范围很大：

```text
全屏技能
超大圆
超长光束
覆盖大半地图的治疗
```

它会覆盖大量 cell。此时走网格可能反而更慢，因为还要遍历 cell、去重、收集候选。

所以需要 fallback：

```text
覆盖 cell 数超过阈值
  -> 直接遍历 active targets
```

本质原则：

```text
小范围查询用空间划分
大范围查询直接全量遍历
```

不要把网格当成所有查询的唯一答案。

## 14. UnitManager 的核心价值

空间划分课程最终必须补单位管理，因为大量单位的成本不只在碰撞。

`BattleUnitManager` 解决：

```text
单位生成
单位死亡
单位回收
单位位置
单位阵营
单位状态
单位血量
单位表现句柄
单位碰撞目标句柄
```

推荐句柄：

```text
BattleUnitHandle = index + generation
```

为什么要 generation：

```text
单位 A 占用 index 5 generation 1
A 死亡回收
单位 B 复用 index 5 generation 2
旧的 A handle 不应该能操作 B
```

推荐数据结构：

```text
positions[]
radii[]
camps[]
states[]
layers[]
hp[]
active[]
generations[]
renderHandles[]
collisionTargetIds[]
freeStack[]
```

这个结构让不同系统只访问自己需要的数组：

```text
移动系统访问 positions
碰撞同步访问 positions/radii/camps/states/layers
伤害系统访问 hp/states
表现系统访问 renderHandles
```

## 15. UnitManager 和 CollisionManager 的关系

UnitManager 是业务单位中心，CollisionManager 是查询加速结构。

生成单位时：

```text
UnitManager 分配 unit index
RenderManager 分配表现 handle
CollisionManager 注册 target
UnitManager 保存 collisionTargetId
```

移动单位时：

```text
UnitManager 更新 positions[]
批量同步到 CollisionManager
CollisionManager 重建或更新网格
```

死亡单位时：

```text
UnitManager 设置 Dead state
CollisionManager 禁用或注销 target
RenderManager 播放死亡表现
延迟 Despawn 回收 index
```

不要让 CollisionManager 反过来管理单位生命周期。它只负责查询。

## 16. 表现层为什么只用 handle

单位数据不应该直接保存 `BakedSpineVitPlayer`、血条 GameObject 或飘字对象。

更合理的是保存句柄：

```text
spineHandle
hpBarHandle
nameHandle
```

表现层通过句柄找到自己的数据。

这样做的好处：

- UnitManager 不依赖具体 Renderer。
- CollisionManager 不依赖表现层。
- Spine VIT、MeshPlayer、FloatText 后续都能独立 Manager 化。
- 单位死亡后，表现可以延迟回收，不影响单位逻辑回收。

## 17. 调试路径和正式路径分开

当前 GameObject Detector 很有价值，但它应该是调试包装。

调试路径可以做：

```text
Scene Gizmos
拖动检测形状
命中变色
显示 cell
显示候选数量
```

正式路径只做：

```text
数组数据
网格重建
查询
返回结果
```

正式路径不要调用：

```text
FindObjectsOfType
GetComponent
AddComponent
Gizmos
Debug.Log 高频输出
SpineVitColorController
```

这样性能数据才可信。

## 18. 压测应该看什么

推荐压测规模：

```text
500 单位 + 100 抛射物
1000 单位 + 300 抛射物
2000 单位 + 500 抛射物
```

每次记录：

```text
单位数量
抛射物数量
查询次数
平均候选数量
narrow phase 次数
命中数量
网格重建耗时
查询耗时
GC Alloc
```

不要只看总帧率。要知道时间花在哪里：

```text
是 UnitManager 同步慢
还是网格重建慢
还是候选太多
还是 narrow phase 太多
还是表现层太重
```

## 19. 推荐演进顺序

不要一开始就做最终形态。推荐按这个顺序：

```text
1. 暴力检测，保证形状正确
2. GameObject 验证版，保证可视化调试
3. Dictionary 网格，理解 broad phase
4. 数组网格，进入正式运行时结构
5. query buffer + stamp，去掉 GC 和 HashSet
6. query filters，减少无效 narrow phase
7. ProjectileManager sweep，解决高速穿透
8. large query fallback，处理全屏技能
9. UnitManager，管理大量单位生命周期
10. RenderManager handle 化，表现层解耦
11. 压测和参数调优
```

每一步都能独立验证，避免同时引入太多变量。

## 20. 总结

这套方案的核心不是某一个算法，而是一组边界清晰的工程取舍：

```text
用暴力检测做正确性基准
用形状数学做 narrow phase
用 AABB + 固定网格做 broad phase
用数组替代 Dictionary/List/HashSet
用 query buffer + stamp 保证无 GC
用 sweep 解决高速抛射物
用 fallback 处理大范围技能
用 UnitManager 管大量单位
用 RenderManager 解耦表现
用 Debug wrapper 保留可视化调试
```

最终目标是：

```text
碰撞系统只负责“快速找目标”
单位系统只负责“管理单位数据”
战斗系统只负责“规则和结算”
表现系统只负责“把结果画出来”
```

只要这几个边界守住，后续无论是继续优化空间划分、迁移 Manager Draw、扩展抛射物类型，还是优化 Spine/飘字/HUD，都不会互相牵制。
