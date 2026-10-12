"""
Retries for requests to the FWO API and middleware which never reached the service behind apache.

Two failures are retried: a connection that could not be established (refused, or connect timeout)
and an HTTP 503. Both happen for a few seconds whenever the installer or an operator restarts
apache, the api or the middleware. A refused connection never reached anything, and a 503 from
apache's proxy means the backend was not reached - so for the FWO API both are safe to repeat even
for mutations.

The middleware itself can also answer 503 for some of its endpoints, after it has processed the
request. Only idempotent middleware endpoints may therefore be called through this retry. Today the
importer only calls reads and the token endpoints there, where a repeat is harmless.

Anything that may have reached the backend is deliberately not retried: a read timeout, a connection
dropped after the request was sent, a 502 from a proxy timeout. Repeating those could apply a
mutation twice.

Retries stop as soon as a shutdown is requested, except during a deferred rollback - see
send_with_retry.
"""

from __future__ import annotations

import time
from typing import TYPE_CHECKING

import fwo_globals
import requests
from fwo_const import HTTP_STATUS_SERVICE_UNAVAILABLE
from fwo_log import FWOLogger
from urllib3.exceptions import ConnectTimeoutError, MaxRetryError, NewConnectionError

if TYPE_CHECKING:
    from collections.abc import Callable, Sequence


def _sleep(seconds: float) -> None:
    # module level indirection so tests can skip the waits without patching time.sleep globally
    time.sleep(seconds)


def is_unsent_request_error(error: BaseException) -> bool:
    """
    True if error proves that the request never reached the server: the connection was refused
    or could not be established in time. TLS errors are configuration problems and excluded.
    """
    if isinstance(error, requests.exceptions.SSLError):
        return False
    if isinstance(error, requests.exceptions.ConnectTimeout):
        return True
    if not isinstance(error, requests.exceptions.ConnectionError):
        return False
    reason = error.args[0] if error.args else None
    if isinstance(reason, MaxRetryError):
        reason = reason.reason
    return isinstance(reason, (NewConnectionError, ConnectTimeoutError))


def is_service_unreachable(error: BaseException) -> bool:
    """
    True if error means the service could not be talked to at all (refused, connect timeout, or
    the connection dropped), as opposed to a TLS configuration error or a slow response.
    """
    return isinstance(error, requests.exceptions.ConnectionError) and not isinstance(
        error, requests.exceptions.SSLError
    )


def send_with_retry(
    send: Callable[[], requests.Response],
    target: str,
    delays: Sequence[float],
) -> requests.Response:
    """
    Calls send() and repeats it after each of delays while it fails with an unsent-request error
    or answers 503. The outcome of the final attempt - response or exception - is returned as is.

    Once a shutdown is requested, no further attempt is made: the outcome of the attempt just made
    is returned (or raised) at once, so retries never delay a stop. During a deferred rollback
    (fwo_globals.shutdown_deferral_active) retries go on, so the rollback can still finish.
    """
    for attempt, delay in enumerate(delays, start=1):
        try:
            response = send()
        except requests.exceptions.ConnectionError as error:
            if not is_unsent_request_error(error) or _stop_requested():
                raise
            FWOLogger.warning(f"{target} unreachable ({error!s}) - retry {attempt}/{len(delays)} in {delay}s")
        else:
            if response.status_code != HTTP_STATUS_SERVICE_UNAVAILABLE or _stop_requested():
                return response
            FWOLogger.warning(f"{target} answered 503 - retry {attempt}/{len(delays)} in {delay}s")
        _sleep(delay)
    return send()


def _stop_requested() -> bool:
    """True if a shutdown is pending and no rollback has deferred it."""
    return fwo_globals.shutdown_requested and not fwo_globals.shutdown_deferral_active
