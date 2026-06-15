# -*- coding: utf-8 -*-
from __future__ import annotations

import argparse
import json
import shutil
from collections import deque
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


def load_manifest(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as fh:
        data = json.load(fh)
    if not isinstance(data, dict) or not isinstance(data.get("assets"), list):
        raise ValueError("Manifest must be an object with an assets list.")
    return data


def is_keyish(r: int, g: int, b: int, a: int) -> bool:
    if a == 0:
        return False
    return (
        g >= 135
        and r <= 105
        and b <= 105
        and g >= r * 1.5 + 22
        and g >= b * 1.5 + 22
    ) or (g >= 222 and r <= 92 and b <= 92)


def remove_key_connected(img: Image.Image) -> Image.Image:
    rgba = img.convert("RGBA")
    w, h = rgba.size
    pix = rgba.load()
    remove = bytearray(w * h)
    queue: deque[tuple[int, int]] = deque()

    def seed(x: int, y: int) -> None:
        idx = y * w + x
        if remove[idx]:
            return
        r, g, b, a = pix[x, y]
        if is_keyish(r, g, b, a):
            remove[idx] = 1
            queue.append((x, y))

    for x in range(w):
        seed(x, 0)
        seed(x, h - 1)
    for y in range(h):
        seed(0, y)
        seed(w - 1, y)

    while queue:
        x, y = queue.popleft()
        for ny in range(max(0, y - 1), min(h, y + 2)):
            for nx in range(max(0, x - 1), min(w, x + 2)):
                idx = ny * w + nx
                if remove[idx]:
                    continue
                r, g, b, a = pix[nx, ny]
                if is_keyish(r, g, b, a):
                    remove[idx] = 1
                    queue.append((nx, ny))

    for _ in range(2):
        add: list[int] = []
        for y in range(h):
            for x in range(w):
                idx = y * w + x
                if remove[idx] or pix[x, y][3] == 0:
                    continue
                r, g, b, a = pix[x, y]
                edge_green = (
                    g >= 105
                    and r <= 120
                    and b <= 120
                    and g >= r * 1.22 + 15
                    and g >= b * 1.22 + 15
                )
                if not edge_green:
                    continue
                near_clear = False
                for ny in range(max(0, y - 1), min(h, y + 2)):
                    for nx in range(max(0, x - 1), min(w, x + 2)):
                        nidx = ny * w + nx
                        if remove[nidx] or pix[nx, ny][3] == 0:
                            near_clear = True
                            break
                    if near_clear:
                        break
                if near_clear:
                    add.append(idx)
        if not add:
            break
        for idx in add:
            remove[idx] = 1

    for y in range(h):
        for x in range(w):
            if remove[y * w + x]:
                r, g, b, a = pix[x, y]
                pix[x, y] = (r, g, b, 0)
    return rgba


def trim_and_pad(img: Image.Image, pad: int) -> Image.Image:
    bbox = img.getchannel("A").getbbox()
    if bbox is None:
        return img
    crop = img.crop(bbox)
    out = Image.new("RGBA", (crop.width + pad * 2, crop.height + pad * 2), (0, 0, 0, 0))
    out.alpha_composite(crop, (pad, pad))
    return out


def cut_asset(sheet: Image.Image, spec: dict[str, Any]) -> Image.Image:
    name = spec.get("name")
    box = spec.get("box")
    if not isinstance(name, str) or not name.endswith(".png"):
        raise ValueError(f"Asset has invalid name: {name!r}")
    if (
        not isinstance(box, list)
        or len(box) != 4
        or not all(isinstance(v, int) for v in box)
    ):
        raise ValueError(f"{name} has invalid box: {box!r}")
    pad = int(spec.get("pad", 24))
    part = sheet.crop(tuple(box))
    keyed = remove_key_connected(part)
    return trim_and_pad(keyed, pad)


def rgba_pixels(img: Image.Image):
    if hasattr(img, "get_flattened_data"):
        return img.get_flattened_data()
    return img.getdata()


def validate_png(path: Path, min_margin: int) -> list[str]:
    errors: list[str] = []
    img = Image.open(path).convert("RGBA")
    alpha = img.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None:
        return [f"{path.name}: empty alpha"]

    edge = 0
    ap = alpha.load()
    for x in range(img.width):
        edge += ap[x, 0] > 0
        edge += ap[x, img.height - 1] > 0
    for y in range(img.height):
        edge += ap[0, y] > 0
        edge += ap[img.width - 1, y] > 0
    if edge:
        errors.append(f"{path.name}: alpha on canvas edge ({edge})")

    left, top, right, bottom = bbox
    margins = (left, top, img.width - right, img.height - bottom)
    if min(margins) < min_margin:
        errors.append(f"{path.name}: transparent margin {margins} < {min_margin}")

    chroma = 0
    for r, g, b, a in rgba_pixels(img):
        if (
            a > 0
            and g >= 175
            and r <= 55
            and b <= 55
            and g >= r * 3.2 + 20
            and g >= b * 3.2 + 20
        ):
            chroma += 1
    if chroma:
        errors.append(f"{path.name}: chroma-green remnants ({chroma})")
    return errors


def make_preview(out_dir: Path, preview_path: Path) -> None:
    files = sorted(out_dir.glob("*.png"))
    try:
        font = ImageFont.truetype("arial.ttf", 13)
    except Exception:
        font = ImageFont.load_default()

    cols = 4
    cell_w = 300
    cell_h = 250
    rows = max(1, (len(files) + cols - 1) // cols)
    sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), (255, 255, 255))

    for idx, path in enumerate(files):
        img = Image.open(path).convert("RGBA")
        tile = Image.new("RGBA", (cell_w, cell_h), (245, 245, 245, 255))
        draw = ImageDraw.Draw(tile)
        for y in range(0, cell_h, 20):
            for x in range(0, cell_w, 20):
                if ((x // 20) + (y // 20)) % 2 == 0:
                    draw.rectangle((x, y, x + 19, y + 19), fill=(220, 220, 220, 255))
        scale = min((cell_w - 30) / img.width, (cell_h - 58) / img.height, 1.0)
        resized = img.resize(
            (max(1, int(img.width * scale)), max(1, int(img.height * scale))),
            Image.Resampling.LANCZOS,
        )
        tile.alpha_composite(
            resized,
            ((cell_w - resized.width) // 2, 34 + (cell_h - 58 - resized.height) // 2),
        )
        draw.rectangle((0, 0, cell_w, 28), fill=(235, 245, 255, 255))
        draw.text((5, 6), path.name, fill=(0, 55, 100, 255), font=font)
        sheet.paste(tile.convert("RGB"), ((idx % cols) * cell_w, (idx // cols) * cell_h))

    preview_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(preview_path)


def copy_sources(manifest: dict[str, Any], manifest_path: Path, out_dir: Path) -> None:
    source_dir_value = manifest.get("source_dir")
    if not source_dir_value:
        return
    source_dir = Path(source_dir_value)
    if not source_dir.is_absolute():
        source_dir = manifest_path.parent / source_dir
    source_dir.mkdir(parents=True, exist_ok=True)
    for key in ("sheet", "background"):
        value = manifest.get(key)
        if value:
            src = Path(value)
            if not src.is_absolute():
                src = manifest_path.parent / src
            shutil.copy2(src, source_dir / src.name)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Slice a regenerated Hybrid UI source sheet by manifest boxes."
    )
    parser.add_argument("--sheet", required=True, help="Generated chroma-green source sheet.")
    parser.add_argument("--manifest", required=True, help="JSON manifest with asset boxes.")
    parser.add_argument("--out-dir", required=True, help="Final view/category output directory.")
    parser.add_argument("--preview", required=True, help="Preview contact sheet path.")
    parser.add_argument("--clear", action="store_true", help="Delete existing PNGs in out-dir first.")
    parser.add_argument("--min-margin", type=int, default=8, help="Minimum transparent edge margin.")
    args = parser.parse_args()

    sheet_path = Path(args.sheet)
    manifest_path = Path(args.manifest)
    out_dir = Path(args.out_dir)
    preview_path = Path(args.preview)

    manifest = load_manifest(manifest_path)
    out_dir.mkdir(parents=True, exist_ok=True)
    if args.clear:
        for png in out_dir.glob("*.png"):
            png.unlink()

    sheet = Image.open(sheet_path).convert("RGBA")
    for spec in manifest["assets"]:
        asset = cut_asset(sheet, spec)
        asset.save(out_dir / spec["name"])

    background = manifest.get("background")
    if background:
        src = Path(background["source"])
        if not src.is_absolute():
            src = manifest_path.parent / src
        Image.open(src).convert("RGB").save(out_dir / background["name"])

    make_preview(out_dir, preview_path)
    copy_sources(manifest, manifest_path, out_dir)

    errors: list[str] = []
    for path in sorted(out_dir.glob("*.png")):
        if path.name.endswith("Background.png"):
            continue
        errors.extend(validate_png(path, args.min_margin))
    if errors:
        raise SystemExit("Validation failed:\n" + "\n".join(errors))
    print(f"Generated {len(list(out_dir.glob('*.png')))} PNGs: {out_dir}")
    print(f"Preview: {preview_path}")


if __name__ == "__main__":
    main()
