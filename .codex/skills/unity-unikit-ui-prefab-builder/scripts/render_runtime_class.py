#!/usr/bin/env python3
"""Render a hand-written UniKit UI runtime partial class from a screen spec."""

from __future__ import annotations

import argparse
from pathlib import Path

from spec_utils import as_dict, as_list, csharp_field_name, load_spec


TEMPLATE_BY_TYPE = {
    "View": "UIView.cs.tmpl",
    "Panel": "UIPanel.cs.tmpl",
    "Node": "UINode.cs.tmpl",
    "Component": "UINode.cs.tmpl",
    "Item": "ListItem.cs.tmpl",
}


def render_template(template: str, values: dict[str, str]) -> str:
    output = template
    for key, value in values.items():
        output = output.replace("{{" + key + "}}", value)
    return output


def binding_map(spec: dict) -> dict[str, dict]:
    result: dict[str, dict] = {}
    for binding in as_list(spec.get("bindings")):
        binding = as_dict(binding)
        name = str(binding.get("name", ""))
        if name:
            result[name] = binding
    return result


def make_button_handlers(spec: dict, bindings: dict[str, dict]) -> str:
    lines: list[str] = []
    for button in as_list(spec.get("buttons")):
        button = as_dict(button)
        binding_name = str(button.get("binding", ""))
        handler = str(button.get("handler", ""))
        binding = bindings.get(binding_name, {"name": binding_name})
        field = csharp_field_name(binding)
        lines.append(f"            {field}.SetOnClick({handler});")
    return "\n".join(lines) if lines else "            // TODO: register UI events."


def make_handler_methods(spec: dict) -> str:
    methods: list[str] = []
    for button in as_list(spec.get("buttons")):
        button = as_dict(button)
        handler = str(button.get("handler", ""))
        if not handler:
            continue
        methods.append(
            "        private void " + handler + "()\n"
            "        {\n"
            "            // TODO: implement button behavior.\n"
            "        }\n"
        )
    return "\n".join(methods).rstrip()


def make_refresh_body(spec: dict, bindings: dict[str, dict]) -> str:
    lines: list[str] = []
    for item in as_list(spec.get("runtime_data")):
        item = as_dict(item)
        data_name = str(item.get("name", ""))
        target = str(item.get("target_binding", ""))
        binding = bindings.get(target)
        if not data_name or not binding:
            continue
        field = csharp_field_name(binding)
        component = str(binding.get("component", ""))
        if component in {"Text", "TextMeshProUGUI"}:
            lines.append(f"            {field}.text = data.{data_name}.ToString();")
        elif component == "Image":
            lines.append(f"            // TODO: assign sprite for {field} from data.{data_name}.")
        else:
            lines.append(f"            // TODO: render data.{data_name} into {field}.")

    for item in as_list(spec.get("lists")):
        item = as_dict(item)
        binding_name = str(item.get("binding", ""))
        binding = bindings.get(binding_name, {"name": binding_name})
        field = csharp_field_name(binding)
        data_type = str(item.get("data_type", "TData"))
        item_type = str(item.get("item_type", "TItem"))
        lines.append(f"            // {field}.BindItems<{data_type}, {item_type}>(data.{binding_name});")

    return "\n".join(lines) if lines else "            // TODO: render data."


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("spec", help="Path to ui-screen-spec.yaml")
    parser.add_argument("--output", help="Optional output .cs path")
    args = parser.parse_args()

    spec_path = Path(args.spec).resolve()
    spec = load_spec(spec_path)
    ui_type = str(spec.get("ui_type", "View"))
    template_name = TEMPLATE_BY_TYPE.get(ui_type)
    if template_name is None:
        raise SystemExit(f"Unsupported ui_type for rendering: {ui_type}")

    skill_root = Path(__file__).resolve().parents[1]
    template_path = skill_root / "templates" / template_name
    template = template_path.read_text(encoding="utf-8")
    runtime = as_dict(spec.get("runtime"))
    bindings = binding_map(spec)
    output = render_template(
        template,
        {
            "namespace": str(spec.get("namespace", "Game.Play.UI.View")),
            "class_name": str(spec.get("class_name", spec.get("screen_name", "NewView"))),
            "data_type": str(runtime.get("data_type", "object")),
            "refresh_method": str(runtime.get("refresh_method", "Render")),
            "button_handlers": make_button_handlers(spec, bindings),
            "handler_methods": make_handler_methods(spec),
            "refresh_body": make_refresh_body(spec, bindings),
        },
    )

    if args.output:
        out_path = Path(args.output).resolve()
        out_path.parent.mkdir(parents=True, exist_ok=True)
        out_path.write_text(output, encoding="utf-8", newline="\n")
        print(f"Generated runtime class: {out_path}")
    else:
        print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
