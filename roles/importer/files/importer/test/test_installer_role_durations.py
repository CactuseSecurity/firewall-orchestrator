"""Tests for the controller-only installer role-duration recap."""

from __future__ import annotations

import importlib.util
import sys
from dataclasses import dataclass, field
from types import ModuleType
from typing import TYPE_CHECKING
from unittest.mock import patch

import pytest

if TYPE_CHECKING:
    from collections.abc import Iterator

if importlib.util.find_spec("callback_plugins") is None:
    pytest.skip("Installer callback requires the source checkout", allow_module_level=True)


@dataclass
class FakeRole:
    """Typed stand-in for the role name provided by Ansible."""

    name: str

    def get_name(self) -> str:
        """Return the role's name."""
        return self.name


@dataclass
class FakeTask:
    """Typed stand-in for a task's role association."""

    _role: FakeRole | None


@dataclass
class FakeDisplay:
    """Capture the callback output without dynamic mock attributes."""

    banners: list[str] = field(default_factory=list[str])
    lines: list[str] = field(default_factory=list[str])

    def banner(self, msg: str) -> None:
        """Record a section banner."""
        self.banners.append(msg)

    def display(self, msg: str) -> None:
        """Record a recap row."""
        self.lines.append(msg)


class FakeCallbackBase:
    """Stand-in for Ansible's callback base class, so these tests need no Ansible installation."""

    def __init__(self) -> None:
        """Provide the controller display the callback writes to."""
        self._display: FakeDisplay = FakeDisplay()


class FakeCallbackPluginModule(ModuleType):
    """Stand-in for ansible.plugins.callback, exposing only the callback base class."""

    CallbackBase = FakeCallbackBase


# Ansible is an installer dependency only; keep it out of the test and production requirements.
FAKE_ANSIBLE_MODULES: dict[str, ModuleType] = {
    "ansible": ModuleType("ansible"),
    "ansible.plugins": ModuleType("ansible.plugins"),
    "ansible.plugins.callback": FakeCallbackPluginModule("ansible.plugins.callback"),
}

with patch.dict(sys.modules, FAKE_ANSIBLE_MODULES):
    from callback_plugins import fwo_role_durations as profiling


def create(name: str | None) -> FakeTask:
    """Build a task with just the role association used by the callback."""
    return FakeTask(None if name is None else FakeRole(name))


def test_combines_repeated_roles_and_excludes_standalone_tasks(monkeypatch: pytest.MonkeyPatch) -> None:
    times: Iterator[float] = iter([0.0, 2.0, 5.0, 9.0, 10.0])
    monkeypatch.setattr(profiling, "monotonic", lambda: next(times))
    callback: profiling.CallbackModule = profiling.CallbackModule()
    display: FakeDisplay = FakeDisplay()
    monkeypatch.setattr(callback, "_display", display)

    callback.v2_playbook_on_task_start(create("common"), False)
    callback.v2_playbook_on_task_start(create(None), False)
    callback.v2_playbook_on_task_start(create("database"), False)
    callback.v2_playbook_on_task_start(create("common"), False)
    assert display.lines == []
    assert display.banners == []
    callback.v2_playbook_on_stats(object())

    assert callback.roleDurations == {"common": 3.0, "database": 4.0}
    assert display.banners == ["ROLE DURATIONS"]
    assert [line.strip() for line in display.lines] == [
        "database      4.00s",
        "common        3.00s",
    ]


def test_handlers_are_timed_without_counting_time_between_plays(monkeypatch: pytest.MonkeyPatch) -> None:
    times: Iterator[float] = iter([0.0, 1.0, 3.0, 8.0, 10.0])
    monkeypatch.setattr(profiling, "monotonic", lambda: next(times))
    callback: profiling.CallbackModule = profiling.CallbackModule()
    monkeypatch.setattr(callback, "_display", FakeDisplay())

    callback.v2_playbook_on_task_start(create("common"), False)
    callback.v2_playbook_on_handler_task_start(create("common"))
    callback.v2_playbook_on_play_start(object())
    callback.v2_playbook_on_task_start(create("ui"), False)
    callback.v2_playbook_on_stats(object())

    assert callback.roleDurations == {"common": 3.0, "ui": 2.0}


def test_no_role_tasks_produce_no_recap(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(profiling, "monotonic", lambda: 0.0)
    callback: profiling.CallbackModule = profiling.CallbackModule()
    display: FakeDisplay = FakeDisplay()
    monkeypatch.setattr(callback, "_display", display)

    callback.v2_playbook_on_task_start(create(None), False)
    callback.v2_playbook_on_stats(object())

    assert display.banners == []
    assert display.lines == []


def test_equal_durations_are_ordered_by_role_name(monkeypatch: pytest.MonkeyPatch) -> None:
    times: Iterator[float] = iter([0.0, 1.0, 2.0])
    monkeypatch.setattr(profiling, "monotonic", lambda: next(times))
    callback: profiling.CallbackModule = profiling.CallbackModule()
    display: FakeDisplay = FakeDisplay()
    monkeypatch.setattr(callback, "_display", display)

    callback.v2_playbook_on_task_start(create("ui"), False)
    callback.v2_playbook_on_task_start(create("api"), False)
    callback.v2_playbook_on_stats(object())

    assert [line.split()[0] for line in display.lines] == ["api", "ui"]


def test_finishing_an_interval_twice_does_not_count_it_twice(monkeypatch: pytest.MonkeyPatch) -> None:
    """Closing an already completed task must not add time to its role."""
    monkeypatch.setattr(profiling, "monotonic", lambda: 0.0)
    callback: profiling.CallbackModule = profiling.CallbackModule()

    callback.v2_playbook_on_task_start(create("common"), False)
    callback.finish(2.0)
    callback.finish(5.0)

    assert callback.roleDurations == {"common": 2.0}
    assert callback.activeRole is None
    assert callback.taskStartedAt is None
