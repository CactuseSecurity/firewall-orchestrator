from __future__ import annotations

from typing import Any, cast

import pytest
import requests
from fwo_api import MAX_LOGIN_ERROR_RESPONSE_LEN, FwoApi
from fwo_exceptions import FwoApiLoginFailedError

BASE_URL = "http://localhost:8880/"
TOKEN_ENDPOINT = BASE_URL + "api/AuthenticationToken/GetTokenPair"
TEST_USER = "importer"
TEST_PASSWORD = "secret"  # noqa: S105


class _FakeResponse:
    def __init__(self, status_code: int, text: str) -> None:
        self.status_code = status_code
        self.text = text


# subclasses the real session so the context manager protocol used by FwoApi.login keeps working
class _FakeSession(requests.Session):
    def __init__(self, response: _FakeResponse | None = None, exception: Exception | None = None) -> None:
        super().__init__()
        self._response = response
        self._exception = exception
        self.posted_url: str | None = None

    def post(self, url: str | bytes, *_args: Any, **_kwargs: Any) -> requests.Response:
        self.posted_url = str(url)
        if self._exception is not None:
            raise self._exception
        return cast("requests.Response", self._response)


def _patch_session(monkeypatch: pytest.MonkeyPatch, session: _FakeSession) -> None:
    monkeypatch.setattr(requests, "Session", lambda: session)


def test_login_returns_response_text_on_success(monkeypatch: pytest.MonkeyPatch) -> None:
    session = _FakeSession(_FakeResponse(200, '{"AccessToken": "jwt"}'))
    _patch_session(monkeypatch, session)

    result = FwoApi.login(TEST_USER, TEST_PASSWORD, BASE_URL)

    assert result == '{"AccessToken": "jwt"}'
    assert session.posted_url == TOKEN_ENDPOINT


def test_login_error_reports_status_and_response_body(monkeypatch: pytest.MonkeyPatch) -> None:
    _patch_session(monkeypatch, _FakeSession(_FakeResponse(400, "A0002 Invalid credentials")))

    with pytest.raises(FwoApiLoginFailedError) as excinfo:
        FwoApi.login(TEST_USER, TEST_PASSWORD, BASE_URL)

    message = excinfo.value.message
    assert "http_status: 400" in message
    assert "A0002 Invalid credentials" in message
    assert TOKEN_ENDPOINT in message
    assert TEST_USER in message
    # the password must never end up in a log line
    assert TEST_PASSWORD not in message


def test_login_error_truncates_long_response_bodies(monkeypatch: pytest.MonkeyPatch) -> None:
    long_body = "x" * (MAX_LOGIN_ERROR_RESPONSE_LEN * 3)
    _patch_session(monkeypatch, _FakeSession(_FakeResponse(500, long_body)))

    with pytest.raises(FwoApiLoginFailedError) as excinfo:
        FwoApi.login(TEST_USER, TEST_PASSWORD, BASE_URL)

    assert "x" * MAX_LOGIN_ERROR_RESPONSE_LEN in excinfo.value.message
    assert "x" * (MAX_LOGIN_ERROR_RESPONSE_LEN + 1) not in excinfo.value.message


def test_login_reports_connection_errors_separately(monkeypatch: pytest.MonkeyPatch) -> None:
    _patch_session(monkeypatch, _FakeSession(exception=requests.exceptions.ConnectionError("refused")))

    with pytest.raises(FwoApiLoginFailedError) as excinfo:
        FwoApi.login(TEST_USER, TEST_PASSWORD, BASE_URL)

    # a dead endpoint must stay distinguishable from a rejected login
    assert "error during login to url" in excinfo.value.message
    assert "http_status" not in excinfo.value.message


def test_login_without_base_url_fails_fast() -> None:
    with pytest.raises(FwoApiLoginFailedError) as excinfo:
        FwoApi.login(TEST_USER, TEST_PASSWORD, None)

    assert "user_management_api_base_url is None" in excinfo.value.message
