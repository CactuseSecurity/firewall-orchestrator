# pyright: reportPrivateUsage=false
from __future__ import annotations

import base64
import json
import time
from typing import TYPE_CHECKING, Any, ClassVar, cast

import pytest
import requests
from fwo_api import JWT_REFRESH_MARGIN_SECONDS, FwoApi
from fwo_exceptions import FwoApiLoginFailedError, FwoImporterError
from services.enums import Lifetime, Services

if TYPE_CHECKING:
    from services.service_provider import ServiceProvider

BASE_URL = "http://localhost:8880/"
REFRESH_ENDPOINT = BASE_URL + "api/AuthenticationToken/Refresh"


def _make_jwt(exp: float | None) -> str:
    """Builds a syntactically valid (but unsigned) JWT carrying the given 'exp' claim."""
    payload: dict[str, Any] = {} if exp is None else {"exp": exp}
    payload_b64 = base64.urlsafe_b64encode(json.dumps(payload).encode()).rstrip(b"=").decode()
    return f"header.{payload_b64}.signature"


class _FakeResponse:
    def __init__(self, status_code: int = 200, json_data: Any = None, text: str = "") -> None:
        self.status_code = status_code
        self._json_data = json_data
        self.text = text

    def json(self) -> Any:
        if self._json_data is None:
            raise ValueError("response has no JSON body")
        return self._json_data

    def raise_for_status(self) -> None:
        if self.status_code >= 400:
            raise requests.exceptions.HTTPError(f"{self.status_code} error", response=cast("requests.Response", self))


class _FakeSession(requests.Session):
    """
    Fake session subclassing the real one so the `with ... as session:` protocol keeps working.
    Every call to the given verb returns the next response from a queue, in order.
    """

    def __init__(self, responses: list[_FakeResponse]) -> None:
        super().__init__()
        self._responses = list(responses)
        self.calls = 0
        self.posted_urls: list[str] = []

    def _next_response(self, url: str | bytes | None = None) -> _FakeResponse:
        self.calls += 1
        if url is not None:
            self.posted_urls.append(str(url))
        return self._responses.pop(0)

    def post(self, url: str | bytes | None = None, *_args: Any, **_kwargs: Any) -> requests.Response:
        return cast("requests.Response", self._next_response(url))

    def get(self, url: str | bytes | None = None, *_args: Any, **_kwargs: Any) -> requests.Response:
        return cast("requests.Response", self._next_response(url))


def _patch_session(monkeypatch: pytest.MonkeyPatch, session: _FakeSession) -> None:
    monkeypatch.setattr(requests, "Session", lambda: session)


def _register_fwo_config(service_provider: ServiceProvider, base_url: str = BASE_URL) -> None:
    service_provider.register(
        Services.FWO_CONFIG,
        lambda: {"user_management_api_base_url": base_url},
        Lifetime.SINGLETON,
    )


class TestGetJwtExpiryEpoch:
    def test_reads_exp_claim(self) -> None:
        assert FwoApi._get_jwt_expiry_epoch(_make_jwt(exp=1234567890)) == 1234567890

    def test_returns_none_when_exp_claim_missing(self) -> None:
        assert FwoApi._get_jwt_expiry_epoch(_make_jwt(exp=None)) is None

    def test_returns_none_for_malformed_token(self) -> None:
        assert FwoApi._get_jwt_expiry_epoch("not-a-jwt") is None


class TestContainsJwtExpiredError:
    def test_detects_expired_jwt_in_dict_shaped_errors(self) -> None:
        body = {"errors": [{"message": "Could not verify JWT: JWTExpired"}]}
        assert FwoApi._contains_jwt_expired_error(body) is True

    def test_detects_expired_jwt_in_bare_list_shaped_errors(self) -> None:
        body = [{"message": "Could not verify JWT: JWTExpired"}]
        assert FwoApi._contains_jwt_expired_error(body) is True

    def test_ignores_unrelated_graphql_errors(self) -> None:
        body = {"errors": [{"message": "field 'foo' not found in type"}]}
        assert FwoApi._contains_jwt_expired_error(body) is False

    def test_returns_false_when_errors_key_missing_or_empty(self) -> None:
        assert FwoApi._contains_jwt_expired_error({}) is False
        assert FwoApi._contains_jwt_expired_error({"errors": []}) is False

    def test_ignores_non_dict_error_entries(self) -> None:
        assert FwoApi._contains_jwt_expired_error({"errors": ["oops"]}) is False

    def test_returns_false_for_none_body(self) -> None:
        assert FwoApi._contains_jwt_expired_error(None) is False


class TestRefreshJwt:
    def test_updates_jwt_and_rotated_refresh_token_on_success(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession(
            [_FakeResponse(200, text=json.dumps({"AccessToken": "new-jwt", "RefreshToken": "new-refresh"}))]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "old-jwt", "old-refresh")

        new_jwt = api.refresh_jwt("old-refresh", BASE_URL)

        assert new_jwt == "new-jwt"
        assert api.fwo_jwt == "new-jwt"
        assert api.fwo_refresh_token == "new-refresh"  # noqa: S105
        assert session.posted_urls == [REFRESH_ENDPOINT]

    def test_keeps_existing_refresh_token_when_response_does_not_rotate_it(
        self, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        session = _FakeSession([_FakeResponse(200, text=json.dumps({"AccessToken": "new-jwt"}))])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "old-jwt", "old-refresh")

        api.refresh_jwt("old-refresh", BASE_URL)

        assert api.fwo_jwt == "new-jwt"
        assert api.fwo_refresh_token == "old-refresh"  # noqa: S105

    def test_leaves_jwt_untouched_when_refresh_call_fails(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(400, text="invalid or expired refresh token")])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "old-jwt", "old-refresh")

        with pytest.raises(FwoApiLoginFailedError) as excinfo:
            api.refresh_jwt("old-refresh", BASE_URL)

        assert "http_status: 400" in excinfo.value.message
        assert api.fwo_jwt == "old-jwt"


class TestTryRefreshJwt:
    def test_returns_false_without_a_refresh_token(self) -> None:
        api = FwoApi(BASE_URL, "jwt", None)
        assert api._try_refresh_jwt() is False

    def test_returns_true_and_updates_jwt_on_success(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        session = _FakeSession(
            [_FakeResponse(200, text=json.dumps({"AccessToken": "new-jwt", "RefreshToken": "new-refresh"}))]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "old-jwt", "old-refresh")

        assert api._try_refresh_jwt() is True
        assert api.fwo_jwt == "new-jwt"

    def test_returns_false_and_leaves_jwt_untouched_when_refresh_call_fails(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        session = _FakeSession([_FakeResponse(400, text="expired refresh token")])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "old-jwt", "old-refresh")

        assert api._try_refresh_jwt() is False
        assert api.fwo_jwt == "old-jwt"


class TestEnsureJwtFresh:
    def test_does_nothing_without_a_refresh_token(self, monkeypatch: pytest.MonkeyPatch) -> None:
        api = FwoApi(BASE_URL, _make_jwt(exp=1), None)
        refresh_calls: list[bool] = []
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: refresh_calls.append(True))

        api._ensure_jwt_fresh()

        assert refresh_calls == []

    def test_does_nothing_when_jwt_is_far_from_expiry(self, monkeypatch: pytest.MonkeyPatch) -> None:
        api = FwoApi(BASE_URL, _make_jwt(exp=time.time() + 10_000), "refresh-token")
        refresh_calls: list[bool] = []
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: refresh_calls.append(True))

        api._ensure_jwt_fresh()

        assert refresh_calls == []

    def test_does_nothing_when_expiry_cannot_be_determined(self, monkeypatch: pytest.MonkeyPatch) -> None:
        api = FwoApi(BASE_URL, "not-a-jwt", "refresh-token")
        refresh_calls: list[bool] = []
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: refresh_calls.append(True))

        api._ensure_jwt_fresh()

        assert refresh_calls == []

    def test_refreshes_when_jwt_is_within_the_margin(self, monkeypatch: pytest.MonkeyPatch) -> None:
        soon = time.time() + (JWT_REFRESH_MARGIN_SECONDS / 2)
        api = FwoApi(BASE_URL, _make_jwt(exp=soon), "refresh-token")
        refresh_calls: list[bool] = []
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: refresh_calls.append(True) or True)

        api._ensure_jwt_fresh()

        assert refresh_calls == [True]


class TestCallRetriesOnJwtExpiry:
    """Covers call()'s reactive retry when Hasura reports an expired JWT as a GraphQL-level error."""

    _EXPIRED_BODY: ClassVar[dict[str, Any]] = {"errors": [{"message": "Could not verify JWT: JWTExpired"}]}

    def test_retries_once_and_returns_the_retried_result(self, monkeypatch: pytest.MonkeyPatch) -> None:
        success_body = {"data": {"ok": True}}
        session = _FakeSession(
            [_FakeResponse(200, json_data=self._EXPIRED_BODY), _FakeResponse(200, json_data=success_body)]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: True)

        result = api.call("query { ok }")

        assert result == success_body
        assert session.calls == 2

    def test_raises_when_the_jwt_refresh_itself_fails(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(200, json_data=self._EXPIRED_BODY)])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: False)

        with pytest.raises(FwoImporterError, match="JWT expired and could not be refreshed"):
            api.call("query { ok }")

        assert session.calls == 1

    def test_gives_up_after_a_single_retry_if_still_expired(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession(
            [_FakeResponse(200, json_data=self._EXPIRED_BODY), _FakeResponse(200, json_data=self._EXPIRED_BODY)]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        refresh_calls: list[bool] = []
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: refresh_calls.append(True) or True)

        with pytest.raises(FwoImporterError):
            api.call("query { ok }")

        # exactly one refresh + retry attempt, no unbounded retry loop
        assert session.calls == 2
        assert refresh_calls == [True]


class TestCallEndpointRetriesOnJwtExpiry:
    """Covers call_endpoint()'s reactive retry when the middleware answers with a plain HTTP 401."""

    def test_retries_once_and_returns_the_retried_result(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        session = _FakeSession([_FakeResponse(401), _FakeResponse(200, json_data={"ok": True})])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: True)

        result = api.call_endpoint("GET", "SomeEndpoint")

        assert result == {"ok": True}
        assert session.calls == 2

    def test_raises_login_failed_when_the_jwt_refresh_itself_fails(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        session = _FakeSession([_FakeResponse(401)])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: False)

        with pytest.raises(FwoApiLoginFailedError, match="Authentication failed for endpoint"):
            api.call_endpoint("GET", "SomeEndpoint")

        assert session.calls == 1
