import signal
import fwo_globals

def handle_shutdown_signal(signum, frame):
    fwo_globals.shutdown_requested = True
    print(f"Received shutdown signal: {signal.Signals(signum).name}. Performing cleanup...")

def registerSignallingHandlers():
    # Register signal handlers for system shutdown interrupts
    signal.signal(signal.SIGTERM, handle_shutdown_signal)  # Handle termination signal
    signal.signal(signal.SIGINT, handle_shutdown_signal)   # Handle interrupt signal (e.g., Ctrl+C)
