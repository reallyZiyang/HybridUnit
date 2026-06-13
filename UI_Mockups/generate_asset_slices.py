# -*- coding: utf-8 -*-
from __future__ import annotations

import runpy
import sys
from pathlib import Path


def main() -> None:
    project_root = Path(__file__).resolve().parents[1]
    script = project_root / ".codex" / "skills" / "hybrid-ui-asset-slicer" / "scripts" / "generate_asset_slices.py"
    if not script.exists():
        raise FileNotFoundError(f"Missing canonical slicer script: {script}")
    sys.argv = [str(script), str(project_root)]
    runpy.run_path(str(script), run_name="__main__")


if __name__ == "__main__":
    main()
