from __future__ import annotations

# Labels of app servers read from the IP csv files of the app data import.
# A csv column named "label:<key>" (e.g. "label:Stage") holds the value of label <key> for the app server of the line.
# Without such columns no labels are written, so that FWO keeps the existing labels of the app servers;
# with such columns every app server gets the labels of its line, empty cells meaning no label of that key.

LABEL_COLUMN_PREFIX: str = "label:"


def find_label_columns(headers: list[str]) -> dict[int, str]:
    """Return the label key of every label column by its column number."""
    label_columns: dict[int, str] = {}
    for column_no, header in enumerate(headers):
        if header.lower().startswith(LABEL_COLUMN_PREFIX):
            label_key: str = header[len(LABEL_COLUMN_PREFIX) :].strip()
            if label_key != "":
                label_columns[column_no] = label_key
    return label_columns


def get_line_labels(line: list[str], label_columns: dict[int, str]) -> list[dict[str, str]] | None:
    """
    Return the labels of a csv line as app data import labels.

    Returns None if the csv file has no label columns, so that the labels of the app server stay untouched.
    """
    if len(label_columns) == 0:
        return None
    labels: list[dict[str, str]] = []
    for column_no, label_key in label_columns.items():
        value: str = line[column_no].strip() if column_no < len(line) else ""
        if value != "":
            labels.append({"label_key": label_key, "label_value": value})
    return labels
