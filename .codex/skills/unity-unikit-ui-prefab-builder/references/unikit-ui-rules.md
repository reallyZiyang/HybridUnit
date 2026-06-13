# UniKit UI Rules

## Core Types

- `UIView`: independently opened page. Open with `UIManager.Open<T>()` or `this.Open<T>()`.
- `UIPanel`: reusable module inside a controller. A referenced panel is initialized through `AsPanel<T>()`.
- `UINode`: ordinary component, list item, or lightweight reusable node.
- `UIDataBinding`: prefab-root binding component that generates partial classes and field initialization.

## Prefab Naming

- View prefab file names must start with `UI_`.
- `UIManager` loads `UI_` + class name. `MainMenuView` loads asset key `UI_MainMenuView`.
- Keep class names and prefab names aligned.

## Binding Naming

Use stable object names for generated fields:

- `Btn*` for `Button`
- `Txt*` for `Text` or `TextMeshProUGUI`
- `Img*` for `Image`
- `Tog*` for `Toggle`
- `Sld*` for `Slider`
- `Input*` for input fields
- `Ddl*` for dropdowns
- `List*` for `ListView`
- `Scroll*` for `ScrollView` or `InfiniteScrollView`

Only bind nodes used by code. Avoid binding decorative children.

After prefab hierarchy or component changes, re-run Scanning before code generation. Scanning may find more named UI components than runtime code needs; remove decorative or static-only nodes from `bindItems` before generating.

## Generation Rules

- Use the `UIDataBinding` inspector: `Scanning`, then review, then `Generate`.
- If `bindItems` changed in any way, including target node, field name, access, reference type, or component type, run `UIDataBindingUtilities.Generate(binding)` again.
- `UIDataBindingUtilities.Generate(binding)` is the canonical generator for both the UI class and `*.Bindings.cs`.
- Do not edit generated `*.Bindings.cs`.
- Do not hand-write generated binding fields such as `m_BtnStart` or `OnInitBindings()`.
- Put behavior logic in the main partial class after UniKit has generated it only when the user explicitly asks for interaction, navigation, data refresh, or business integration.
- Default UI work stops at prefab, sprites/materials, `UIDataBinding`, and generated UI class plus `*.Bindings.cs`. Do not add button handlers, navigation calls, `Render` methods, `[UIDataReceiver]`, fake data, debug logs, or business placeholders by default.
- Do not write the main UI class file from a builder script. `UIDataBindingUtilities.Generate(binding)` already creates the main class through UniKit's template if it does not exist.
- Do not add task-specific codegen methods such as `PatchMainMenuBehavior()` that overwrite `MainMenuView.cs`. If behavior changes are needed, edit only the generated main partial after UniKit has created it.
- Do not leave an editor build script or menu command as the deliverable. Temporary editor automation must be run, verified, and removed before completion unless the user explicitly asks to keep it.
- A UI task is not complete until the final prefab asset exists in the collected UI prefab path and its generated DataBinding code exists in the runtime UI code path.
- Ensure `UISettings.outputPaths` maps the UI prefab source path to the runtime code target before generating.
- For each Image, decide whether it is fixed-size art or a stretchable surface. Stretchable panels, buttons, stat tiles, and bar slots must use the project `UISpriteSlice.SetBorder3` border rule and Image `Type = Sliced`; decorative icons, avatars, ribbons, and confetti stay Simple unless intentionally stretched.
- `UISpriteSlice.SetBorder3` is the UniKit editor tool at `Packages/UniKit-UI/Editor/Tools/UISpriteSlice.cs`: it sets `TextureImporter.isReadable = true`, sets `spriteBorder = (texture.width / 3, texture.height / 3, texture.width / 3, texture.height / 3)`, then reimports with `ImportAssetOptions.ForceUpdate`.
- If automation needs path-based slicing instead of Unity Selection/MenuItem, add an Editor-only static helper with this exact algorithm and call it on explicit texture asset paths. Do not write arbitrary border values by hand in prefab or `.meta` generation.
- After visual UI work, render or screenshot the prefab and compare it against the reference mockup. If key shapes or proportions are clearly off, regenerate the responsible art or adjust the layout before accepting the prefab.

Recommended mapping:

- `source`: `Assets/Res/UI`
- `target`: `Assets/Scripts/Game/Play/Runtime/UI/View`
- `nameSpace`: `Game.Play.UI.View`

## TMP Materials

- All normal labels must be TMP text, not baked sprite text.
- Match effect-image typography by using TMP material variants for face, outline, glow, and underlay shadow.
- Put reusable TMP material variants in `Assets/Res/UI/Common/Material/` with names like `TMP_Button_WhiteBrownOutline.mat`.
- Use the base font material only for plain text. If a text needs outline or shadow, assign a dedicated material variant to the TMP component.
- Use TMP Underlay for shadows. Do not add duplicate sibling text objects to fake one label's outline or shadow.
- If one TMP material cannot fully match the mockup, keep the closest material-only result and report the limitation instead of adding extra text controls.

## Prefab Text Encoding

- Prefer Unity Editor APIs for setting TMP text so Unity owns prefab serialization.
- When directly editing prefab YAML, store Chinese and other non-ASCII `m_text` values as `\uXXXX` escapes.
- Never rely on PowerShell here-strings, default terminal encodings, or scripts without explicit UTF-8 when writing localized prefab text.
- Any YAML-generation script must escape non-ASCII TMP text before writing `m_text`.
- After prefab generation, run `rg 'm_text: "\?\?+' Assets/Res/UI` and fix every accidental question-mark replacement unless the design intentionally uses question marks.

## Runtime Code Patterns

These patterns apply only when behavior is explicitly in scope. If the task is just to make a UI screen or prefab, leave the generated main partial class empty except for framework lifecycle stubs.

Register callbacks in `OnInit()`:

```csharp
protected override void OnInit()
{
    m_BtnClose.SetOnClick(Close);
}
```

Use explicit atlas loading:

```csharp
m_ImgIcon.SetAtlasSprite(UIAtlas.k_Menu, "SpriteName");
m_ImgCommon.SetAtlasSprite("SA_UI_Icon", "IconName");
```

Bind simple lists:

```csharp
m_ListRewards.BindItems<RewardData, RewardItem>(items);
```

Use the project UI extensions when needed:

- `UIContextNode`: access `GameContext` from ordinary UI nodes.
- `UIEffect`: load and control UI particles.
- `UIPointerEventPass`: pass click events through masks except excluded areas.
- `UIUtilities.PlaceTips`: place tooltips near targets with screen-bound clamping.
