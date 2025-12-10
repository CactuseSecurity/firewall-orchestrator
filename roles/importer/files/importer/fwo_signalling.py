import signal
from typing import Any

import fwo_globals
from fwo_exceptions import ShutdownRequestedError


def handle_shutdown_signal(_: int, __: Any):
    fwo_globals.shutdown_requested = True
    raise ShutdownRequestedError


def register_signalling_handlers():
    # Register signal handlers for system shutdown interrupts
    signal.signal(signal.SIGTERM, handle_shutdown_signal)  # Handle termination signal
    signal.signal(signal.SIGINT, handle_shutdown_signal)  # Handle interrupt signal (e.g., Ctrl+C)
