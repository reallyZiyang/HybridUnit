# -*- coding: utf-8 -*-
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


def resolve_path(value: str, manifest_path: Path, project_root: Path) -> Path:
    path = Path(value)
    if path.is_absolute():
        return path
    local = manifest_path.parent / path
    if local.exists():
        return local
    return project_root / path


def load_font(size: int, font_path: str | None = None) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = []
    if font_path:
        candidates.append(Path(font_path))
    candidates.extend(
        [
            Path("C:/Windows/Fonts/msyhbd.ttc"),
            Path("C:/Windows/Fonts/msyh.ttc"),
            Path("C:/Windows/Fonts/simhei.ttf"),
            Path("C:/Windows/Fonts/arialbd.ttf"),
        ]
    )
    for path in candidates:
        if path.exists():
            try:
                return ImageFont.truetype(str(path), size)
            except Exception:
                pass
    return ImageFont.load_default()


def resize_layer(img: Image.Image, layer: dict[str, Any]) -> Image.Image:
    if "nineSlice" in layer and "size" in layer:
        return nine_slice_resize(img, layer["size"], layer["nineSlice"])
    if "size" in layer:
        width, height = layer["size"]
        return img.resize((int(width), int(height)), Image.Resampling.LANCZOS)
    scale = float(layer.get("scale", 1.0))
    if scale == 1.0:
        return img
    return img.resize(
        (max(1, round(img.width * scale)), max(1, round(img.height * scale))),
        Image.Resampling.LANCZOS,
    )


def nine_slice_resize(img: Image.Image, size: list[int], borders: list[int]) -> Image.Image:
    """Resize a UI sprite while preserving its corners and edge thickness."""
    img = img.convert("RGBA")
    out_w, out_h = int(size[0]), int(size[1])
    left, top, right, bottom = [int(v) for v in borders]
    src_w, src_h = img.size

    if left + right >= src_w or top + bottom >= src_h:
        raise ValueError(f"Invalid nineSlice {borders} for source size {img.size}")
    if left + right >= out_w or top + bottom >= out_h:
        raise ValueError(f"Invalid nineSlice {borders} for output size {(out_w, out_h)}")

    xs = [0, left, src_w - right, src_w]
    ys = [0, top, src_h - bottom, src_h]
    dx = [0, left, out_w - right, out_w]
    dy = [0, top, out_h - bottom, out_h]
    out = Image.new("RGBA", (out_w, out_h), (0, 0, 0, 0))

    for row in range(3):
        for col in range(3):
            src_box = (xs[col], ys[row], xs[col + 1], ys[row + 1])
            dst_box = (dx[col], dy[row], dx[col + 1], dy[row + 1])
            part = img.crop(src_box)
            dst_w = dst_box[2] - dst_box[0]
            dst_h = dst_box[3] - dst_box[1]
            if dst_w <= 0 or dst_h <= 0:
                continue
            if part.size != (dst_w, dst_h):
                part = part.resize((dst_w, dst_h), Image.Resampling.LANCZOS)
            out.alpha_composite(part, (dst_box[0], dst_box[1]))
    return out


def draw_text_layer(canvas: Image.Image, layer: dict[str, Any]) -> None:
    draw = ImageDraw.Draw(canvas)
    text = str(layer["text"])
    font = load_font(int(layer.get("fontSize", 64)), layer.get("font"))
    fill = tuple(layer.get("fill", [255, 255, 255, 255]))
    stroke_fill = tuple(layer.get("strokeFill", [79, 45, 22, 255]))
    stroke_width = int(layer.get("strokeWidth", 0))
    x = int(layer.get("x", 0))
    y = int(layer.get("y", 0))

    if layer.get("anchor") == "center":
        bbox = draw.textbbox((0, 0), text, font=font, stroke_width=stroke_width)
        x -= (bbox[2] - bbox[0]) // 2
        y -= (bbox[3] - bbox[1]) // 2

    draw.text(
        (x, y),
        text,
        font=font,
        fill=fill,
        stroke_width=stroke_width,
        stroke_fill=stroke_fill,
    )


def compose(manifest_path: Path, out_path: Path, project_root: Path) -> None:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    width, height = manifest.get("canvas", [1080, 1920])
    canvas = Image.new("RGBA", (int(width), int(height)), tuple(manifest.get("fill", [0, 0, 0, 0])))

    background = manifest.get("background")
    if background:
        bg_path = resolve_path(background["path"], manifest_path, project_root)
        bg = Image.open(bg_path).convert("RGBA")
        bg = bg.resize(canvas.size, Image.Resampling.LANCZOS)
        canvas.alpha_composite(bg, (0, 0))

    layers = sorted(manifest.get("layers", []), key=lambda item: int(item.get("zOrder", 0)))
    for layer in layers:
        if layer.get("type", "image") == "text":
            draw_text_layer(canvas, layer)
            continue

        src_path = resolve_path(layer["path"], manifest_path, project_root)
        img = Image.open(src_path).convert("RGBA")
        img = resize_layer(img, layer)
        x = int(layer.get("x", 0))
        y = int(layer.get("y", 0))
        canvas.alpha_composite(img, (x, y))

    out_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(out_path)


def main() -> None:
    parser = argparse.ArgumentParser(description="Compose a 1080x1920 Hybrid effect mockup from final PNG assets.")
    parser.add_argument("--manifest", required=True, help="Layout manifest JSON.")
    parser.add_argument("--out", required=True, help="Output effect mockup PNG.")
    parser.add_argument("--project-root", default=".", help="Project root for resolving repo-relative paths.")
    args = parser.parse_args()

    compose(Path(args.manifest), Path(args.out), Path(args.project_root))
    print(f"Composed effect mockup: {args.out}")


if __name__ == "__main__":
    main()
