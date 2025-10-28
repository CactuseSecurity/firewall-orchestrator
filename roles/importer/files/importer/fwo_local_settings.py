import os
import json
from pathlib import Path

csharp_unit_tests_verbose: bool = False

def _load_from_env():

    global csharp_unit_tests_verbose

    path = os.getenv("FWORCH_LOCAL_SETTINGS_PATH")

    if path and Path(path).is_file():
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            csharp_unit_tests_verbose = bool(
                data.get("test.unittests.csharp.verbose", False)
            )
        except Exception as e:
            print(f"Reading local settings from {path} failed ({e}). Using defaults.")

_load_from_env()

