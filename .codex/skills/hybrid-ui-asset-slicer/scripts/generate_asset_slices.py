# -*- coding: utf-8 -*-
"""Legacy fixed-output prototype slicer.

This script predates the current Hybrid mockup asset workflow. It writes the
old hard-coded `UI_Mockups/AssetSlices_Prototype` output and should not be used
for the current `UI_Mockups/UI/素材/{Common/基础风格|Menu|Battle|Result}`
deliverables unless the user explicitly asks for the legacy prototype output.

Use `slice_asset_sheet.py` with a manifest of true asset bounds for the current
workflow.
"""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


def resolve_project_root(arg: str | None) -> Path:
    if arg:
        return Path(arg).resolve()
    # script path: <project>/.codex/skills/hybrid-ui-asset-slicer/scripts/generate_asset_slices.py
    return Path(__file__).resolve().parents[4]


ASSETS = {
    "battle_core_256.png": ((24, 31, 320, 335), (256, 256), 8),
    "hero_ranger_idle_256.png": ((357, 54, 629, 330), (256, 256), 8),
    "hero_knight_idle_256.png": ((667, 55, 944, 337), (256, 256), 8),
    "hero_axe_idle_256.png": ((958, 42, 1235, 323), (256, 256), 8),
    "monster_goblin_idle_128.png": ((50, 390, 310, 603), (128, 128), 6),
    "skill_arrow_rain_128.png": ((347, 377, 594, 623), (128, 128), 5),
    "skill_fireball_128.png": ((638, 354, 936, 646), (128, 128), 3),
    "skill_guard_aura_128.png": ((949, 379, 1212, 623), (128, 128), 5),
    "ui_button_orange_512x160.png": ((23, 706, 332, 856), (512, 160), 0),
    "ui_button_blue_512x160.png": ((343, 706, 640, 856), (512, 160), 0),
    "ui_button_purple_360x120.png": ((651, 711, 879, 851), (360, 120), 5),
    "ui_panel_result_720x640.png": ((884, 660, 1242, 900), (720, 640), 8),
    "ui_card_upgrade_300x560.png": ((80, 887, 296, 1178), (300, 560), 0),
    "ui_icon_settings_128.png": ((345, 933, 569, 1160), (128, 128), 5),
    "ui_bar_hp_fill_512x48.png": ((596, 1015, 884, 1105), (512, 48), 4),
    "ui_bar_exp_fill_512x48.png": ((910, 1014, 1204, 1104), (512, 48), 4),
}


def clear_output() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for path in OUT.glob("*.png"):
        path.unlink()


def save(img: Image.Image, name: str) -> Image.Image:
    OUT.mkdir(parents=True, exist_ok=True)
    img.save(OUT / name)
    return img


def remove_green_screen(img: Image.Image) -> Image.Image:
    rgba = img.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    alpha = Image.new("L", (w, h), 255)
    ap = alpha.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            green = g > 145 and g > r * 1.35 and g > b * 1.35
            edge_green = g > 105 and g > r * 1.18 and g > b * 1.18
            if green:
                ap[x, y] = 0
            elif edge_green:
                ap[x, y] = int(max(0, min(255, (abs(g - 120) + abs(r - 40) + abs(b - 40)) * 1.4)))
    alpha = alpha.filter(ImageFilter.GaussianBlur(0.35))
    rgba.putalpha(alpha)
    return rgba


def alpha_bbox(img: Image.Image) -> tuple[int, int, int, int]:
    alpha = img.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        return (0, 0, img.width, img.height)
    x1, y1, x2, y2 = bbox
    pad = 3
    return (max(0, x1 - pad), max(0, y1 - pad), min(img.width, x2 + pad), min(img.height, y2 + pad))


def fit(img: Image.Image, size: tuple[int, int], pad: int) -> Image.Image:
    img = img.crop(alpha_bbox(img))
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    max_w = max(1, size[0] - pad * 2)
    max_h = max(1, size[1] - pad * 2)
    scale = min(max_w / img.width, max_h / img.height)
    resized = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.Resampling.LANCZOS)
    out.alpha_composite(resized, ((size[0] - resized.width) // 2, (size[1] - resized.height) // 2))
    return out


def cut_asset(sheet: Image.Image, box: tuple[int, int, int, int], size: tuple[int, int], pad: int) -> Image.Image:
    part = sheet.crop(box)
    keyed = remove_green_screen(part)
    return fit(keyed, size, pad)


def rgba_pixels(img: Image.Image):
    if hasattr(img, "get_flattened_data"):
        return img.get_flattened_data()
    return img.getdata()


def progress_bg() -> Image.Image:
    out = Image.new("RGBA", (512, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    d.rounded_rectangle((1, 1, 510, 46), 22, fill=(9, 13, 18, 245))
    d.rounded_rectangle((5, 5, 506, 42), 18, fill=(42, 49, 55, 255))
    d.rounded_rectangle((14, 10, 498, 38), 13, fill=(13, 22, 28, 255))
    d.rounded_rectangle((18, 14, 494, 34), 10, fill=(28, 40, 47, 255))
    d.rounded_rectangle((18, 14, 494, 22), 8, fill=(53, 65, 72, 120))
    return out


def progress_fill_from_sheet(sheet: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    sample = sheet.crop(box).convert("RGBA")
    colors = []
    for r, g, b, a in rgba_pixels(sample):
        if a == 0:
            continue
        if r < 45 and g < 45 and b < 45:
            continue
        if g > r or b > r:
            colors.append((r, g, b))
    if not colors:
        colors = [(96, 220, 45)]
    avg = tuple(sum(c[i] for c in colors) // len(colors) for i in range(3))
    hi = tuple(min(255, int(c * 1.25) + 18) for c in avg)
    lo = tuple(max(0, int(c * 0.75)) for c in avg)

    out = Image.new("RGBA", (512, 48), (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    d.rounded_rectangle((18, 10, 494, 38), 14, fill=lo + (255,))
    d.rounded_rectangle((22, 12, 490, 33), 10, fill=avg + (255,))
    d.rounded_rectangle((30, 14, 482, 21), 6, fill=hi + (185,))
    return out


def preview_sheet(assets: dict[str, Image.Image]) -> Image.Image:
    out = Image.new("RGBA", (1024, 1536), (34, 51, 42, 255))
    d = ImageDraw.Draw(out)
    x, y = 28, 28
    for name, img in assets.items():
        if name.startswith("preview_"):
            continue
        d.rounded_rectangle((x, y, x + 210, y + 220), 10, fill=(255, 242, 192, 255), outline=(20, 25, 25, 255), width=3)
        thumb = fit(img, (180, 170), 8)
        out.alpha_composite(thumb, (x + 15, y + 10))
        d.text((x + 8, y + 190), name.replace(".png", ""), fill=(18, 24, 24, 255))
        x += 246
        if x > 790:
            x = 28
            y += 240
    return out


def preview_context(assets: dict[str, Image.Image]) -> Image.Image:
    bg = Image.new("RGBA", (1080, 1920), (111, 181, 70, 255))
    d = ImageDraw.Draw(bg)
    for i in range(0, 1080, 90):
        for j in range(0, 1920, 90):
            col = (100 + (i + j) % 40, 171 + (i * 2 + j) % 30, 70, 255)
            d.ellipse((i - 16, j - 8, i + 18, j + 12), fill=col)
    d.rounded_rectangle((34, 28, 1046, 236), 32, fill=(26, 35, 44, 245), outline=(220, 230, 220, 255), width=5)
    bg.alpha_composite(assets["ui_icon_settings_128.png"], (890, 72))
    bg.alpha_composite(assets["ui_bar_hp_bg_512x48.png"], (330, 72))
    bg.alpha_composite(assets["ui_bar_hp_fill_512x48.png"], (330, 72))
    bg.alpha_composite(assets["ui_bar_exp_bg_512x48.png"], (330, 145))
    bg.alpha_composite(assets["ui_bar_exp_fill_512x48.png"], (330, 145))
    for row in range(5):
        for col in range(6):
            bg.alpha_composite(assets["monster_goblin_idle_128.png"], (110 + col * 145 + (row % 2) * 55, 350 + row * 150))
    bg.alpha_composite(assets["battle_core_256.png"], (412, 1230))
    bg.alpha_composite(assets["hero_ranger_idle_256.png"], (170, 1110))
    bg.alpha_composite(assets["hero_knight_idle_256.png"], (415, 1070))
    bg.alpha_composite(assets["hero_axe_idle_256.png"], (665, 1115))
    bg.alpha_composite(assets["ui_button_blue_512x160.png"], (284, 1690))
    return bg


def expected_sizes() -> dict[str, tuple[int, int]]:
    sizes = {name: size for name, (_, size, _) in ASSETS.items()}
    sizes["ui_bar_hp_bg_512x48.png"] = (512, 48)
    sizes["ui_bar_exp_bg_512x48.png"] = (512, 48)
    sizes["preview_asset_sheet.png"] = (1024, 1536)
    sizes["preview_in_context.png"] = (1080, 1920)
    return sizes


def validate_outputs(out_dir: Path) -> list[str]:
    errors: list[str] = []
    for name, size in expected_sizes().items():
        path = out_dir / name
        if not path.exists():
            errors.append(f"missing {name}")
            continue
        img = Image.open(path).convert("RGBA")
        if img.size != size:
            errors.append(f"{name} size {img.size} != {size}")
        if not name.startswith("preview_"):
            alpha = img.getchannel("A")
            corners = [
                alpha.getpixel((0, 0)),
                alpha.getpixel((img.width - 1, 0)),
                alpha.getpixel((0, img.height - 1)),
                alpha.getpixel((img.width - 1, img.height - 1)),
            ]
            if any(corners):
                errors.append(f"{name} has non-transparent corners")
            edge = 0
            for x in range(img.width):
                edge += alpha.getpixel((x, 0)) > 0
                edge += alpha.getpixel((x, img.height - 1)) > 0
            for y in range(img.height):
                edge += alpha.getpixel((0, y)) > 0
                edge += alpha.getpixel((img.width - 1, y)) > 0
            if edge:
                errors.append(f"{name} has alpha on canvas edge: {edge}")

    for name in ("ui_bar_hp_bg_512x48.png", "ui_bar_exp_bg_512x48.png"):
        path = out_dir / name
        if not path.exists():
            continue
        bright = 0
        for r, g, b, a in rgba_pixels(Image.open(path).convert("RGBA")):
            if a > 0 and ((g > 150 and r < 120) or (b > 145 and r < 135)):
                bright += 1
        if bright > 20:
            errors.append(f"{name} contains fill-like bright pixels: {bright}")

    for name in ("ui_bar_hp_fill_512x48.png", "ui_bar_exp_fill_512x48.png"):
        path = out_dir / name
        if not path.exists():
            continue
        dark = 0
        for r, g, b, a in rgba_pixels(Image.open(path).convert("RGBA")):
            if a > 0 and r < 45 and g < 45 and b < 45:
                dark += 1
        if dark:
            errors.append(f"{name} contains frame-like dark pixels: {dark}")

    return errors


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate Hybrid prototype UI asset slices from a chroma-green sheet.")
    parser.add_argument("project_root", nargs="?", help="Hybrid project root. Defaults to the repo root inferred from this skill path.")
    args = parser.parse_args()

    project_root = resolve_project_root(args.project_root)
    ui_mockups = project_root / "UI_Mockups"
    global OUT
    OUT = ui_mockups / "AssetSlices_Prototype"
    source = ui_mockups / "generated_asset_sheet_source.png"

    if not source.exists():
        raise FileNotFoundError(f"Missing source sheet: {source}")
    clear_output()
    sheet = Image.open(source).convert("RGBA")
    assets: dict[str, Image.Image] = {}
    for name, (box, size, pad) in ASSETS.items():
        if name in ("ui_bar_hp_fill_512x48.png", "ui_bar_exp_fill_512x48.png"):
            assets[name] = save(progress_fill_from_sheet(sheet, box), name)
            continue
        assets[name] = save(cut_asset(sheet, box, size, pad), name)
    assets["ui_bar_hp_bg_512x48.png"] = save(progress_bg(), "ui_bar_hp_bg_512x48.png")
    assets["ui_bar_exp_bg_512x48.png"] = save(progress_bg(), "ui_bar_exp_bg_512x48.png")
    save(preview_sheet(assets), "preview_asset_sheet.png")
    save(preview_context(assets), "preview_in_context.png")

    errors = validate_outputs(OUT)
    if errors:
        raise SystemExit("Validation failed:\n" + "\n".join(errors))
    print(f"Generated and validated asset slices: {OUT}")


if __name__ == "__main__":
    main()
