import importlib.util
from contextlib import AbstractContextManager
from pathlib import Path
from types import ModuleType
from typing import Protocol, TextIO, cast

import pytest


class FileLockModule(Protocol):
    LOCK_EX: int
    LOCK_UN: int

    def flock(self, file_descriptor: int, lock_type: int) -> None: ...


class AcquireLockScript(Protocol):
    fcntl: FileLockModule | None

    def locked_file(self, lock_file_path: Path, mode: str) -> AbstractContextManager[TextIO]: ...

    def append_line(self, lock_file: TextIO, line: str) -> None: ...


class ReleaseLockScript(Protocol):
    fcntl: FileLockModule | None

    def release_lock(self, lock_file_path: Path) -> None: ...


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


def test_acquire_lock_file_transactions_use_flock(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    acquire_lock = cast("AcquireLockScript", load_script("acquire_lock.py"))
    fake_fcntl = FakeFcntl()

    monkeypatch.setattr(acquire_lock, "fcntl", fake_fcntl)
    lock_file_path = tmp_path / "fworch_log.lock"

    with acquire_lock.locked_file(lock_file_path, "a+") as lock_file:
        acquire_lock.append_line(lock_file, "REQUESTED")

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "REQUESTED\n"


def test_release_lock_writes_state_under_flock(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    release_lock = cast("ReleaseLockScript", load_script("release_lock.py"))
    fake_fcntl = FakeFcntl()

    monkeypatch.setattr(release_lock, "fcntl", fake_fcntl)
    lock_file_path = tmp_path / "fworch_log.lock"

    release_lock.release_lock(lock_file_path)

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "RELEASED\n"
