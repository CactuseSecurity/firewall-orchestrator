import tempfile
import threading
from pathlib import Path
from typing import Any, Protocol

import fwo_log
import pytest
from fwo_log import FileLockModule, FWOLogger, LogLock
from test import TEST_LOG_LOCK_DIR

LogEvent = str | tuple[str, str, int] | tuple[str, str, Any, int]


class FakeFcntl:
    """
    Records the flock calls of the test thread only.

    The module-level fcntl replacement is also seen by the FWOLogLock background thread that
    test/__init__.py starts. Its calls are passed on to the real module, so they neither show up
    in the recorded calls nor lose their locking.
    """

    # same values as the real module, so that calls of other threads can be passed on unchanged
    LOCK_EX = fwo_log.fcntl.LOCK_EX if fwo_log.fcntl is not None else 1
    LOCK_UN = fwo_log.fcntl.LOCK_UN if fwo_log.fcntl is not None else 2

    def __init__(self, delegate: FileLockModule | None) -> None:
        self.calls: list[tuple[int, int]] = []
        self.delegate = delegate
        self.recording_thread_id = threading.get_ident()

    def flock(self, file_descriptor: int, lock_type: int) -> None:
        if threading.get_ident() == self.recording_thread_id:
            self.calls.append((file_descriptor, lock_type))
        elif self.delegate is not None:
            self.delegate.flock(file_descriptor, lock_type)


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


def test_session_log_lock_thread_watches_private_lock_file() -> None:
    # test/__init__.py starts the FWOLogLock thread for the whole session. It must not poll the
    # production lock file, which a log swap on an installed FWO host writes to while the tests run.
    session_lock_file = Path(LogLock.lock_file_path)

    assert LogLock.lock_thread is not None
    assert LogLock.lock_thread.is_alive()
    assert session_lock_file.parent == TEST_LOG_LOCK_DIR
    assert Path(tempfile.gettempdir()) in session_lock_file.parents


def test_log_lock_file_transactions_use_flock(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    fake_fcntl = FakeFcntl(fwo_log.fcntl)
    lock_file_path = tmp_path / "importer_api_log.lock"

    # A subclass keeps the test lock file away from the FWOLogLock background thread, which uses
    # LogLock itself: seeing REQUESTED in it would make the thread take LogLock.semaphore.
    class IsolatedLogLock(LogLock):
        pass

    monkeypatch.setattr("fwo_log.fcntl", fake_fcntl)
    monkeypatch.setattr(IsolatedLogLock, "lock_file_path", lock_file_path)

    with IsolatedLogLock.locked_file("a+") as lock_file:
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


def test_log_lock_semaphore_is_bounded() -> None:
    # The handle_log_lock state machine pairs each acquire (REQUESTED/GRANTED) with
    # exactly one release (RELEASED/FORCEFULLY RELEASED). A BoundedSemaphore makes an
    # unpaired release fail fast instead of silently breaking mutual exclusion, so the
    # production lock must be the bounded variant.
    assert isinstance(LogLock.semaphore, threading.BoundedSemaphore)


def test_unpaired_release_fails_fast_with_bounded_semaphore() -> None:
    # Reproduce a lock-file "drift": a release without an intervening acquire (e.g. two
    # consecutive release states). The bounded semaphore raises immediately and refuses
    # to raise its counter above the initial value, so mutual exclusion stays intact...
    bounded = threading.BoundedSemaphore()
    bounded.acquire()
    bounded.release()  # paired release - fine

    with pytest.raises(ValueError, match="released too many times"):
        bounded.release()  # unpaired drift -> fail fast

    # ...whereas a plain (unbounded) Semaphore would silently let the counter climb to 2,
    # letting two parties "own" the lock at once and corrupting the log swap unnoticed.
    plain = threading.Semaphore()
    plain.acquire()
    plain.release()
    plain.release()  # no error: the bug this guards against would pass silently


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
