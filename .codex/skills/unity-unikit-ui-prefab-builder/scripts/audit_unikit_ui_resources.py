#!/usr/bin/env python3
"""Read-only audit for Hybrid UniKit UI resource placement and YooAsset collectors."""

from __future__ import annotations

import argparse
import re
from collections import defaultdict
from pathlib import Path


EXPECTED_DIRS = [
    "Assets/Res/UI/Atlas",
    "Assets/Res/UI/Common/Font",
    "Assets/Res/UI/Common/Material",
    "Assets/Res/UI/Common/Shader",
    "Assets/Res/UI/Common/Prefabs/Views",
    "Assets/Res/UI/Common/Prefabs/Panels",
    "Assets/Res/UI/Common/Prefabs/Items",
    "Assets/Res/UI/Common/Sprites/Icon",
    "Assets/Res/UI/Common/Sprites/Misc",
    "Assets/Res/UI/Views/Menu/Prefabs/Views",
    "Assets/Res/UI/Views/Menu/Prefabs/Panels",
    "Assets/Res/UI/Views/Menu/Prefabs/Items",
    "Assets/Res/UI/Views/Menu/Sprites",
    "Assets/Res/UI/Views/Battle/Prefabs/Views",
    "Assets/Res/UI/Views/Battle/Prefabs/Panels",
    "Assets/Res/UI/Views/Battle/Prefabs/Items",
    "Assets/Res/UI/Views/Battle/Sprites",
    "Assets/Res/UI/Views/Result/Prefabs/Views",
    "Assets/Res/UI/Views/Result/Prefabs/Panels",
    "Assets/Res/UI/Views/Result/Prefabs/Items",
    "Assets/Res/UI/Views/Result/Sprites",
]

EXPECTED_COLLECTORS = {
    "Assets/Res/UI/Atlas": "CollectAll",
    "Assets/Res/UI/Views": "CollectPrefab",
    "Assets/Res/UI/Common": "CollectPrefab",
    "Assets/Res/UI/Common/Font": "CollectAll",
    "Assets/Res/UI/Common/Material": "CollectAll",
    "Assets/Res/UI/Common/Shader": "CollectAll",
}

ATLAS_SPRITE_DIRS = [
    "Assets/Res/UI/Common/Sprites/Icon",
    "Assets/Res/UI/Common/Sprites/Misc",
    "Assets/Res/UI/Views/Menu/Sprites",
    "Assets/Res/UI/Views/Battle/Sprites",
    "Assets/Res/UI/Views/Result/Sprites",
]

SPRITE_EXTS = {".png", ".jpg", ".jpeg", ".psd", ".tga"}
RECOMMENDED_UI_SETTINGS = {
    "source": "Assets/Res/UI",
    "target": "Assets/Scripts/Game/Play/Runtime/UI/View",
    "nameSpace": "Game.Play.UI.View",
}


def norm(path: Path, root: Path) -> str:
    return path.relative_to(root).as_posix()


def parse_collectors(text: str) -> dict[str, dict[str, str]]:
    collectors: dict[str, dict[str, str]] = {}
    current: dict[str, str] | None = None
    for line in text.splitlines():
        path_match = re.search(r"CollectPath:\s*(.+)$", line)
        if path_match:
            collect_path = path_match.group(1).strip()
            current = collectors.setdefault(collect_path, {})
            continue
        if current is None:
            continue
        for key in ("AddressRuleName", "PackRuleName", "FilterRuleName", "CollectorType"):
            match = re.search(rf"{key}:\s*(.+)$", line)
            if match:
                current[key] = match.group(1).strip()
    return collectors


def parse_package_flags(text: str) -> dict[str, str]:
    flags: dict[str, str] = {}
    for key in ("EnableAddressable", "SupportExtensionless", "LocationToLower", "IncludeAssetGUID"):
        match = re.search(rf"\b{key}:\s*(.+)$", text, re.MULTILINE)
        if match:
            flags[key] = match.group(1).strip()
    return flags


def is_under(path: str, parent: str) -> bool:
    return path == parent or path.startswith(parent.rstrip("/") + "/")


def guid_to_paths(root: Path) -> dict[str, Path]:
    result: dict[str, Path] = {}
    for meta in (root / "Assets").rglob("*.meta"):
        text = meta.read_text(encoding="utf-8", errors="ignore")
        match = re.search(r"^guid:\s*([0-9a-fA-F]+)\s*$", text, re.MULTILINE)
        if match:
            result[match.group(1)] = meta.with_suffix("")
    return result


def parse_atlas_packables(atlas_path: Path) -> list[str]:
    text = atlas_path.read_text(encoding="utf-8", errors="ignore")
    return re.findall(r"guid:\s*([0-9a-fA-F]+)", text)


def collected_files(root: Path, collect_path: str, filter_rule: str) -> list[Path]:
    base = root / collect_path
    if not base.exists():
        return []
    files = [p for p in base.rglob("*") if p.is_file() and p.suffix != ".meta"]
    if filter_rule == "CollectPrefab":
        return [p for p in files if p.suffix == ".prefab"]
    return files


def parse_ui_settings(text: str) -> list[dict[str, str]]:
    mappings: list[dict[str, str]] = []
    current: dict[str, str] | None = None
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("- source:"):
            current = {"source": stripped.split(":", 1)[1].strip()}
            mappings.append(current)
            continue
        if current is None:
            continue
        for key in ("target", "nameSpace"):
            if stripped.startswith(f"{key}:"):
                current[key] = stripped.split(":", 1)[1].strip()
    return mappings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("project_root", nargs="?", default=".", help="Unity project root")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    warnings: list[str] = []
    notes: list[str] = []

    for rel in EXPECTED_DIRS:
        if not (root / rel).is_dir():
            warnings.append(f"Missing expected directory: {rel}")

    collector_file = root / "Assets/AssetBundleCollectorSetting.asset"
    package_flags: dict[str, str] = {}
    if not collector_file.is_file():
        warnings.append("Missing Assets/AssetBundleCollectorSetting.asset")
        collectors = {}
    else:
        collector_text = collector_file.read_text(encoding="utf-8", errors="ignore")
        collectors = parse_collectors(collector_text)
        package_flags = parse_package_flags(collector_text)

    if package_flags.get("EnableAddressable") != "1":
        warnings.append("YooAsset EnableAddressable is not enabled")
    if package_flags.get("SupportExtensionless") != "1":
        warnings.append("YooAsset SupportExtensionless is not enabled")
    if package_flags.get("LocationToLower") != "0":
        warnings.append("YooAsset LocationToLower should remain disabled for current key casing")

    for rel, expected_filter in EXPECTED_COLLECTORS.items():
        data = collectors.get(rel)
        if not data:
            warnings.append(f"Missing YooAsset collector: {rel}")
            continue
        if data.get("AddressRuleName") != "AddressByFileName":
            warnings.append(f"{rel}: AddressRuleName is {data.get('AddressRuleName')}, expected AddressByFileName")
        if data.get("PackRuleName") != "PackDirectory":
            warnings.append(f"{rel}: PackRuleName is {data.get('PackRuleName')}, expected PackDirectory")
        if data.get("FilterRuleName") != expected_filter:
            warnings.append(f"{rel}: FilterRuleName is {data.get('FilterRuleName')}, expected {expected_filter}")

    short_keys: dict[str, list[str]] = defaultdict(list)
    for collect_path, data in collectors.items():
        if data.get("AddressRuleName") != "AddressByFileName":
            continue
        for file_path in collected_files(root, collect_path, data.get("FilterRuleName", "CollectAll")):
            key = file_path.stem
            short_keys[key].append(norm(file_path, root))

    for key, paths in sorted(short_keys.items()):
        unique_paths = sorted(set(paths))
        if len(unique_paths) > 1:
            warnings.append(f"Duplicate AddressByFileName key '{key}': {', '.join(unique_paths)}")

    guid_paths = guid_to_paths(root)
    atlas_packable_paths: dict[str, list[str]] = {}
    atlas_root = root / "Assets/Res/UI/Atlas"
    if atlas_root.is_dir():
        for atlas in atlas_root.glob("SA_UI_*.spriteatlasv2"):
            atlas_packable_paths[atlas.stem] = [
                norm(guid_paths[guid], root)
                for guid in parse_atlas_packables(atlas)
                if guid in guid_paths
            ]

    ui_root = root / "Assets/Res/UI"
    files = [p for p in ui_root.rglob("*") if p.is_file() and p.suffix != ".meta"] if ui_root.is_dir() else []

    for file_path in files:
        rel = norm(file_path, root)
        suffix = file_path.suffix.lower()
        if suffix == ".prefab":
            valid = is_under(rel, "Assets/Res/UI/Views") or is_under(rel, "Assets/Res/UI/Common")
            if not valid:
                warnings.append(f"Prefab is outside collected prefab paths: {rel}")
        elif suffix in SPRITE_EXTS:
            valid = any(is_under(rel, folder) for folder in ATLAS_SPRITE_DIRS)
            if not valid:
                warnings.append(f"UI sprite is outside atlas-backed sprite folders: {rel}")
            else:
                covered = False
                for packable_paths in atlas_packable_paths.values():
                    if any(is_under(rel, packable) for packable in packable_paths):
                        covered = True
                        break
                if not covered:
                    warnings.append(f"UI sprite is not covered by any SA_UI_* atlas packable: {rel}")

    views_root = root / "Assets/Res/UI/Views"
    if views_root.is_dir():
        for bucket_dir in sorted(path for path in views_root.iterdir() if path.is_dir()):
            bucket = bucket_dir.name
            for rel in ("Prefabs/Views", "Prefabs/Panels", "Prefabs/Items", "Sprites"):
                if not (bucket_dir / rel).is_dir():
                    warnings.append(f"UI bucket {bucket} is missing {rel}")
            sprites_dir = f"Assets/Res/UI/Views/{bucket}/Sprites"
            atlas_name = f"SA_UI_{bucket}"
            atlas_path = root / "Assets/Res/UI/Atlas" / f"{atlas_name}.spriteatlasv2"
            if (bucket_dir / "Sprites").is_dir() and not atlas_path.is_file():
                warnings.append(f"UI bucket {bucket} is missing atlas {atlas_name}.spriteatlasv2")
            elif atlas_path.is_file():
                packables = atlas_packable_paths.get(atlas_name, [])
                if sprites_dir not in packables:
                    warnings.append(f"Atlas {atlas_name} does not pack {sprites_dir}")

    for atlas_name, sprite_dir in {
        "SA_UI_Icon": "Assets/Res/UI/Common/Sprites/Icon",
        "SA_UI_Misc": "Assets/Res/UI/Common/Sprites/Misc",
    }.items():
        atlas_path = root / "Assets/Res/UI/Atlas" / f"{atlas_name}.spriteatlasv2"
        if not atlas_path.is_file():
            warnings.append(f"Missing common atlas: {atlas_name}.spriteatlasv2")
            continue
        if sprite_dir not in atlas_packable_paths.get(atlas_name, []):
            warnings.append(f"Atlas {atlas_name} does not pack {sprite_dir}")

    settings_path = root / "Assets/Settings/Editor/UniKit UI Settings.asset"
    if not settings_path.is_file():
        notes.append("UniKit UI Settings asset was not found; create it before generating UIDataBinding code.")
    else:
        mappings = parse_ui_settings(settings_path.read_text(encoding="utf-8", errors="ignore"))
        if RECOMMENDED_UI_SETTINGS not in mappings:
            warnings.append(
                "UniKit UI Settings outputPaths is missing the recommended mapping: "
                f"{RECOMMENDED_UI_SETTINGS}"
            )

    print("UniKit UI resource audit")
    print(f"Project: {root}")
    print(f"UI files: {len(files)}")
    if warnings:
        print("\nWarnings:")
        for item in warnings:
            print(f"- {item}")
        return 1

    if notes:
        print("\nNotes:")
        for item in notes:
            print(f"- {item}")

    print("No issues found.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
