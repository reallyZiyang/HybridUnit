#!/usr/bin/env python3
"""Shared helpers for UniKit UI screen specs."""

from __future__ import annotations

from pathlib import Path
from typing import Any


LIST_SECTIONS = {
    "sprites",
    "bindings",
    "buttons",
    "lists",
    "runtime_data",
    "acceptance_screenshots",
}


def parse_scalar(value: str) -> Any:
    value = value.strip()
    if value in {"", "null", "None"}:
        return ""
    if value in {"true", "True"}:
        return True
    if value in {"false", "False"}:
        return False
    if value.isdigit():
        return int(value)
    if len(value) >= 2 and value[0] in {"'", '"'} and value[-1] == value[0]:
        return value[1:-1]
    return value


def split_key_value(text: str) -> tuple[str, Any]:
    if ":" not in text:
        raise ValueError(f"Expected key: value pair, got: {text}")
    key, value = text.split(":", 1)
    return key.strip(), parse_scalar(value)


def parse_simple_yaml(text: str) -> dict[str, Any]:
    """Parse the constrained YAML shape used by templates/ui-screen-spec.yaml."""
    data: dict[str, Any] = {}
    current_key: str | None = None
    current_item: dict[str, Any] | None = None

    for raw_line in text.splitlines():
        if not raw_line.strip() or raw_line.lstrip().startswith("#"):
            continue

        indent = len(raw_line) - len(raw_line.lstrip(" "))
        line = raw_line.strip().lstrip("\ufeff")

        if indent == 0:
            key, value = split_key_value(line)
            current_key = key
            current_item = None
            if value == "":
                data[key] = [] if key in LIST_SECTIONS else {}
            else:
                data[key] = value
            continue

        if current_key is None:
            raise ValueError(f"Nested value without parent: {line}")

        section = data[current_key]
        if indent == 2 and line.startswith("- "):
            if not isinstance(section, list):
                raise ValueError(f"Section {current_key} is not a list")
            current_item = {}
            section.append(current_item)
            rest = line[2:].strip()
            if rest:
                key, value = split_key_value(rest)
                current_item[key] = value
            continue

        if indent == 2:
            if not isinstance(section, dict):
                raise ValueError(f"Section {current_key} is not a mapping")
            key, value = split_key_value(line)
            section[key] = value
            continue

        if indent == 4 and isinstance(section, list) and current_item is not None:
            key, value = split_key_value(line)
            current_item[key] = value
            continue

        raise ValueError(f"Unsupported YAML shape: {raw_line}")

    return data


def load_spec(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8")
    try:
        import yaml  # type: ignore

        loaded = yaml.safe_load(text)
        if not isinstance(loaded, dict):
            raise ValueError("Spec root must be a mapping")
        return loaded
    except ModuleNotFoundError:
        return parse_simple_yaml(text)


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def as_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def pascal_case(value: str) -> str:
    parts = [part for part in value.replace("-", "_").split("_") if part]
    return "".join(part[:1].upper() + part[1:] for part in parts)


def csharp_field_name(binding: dict[str, Any]) -> str:
    name = str(binding.get("name", ""))
    access = str(binding.get("access", "private")).lower()
    return name if access == "public" else f"m_{name}"
