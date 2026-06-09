# 08. 目标注册和数据化

正式碰撞层不应该每次从 `Transform` 读取所有数据，也不应该直接保存 `BakedSpineVitPlayer`。它应该保存数据。

## 本章目标

实现 `BattleCollisionManager` 的目标注册、注销、位置更新。

目标数据：

```text
id
position
radius
camp
state
layer
renderHandle
active
```

## 数据数组

```csharp
private int[] ids;
private Vector2[] positions;
private float[] radii;
private int[] camps;
private int[] states;
private int[] layers;
private int[] renderHandles;
private bool[] active;
private int targetCount;
```

`targetIndex` 是数组下标，`targetId` 是对外句柄。第一版可以让它们一致，后续再加 generation 防止旧 id 误用。

## 注册

```csharp
public int RegisterTarget(Vector2 position, float radius, int camp, int state, int layer, int renderHandle)
{
    int index = targetCount++;
    ids[index] = index;
    positions[index] = position;
    radii[index] = Mathf.Max(0f, radius);
    camps[index] = camp;
    states[index] = state;
    layers[index] = layer;
    renderHandles[index] = renderHandle;
    active[index] = true;
    return index;
}
```

第一版如果容量满了，应该明确报错或返回失败，不要静默扩容。

## 注销

```csharp
public void UnregisterTarget(int targetId)
{
    if (!IsValidTarget(targetId))
    {
        return;
    }

    active[targetId] = false;
}
```

第一版可以不做数组紧凑，避免删除目标导致 index 改变。

## 更新位置

```csharp
public void UpdateTargetPosition(int targetId, Vector2 position)
{
    if (!IsValidTarget(targetId))
    {
        return;
    }

    positions[targetId] = position;
}
```

正式运行时由单位系统在移动后调用。不要让 `BattleCollisionManager` 自己遍历 GameObject。

## renderHandle 的意义

碰撞层只保存 `renderHandle`，不直接引用 Spine：

```text
targetIndex -> renderHandle
CombatSystem 结算命中
RenderSystem 根据 renderHandle 播放受击表现
```

这样碰撞层和表现层解耦。

## 验收标准

- 注册目标后能被查询命中。
- 注销目标后不再命中。
- 更新位置后，网格重建和查询结果同步变化。
- 碰撞层不引用 `BakedSpineVitPlayer` 或 `SpineVitColorController`。
