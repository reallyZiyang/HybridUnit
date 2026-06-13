---
name: hybrid-ui-asset-slicer
description: Generate and slice prototype Unity UI and 2D battle sprites for the Hybrid project from a reference effect image. Use when producing transparent PNG cutouts for roguelike battle UI, hero/monster sprites, skill icons, textless buttons, panels, cards, settings icons, title-art exceptions, or split progress bars under UI_Mockups/AssetSlices_Prototype. Enforce that normal UI text is made in Unity TMP, not baked into cutout PNGs.
---

# Hybrid UI Asset Slicer

## Purpose

Use this project-local skill to turn a polished reference mockup into reusable prototype PNG assets for the Hybrid Unity project.

Hard text rule:

- Do not bake ordinary UI text into generated cutouts. Unity TMP owns labels, numbers, descriptions, button text, stats, percentages, panel/card titles, and all runtime-localized copy.
- Buttons, panels, upgrade cards, skill icons, progress bars, hero/monster sprites, settings icons, and battle sprites must be textless.
- The only allowed text-bearing cutouts are explicit title-art exceptions requested by the user, such as a main logo or decorative subtitle ribbon. Treat these as art assets, not normal UI labels.
- If an imagegen sheet contains unintended letters, Chinese characters, numbers, or pseudo-text on a non-exception asset, reject that sheet and regenerate before slicing.

The stable workflow is:

1. Use `imagegen` to create a single 4x4 asset sheet on pure chroma green.
2. Copy the generated sheet to `UI_Mockups/generated_asset_sheet_source.png`.
3. Run `scripts/generate_asset_slices.py <project-root>`.
4. Review `UI_Mockups/AssetSlices_Prototype/preview_asset_sheet.png` and `preview_in_context.png`.

Do not directly crop the original mockup/effect image for final assets. Original mockups usually have baked backgrounds, occlusion, shadows, and incomplete elements.

Generation responsibility rule:

- Use `imagegen` for visual creation: mockups, title/logo art, decorative subtitle banners, backgrounds, characters, monsters, skill icons, and complex UI art direction.
- Use Python only for mechanical processing: green-screen removal, crop/fit/padding, progress bg/fill separation, deterministic simple bars, preview composition, and validation.
- Do not use Python/PIL to recreate complex title lettering, Chinese art text, logos, painterly backgrounds, characters, or fantasy UI style. If the output needs to look like the reference art, generate it with `imagegen` first and then only process it mechanically.
- All final visual cutouts must originate from an `imagegen` output or an approved existing art asset. Python/PIL must not draw final panel, button, card, icon, character, ribbon, or fantasy UI artwork from primitives as a substitute for generated art.
- For stretchable UI art such as panels, buttons, stat tiles, and bar slots, generate assets with clean corners, straight edges, and enough interior area for 9-slice use. Record the intended border size when moving the asset into Unity.
- Title-art exceptions should usually be generated as direct transparent PNGs, not as part of the standard textless control sheet, unless the user explicitly asks for a title-art sheet.

Encoding rule:

- Avoid putting Chinese text directly into inline PowerShell-to-Python heredocs. On some Windows shells this can arrive in Python as `????`, which makes previews misleading even when Unity prefab text is correct.
- For preview-only Chinese text, read strings from a UTF-8 file or use Python Unicode escapes such as `\u5f00\u59cb\u6218\u6597`.
- Do not use Python preview text rendering as proof that TMP is correct. Validate real UI text in the prefab YAML and in Unity/TMP.
- Do not bake fallback Chinese preview text into final cutouts; ordinary labels must remain Unity TMP text.

Large asset placement rule:

- If either output dimension is greater than `1024px`, place the PNG under `Assets/Res/UI/Common/Sprites/Large/`.
- Keep smaller UI-control and battle-sprite outputs in the relevant view-specific or common sprite directory.
- Preserve `.meta` GUIDs when moving an existing large sprite, and update prefab references only when a new sprite GUID is introduced.

## Imagegen Prompt

Read `references/asset-sheet-prompt.md` and adapt only the reference-image wording or requested asset list. Keep these constraints:

- Pure chroma green background: `#00ff00`.
- 4 columns x 4 rows.
- One centered asset per cell, no overlap, generous margin.
- No text, labels, Chinese characters, numbers, Latin letters, pseudo-glyphs, or scenery on ordinary assets.
- Keep UI controls textless because Unity TMP renders all labels at prefab/runtime level.
- Include title-art text only when the user explicitly asks for named title/logo/banner slices; do not include it in the standard sheet.
- Thick dark outline, glossy cartoon UI, Q-version fantasy style.
- Progress bg/frame and fill visuals must come from imagegen or approved existing art. Scripts may only split, crop, fit, pad, remove chroma key, and validate them.

After imagegen returns a sheet, copy the chosen PNG into:

`UI_Mockups/generated_asset_sheet_source.png`

Leave the original generated image in place.

## Script Usage

Run from the project root:

```powershell
python .codex/skills/hybrid-ui-asset-slicer/scripts/generate_asset_slices.py .
```

If no argument is provided, the script may infer the project root from its location, but passing `.` is preferred.

The script writes:

- `UI_Mockups/AssetSlices_Prototype/*.png`
- `UI_Mockups/AssetSlices_Prototype/preview_asset_sheet.png`
- `UI_Mockups/AssetSlices_Prototype/preview_in_context.png`

The project may also keep a copy at `UI_Mockups/generate_asset_slices.py`; the skill script is the canonical workflow source.

## Required Outputs

The standard output set is:

- `battle_core_256.png`
- `hero_ranger_idle_256.png`
- `hero_knight_idle_256.png`
- `hero_axe_idle_256.png`
- `monster_goblin_idle_128.png`
- `skill_arrow_rain_128.png`
- `skill_fireball_128.png`
- `skill_guard_aura_128.png`
- `ui_button_orange_512x160.png`
- `ui_button_blue_512x160.png`
- `ui_button_purple_360x120.png`
- `ui_panel_result_720x640.png`
- `ui_card_upgrade_300x560.png`
- `ui_icon_settings_128.png`
- `ui_bar_hp_bg_512x48.png`
- `ui_bar_hp_fill_512x48.png`
- `ui_bar_exp_bg_512x48.png`
- `ui_bar_exp_fill_512x48.png`

All standard outputs must be textless. If a task needs title art, add explicit optional outputs such as `ui_title_logo_*.png` or `ui_subtitle_banner_*.png`; do not put normal button/card/panel text into these standard assets.

## Validation Rules

Always run the script validation and treat failures as blocking:

- Every expected PNG exists and matches its filename size.
- Every non-preview PNG has transparent corners.
- No non-preview PNG has alpha pixels on the canvas edge.
- Progress `*_bg` images contain frame/slot only, no bright fill.
- Progress `*_fill` images contain fill/highlight only, no dark frame or slot.
- Standard cutouts contain no baked text, labels, numbers, Chinese characters, Latin letters, or pseudo-text. Manually inspect `preview_asset_sheet.png`; automatic validation does not replace this check.
- Title-art exceptions are only valid when explicitly requested and must be named as title/logo/banner art.
- Any output with width or height greater than `1024px` is stored under `Assets/Res/UI/Common/Sprites/Large/`.
- The script must not read `CharacterPrefabs`, old GUI assets, or directly crop the original four-panel mockup.
- Generated UI surfaces that will be stretched in Unity must include an explicit suggested 9-slice border and must be validated with transparent edges and intact corners.

If a cutout is incomplete, expand the source crop box and/or add output padding. Do not accept edge alpha.

## Unity Notes

These are prototype PNGs for Canvas `Image` or `SpriteRenderer`. This skill does not create Unity `.meta`, SpriteAtlas, 9-slice metadata, pressed/disabled button states, or animation frames.

Use TMP for all normal Unity UI text. When composing prefabs, place TMP labels over the textless button/panel/card sprites instead of generating new text-bearing PNGs.
