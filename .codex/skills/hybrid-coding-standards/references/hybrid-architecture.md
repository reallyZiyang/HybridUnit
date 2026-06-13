# Hybrid Architecture Notes

These are current Hybrid repository facts observed under `D:/UnityHub/Projects/Hybrid`. Reconfirm before relying on them for critical edits because the repository may evolve.

## Main Runtime Layout

- Gameplay runtime code is under `Assets/Scripts/Game/Play/Runtime`.
- The runtime assembly is `Assets/Scripts/Game/Play/Runtime/Game.Play.asmdef`.
- Project-level gameplay systems currently live under `Assets/Scripts/Game/Play/Runtime/Systems`.
- General battle runtime implementation lives under `Assets/Scripts/Game/Play/Runtime/Battle`.
- UI extensions live under `Assets/Scripts/Game/Play/Runtime/UI/Extensions`.
- Generated or package code should not be edited as a default implementation path.

## UniKit Framework Contracts

Core framework types are in `Packages/UniKit-Framework/Runtime/Base`:

- `AbstractModel : Initializable, IModel`
- `AbstractSystem : Initializable, ISystem`
- `AbstractCommand` and `AbstractCommand<TResult>`
- `IContext`, `IContextOwner`, `ISystem`, `IModel`, `ICommand`, `IQuery`

`GameContext` is the project runtime context:

- It derives from `AbstractContext<GameContext>`.
- It reflects the executing assembly for `AbstractModel` implementations and registers them.
- It reflects the executing assembly for `ISystem` implementations, sorts them by `OrderAttribute`, and registers each system by its interface.
- Normal code should use `GetModel<T>()`, `GetSystem<T>()`, and `SendCommand(...)` through the existing context helpers instead of making parallel service locators.
- `AbstractContext.InitAsync()` initializes all registered models before systems, then registers `IUpdateSystem` implementations with `UpdateDriver`.
- `Initializable.Init()` and `Dispose()` guard duplicate lifecycle calls; put setup in `OnInitialize()` and cleanup in `OnDispose()`.
- `AbstractCommand` / `AbstractCommand<TResult>` receive `Context` and execute through `SendCommand(...)`.
- `AbstractQuery<TResult>` / `AbstractQuery<TParameter,TResult>` receive `Context` and execute through `SendQuery(...)`.

## System Layer Pattern

For project systems:

- Keep public capability in an interface under a local `Interface` folder when the surrounding module does so.
- Put implementation in a local `System` folder.
- Implement `AbstractSystem` and the matching `I...System` interface.
- Use `[Order(...)]` only after checking nearby systems and initialization ordering needs.
- Keep cross-system access through `Context.GetSystem<TInterface>()` or local extension helpers.
- Store durable domain state in `AbstractModel` implementations.
- Put long-lived domain capability, lifecycle, and per-frame work in `AbstractSystem` implementations.
- Use `Command` for composite operations such as starting a level, ending battle flow, submitting a player action, or switching multiple pieces of flow state.
- Use `Query` for composite read aggregation such as result summaries, cross-model filtering, or derived data assembled from multiple systems.
- Do not put durable business logic in commands; commands should orchestrate by calling systems and updating models through the framework.

## Bindable State

The project Bindable implementation lives under `Assets/3rd/GameKits/Bindable/Runtime`:

- `Bindable<T>.Value` invokes `OnValueChanged` when the assigned value changes.
- `NotifyChanged()` manually emits a change after in-place object or collection mutation.
- `BindableList<T>` and `BindableDictionary<TKey,TValue>` expose element events and call `NotifyChanged()` for collection changes.
- UI and components should subscribe through extension methods such as `Bind(...)`, `BindText(...)`, `BindActive(...)`, `BindFillAmount(...)`, and `BindToggle(...)`.
- The binding extensions add `BindableLifecycleBinder`, which disposes subscriptions in `OnDestroy()`.
- Prefer binding UI to `Model` or system-owned bindables over manually pushing refresh calls through multiple views.

## Current Battle Runtime Facts

Current Hybrid battle code is not the same shape as the old Level-based project.

Known current entry points include:

- `Assets/Scripts/Game/Play/Runtime/Systems/Battle/Interface/IBattleRuntimeSystem.cs`
- `Assets/Scripts/Game/Play/Runtime/Systems/Battle/System/BattleRuntimeSystem.cs`
- `Assets/Scripts/Game/Play/Runtime/Systems/Battle/Interface/IBattleCollisionSystem.cs`
- `Assets/Scripts/Game/Play/Runtime/Systems/Battle/System/BattleCollisionSystem.cs`
- `Assets/Scripts/Game/Play/Runtime/Battle/Runtime/BattleCommandBuffer.cs`
- `Assets/Scripts/Game/Play/Runtime/Battle/Unit`
- `Assets/Scripts/Game/Play/Runtime/Battle/Skill`
- `Assets/Scripts/Game/Play/Runtime/Battle/Buff`
- `Assets/Scripts/Game/Play/Runtime/Battle/Projectile`
- `Assets/Scripts/Game/Play/Runtime/Battle/Rendering`

`BattleRuntimeSystem` currently coordinates runtime data, unit management, collision, AI, skills, buffs, projectiles, pushing, interception, rendering, pause state, and logic ticks.

When changing battle behavior, first identify whether the change belongs to:

- runtime orchestration in `BattleRuntimeSystem`
- collision ownership in `BattleCollisionSystem` or `BattleCollisionManager`
- command production or execution in `BattleCommandBuffer` / effect execution
- unit state in `Battle/Unit`
- skill, buff, projectile, push, AI, interception, or rendering subsystems
- config data from `Game.Data.Configs.Tables` / `API.Tables`

Do not introduce old `LevelContext`, `BattleDriver`, or `BattleField` concepts unless they are first confirmed in the current Hybrid repo.

## Old Project References

The old project paths supplied by the user are useful for extracting workflow and design intent:

- `C:/Users/zzy/Desktop/Packages/Scripts/Game/Play/Runtime/Systems/Level`
- `C:/Users/zzy/Desktop/Packages/Scripts/Game/Play/Runtime/UI/View/Battle`
- `C:/Users/zzy/Desktop/agents`

Use them only as reference material. Before porting any class name, field, event, protocol, UI binding, or asset key, confirm the equivalent exists or should be newly created in Hybrid.
