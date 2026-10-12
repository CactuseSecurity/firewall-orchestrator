from __future__ import annotations

import signal
from contextlib import contextmanager
from typing import TYPE_CHECKING, Any

import fwo_globals
from fwo_exceptions import ShutdownRequestedError

if TYPE_CHECKING:
    from collections.abc import Generator


def handle_shutdown_signal(_: int, __: Any) -> None:
    fwo_globals.shutdown_requested = True
    if fwo_globals.shutdown_deferral_active:
        # a rollback is running - raising here would abort it half way; the flag alone stops the
        # importer at its next shutdown check
        return
    raise ShutdownRequestedError


def handle_drain_signal(_: int, __: Any) -> None:
    # never raises: the running import has to finish. import_main_loop.wait_with_shutdown_check exits
    # with fwo_const.IMPORTER_DRAINED_EXIT_CODE at the next point where no import holds a lock
    fwo_globals.drain_requested = True


@contextmanager
def shutdown_deferred() -> Generator[None]:
    """Within this block a shutdown signal is recorded but does not interrupt the running code."""
    previous = fwo_globals.shutdown_deferral_active
    fwo_globals.shutdown_deferral_active = True
    try:
        yield
    finally:
        fwo_globals.shutdown_deferral_active = previous


def register_signalling_handlers() -> None:
    # Register signal handlers for system shutdown interrupts
    signal.signal(signal.SIGTERM, handle_shutdown_signal)  # Handle termination signal
    signal.signal(signal.SIGINT, handle_shutdown_signal)  # Handle interrupt signal (e.g., Ctrl+C)
    signal.signal(signal.SIGUSR1, handle_drain_signal)  # finish the running import, then exit (upgrade)
