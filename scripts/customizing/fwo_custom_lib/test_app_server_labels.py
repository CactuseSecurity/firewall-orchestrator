from scripts.customizing.fwo_custom_lib.app_server_labels import find_label_columns, get_line_labels


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
