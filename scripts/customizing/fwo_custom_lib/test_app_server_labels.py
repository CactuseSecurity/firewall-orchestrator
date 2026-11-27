# pyright: reportPrivateUsage=false
import logging
import re

from scripts.customizing.fwo_custom_lib.app_server_labels import find_label_columns, get_line_labels, is_label_column
from scripts.customizing.fwo_custom_lib.read_app_data_csv import DEFAULT_IP_HEADER_PATTERNS, _find_header_index


def test_find_label_columns_maps_column_numbers_to_keys() -> None:
    headers: list[str] = ["col: Alfabet-ID", "col: IP", "label:Stage", "LABEL: OS ", "label:", "remark"]

    assert find_label_columns(headers) == {2: "Stage", 3: "OS"}


def test_line_labels_skip_empty_cells() -> None:
    label_columns: dict[int, str] = {2: "Stage", 3: "OS"}

    assert get_line_labels(["APP-001", "10.0.0.1", " Prod ", ""], label_columns) == [
        {"label_key": "Stage", "label_value": "Prod"}
    ]
    assert get_line_labels(["APP-001", "10.0.0.1"], label_columns) == []


def test_line_labels_are_none_without_label_columns() -> None:
    assert get_line_labels(["APP-001", "10.0.0.1"], {}) is None


def test_is_label_column() -> None:
    assert is_label_column("Label:Stage")
    assert not is_label_column("Server: IP")


def test_header_search_skips_label_columns() -> None:
    headers: list[str] = ["label:IP-Zone", "label:Alfabet-ID", "App: Alfabet-ID", "Server: IP"]
    logger: logging.Logger = logging.getLogger("test")
    ip_column: int = _find_header_index(
        headers, re.compile(DEFAULT_IP_HEADER_PATTERNS["ip"]), "ip", "servers.csv", logger
    )
    app_id_column: int = _find_header_index(
        headers, re.compile(DEFAULT_IP_HEADER_PATTERNS["app_id"]), "app_id", "servers.csv", logger
    )
    assert (ip_column, app_id_column) == (3, 2)
