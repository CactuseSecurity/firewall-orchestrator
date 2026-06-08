from pathlib import Path
from typing import Any, Protocol

import pytest
from fwo_log import FWOLogger, LogLock

LogEvent = str | tuple[str, str, int] | tuple[str, str, Any, int]


class FakeFcntl:
    LOCK_EX = 1
    LOCK_UN = 2

    def __init__(self) -> None:
        self.calls: list[tuple[int, int]] = []

    def flock(self, file_descriptor: int, lock_type: int) -> None:
        self.calls.append((file_descriptor, lock_type))


class TrackingSemaphore:
    def __init__(self) -> None:
        self.events: list[LogEvent] = []

    def acquire(self) -> None:
        self.events.append("acquire")

    def release(self) -> None:
        self.events.append("release")


class TrackingLogger:
    def __init__(self, events: list[LogEvent]) -> None:
        self.events = events

    def debug(self, msg: str, stacklevel: int) -> None:
        self.events.append(("debug", msg, stacklevel))

    def error(self, msg: str, stacklevel: int) -> None:
        self.events.append(("error", msg, stacklevel))

    def info(self, msg: str, stacklevel: int) -> None:
        self.events.append(("info", msg, stacklevel))

    def warning(self, msg: str, stacklevel: int) -> None:
        self.events.append(("warning", msg, stacklevel))

    def exception(self, msg: str, exc_info: Any, stacklevel: int) -> None:
        self.events.append(("exception", msg, exc_info, stacklevel))


class TrackingLoggerProtocol(Protocol):
    def get_logger(self) -> TrackingLogger: ...


class TrackingLoggerInstance(TrackingLoggerProtocol):
    def __init__(self, logger: TrackingLogger, debug_level: int = 1) -> None:
        self.logger = logger
        self.debug_level = debug_level

    def get_logger(self) -> TrackingLogger:
        return self.logger


def test_logger_initialization_starts_log_lock(monkeypatch: pytest.MonkeyPatch) -> None:
    start_calls: list[str] = []

    monkeypatch.setattr(LogLock, "start", lambda: start_calls.append("started"))

    FWOLogger(1)

    assert start_calls == ["started"]


def test_log_lock_file_transactions_use_flock(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    fake_fcntl = FakeFcntl()
    lock_file_path = tmp_path / "importer_api_log.lock"

    monkeypatch.setattr("fwo_log.fcntl", fake_fcntl)
    monkeypatch.setattr(LogLock, "lock_file_path", lock_file_path)

    with LogLock.locked_file("a+") as lock_file:
        lock_file.write("REQUESTED\n")

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "REQUESTED\n"


def test_info_logging_uses_log_lock(monkeypatch: pytest.MonkeyPatch) -> None:
    tracking_semaphore = TrackingSemaphore()
    tracking_logger = TrackingLogger(tracking_semaphore.events)

    monkeypatch.setattr(LogLock, "semaphore", tracking_semaphore)
    monkeypatch.setattr(FWOLogger, "instance", TrackingLoggerInstance(tracking_logger))

    FWOLogger.info("message")

    assert tracking_semaphore.events == ["acquire", ("info", "message", 2), "release"]


def test_debug_logging_uses_log_lock_when_level_matches(monkeypatch: pytest.MonkeyPatch) -> None:
    tracking_semaphore = TrackingSemaphore()
    tracking_logger = TrackingLogger(tracking_semaphore.events)

    monkeypatch.setattr(LogLock, "semaphore", tracking_semaphore)
    monkeypatch.setattr(FWOLogger, "instance", TrackingLoggerInstance(tracking_logger, debug_level=2))

    FWOLogger.debug("message", needed_level=2)

    assert tracking_semaphore.events == ["acquire", ("debug", "message", 2), "release"]


def test_debug_logging_skips_log_lock_when_level_is_too_low(monkeypatch: pytest.MonkeyPatch) -> None:
    tracking_semaphore = TrackingSemaphore()
    tracking_logger = TrackingLogger(tracking_semaphore.events)

    monkeypatch.setattr(LogLock, "semaphore", tracking_semaphore)
    monkeypatch.setattr(FWOLogger, "instance", TrackingLoggerInstance(tracking_logger, debug_level=1))

    FWOLogger.debug("message", needed_level=2)

    assert tracking_semaphore.events == []


def test_error_warning_and_exception_logging_use_log_lock(monkeypatch: pytest.MonkeyPatch) -> None:
    tracking_semaphore = TrackingSemaphore()
    tracking_logger = TrackingLogger(tracking_semaphore.events)

    monkeypatch.setattr(LogLock, "semaphore", tracking_semaphore)
    monkeypatch.setattr(FWOLogger, "instance", TrackingLoggerInstance(tracking_logger))

    FWOLogger.error("error message")
    FWOLogger.warning("warning message")
    FWOLogger.exception("exception message", exc_info=True)

    assert tracking_semaphore.events == [
        "acquire",
        ("error", "error message", 2),
        "release",
        "acquire",
        ("warning", "warning message", 2),
        "release",
        "acquire",
        ("exception", "exception message", True, 2),
        "release",
    ]
