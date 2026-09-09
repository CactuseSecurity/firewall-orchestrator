import logging
from pathlib import Path

import pytest

from scripts.customizing.log_data_import.import_log_data_from_git import (
    LogDataEntry,
    RejectedFile,
    convert_csv_file,
    convert_row,
)

VALID_ROW: dict[str, str] = {
    "App ID": "APP-1",
    "Log count": "42",
    "Src IP": "192.0.2.1",
    "Dst IP": "198.51.100.1",
    "Port": "443",
    "Protocol": "6",
}


def test_convert_row_applies_optional_defaults() -> None:
    converted: LogDataEntry = convert_row(VALID_ROW)

    assert converted["action"] == "accept"
    assert converted["port"] == 443
    assert converted["protocol"] == 6


def test_convert_row_rejects_port_without_tcp_or_udp() -> None:
    invalid_row: dict[str, str] = {**VALID_ROW, "Protocol": "1"}

    try:
        convert_row(invalid_row)
    except ValueError as exception:
        assert "Port is only valid" in str(exception)
    else:
        raise AssertionError("Expected ValueError for a port without TCP or UDP")


def test_convert_csv_file_logs_invalid_rows_and_rejects_the_file(tmp_path: Path) -> None:
    csv_file: Path = tmp_path / "logs.csv"
    csv_file.write_text(
        "App ID,Log count,Src IP,Dst IP,Port,Protocol\n"
        "APP-1,42,192.0.2.1,198.51.100.1,443,6\n"
        "APP-2,1,192.0.2.2,198.51.100.2,443,1\n",
        encoding="utf-8",
    )
    logger: logging.Logger = logging.getLogger("log-data-import-test")
    collector: LogMessageCollector = LogMessageCollector()
    original_level: int = logger.level
    logger.addHandler(collector)
    logger.setLevel(logging.WARNING)
    rejected: RejectedFile = RejectedFile()

    try:
        with pytest.raises(ValueError, match=r"1 row\(s\) could not be converted"):
            convert_csv_file(csv_file, tmp_path, rejected, logger)
    finally:
        logger.removeHandler(collector)
        logger.setLevel(original_level)

    assert any("ignoring logs.csv line 3" in message for message in collector.messages)
    assert rejected.row_count == 1
    assert rejected.examples == ["line 3: Port is only valid with Protocol 6 or 17"]


@pytest.mark.parametrize("blanked_column", ["App ID", "Src IP", "Dst IP"])
def test_convert_row_rejects_a_blank_mandatory_value(blanked_column: str) -> None:
    incomplete_row: dict[str, str] = {**VALID_ROW, blanked_column: "   "}

    with pytest.raises(ValueError, match="must be present"):
        convert_row(incomplete_row)


@pytest.mark.parametrize("log_count", ["0", "-5"])
def test_convert_row_rejects_a_log_count_below_one(log_count: str) -> None:
    """A flow logged zero or fewer times carries no information the importer could store."""
    unlogged_row: dict[str, str] = {**VALID_ROW, "Log count": log_count}

    with pytest.raises(ValueError, match="must be present"):
        convert_row(unlogged_row)


class LogMessageCollector(logging.Handler):
    """Collect formatted log messages emitted during a test."""

    def __init__(self) -> None:
        super().__init__()
        self.messages: list[str] = []

    def emit(self, record: logging.LogRecord) -> None:
        self.messages.append(self.format(record))
