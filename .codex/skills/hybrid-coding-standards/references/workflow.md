# Workflow Guardrails

This reference adapts the old agent workflow for Codex work in the Hybrid repository.

## Read Order

Use this order before implementation:

1. Repository or task instructions already active in the conversation.
2. User-provided seed files, target paths, or named feature directories.
3. Direct companion files:
   - interface for a system implementation
   - system implementation for an interface
   - model/data used by the target system
   - `*.Bindings.cs` or partial files for a View
   - runtime managers, buffers, or data structs directly referenced by a battle file
4. One more layer of direct dependency only when a single missing fact blocks a correct edit.

Avoid reading whole modules, whole UI trees, whole battle trees, or old-project directories unless the task explicitly asks for a migration or comparison.

## Bounded Exploration

Seed-file expansion is limited:

- Layer 0: seed or target files.
- Layer 1: files directly referenced by Layer 0.
- Layer 2: one directly referenced file group needed to resolve one missing fact.
- Layer 3 and beyond: stop and report instead of widening scope.

Search is exceptional:

- Use `rg` or `rg --files`.
- Scope search to the smallest known path or pattern.
- Search for one concrete symbol, file name, namespace, field, prefab key, or API at a time.
- Do not use search as a replacement for reading the known seed chain.

## Stop Conditions

Stop and ask or report uncertainty when:

- more than one key fact remains unknown after the two-layer read budget
- the task belongs to another layer than first described, such as UI evidence pointing to system data or battle evidence pointing to config/resource setup
- implementation would require editing generated code, third-party package code, or unrelated modules
- old project references conflict with current Hybrid repository facts
- a required class, field, binding, prefab, atlas, protocol, or asset key cannot be confirmed

## Anti-Hallucination Rules

- File-confirmed information is fact. Similar old modules are examples, not facts.
- Do not invent field names, protocol names, request/response types, UI node names, prefab keys, atlas names, or refresh chains.
- Do not assume a similar module behaves identically unless the target module confirms the same flow.
- Do not hide uncertainty behind generic wording like "wire it to the UI" or "adapt the data source"; identify the concrete source or mark it unknown.
- Do not use changed-files inspection or git status as a routine first step. Use it only when the user asks, when preparing a commit, or when a dirty-worktree question affects the task.

## Implementation Shape

Default to the smallest change that fixes the confirmed behavior:

- Put business rules in systems or runtime services, not in UI views.
- Put UI display and interaction glue in views or UI nodes, not in gameplay runtime internals.
- Preserve existing partial class, namespace, folder, and naming patterns.
- Add abstractions only when existing local patterns already call for them or when they remove real duplication in the touched surface.
- Keep generated files separate from hand-written code.

## Reporting Shape

For completed work:

```text
Changed:
- ...
Reference pattern:
- ...
Validated:
- ...
Not verified / risk:
- ...
```

For uncertain work:

```text
Confirmed facts:
- ...
Unknowns:
- ...
Next implementation path:
- ...
```
