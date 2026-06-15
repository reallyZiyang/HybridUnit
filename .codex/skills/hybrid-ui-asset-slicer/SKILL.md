---
name: hybrid-ui-asset-slicer
description: Generate effect-first Hybrid UI 1080x1920 mockups and strongly constrained transparent PNG slices under UI_Mockups/UI. Use when producing per-view Menu, Battle, Result, Loading, upgrade-selection mockups, base-style common UI, title-art exceptions, textless buttons/panels/cards/bars/icons, chibi battle sprites, effects, source sheets, or preview sheets from references. Enforce one view per effect image, effect mockup as visual source of truth, regenerated cutout sheets that closely match the effect, and Unity TMP for normal UI text.
---

# Hybrid UI Asset Slicer

## Core Rule

Use this skill to create mockup-slice assets, not Unity runtime resources.

- Write final assets under `UI_Mockups/UI/素材`.
- Do not write to `Assets/Res/UI`, do not create `.meta`, do not configure atlases, and do not edit prefabs.
- Do not generate new A/B/C/D four-panel combined effect mockups. Old four-panel images may be used only as historical references, not as deliverables or direct cutting targets.
- Do not directly crop effect mockups for final assets. Regenerate clean source sheets with `imagegen`, then mechanically key, trim, pad, validate, and preview.
- Treat the final single-screen effect mockup as the visual source of truth. Slice assets are regenerated from strong references to that effect mockup; they are not allowed to redesign the object.
- Keep ordinary UI controls textless. Unity TMP owns labels, numbers, descriptions, button text, stats, percentages, panel/card titles, and runtime-localized copy.
- Allow baked text only for explicit title-art exceptions such as main logo, subtitle ribbon, result title, or victory banner. Name these as title/logo/ribbon/banner assets.

## Effect Mockup Rule

Generate interface effect mockups as separate full-screen images and use them as the visual source of truth:

- Each effect mockup must be exactly one interface, vertical, `1080x1920`.
- Do not combine multiple interfaces into one image. Do not add A/B/C/D markers, quadrant labels, panel dividers, or four-panel borders.
- Save single-screen effect mockups here:
  - `UI_Mockups/UI/效果图/Menu/Menu_Effect.png`
  - `UI_Mockups/UI/效果图/Battle/Battle_Effect.png`
  - `UI_Mockups/UI/效果图/Battle/Battle_UpgradeSelect_Effect.png`
  - `UI_Mockups/UI/效果图/Result/Result_Effect.png`
  - `UI_Mockups/UI/效果图/Loading/Loading_Effect.png`
  - `UI_Mockups/UI/效果图/通用/BaseStyle_ComponentBoard_A_v2_source.png`
- Use the matching single-screen effect mockup as the semantic and appearance reference when generating a view's cutout sheet. Do not use a combined four-panel mockup as the semantic reference for Menu, Battle, Result, or Loading slices.

Effect-first consistency rule:

- Generate the final effect mockup directly with imagegen at `1080x1920`. It should be visually polished enough to present by itself.
- Treat the effect mockup as the source of truth for object shape, proportions, colors, outlines, material, orientation, and UI control appearance.
- When regenerating or extending a view effect mockup, use the matching existing `*_Effect.png` as the layout/content standard and `通用/BaseStyle_ComponentBoard_A_v2_source.png` as the base-style component standard.
- Use `UI_Mockups/UI/效果图/preview_effect_screens.png` only for human review of the standard set. Do not use that preview sheet as an imagegen reference because it combines multiple screens.
- If a new effect mockup replaces one of the standard files, validate it is still one screen at `1080x1920`, has no A/B/C/D labels, and does not drift from the corresponding standard layout unless the user explicitly requested a layout change.
- Do not directly crop effect mockups for final assets. Generate clean chroma-key source sheets that strongly match the effect mockup, then mechanically key, trim, pad, validate, and preview.
- In asset-sheet prompts, explicitly require each asset to copy the effect mockup element's silhouette, proportions, color palette, border thickness, material, highlight style, and facing direction. Do not merely ask for the same concept.
- Generate large or high-risk assets at low density, ideally one asset or 2x2 per sheet. Use 4x4 only for small icons, effects, or simple decorations.
- Keep concrete visual assets separate when they may need independent placement: core, heroes, monsters, skill icons, special decorations, title art, and major interactable objects. Do not merge three heroes into one group unless the requested final asset is explicitly a grouped foreground.
- Ordinary UI controls may be reused from `Common/基础风格`; view-specific concrete assets belong in the matching view folder.

## Output Routing

Mirror the mockup-library organization that matches the project view names:

- `UI_Mockups/UI/素材/Common/基础风格`: reusable base-style components only.
- `UI_Mockups/UI/素材/Menu`: Menu-view-specific background, title art, and menu-only decorations.
- `UI_Mockups/UI/素材/Battle`: Battle view assets, including upgrade-selection assets because the Unity project has `Views/Battle`.
- `UI_Mockups/UI/素材/Result`: Result view assets.
- `UI_Mockups/UI/素材/Loading`: global loading-screen assets used for hot update, first entry, and scene switching mockups.
- `UI_Mockups/UI/素材/{View}_source`: copies of generated source sheets/backgrounds used for that view.
- `UI_Mockups/UI/素材/{View}_preview.png`: contact-sheet preview for manual inspection.
- `UI_Mockups/UI/素材/{View}_source`: generated source sheets/backgrounds used for that view.

Do not put view-specific assets in `Common`. Do not put gameplay, Menu, Battle, or Result assets into `Common/基础风格`.

## Generation Workflow

1. Generate or select the final single-screen effect mockup first. The effect mockup is the visual source of truth and must be a direct, polished `1080x1920` image.
2. Use the matching effect mockup plus `BaseStyle_ComponentBoard_A_v2_source.png` as strong references for cutout source sheets.
3. Generate transparent-ready source sheets on pure `#00ff00` chroma green. Generate full-screen backgrounds separately without UI overlays when requested.
4. Generate concrete assets as independent PNGs when they need independent placement: heroes, monsters, core, icons, special decorations, title art, and major interactable objects.
5. Choose sheet density by asset type:
   - Use 2x2 or other low-density sheets for large panels, cards, long banners, buttons, and title-art.
   - Use 3x3 or 4x4 only for smaller icons, sprites, effects, or simple decorations with generous spacing.
6. Copy generated sources into the matching `{View}_source` folder. Leave the original imagegen output in place.
7. Slice with real asset bounds: use connected-component analysis or hand-entered source boxes from visual inspection. Treat grid cells only as a generation guide; never trust fixed grid slicing blindly.
8. Remove chroma key only as mechanical processing. Do not redraw final UI art, title lettering, characters, fantasy panels, or icons in Python.
9. Crop to non-transparent pixels, add transparent padding, generate a preview sheet, and run validation.
10. Manually inspect the asset preview against the effect mockup. Reject source sheets where assets are redesigned, noticeably drift in silhouette/proportion/color, contain text unexpectedly, include green background residue, or are cut off.

## Prompt Reference

Read `references/asset-sheet-prompt.md` before generating a new source sheet. Adapt the view name, asset list, and title-art exceptions. Keep:

- pure `#00ff00` background for cutout sheets,
- single-screen effect mockups are not chroma-key sheets and must remain normal full-scene `1080x1920` images,
- one complete asset per requested cell/area,
- generous margins,
- no ordinary text, numbers, labels, Latin letters, Chinese characters, or pseudo-glyphs,
- exact text only for named title-art exceptions,
- A V2 base style: light beige panels, thick black outline, grey metal corners, flat colors, simplified highlights.

## Script Usage

Use `scripts/slice_asset_sheet.py` for the current workflow when you have a generated sheet and a JSON manifest of true crop boxes:

```powershell
python .codex/skills/hybrid-ui-asset-slicer/scripts/slice_asset_sheet.py `
  --sheet UI_Mockups/UI/素材/Battle_source/Battle_Generated_Sheet.png `
  --manifest UI_Mockups/UI/素材/Battle_source/Battle_manifest.json `
  --out-dir UI_Mockups/UI/素材/Battle `
  --preview UI_Mockups/UI/素材/Battle_preview.png
```

Manifest shape:

```json
{
  "assets": [
    { "name": "Battle_HudPanelBase.png", "box": [18, 80, 400, 245], "pad": 24 },
    { "name": "Battle_UpgradeTitle.png", "box": [22, 955, 405, 1095], "pad": 24, "title_art": true }
  ]
}
```

The legacy `scripts/generate_asset_slices.py` is the old hard-coded prototype slicer for `UI_Mockups/AssetSlices_Prototype`; do not use it for the current `UI_Mockups/UI/素材` workflow unless a user explicitly asks for the old prototype output.

`scripts/compose_effect_from_manifest.py` is optional and only for alignment previews or debugging. Do not use it to create final effect mockups unless the user explicitly asks for a composed preview:

```powershell
python .codex/skills/hybrid-ui-asset-slicer/scripts/compose_effect_from_manifest.py `
  --manifest UI_Mockups/UI/素材/Menu_source/Menu_layout_manifest.json `
  --out UI_Mockups/UI/效果图/Menu/Menu_Effect.png `
  --project-root .
```

Optional preview manifest shape:

```json
{
  "canvas": [1080, 1920],
  "background": { "path": "UI_Mockups/UI/素材/Menu/Menu_Background.png" },
  "layers": [
    { "path": "UI_Mockups/UI/素材/Menu/Menu_CoreCrystal.png", "x": 360, "y": 575, "scale": 1.0, "zOrder": 20 },
    { "path": "UI_Mockups/UI/素材/Common/基础风格/Base_ButtonOrange.png", "x": 120, "y": 1510, "size": [840, 220], "nineSlice": [92, 64, 92, 64], "zOrder": 40 },
    { "type": "text", "text": "开始战斗", "x": 540, "y": 1710, "anchor": "center", "fontSize": 86, "zOrder": 90 }
  ]
}
```

## Validation Rules

Treat validation failures as blocking:

- Output directory contains only assets for that view/category.
- Every interface effect mockup is `1080x1920`, contains one view only, and has no A/B/C/D labels or four-panel dividers.
- Final effect mockups are directly generated images unless the user explicitly asked for a composed preview.
- Effect mockups are the visual source of truth for matching slice assets.
- Slice assets visibly match the corresponding effect mockup element's silhouette, proportions, color palette, border thickness, material, highlight style, and facing direction.
- If a generated asset sheet drifts noticeably from the effect mockup, regenerate that asset alone or in a lower-density sheet; do not accept a poor batch sheet.
- Every transparent PNG is non-empty, has transparent corners, has no alpha pixels on the canvas edge, and keeps at least 8px transparent margin.
- No obvious pure chroma-green background remains on cutouts.
- Ordinary controls contain no baked text, numbers, labels, Chinese characters, Latin letters, or pseudo-text.
- Progress bar slots and fills are separated when separate assets are requested.
- Preview shows no adjacent asset fragments, cropped corners, broken outlines, missing limbs, or title-art truncation.
- Title-art exceptions are readable and match the requested text exactly.

## Unity Notes

These outputs are mockup-library PNGs for later Unity work. When building prefabs, place TMP labels over textless sprites instead of generating text-bearing normal UI PNGs.
