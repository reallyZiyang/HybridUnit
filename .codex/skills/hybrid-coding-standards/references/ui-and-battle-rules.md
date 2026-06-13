# UI And Battle Rules

Use this reference when the task touches UniKit UI, UI runtime code, generated bindings, battle UI, or battle runtime behavior.

## UniKit UI Rules

For UI prefab, binding generation, YooAsset, atlas, or generated binding work, load the project-local `unity-unikit-ui-prefab-builder` skill and follow it.

Default UI rules:

- Use `UIView`, `UIPanel`, and `UINode` according to the existing UniKit UI layer.
- Use `UIManager` or existing UniKit open/close helpers instead of introducing another window manager.
- Use `UIDataBinding` and generated `*.Bindings.cs` for bound nodes.
- Do not hand-write generated binding fields or generated `OnInitBindings()` code.
- Put hand-written behavior in the non-generated partial class.
- Keep UI code responsible for display, interaction callbacks, and refresh orchestration.
- Keep gameplay rules, battle calculations, and durable state in systems or runtime services.
- For lists, repeated items, tooltips, effects, click-through masks, and context-aware nodes, first check existing helpers under `Assets/Scripts/Game/Play/Runtime/UI/Extensions` and `Packages/UniKit-UI/Runtime`.

Before editing UI code, confirm:

- the target View/Panel/Node class
- companion generated binding file if present
- prefab or binding generation ownership if relevant
- data source and refresh trigger
- open/close path
- whether the UI task is actually blocked on system or battle data

## UI Data Boundaries

Do not fabricate UI state sources.

- If a View displays system data, confirm the model/system/query/command that owns it.
- If a View triggers gameplay behavior, route the action through existing systems or commands.
- If a binding field or node name is not confirmed, mark it unknown and inspect the prefab/binding source before coding against it.
- Do not use `GetComponent` traversal as a replacement for the existing binding pattern unless the local file already follows that style or the task explicitly requires it.

## Current Battle Runtime Rules

Current Hybrid battle runtime should be treated as the source of truth, not the old Level-agent structure.

Start from the smallest confirmed entry point:

- `IBattleRuntimeSystem` for public battle runtime capability.
- `BattleRuntimeSystem` for initialization, ticking, spawn/despawn, command flushing, pause, and high-level coordination.
- `BattleCommandBuffer` for deferred battle actions such as damage, heal, buff, projectile spawn, and despawn.
- `BattleUnitManager` and adjacent `Battle/Unit` files for unit state and handles.
- Specific managers for skills, buffs, projectiles, push, AI, interception, collision, and rendering.

When fixing battle behavior:

- Identify who creates the event or command, who consumes it, who mutates unit/runtime state, and who drives rendering.
- Prefer fixing the root cause in the owning manager or system instead of adding bypass flags in UI or orchestration code.
- Keep deterministic logic in runtime systems and managers; keep visual-only behavior in render world or UI.
- Do not make UI drive core battle timing, collision, skill effects, or result authority.
- Preserve pause, tick, capacity, and disposal behavior when touching runtime orchestration.

## Old Battle Reference Handling

Old project battle concepts can guide analysis but must not be imposed on Hybrid:

- `LevelContext`
- `BattleDriver`
- `RoundBattleDriver`
- `BattleField`
- `BattleEventProcessor`
- old `Battle` View classes

Only use those names in implementation if they are confirmed in Hybrid or the task is explicitly to migrate/create that architecture.

## Cross-Area Tasks

For tasks involving both UI and battle/system code:

1. Confirm the runtime or system data source first.
2. Confirm the UI display and interaction surface second.
3. Define the exact handoff: method, command, query, model field, event, or callback.
4. Avoid vague conclusions such as "UI adapts to the system"; name the actual interface or mark it unknown.
5. Validate both compile-time references and the user-facing flow when possible.
