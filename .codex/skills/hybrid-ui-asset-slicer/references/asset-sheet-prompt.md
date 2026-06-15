# Asset Sheet Prompt Reference

Use this reference when generating Hybrid single-screen effect mockups and strongly constrained cutout source sheets. The final output belongs under `UI_Mockups/UI/素材`, not `Assets/Res/UI` and not `UI_Mockups/AssetSlices_Prototype`.

## Effect-First Rule

- Treat the final single-screen effect mockup as the visual source of truth.
- Generate the effect mockup directly as a polished `1080x1920` screen. Do not build the final effect mockup by compositing slice assets unless the user explicitly asks for a preview composition.
- Generate cutout source sheets from strong references to the effect mockup and A V2 base-style image.
- In source-sheet prompts, require each asset to copy the matching effect element's silhouette, proportions, color palette, border thickness, material, highlight style, and facing direction. Do not merely ask for the same concept.
- If a batch source sheet drifts, regenerate the problem asset alone or in a lower-density sheet.
- Keep independently placed concrete assets independent: core, heroes, monsters, skill icons, title art, special decorations, and major interactable objects.

## Effect Mockup Generation

Use this prompt shape when a view effect mockup is missing or needs regeneration. The effect mockup is the final visual reference.

```text
Use case: game UI mockup
Asset type: Hybrid project <Menu | Battle | Battle upgrade selection | Result | Loading> effect mockup
Input images: use the matching current single-screen *_Effect.png as the layout/content standard, and use BaseStyle_ComponentBoard_A_v2_source.png as the base-style component standard. Use old four-panel mockups only as loose historical reference if needed.
Primary request: Generate one complete vertical mobile UI screen for <view>. The output must be a single full-screen effect mockup, exactly 1080x1920, not a collage.
Style/medium: polished Q-version fantasy mobile game UI, A V2 base style, thick dark outlines, light beige panels, grey metal corners, flat colors, simplified highlights, clean silhouettes.
Composition/framing: one interface only, full-bleed 9:16 mobile screen. Match the current standard set's layout density, element scale, camera framing, and UI hierarchy for this view unless the user explicitly requests a layout change.
Constraints: Do not combine multiple views. Do not create a 2x2 or A/B/C/D four-panel mockup. Do not add quadrant labels, A/B/C/D markers, panel dividers, black split lines, or comparison frames.
Text policy: normal UI labels may appear in effect mockups for visual communication, but ordinary reusable cutout assets generated later must be textless.
Output path: UI_Mockups/UI/效果图/<View>/<View>_Effect.png
```

Current standard effect mockup paths:

- `UI_Mockups/UI/效果图/Menu/Menu_Effect.png`
- `UI_Mockups/UI/效果图/Battle/Battle_Effect.png`
- `UI_Mockups/UI/效果图/Battle/Battle_UpgradeSelect_Effect.png`
- `UI_Mockups/UI/效果图/Result/Result_Effect.png`
- `UI_Mockups/UI/效果图/Loading/Loading_Effect.png`
- `UI_Mockups/UI/效果图/通用/BaseStyle_ComponentBoard_A_v2_source.png`
- `UI_Mockups/UI/效果图/preview_effect_screens.png` is for human review only. Do not use it as an imagegen reference because it is a combined preview sheet.

## Asset Sheet Generation

```text
Use case: game UI asset sheet
Asset type: Hybrid project <Common base style | Menu | Battle | Result> cutout sheet
Input images: use the matching single-screen 1080x1920 effect mockup as the visual source of truth, and use BaseStyle_ComponentBoard_A_v2_source.png as the only UI style reference.
Primary request: Generate a clean source sheet on a perfectly flat solid #00ff00 chroma-key background. Place one complete asset in each requested cell/area with generous margin, no overlap, and no cropped edges.
Style/medium: polished Q-version fantasy mobile game UI, A V2 base style, thick dark outlines, light beige panels, grey metal corners, flat colors, simplified highlights, clean silhouettes.
Constraints: Background must be uniform #00ff00 only, with no shadows, gradients, texture, scenery, floor plane, watermark, or extra marks. Ordinary UI controls must be textless. Do not include labels, numbers, Latin letters, Chinese characters, or pseudo-glyphs except for the title-art exceptions explicitly listed below. Generate each independently placed hero, monster, core, icon, or decoration as a separate complete asset unless a grouped asset is explicitly requested. For each requested asset, copy the matching effect mockup element's silhouette, proportions, color palette, border thickness, material, highlight style, and facing direction; do not redesign it.
Title-art exceptions: <list exact assets and exact text, or "none">.
Assets in order: <list the requested assets>.
Avoid: four-panel references, mixed-view references, partial adjacent assets, touching cell borders, fake text, cropped outlines, distorted characters, baked runtime UI copy, merged heroes, merged gameplay objects, and concept-only redesigns that drift from the effect mockup.
```

## Optional Composition Preview

Use composition only as an optional alignment preview or debugging aid. Do not use it for final effect mockup delivery unless explicitly requested.

```text
Canvas: 1080x1920 RGBA.
Background: use the view background PNG, resized to fill the canvas.
Layers: list final PNG slice paths with x, y, scale or size, and zOrder. Use size + nineSlice [left, top, right, bottom] for stretchable controls; use scale only for concrete art such as cores, heroes, monsters, icons, sparkles, and decorations.
Text preview: ordinary UI text may be drawn by the composition script for mockup communication, but the underlying normal UI sprites remain textless.
Output path: UI_Mockups/UI/效果图/<View>/<View>_ComposedPreview.png
```

## Layout Guidance

- Use 2x2 or another low-density sheet for large panels, cards, long banners, buttons, result panels, and title-art.
- Use 3x3 or 4x4 only for smaller icons, sprites, effects, progress fills, and compact decorations.
- Treat the grid as a generation guide only. Final slicing must use connected-component bounds or hand-entered boxes from visual inspection.
- If a generated sheet ignores the grid but assets are complete and isolated, keep it and slice by true bounds.
- If an asset is visibly incomplete, merged with another asset, contains unintended text, or has poor title text, reject that source and regenerate.
- If a concrete asset is meant to be positioned independently in Unity, reject sheets that merge it with adjacent concrete assets.
- If a generated source sheet produces assets that are noticeably different from the effect mockup, reject it and regenerate the problem asset alone or at lower sheet density.
- Effect mockups themselves are not chroma-key sheets. Only cutout source sheets use `#00ff00`.

## Routing Defaults

- `Common/基础风格`: textless reusable base-style UI parts only.
- `Menu`: menu-only background, logo/title art, subtitle ribbon, start-button base, menu decorations.
- `Battle`: battle background, HUD, settings, heroes, monsters, core, effects, upgrade-selection title/card/icons.
- `Result`: result background, result title, victory banner, result panel, stat tiles, result icons, return button, performance bars, confetti.
- `Loading`: global loading background, loading core, progress bar slot/fill, status panel, sparkles, and other loading-only decorations.
- `UI_Mockups/UI/效果图/<View>`: one `1080x1920` effect mockup per view, never a combined four-panel image.
- Existing `*_Effect.png` files under `UI_Mockups/UI/效果图` are the current visual standard for future source-sheet generation.
- `{View}_source`: generated source sheets/backgrounds.
- `{View}_preview.png`: contact-sheet preview for inspection.
- `{View}_source/{View}_layout_manifest.json`: optional composition manifest for debugging or alignment previews only.

## Text Policy

Normal UI text is always produced later in Unity TMP. Title/logo/ribbon/banner artwork may contain text only when the user requested that exact title-art exception. Spell out the exact allowed text in the prompt and validate it manually in the preview.

## Mechanical Processing

After image generation, use scripts only for chroma-key removal, crop/fit/padding, preview composition, and validation. Do not use Python/PIL as the primary renderer for Chinese title lettering, fantasy UI panels, icons, characters, monsters, or backgrounds.
