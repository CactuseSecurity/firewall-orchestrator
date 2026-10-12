from __future__ import annotations

from typing import Any, cast

import fwo_globals
import fwo_http_retry
import pytest
import requests
from fwo_http_retry import is_service_unreachable, is_unsent_request_error, send_with_retry
from urllib3.exceptions import MaxRetryError, NewConnectionError, ProtocolError

HTTP_OK = 200
HTTP_BAD_GATEWAY = 502
HTTP_SERVICE_UNAVAILABLE = 503
TARGET = "FWO API"


class _Response:
    def __init__(self, status_code: int) -> None:
        self.status_code = status_code


def _refused() -> requests.exceptions.ConnectionError:
    # the shape requests produces for "connection refused": ConnectionError(MaxRetryError(reason=NewConnectionError))
    reason = NewConnectionError(cast("Any", None), "Connection refused")
    return requests.exceptions.ConnectionError(MaxRetryError(cast("Any", None), "https://fworch/api", reason=reason))


def _dropped() -> requests.exceptions.ConnectionError:
    # the shape requests produces when the server closes the connection after the request was sent
    return requests.exceptions.ConnectionError(ProtocolError("Connection aborted.", ConnectionResetError()))


class _Sender:
    """Plays back one outcome per call: a status code to answer with or an exception to raise."""

    def __init__(self, outcomes: list[int | Exception]) -> None:
        self._outcomes = outcomes
        self.calls = 0

    def __call__(self) -> requests.Response:
        outcome = self._outcomes[self.calls]
        self.calls += 1
        if isinstance(outcome, Exception):
            raise outcome
        return cast("requests.Response", _Response(outcome))


@pytest.fixture(autouse=True)
def no_shutdown(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(fwo_globals, "shutdown_requested", False)
    monkeypatch.setattr(fwo_globals, "shutdown_deferral_active", False)


@pytest.fixture
def sleeps(monkeypatch: pytest.MonkeyPatch) -> list[float]:
    recorded: list[float] = []
    monkeypatch.setattr(fwo_http_retry, "_sleep", recorded.append)
    return recorded


class TestSendWithRetry:
    def test_returns_first_response_without_sleeping(self, sleeps: list[float]) -> None:
        sender = _Sender([HTTP_OK])

        response = send_with_retry(sender, TARGET, (1, 2))

        assert response.status_code == HTTP_OK
        assert sender.calls == 1
        assert sleeps == []

    def test_retries_503_until_success(self, sleeps: list[float]) -> None:
        sender = _Sender([HTTP_SERVICE_UNAVAILABLE, HTTP_SERVICE_UNAVAILABLE, HTTP_OK])

        response = send_with_retry(sender, TARGET, (1, 2, 3))

        assert response.status_code == HTTP_OK
        assert sleeps == [1, 2]

    def test_returns_last_503_when_delays_are_exhausted(self, sleeps: list[float]) -> None:
        sender = _Sender([HTTP_SERVICE_UNAVAILABLE] * 3)

        response = send_with_retry(sender, TARGET, (1, 2))

        assert response.status_code == HTTP_SERVICE_UNAVAILABLE
        assert sender.calls == 3
        assert sleeps == [1, 2]

    def test_retries_refused_connection(self, sleeps: list[float]) -> None:
        sender = _Sender([_refused(), HTTP_OK])

        response = send_with_retry(sender, TARGET, (5,))

        assert response.status_code == HTTP_OK
        assert sleeps == [5]

    @pytest.mark.usefixtures("sleeps")
    def test_reraises_refused_connection_when_delays_are_exhausted(self) -> None:
        sender = _Sender([_refused(), _refused()])

        with pytest.raises(requests.exceptions.ConnectionError):
            send_with_retry(sender, TARGET, (5,))

        assert sender.calls == 2

    def test_does_not_retry_dropped_connection(self, sleeps: list[float]) -> None:
        sender = _Sender([_dropped(), HTTP_OK])

        with pytest.raises(requests.exceptions.ConnectionError):
            send_with_retry(sender, TARGET, (5,))

        assert sender.calls == 1
        assert sleeps == []

    @pytest.mark.usefixtures("sleeps")
    def test_does_not_retry_502(self) -> None:
        sender = _Sender([HTTP_BAD_GATEWAY, HTTP_OK])

        response = send_with_retry(sender, TARGET, (5,))

        assert response.status_code == HTTP_BAD_GATEWAY
        assert sender.calls == 1

    @pytest.mark.usefixtures("sleeps")
    def test_does_not_retry_ssl_errors(self) -> None:
        sender = _Sender([requests.exceptions.SSLError("certificate verify failed"), HTTP_OK])

        with pytest.raises(requests.exceptions.SSLError):
            send_with_retry(sender, TARGET, (5,))

        assert sender.calls == 1

    @pytest.mark.usefixtures("sleeps")
    def test_without_delays_sends_exactly_once(self) -> None:
        sender = _Sender([HTTP_SERVICE_UNAVAILABLE])

        response = send_with_retry(sender, TARGET, ())

        assert response.status_code == HTTP_SERVICE_UNAVAILABLE
        assert sender.calls == 1


class TestSendWithRetryDuringShutdown:
    def test_requested_shutdown_returns_the_503_without_waiting(self, sleeps: list[float]) -> None:
        fwo_globals.shutdown_requested = True
        sender = _Sender([HTTP_SERVICE_UNAVAILABLE, HTTP_OK])

        response = send_with_retry(sender, TARGET, (1, 2))

        assert response.status_code == HTTP_SERVICE_UNAVAILABLE
        assert sender.calls == 1
        assert sleeps == []

    def test_requested_shutdown_reraises_the_refused_connection_without_waiting(self, sleeps: list[float]) -> None:
        fwo_globals.shutdown_requested = True
        sender = _Sender([_refused(), HTTP_OK])

        with pytest.raises(requests.exceptions.ConnectionError):
            send_with_retry(sender, TARGET, (1, 2))

        assert sender.calls == 1
        assert sleeps == []

    def test_retries_go_on_during_a_deferred_rollback(self, sleeps: list[float]) -> None:
        fwo_globals.shutdown_requested = True
        fwo_globals.shutdown_deferral_active = True
        sender = _Sender([HTTP_SERVICE_UNAVAILABLE, _refused(), HTTP_OK])

        response = send_with_retry(sender, TARGET, (1, 2))

        assert response.status_code == HTTP_OK
        assert sleeps == [1, 2]


class TestErrorClassification:
    @pytest.mark.parametrize(
        ("error", "expected"),
        [
            pytest.param(_refused(), True, id="refused"),
            pytest.param(requests.exceptions.ConnectTimeout("connect timed out"), True, id="connect-timeout"),
            pytest.param(_dropped(), False, id="dropped"),
            pytest.param(requests.exceptions.SSLError("bad certificate"), False, id="ssl"),
            pytest.param(requests.exceptions.ReadTimeout("read timed out"), False, id="read-timeout"),
        ],
    )
    def test_is_unsent_request_error(self, error: Exception, expected: bool) -> None:
        assert is_unsent_request_error(error) is expected

    @pytest.mark.parametrize(
        ("error", "expected"),
        [
            pytest.param(_refused(), True, id="refused"),
            pytest.param(_dropped(), True, id="dropped"),
            pytest.param(requests.exceptions.ConnectTimeout("connect timed out"), True, id="connect-timeout"),
            pytest.param(requests.exceptions.SSLError("bad certificate"), False, id="ssl"),
            pytest.param(requests.exceptions.ReadTimeout("read timed out"), False, id="read-timeout"),
            pytest.param(ValueError("not a request error"), False, id="other"),
        ],
    )
    def test_is_service_unreachable(self, error: Exception, expected: bool) -> None:
        assert is_service_unreachable(error) is expected
