# 14. 大规模单位数据管理

空间划分解决的是“如何快速查找附近可命中目标”。但正式战斗中，目标来自单位系统。单位系统如果仍然依赖大量 `GameObject + Transform + MonoBehaviour Update`，碰撞系统再快也会被单位管理成本拖住。

本章目标是设计一个数据化 `BattleUnitManager`，让大量单位的生成、移动、死亡、回收、碰撞同步和表现绑定都走数组和句柄。

## 本章目标

实现思路上要做到：

```text
单位数据连续存储
单位句柄稳定可校验
死亡和回收不破坏旧引用
位置批量同步给 CollisionManager
表现层通过 renderHandle 解耦
运行时不遍历场景对象
```

本章只讲设计和实现路线，不要求立刻替换当前验证版。

## 为什么需要 UnitManager

如果每个单位都是一个完整 GameObject，并在多个系统里直接访问：

```text
Transform.position
GetComponent<Health>()
GetComponent<BakedSpineVitPlayer>()
GetComponent<Collider>()
```

大量单位时会出现几个问题：

- 每帧访问 Transform 成本高。
- `GetComponent` 和跨组件调用分散。
- 生成/销毁 GameObject 容易造成峰值和 GC。
- 碰撞、渲染、战斗逻辑互相引用，后续难以 Manager 化。
- 单位死亡后旧引用可能误用。

UnitManager 的目标是把单位变成数组数据：

```text
unit index
  -> position
  -> radius
  -> camp
  -> state
  -> hp
  -> renderHandle
  -> collisionTargetId
```

## 句柄：index + generation

不要直接把数组下标暴露成永久 id。因为单位回收后，下标会复用。

推荐句柄：

```csharp
public struct BattleUnitHandle
{
    public int index;
    public int generation;
}
```

`index` 是数组位置，`generation` 是版本号。每次这个 index 被重新分配给新单位时，generation 增加。

校验：

```csharp
private bool IsValid(BattleUnitHandle unit)
{
    int index = unit.index;
    return index >= 0
        && index < capacity
        && active[index]
        && generations[index] == unit.generation;
}
```

这样可以避免旧句柄误操作新单位：

```text
单位 A 使用 index 5 generation 1
A 死亡后 index 5 回收
单位 B 复用 index 5 generation 2
旧的 A handle 无法通过校验
```

## 核心数据数组

第一版 UnitManager 可以使用这些数组：

```csharp
private Vector2[] positions;
private float[] radii;
private int[] camps;
private int[] states;
private int[] layers;
private int[] hp;
private bool[] active;
private int[] generations;
private int[] renderHandles;
private int[] collisionTargetIds;
private int[] freeStack;
private int freeCount;
private int capacity;
```

含义：

```text
positions          单位逻辑位置
radii              碰撞半径
camps              阵营
states             状态位，例如 Alive/Dead/Selectable/Invincible
layers             地面/空中/建筑等层级
hp                 血量
active             当前 index 是否被占用
generations        句柄版本
renderHandles      表现层句柄
collisionTargetIds 碰撞层 target id
freeStack          可复用 index 栈
```

这些数据是 Structure of Arrays。好处是批量处理时访问连续，缓存更友好：

```text
批量移动只访问 positions
碰撞同步只访问 positions/radii/camps/states/layers
伤害结算只访问 hp/states
表现播放只访问 renderHandles
```

## SpawnUnit

Spawn 时优先复用 freeStack，没有可复用 index 再使用新 index。

```csharp
public BattleUnitHandle SpawnUnit(in BattleUnitSpawnDesc desc)
{
    int index;
    if (freeCount > 0)
    {
        index = freeStack[--freeCount];
    }
    else
    {
        index = AllocateNewIndex();
    }

    active[index] = true;
    generations[index]++;
    positions[index] = desc.position;
    radii[index] = Mathf.Max(0f, desc.radius);
    camps[index] = desc.camp;
    states[index] = desc.state;
    layers[index] = desc.layer;
    hp[index] = desc.hp;
    renderHandles[index] = desc.renderHandle;
    collisionTargetIds[index] = -1;

    return new BattleUnitHandle
    {
        index = index,
        generation = generations[index]
    };
}
```

第一版建议容量固定。容量不足时明确报错或返回无效句柄，不要在战斗高峰里隐式扩容。

## 注册 Collision Target

单位生成后，需要注册到碰撞系统：

```csharp
public void RegisterCollisionTarget(BattleUnitHandle unit, BattleCollisionManager collisionManager)
{
    if (!IsValid(unit))
    {
        return;
    }

    int i = unit.index;
    int targetId = collisionManager.RegisterTarget(
        positions[i],
        radii[i],
        camps[i],
        states[i],
        layers[i],
        renderHandles[i]);

    collisionTargetIds[i] = targetId;
}
```

更完整的版本应让 `CollisionManager` 保存 `BattleUnitHandle` 或 unit index，这样查询结果能映射回单位：

```text
collision target index -> unit handle
```

不要让碰撞层保存完整单位对象。

## 移动和位置同步

单位移动时，先只更新 UnitManager：

```csharp
public void SetPosition(BattleUnitHandle unit, Vector2 position)
{
    if (!IsValid(unit))
    {
        return;
    }

    positions[unit.index] = position;
    MarkPositionDirty(unit.index);
}
```

第一版同步策略：

```text
单位系统完成移动
统一 SyncCollisionTargets
CollisionManager 更新 targetPositions
CollisionManager 全量重建网格
```

同步：

```csharp
public void SyncCollisionTargets(BattleCollisionManager collisionManager)
{
    for (int i = 0; i < capacity; i++)
    {
        if (!active[i])
        {
            continue;
        }

        int targetId = collisionTargetIds[i];
        if (targetId < 0)
        {
            continue;
        }

        collisionManager.UpdateTargetPosition(targetId, positions[i]);
    }
}
```

后续优化可以改为 dirty list：

```text
movedUnitIndices[]
movedCount
```

只有移动过的单位才同步。

## 伤害、死亡和注销

伤害只改 UnitManager 数据：

```csharp
public void ApplyDamage(BattleUnitHandle unit, int damage)
{
    if (!IsValid(unit))
    {
        return;
    }

    int i = unit.index;
    hp[i] = Mathf.Max(0, hp[i] - Mathf.Max(0, damage));
    if (hp[i] == 0)
    {
        MarkDead(i);
    }
}
```

死亡第一版建议：

```text
设置 Dead state
禁用或注销 collision target
发送死亡表现事件
延迟 Despawn
```

不要一死亡就立即销毁表现对象，因为死亡动画、溶解、飘字、掉落可能还要播放。

```csharp
private void MarkDead(int index)
{
    states[index] |= UnitStateDead;

    int targetId = collisionTargetIds[index];
    if (targetId >= 0)
    {
        collisionManager.UnregisterTarget(targetId);
        collisionTargetIds[index] = -1;
    }

    // Render event: play death animation/effect by renderHandles[index].
}
```

`collisionManager` 可以通过参数传入，或由更上层的战斗流程统一调用，不建议 UnitManager 偷偷查找单例。

## Despawn 和回收

Despawn 是真正释放单位 index：

```csharp
public void DespawnUnit(BattleUnitHandle unit)
{
    if (!IsValid(unit))
    {
        return;
    }

    int i = unit.index;
    active[i] = false;
    hp[i] = 0;
    renderHandles[i] = -1;
    collisionTargetIds[i] = -1;
    freeStack[freeCount++] = i;
}
```

注意：generation 不在 Despawn 时增加，而是在下一次 Spawn 复用 index 时增加也可以。关键是新单位的 generation 必须和旧句柄不同。

## 状态和过滤数据同步

碰撞查询依赖：

```text
camp
state
layer
```

所以这些字段变化时也要同步：

```csharp
public void SetCamp(BattleUnitHandle unit, int camp)
{
    if (!IsValid(unit))
    {
        return;
    }

    camps[unit.index] = camp;
    MarkFilterDirty(unit.index);
}

public void SetState(BattleUnitHandle unit, int state)
{
    if (!IsValid(unit))
    {
        return;
    }

    states[unit.index] = state;
    MarkFilterDirty(unit.index);
}
```

同步给碰撞层：

```csharp
collisionManager.UpdateTargetFilter(targetId, camps[i], states[i], layers[i]);
```

过滤字段不要每次查询时回查 UnitManager，否则查询路径会变复杂，也更难做到缓存友好。

## 和表现层的关系

UnitManager 不直接持有 `BakedSpineVitPlayer`，只持有表现句柄：

```text
renderHandles[index]
hpBarHandles[index]
nameHandles[index]
```

表现层自己维护：

```text
SpineVitManager
HudMeshManager
FloatTextManager
BakedSequenceManager
```

战斗事件：

```text
DamageApplied(unitHandle, damage, hitPosition)
UnitDead(unitHandle)
StateChanged(unitHandle, state)
```

表现系统消费事件：

```text
播放受击色
生成飘字
更新血条
播放死亡特效
```

这样可以保证：

```text
碰撞层不依赖表现
单位层不依赖具体 Renderer
表现层可以后续迁移到 Manager/Draw
```

## 大规模单位优化思路

### 1. 避免每帧遍历 GameObject

正式路径不要：

```text
FindObjectsOfType
GetComponent
foreach all MonoBehaviour
Transform.position 批量读取
```

位置应由移动系统直接写入 `positions[]`。

### 2. 避免即时销毁

大量单位死亡时不要立即：

```text
Destroy(gameObject)
Instantiate(prefab)
```

应该：

```text
UnitManager 回收 unit index
RenderManager 回收表现对象
CollisionManager 注销 target
```

### 3. 分离逻辑 tick 和表现 tick

单位逻辑：

```text
位置、血量、状态、技能
```

表现：

```text
动画、特效、飘字、血条插值
```

不要让表现动画反向驱动碰撞位置。碰撞位置应该来自逻辑位置。

### 4. 批量同步

第一版：

```text
每帧全量同步 active units
```

优化版：

```text
只同步 moved/filterDirty units
```

更进一步：

```text
单位跨 cell 才更新网格
```

但增量网格维护复杂度高，第一版不建议做。

### 5. 容量预分配

进入战斗前根据关卡规模预分配：

```text
maxUnits
maxProjectiles
maxCollisionTargets
maxQueryResults
```

不要在战斗高峰里扩容。

## 推荐实现顺序

```text
1. BattleUnitHandle: index + generation
2. BattleUnitSpawnDesc
3. UnitManager 固定容量数组
4. Spawn/Despawn/IsValid
5. position/radius/camp/state/layer/hp
6. collisionTargetIds 同步
7. renderHandles 绑定
8. Death 后禁用 collision target
9. dirty list 优化
10. 压测 2000 单位
```

## 验收标准

- Spawn 1000 个单位后，所有 handle 有效。
- Despawn 后旧 handle 因 generation 不匹配而失效。
- index 回收后，新单位不会被旧 handle 操作。
- 单位移动后，CollisionManager 查询位置同步正确。
- 单位死亡后，不再被技能查询命中。
- 阵营、状态、地面/空中层级能通过 QueryOptions 正确过滤。
- 关闭表现层，仅跑 UnitManager + CollisionManager 时无每帧 GC。
- 2000 单位批量移动和同步时，不访问 `GetComponent`，不遍历场景对象。

## 小结

空间划分只解决“查谁被命中”。大规模战斗还需要一个稳定的数据化单位系统。

最终分层应该是：

```text
BattleUnitManager
  管单位生命周期和业务数据

BattleCollisionManager
  管可命中目标和空间查询

ProjectileManager / SkillManager
  发起查询和触发战斗事件

RenderManager
  消费事件并播放表现
```

把这几层分开，后续优化 Spine VIT、特效、飘字、HUD、抛射物时，才不会互相拖住。
