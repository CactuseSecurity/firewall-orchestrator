import argparse
import json
import logging
from pathlib import Path

import pytest

from scripts.customizing.fwo_custom_lib.responsibles_config import (
    parse_responsibles_columns,
    resolve_responsibles_columns_headers,
)


def test_parse_responsibles_columns_expands_quoted_cli_entries() -> None:
    parsed = parse_responsibles_columns(["1:'TISO UserID' 'TISO Backup'", "2:Owner"])

    assert parsed == {"1": ("TISO UserID", "TISO Backup"), "2": ("Owner",)}


def test_parse_responsibles_columns_rejects_header_without_level() -> None:
    with pytest.raises(argparse.ArgumentTypeError, match="expected LEVEL:HEADER"):
        parse_responsibles_columns(["TISO UserID"])


def test_resolve_responsibles_columns_headers_prefers_cli_entries(tmp_path: Path) -> None:
    config_path = tmp_path / "customizing.json"
    config_path.write_text(json.dumps({"responsiblesColumns": {"1": ["From Config"]}}), encoding="utf-8")

    resolved = resolve_responsibles_columns_headers(
        str(config_path),
        logging.getLogger("responsibles-test"),
        cli_responsibles_columns=["2:From CLI"],
    )

    assert resolved == {"2": ("From CLI",)}


def test_resolve_responsibles_columns_headers_rejects_invalid_config_shapes(tmp_path: Path) -> None:
    config_path = tmp_path / "customizing.json"
    config_path.write_text(json.dumps({"responsiblesColumns": {"": ["Owner"]}}), encoding="utf-8")

    with pytest.raises(argparse.ArgumentTypeError, match="non-empty string levels"):
        resolve_responsibles_columns_headers(str(config_path), logging.getLogger("responsibles-test"))
