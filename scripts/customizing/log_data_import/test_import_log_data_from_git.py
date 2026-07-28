import logging
from pathlib import Path

from scripts.customizing.log_data_import.import_log_data_from_git import LogDataEntry, convert_csv_file, convert_row

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


def test_convert_csv_file_logs_and_skips_invalid_rows(tmp_path: Path) -> None:
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

    try:
        converted: list[LogDataEntry] = convert_csv_file(csv_file, tmp_path, logger)
    finally:
        logger.removeHandler(collector)
        logger.setLevel(original_level)

    expected_entries: list[LogDataEntry] = [
        {
            "app_id": "APP-1",
            "log_count": 42,
            "source": "192.0.2.1",
            "destination": "198.51.100.1",
            "protocol": 6,
            "port": 443,
            "action": "accept",
        }
    ]
    assert converted == expected_entries
    assert any("ignoring logs.csv line 3" in message for message in collector.messages)


class LogMessageCollector(logging.Handler):
    """Collect formatted log messages emitted during a test."""

    def __init__(self) -> None:
        super().__init__()
        self.messages: list[str] = []

    def emit(self, record: logging.LogRecord) -> None:
        self.messages.append(self.format(record))
