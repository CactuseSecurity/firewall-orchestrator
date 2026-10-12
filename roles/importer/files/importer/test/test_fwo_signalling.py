from __future__ import annotations

import signal

import fwo_globals
import pytest
from fwo_exceptions import ShutdownRequestedError
from fwo_signalling import handle_drain_signal, handle_shutdown_signal, register_signalling_handlers, shutdown_deferred


@pytest.fixture(autouse=True)
def clean_signal_state(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(fwo_globals, "shutdown_requested", False)
    monkeypatch.setattr(fwo_globals, "shutdown_deferral_active", False)
    monkeypatch.setattr(fwo_globals, "drain_requested", False)


class TestShutdownSignal:
    def test_signal_raises_outside_a_deferral(self) -> None:
        with pytest.raises(ShutdownRequestedError):
            handle_shutdown_signal(signal.SIGINT, None)

        assert fwo_globals.shutdown_requested is True

    def test_signal_only_sets_the_flag_while_deferred(self) -> None:
        with shutdown_deferred():
            handle_shutdown_signal(signal.SIGINT, None)

        assert fwo_globals.shutdown_requested is True

    def test_deferral_is_restored_after_nesting_and_errors(self) -> None:
        with shutdown_deferred():
            with pytest.raises(ValueError, match="inner"), shutdown_deferred():
                raise ValueError("inner")
            assert fwo_globals.shutdown_deferral_active is True

        assert fwo_globals.shutdown_deferral_active is False


class TestDrainSignal:
    def test_drain_signal_only_sets_the_flag(self) -> None:
        handle_drain_signal(signal.SIGUSR1, None)

        assert fwo_globals.drain_requested is True
        assert fwo_globals.shutdown_requested is False

    def test_drain_signal_is_registered(self) -> None:
        previous = {sig: signal.getsignal(sig) for sig in (signal.SIGINT, signal.SIGTERM, signal.SIGUSR1)}
        try:
            register_signalling_handlers()

            assert signal.getsignal(signal.SIGUSR1) is handle_drain_signal
        finally:
            for sig, handler in previous.items():
                signal.signal(sig, handler)
