import importlib.util
from contextlib import AbstractContextManager
from pathlib import Path
from types import ModuleType
from typing import Any, Protocol, TextIO, cast


class MonkeyPatchFixture(Protocol):
    def setattr(self, target: Any, name: str, value: Any) -> None: ...


class FileLockModule(Protocol):
    LOCK_EX: int
    LOCK_UN: int

    def flock(self, file_descriptor: int, lock_type: int) -> None: ...


class AcquireLockScript(Protocol):
    fcntl: FileLockModule | None
    sys: Any

    def locked_file(self, lock_file_path: Path, mode: str) -> AbstractContextManager[TextIO]: ...

    def read_last_line(self, lock_file: TextIO) -> str: ...

    def append_line(self, lock_file: TextIO, line: str) -> None: ...

    def acquire_lock(self, lock_file_path: Path, timeout: float = 10, retry_delay: float = 0.1) -> None: ...

    def main(self) -> None: ...


class ReleaseLockScript(Protocol):
    fcntl: FileLockModule | None
    sys: Any

    def locked_file(self, lock_file_path: Path, mode: str) -> AbstractContextManager[TextIO]: ...

    def release_lock(self, lock_file_path: Path) -> None: ...

    def main(self) -> None: ...


class FakeFcntl:
    LOCK_EX = 1
    LOCK_UN = 2

    def __init__(self) -> None:
        self.calls: list[tuple[int, int]] = []

    def flock(self, file_descriptor: int, lock_type: int) -> None:
        self.calls.append((file_descriptor, lock_type))


def find_repository_root(start: Path) -> Path:
    for candidate in [start.parent, *start.parents]:
        if (candidate / "scripts" / "acquire_lock.py").is_file():
            return candidate

    raise FileNotFoundError("Could not find repository root containing scripts/acquire_lock.py")


def load_script(script_name: str) -> ModuleType:
    repository_root = find_repository_root(Path(__file__).resolve())
    script_path = repository_root / "scripts" / script_name

    if not script_path.is_file():
        raise FileNotFoundError(f"Could not find script: {script_path}")

    spec = importlib.util.spec_from_file_location(script_name.removesuffix(".py"), script_path)
    assert spec is not None
    assert spec.loader is not None

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def skip_sleep(_delay: float) -> None:
    return None


def assert_raises_assertion(callable_object: Any) -> None:
    try:
        callable_object()
    except AssertionError:
        return
    raise AssertionError("Expected AssertionError")


def test_acquire_lock_file_transactions_use_flock(monkeypatch: MonkeyPatchFixture, tmp_path: Path) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))
    fake_fcntl = FakeFcntl()

    monkeypatch.setattr(acquire_lock, "fcntl", fake_fcntl)
    lock_file_path = tmp_path / "fworch_log.lock"

    with acquire_lock.locked_file(lock_file_path, "a+") as lock_file:
        acquire_lock.append_line(lock_file, "REQUESTED")

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "REQUESTED\n"


def test_acquire_lock_returns_when_lock_was_granted(tmp_path: Path) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))
    lock_file_path = tmp_path / "fworch_log.lock"
    lock_file_path.write_text("REQUESTED\nGRANTED\n", encoding="utf-8")

    acquire_lock.acquire_lock(lock_file_path, retry_delay=0)

    assert lock_file_path.read_text(encoding="utf-8") == "REQUESTED\nGRANTED\n"


def test_acquire_lock_forcefully_grants_after_timeout(monkeypatch: MonkeyPatchFixture, tmp_path: Path) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))
    lock_file_path = tmp_path / "fworch_log.lock"
    lock_file_path.write_text("REQUESTED\n", encoding="utf-8")
    timestamps = iter([0.0, 2.0])

    monkeypatch.setattr(acquire_lock, "time", lambda: next(timestamps))
    monkeypatch.setattr(acquire_lock, "sleep", skip_sleep)

    acquire_lock.acquire_lock(lock_file_path, timeout=1, retry_delay=0)

    assert lock_file_path.read_text(encoding="utf-8") == "REQUESTED\nFORCEFULLY GRANTED\n"


def test_acquire_lock_main_requires_lock_file_argument(monkeypatch: MonkeyPatchFixture) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))

    monkeypatch.setattr(acquire_lock.sys, "argv", ["acquire_lock.py"])

    assert_raises_assertion(acquire_lock.main)


def test_acquire_lock_retries_after_lock_failure(monkeypatch: MonkeyPatchFixture, tmp_path: Path) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))
    real_locked_file = acquire_lock.locked_file
    attempts = 0
    lock_file_path = tmp_path / "fworch_log.lock"
    lock_file_path.write_text("GRANTED\n", encoding="utf-8")

    def flaky_locked_file(lock_file_path: Path, mode: str) -> AbstractContextManager[TextIO]:
        nonlocal attempts
        attempts += 1
        if attempts == 1:
            raise OSError("temporary failure")
        return real_locked_file(lock_file_path, mode)

    monkeypatch.setattr(acquire_lock, "locked_file", flaky_locked_file)
    monkeypatch.setattr(acquire_lock, "sleep", skip_sleep)

    acquire_lock.acquire_lock(lock_file_path, retry_delay=0)

    assert attempts == 2


def test_acquire_lock_main_passes_lock_file_argument(monkeypatch: MonkeyPatchFixture, tmp_path: Path) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))
    lock_file_path = tmp_path / "fworch_log.lock"
    received_paths: list[str] = []

    def record_path(path: str) -> None:
        received_paths.append(path)

    monkeypatch.setattr(acquire_lock.sys, "argv", ["acquire_lock.py", str(lock_file_path)])
    monkeypatch.setattr(acquire_lock, "acquire_lock", record_path)

    acquire_lock.main()

    assert received_paths == [str(lock_file_path)]


def test_release_lock_writes_state_under_flock(monkeypatch: MonkeyPatchFixture, tmp_path: Path) -> None:
    release_lock = cast("ReleaseLockScript", load_script("release_lock.py"))
    fake_fcntl = FakeFcntl()

    monkeypatch.setattr(release_lock, "fcntl", fake_fcntl)
    lock_file_path = tmp_path / "fworch_log.lock"

    release_lock.release_lock(lock_file_path)

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "RELEASED\n"


def test_release_lock_retries_after_write_failure(monkeypatch: MonkeyPatchFixture, tmp_path: Path) -> None:
    release_lock = cast("ReleaseLockScript", load_script("release_lock.py"))
    real_locked_file = release_lock.locked_file
    attempts = 0

    def flaky_locked_file(lock_file_path: Path, mode: str) -> AbstractContextManager[TextIO]:
        nonlocal attempts
        attempts += 1
        if attempts == 1:
            raise OSError("temporary failure")
        return real_locked_file(lock_file_path, mode)

    monkeypatch.setattr(release_lock, "locked_file", flaky_locked_file)
    monkeypatch.setattr(release_lock, "sleep", skip_sleep)
    lock_file_path = tmp_path / "fworch_log.lock"

    release_lock.release_lock(lock_file_path)

    assert attempts == 2
    assert lock_file_path.read_text(encoding="utf-8") == "RELEASED\n"


def test_release_lock_main_requires_lock_file_argument(monkeypatch: MonkeyPatchFixture) -> None:
    release_lock = cast("ReleaseLockScript", load_script("release_lock.py"))

    monkeypatch.setattr(release_lock.sys, "argv", ["release_lock.py"])

    assert_raises_assertion(release_lock.main)
