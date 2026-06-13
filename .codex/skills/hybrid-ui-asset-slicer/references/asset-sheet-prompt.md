# Asset Sheet Prompt

Use this prompt as the default when generating a new sheet. Replace the reference-image sentence as needed, but keep the sheet layout, green-screen, no-text, and isolation rules. Normal UI text is always produced later in Unity with TMP, not baked into cutout PNGs.

```text
Create a clean 2D game asset sheet on a flat pure chroma green background (#00ff00), no shadows outside each asset cell, no labels, no text, no numbers, no Chinese characters, no Latin letters, no pseudo-glyphs. Style must match the provided reference image: polished vertical WeChat mini-game roguelike, cute fantasy grassland, thick dark outlines, high saturation, glossy cartoon UI, chibi heroes, goblin enemies, glowing blue crystal core.

Canvas: square asset sheet, 4 columns x 4 rows, each asset centered in its own cell with generous empty green margin and no overlap.

Assets in order left-to-right, top-to-bottom:
1 glowing blue crystal core on small stone pedestal, full object, front view, transparent-ready edges.
2 chibi ranger hero with green hat, bow, leather outfit, idle front 3/4 view.
3 chibi knight hero with silver helmet, small shield and red mace, idle front 3/4 view.
4 chibi axe warrior hero with green hair, black moustache, large axe, idle front 3/4 view.
5 single green goblin monster with yellow ears and small club, idle front 3/4 view.
6 circular arrow rain skill icon, blue disk with three arrows, thick rim.
7 circular fireball skill icon, red/orange flame, thick rim.
8 circular guard aura skill icon, purple shield with white star, thick rim.
9 large orange rounded button, glossy, thick dark outline, completely blank, no text.
10 large blue rounded button, glossy, thick dark outline, completely blank, no text.
11 medium purple rounded button, glossy, thick dark outline, completely blank, no text.
12 beige rounded result panel, thick gold/dark outline, empty interior, completely blank, no text.
13 tall beige upgrade card, thick gold/dark outline, empty interior, completely blank, no text.
14 circular settings gear icon, cream disk, dark gear, thick outline.
15 green health bar fill only, glossy rounded capsule, no frame, no background.
16 blue experience bar fill only, glossy rounded capsule, no frame, no background.

Important: every asset must be isolated and fully visible, no cropping, no background scenery, no Chinese characters, no words, no UI labels, no numbers, no decorative fake text marks. All button/card/panel labels will be added in Unity TMP later. Use the same rendering quality and proportions as the provided reference image.
```

Title-art exception: only when the user explicitly asks for title/logo/banner slices, create a separate sheet or clearly named optional cells for those art assets. Do not mix title-art exceptions into the standard textless controls sheet unless requested.

For title/logo/banner art and other complex visual assets, generate the artwork directly with imagegen as a transparent PNG or dedicated source image, then use scripts only for mechanical trimming, padding, validation, and Unity placement. Do not use Python/PIL as the primary renderer for Chinese title lettering, logos, characters, painterly backgrounds, or reference-style UI art.

Large asset placement: generated cutouts with either dimension greater than 1024px must be saved under `Assets/Res/UI/Common/Sprites/Large/`; smaller controls may stay in the target view sprite folder.

Encoding note: if a script creates preview images containing Chinese labels, store those strings in UTF-8 files or use Python Unicode escapes. Do not type Chinese directly into PowerShell heredocs for Python preview scripts, because it can be converted to question marks on Windows.
