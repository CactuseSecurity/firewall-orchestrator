import importlib.util
from pathlib import Path


class FakeFcntl:
    LOCK_EX = 1
    LOCK_UN = 2

    def __init__(self):
        self.calls = []

    def flock(self, file_descriptor, lock_type):
        self.calls.append((file_descriptor, lock_type))


def load_script(script_name):
    repository_root = Path(__file__).resolve().parents[5]
    script_path = repository_root / "scripts" / script_name
    spec = importlib.util.spec_from_file_location(script_name.replace(".py", ""), script_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def test_acquire_lock_file_transactions_use_flock(monkeypatch, tmp_path):
    acquire_lock = load_script("acquire_lock.py")
    fake_fcntl = FakeFcntl()

    monkeypatch.setattr(acquire_lock, "fcntl", fake_fcntl)
    lock_file_path = tmp_path / "fworch_log.lock"

    with acquire_lock.locked_file(lock_file_path, "a+") as lock_file:
        acquire_lock.append_line(lock_file, "REQUESTED")

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "REQUESTED\n"


def test_release_lock_writes_state_under_flock(monkeypatch, tmp_path):
    release_lock = load_script("release_lock.py")
    fake_fcntl = FakeFcntl()

    monkeypatch.setattr(release_lock, "fcntl", fake_fcntl)
    lock_file_path = tmp_path / "fworch_log.lock"

    release_lock.release_lock(lock_file_path)

    assert [lock_type for _, lock_type in fake_fcntl.calls] == [fake_fcntl.LOCK_EX, fake_fcntl.LOCK_UN]
    assert lock_file_path.read_text() == "RELEASED\n"
