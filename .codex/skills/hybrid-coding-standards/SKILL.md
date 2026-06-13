---
name: hybrid-coding-standards
description: Use this skill for Hybrid project coding standards, especially when writing gameplay, UI behavior, GameContext Model/System/Command/Query logic, Bindable reactive state, battle runtime, resource-adjacent implementation, review, or debugging work in D:/UnityHub/Projects/Hybrid.
---

# Hybrid Coding Standards

Use this project-local skill when writing or reviewing Hybrid gameplay logic, UI behavior, GameContext framework code, Bindable reactive state, battle runtime, or when the user explicitly asks to follow `$hybrid-coding-standards`.

The purpose is to make implementation agents work like the old Hybrid reference agents without copying old project assumptions into this repository. Current repository facts always win.

## First Pass

Before editing or proposing implementation details:

1. Read the user-provided seed files or the most direct target files.
2. Read direct companions: same feature interface/system/model files, same View/Bindings files, adjacent runtime manager files, or direct callers/callees.
3. Expand at most two dependency layers from the seed chain.
4. Use `rg` only as a scoped search when the seed chain cannot answer one specific fact.
5. Stop and report missing facts instead of guessing when more than one key fact remains unknown.

Do not start with broad repository search when a target file or feature area is already known.

## Hard Rules

- Treat only user-provided or file-confirmed information as fact.
- Mark unconfirmed classes, fields, messages, assets, UI nodes, prefabs, and refresh paths as unknown instead of inventing them.
- Keep changes minimal and local to the confirmed implementation surface.
- Do not modify generated code, protocol/codegen output, third-party packages, or unrelated modules unless the user explicitly requests it.
- Do not modify `Packages/UniKit-Framework` or `Packages/UniKit-UI` by default. Prefer project-level code under `Assets/Scripts` unless a framework change is explicitly required and no project-level alternative exists.
- Treat `C:/Users/zzy/Desktop/Packages/...` old-project paths as reference material only. Never assume old classes or flows exist in Hybrid until confirmed under `D:/UnityHub/Projects/Hybrid`.
- Prefer existing Hybrid naming, namespaces, lifecycle methods, and folder boundaries over new abstractions.
- Hybrid business logic must use the actual `GameContext` framework in this repo: `Model` stores durable state, `System` owns domain capability and lifecycle, `Command` handles composite operations, and `Query` handles composite read aggregation.
- Do not let UI classes, scene `MonoBehaviour`s, static helpers, or adapters directly chain multiple systems into business flows. Route those flows through a single system method or a `Command`.
- UI click handlers should be thin: close/open UI as needed, then call one system entrypoint or send one explicit `Command`.
- UI display state should prefer `Bindable` values exposed by a `Model` or system-owned state. For composite reads across multiple sources, add a `Query` instead of scattering read logic in UI.
- At the end, report what changed, which existing pattern was followed, what was validated, and what remains unverified.

## Current Hybrid Defaults

- Runtime code lives mainly under `Assets/Scripts/Game/Play/Runtime`.
- Core framework contracts come from `Packages/UniKit-Framework/Runtime/Base`.
- `GameContext` discovers `AbstractModel` and `ISystem` implementations from the executing assembly and registers systems by interface.
- Use actual framework APIs from this repository: `Context.GetModel<T>()`, `Context.GetSystem<T>()`, `SendCommand(...)`, and `SendQuery(...)`.
- `Bindable<T>`, `BindableList<T>`, and `BindableDictionary<TKey,TValue>` live under `Assets/3rd/GameKits/Bindable/Runtime`; UI subscriptions should use the provided `Bind(...)`, `BindText(...)`, `BindActive(...)`, and related extensions so `BindableLifecycleBinder` can release them on destroy.
- Current battle runtime work should start from confirmed Hybrid files such as `Systems/Battle/System/BattleRuntimeSystem.cs`, `Battle/Runtime/BattleCommandBuffer.cs`, and `Battle/Unit` managers. Do not force old `LevelContext -> Driver -> BattleField` structure onto this project.
- UI code and resources should follow UniKit UI conventions. For prefab, binding generation, YooAsset, atlas, or generated `*.Bindings.cs` work, load and follow `unity-unikit-ui-prefab-builder`.

## References

Load only the reference needed for the current task:

- `references/workflow.md`: bounded exploration, anti-hallucination rules, stop conditions, and reporting shape.
- `references/hybrid-architecture.md`: current Hybrid architecture facts and coding boundaries.
- `references/ui-and-battle-rules.md`: UniKit UI, binding, battle runtime, and old-project reference handling rules.

## Output Standard

For implementation results, keep the final answer concise and include:

- changed behavior or files
- existing pattern or files used as reference
- validation performed
- unverified risks or blocked checks

For uncertain tasks, do not invent a plan. Report:

```text
Confirmed facts:
- ...
Unknowns:
- ...
Next implementation path:
- ...
```
