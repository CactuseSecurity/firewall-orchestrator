"""Print cumulative elapsed task time per role when the installer playbook finishes."""

from __future__ import annotations

from time import monotonic
from typing import TYPE_CHECKING, Protocol

if TYPE_CHECKING:
    # Ansible does not ship types for these interfaces. Describe only the callback boundary we use.
    class Display(Protocol):
        """The controller output operations used by the recap."""

        def banner(self, msg: str) -> None:
            """Print a section banner."""
            ...

        def display(self, msg: str) -> None:
            """Print a recap row."""
            ...

    class Role(Protocol):
        """The role name exposed by Ansible."""

        def get_name(self) -> str:
            """Return the loaded role's name."""
            ...

    class Task(Protocol):
        """The role association exposed by an Ansible task."""

        @property
        def _role(self) -> Role | None:
            """Return the owning role, or None for a standalone task."""
            ...

    class CallbackBase:
        """Typed view of the untyped Ansible callback base class."""

        _display: Display

        def __init__(self) -> None:
            """Initialize the callback's controller display."""
            ...
else:
    from ansible.plugins.callback import CallbackBase

DOCUMENTATION = """
name: fwo_role_durations
type: aggregate
short_description: Show elapsed time per role at the end of the playbook
description:
  - Adds a concise role-duration recap after the normal playbook output.
  - Repeated executions of a role are combined; tasks outside roles are excluded.
  - Measures elapsed task time across hosts, rather than summing per-host times.
requirements:
  - Enable this callback in callbacks_enabled.
"""


class CallbackModule(CallbackBase):
    """Collect role task intervals without adding output during task execution."""

    CALLBACK_VERSION = 2.0
    CALLBACK_TYPE = "aggregate"
    CALLBACK_NAME = "fwo_role_durations"
    CALLBACK_NEEDS_ENABLED = True

    def __init__(self) -> None:
        super().__init__()
        self.roleDurations: dict[str, float] = {}
        self.activeRole: str | None = None
        self.taskStartedAt: float | None = None

    def finish(self, now: float) -> None:
        """Close the current interval once, excluding tasks that have no role."""
        if self.activeRole is not None and self.taskStartedAt is not None:
            self.roleDurations[self.activeRole] = (
                self.roleDurations.get(self.activeRole, 0.0) + now - self.taskStartedAt
            )
        self.activeRole = None
        self.taskStartedAt = None

    def record(self, task: Task) -> None:
        """Start a new role interval, closing the previous task first."""
        now: float = monotonic()
        self.finish(now)
        # Ansible exposes a task's role through this attribute, as its bundled role profiler does.
        role: Role | None = task._role  # pyright: ignore[reportPrivateUsage]  # noqa: SLF001
        self.activeRole = role.get_name() if role is not None else None
        self.taskStartedAt = now

    def v2_playbook_on_task_start(self, task: Task, is_conditional: bool) -> None:  # noqa: ARG002
        """Record one task start, shared by every host executing that task."""
        self.record(task)

    def v2_playbook_on_handler_task_start(self, task: Task) -> None:
        """Include handler time in the owning role's duration."""
        self.record(task)

    def v2_playbook_on_play_start(self, play: object) -> None:  # noqa: ARG002
        """Exclude the interval between plays from the preceding role."""
        self.finish(monotonic())

    def v2_playbook_on_stats(self, stats: object) -> None:  # noqa: ARG002
        """Show one row per role, longest first, after the playbook finishes."""
        self.finish(monotonic())
        if not self.roleDurations:
            return
        self._display.banner("ROLE DURATIONS")
        width: int = max(len(name) for name in self.roleDurations)
        for name, duration in sorted(self.roleDurations.items(), key=lambda entry: (-entry[1], entry[0])):
            self._display.display(f"  {name:<{width}}  {duration:>8.2f}s")
