#!/usr/bin/env python3
"""Validate a UniKit UI screen spec and print fixed implementation plans."""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

from spec_utils import as_dict, as_list, load_spec


ALLOWED_UI_TYPES = {"View", "Panel", "Node", "Item", "Component"}
ATLAS_DESTINATIONS = {
    "SA_UI_Icon": "Assets/Res/UI/Common/Sprites/Icon",
    "SA_UI_Misc": "Assets/Res/UI/Common/Sprites/Misc",
}
COMPONENT_PREFIXES = {
    "Button": "Btn",
    "Image": "Img",
    "Text": "Txt",
    "TextMeshProUGUI": "Txt",
    "Toggle": "Tog",
    "Slider": "Sld",
    "InputField": "Input",
    "TMP_InputField": "Input",
    "Dropdown": "Ddl",
    "ListView": "List",
    "ListItems": "List",
    "ScrollView": "Scroll",
    "InfiniteScrollView": "Scroll",
}


def is_under(path: str, parent: str) -> bool:
    path = path.replace("\\", "/")
    parent = parent.rstrip("/")
    return path == parent or path.startswith(parent + "/")


def unity_path(path: str) -> str:
    return path.replace("\\", "/")


def is_csharp_identifier(value: str) -> bool:
    return bool(re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", value))


def expected_sprite_folder(atlas: str, bucket: str) -> str | None:
    if atlas in ATLAS_DESTINATIONS:
        return ATLAS_DESTINATIONS[atlas]
    if atlas == f"SA_UI_{bucket}":
        return f"Assets/Res/UI/Views/{bucket}/Sprites"
    return None


def validate(project_root: Path, spec: dict) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    notes: list[str] = []

    for key in ("screen_name", "bucket", "ui_type", "namespace", "class_name", "mockup", "prefab", "runtime"):
        if not spec.get(key):
            errors.append(f"Missing required field: {key}")

    bucket = str(spec.get("bucket", ""))
    ui_type = str(spec.get("ui_type", ""))
    class_name = str(spec.get("class_name", ""))
    screen_name = str(spec.get("screen_name", ""))

    if ui_type and ui_type not in ALLOWED_UI_TYPES:
        errors.append(f"ui_type must be one of {sorted(ALLOWED_UI_TYPES)}, got {ui_type}")
    if class_name and not is_csharp_identifier(class_name):
        errors.append(f"class_name is not a valid C# identifier: {class_name}")
    if screen_name and class_name and screen_name != class_name:
        notes.append(f"screen_name differs from class_name: {screen_name} vs {class_name}")

    target_resolution = as_dict(spec.get("target_resolution"))
    if not target_resolution.get("width") or not target_resolution.get("height"):
        errors.append("target_resolution.width and target_resolution.height are required")

    prefab = as_dict(spec.get("prefab"))
    prefab_path = unity_path(str(prefab.get("path", "")))
    prefab_key = str(prefab.get("yoo_key", ""))
    prefab_stem = Path(prefab_path).stem if prefab_path else ""
    if prefab_path:
        if not prefab_path.endswith(".prefab"):
            errors.append(f"prefab.path must end with .prefab: {prefab_path}")
        if ui_type == "View" and not prefab_stem.startswith("UI_"):
            errors.append(f"View prefab file name must start with UI_: {prefab_path}")
        if ui_type == "View" and class_name and prefab_stem != f"UI_{class_name}":
            errors.append(f"View prefab file name should be UI_ + class_name: {prefab_stem} vs UI_{class_name}")
        valid_prefab_root = is_under(prefab_path, "Assets/Res/UI/Views") or is_under(prefab_path, "Assets/Res/UI/Common")
        if not valid_prefab_root:
            errors.append(f"prefab.path is outside collected UI prefab roots: {prefab_path}")
    if prefab_path and prefab_key and prefab_key != prefab_stem:
        errors.append(f"prefab.yoo_key must match prefab file name without extension: {prefab_key} vs {prefab_stem}")

    runtime = as_dict(spec.get("runtime"))
    script_path = unity_path(str(runtime.get("script_path", "")))
    if script_path:
        if not is_under(script_path, "Assets/Scripts/Game/Play/Runtime/UI/View"):
            errors.append(f"runtime.script_path must be under Assets/Scripts/Game/Play/Runtime/UI/View: {script_path}")
        if class_name and Path(script_path).stem != class_name:
            errors.append(f"runtime script file should match class_name: {script_path}")

    mockup = str(spec.get("mockup", ""))
    if mockup and not (project_root / mockup).exists():
        notes.append(f"mockup path does not currently exist: {mockup}")

    bindings = as_list(spec.get("bindings"))
    binding_names = set()
    for index, binding in enumerate(bindings):
        binding = as_dict(binding)
        name = str(binding.get("name", ""))
        component = str(binding.get("component", ""))
        if not name or not component:
            errors.append(f"bindings[{index}] requires name and component")
            continue
        if name in binding_names:
            errors.append(f"Duplicate binding name: {name}")
        binding_names.add(name)
        expected_prefix = COMPONENT_PREFIXES.get(component)
        if expected_prefix and not name.startswith(expected_prefix):
            errors.append(f"Binding {name} for {component} should start with {expected_prefix}")

    for index, sprite in enumerate(as_list(spec.get("sprites"))):
        sprite = as_dict(sprite)
        name = str(sprite.get("name", ""))
        destination = unity_path(str(sprite.get("destination", "")))
        atlas = str(sprite.get("atlas", ""))
        if not name or not destination or not atlas:
            errors.append(f"sprites[{index}] requires name, destination, and atlas")
            continue
        folder = expected_sprite_folder(atlas, bucket)
        if folder is None:
            errors.append(f"sprites[{index}] atlas is not recognized for bucket {bucket}: {atlas}")
            continue
        if not is_under(destination, folder):
            errors.append(f"Sprite {name} destination must be under {folder}: {destination}")

    for index, button in enumerate(as_list(spec.get("buttons"))):
        button = as_dict(button)
        binding = str(button.get("binding", ""))
        handler = str(button.get("handler", ""))
        if binding not in binding_names:
            errors.append(f"buttons[{index}] references unknown binding: {binding}")
        if binding and not binding.startswith("Btn"):
            errors.append(f"buttons[{index}] binding should start with Btn: {binding}")
        if not handler or not is_csharp_identifier(handler):
            errors.append(f"buttons[{index}] handler must be a valid C# identifier: {handler}")

    for index, item in enumerate(as_list(spec.get("lists"))):
        item = as_dict(item)
        binding = str(item.get("binding", ""))
        if binding not in binding_names:
            errors.append(f"lists[{index}] references unknown binding: {binding}")
        for key in ("data_type", "item_type"):
            value = str(item.get(key, ""))
            if not value or not is_csharp_identifier(value):
                errors.append(f"lists[{index}].{key} must be a valid C# identifier")

    return errors, notes


def print_plan(spec: dict) -> None:
    bucket = spec.get("bucket", "")
    prefab = as_dict(spec.get("prefab"))
    runtime = as_dict(spec.get("runtime"))
    print("Prefab plan:")
    print(f"- bucket: {bucket}")
    print(f"- path: {prefab.get('path', '')}")
    print(f"- yoo_key: {prefab.get('yoo_key', '')}")

    print("\nAsset plan:")
    for sprite in as_list(spec.get("sprites")):
        sprite = as_dict(sprite)
        print(f"- {sprite.get('name', '')}: {sprite.get('destination', '')} via {sprite.get('atlas', '')}")

    print("\nBinding plan:")
    for binding in as_list(spec.get("bindings")):
        binding = as_dict(binding)
        print(f"- {binding.get('name', '')}: {binding.get('component', '')} ({binding.get('access', 'private')})")

    print("\nRuntime plan:")
    print(f"- class: {spec.get('namespace', '')}.{spec.get('class_name', '')}")
    print(f"- script_path: {runtime.get('script_path', '')}")
    print(f"- data_type: {runtime.get('data_type', '')}")
    print(f"- refresh_method: {runtime.get('refresh_method', 'Render')}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("project_root", help="Unity project root")
    parser.add_argument("spec", help="Path to ui-screen-spec.yaml")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    spec_path = Path(args.spec).resolve()
    spec = load_spec(spec_path)

    errors, notes = validate(project_root, spec)
    print("UniKit UI screen spec validation")
    print(f"Spec: {spec_path}")
    if errors:
        print("\nErrors:")
        for error in errors:
            print(f"- {error}")
    if notes:
        print("\nNotes:")
        for note in notes:
            print(f"- {note}")
    if errors:
        return 1

    print("\nSpec is valid.")
    print_plan(spec)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
