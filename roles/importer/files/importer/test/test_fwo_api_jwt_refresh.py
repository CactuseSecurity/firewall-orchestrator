# pyright: reportPrivateUsage=false
from __future__ import annotations

import base64
import json
import time
from typing import TYPE_CHECKING, Any, ClassVar, cast

import pytest
import requests
from fwo_api import JWT_REFRESH_MARGIN_SECONDS, FwoApi, _JwtExpiredResponseError
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


_NO_JSON_BODY = object()
"""Marks a response whose body cannot be parsed as JSON - distinct from a body that is JSON 'null'."""


class _FakeResponse:
    def __init__(self, status_code: int = 200, json_data: Any = _NO_JSON_BODY, text: str = "") -> None:
        self.status_code = status_code
        self._json_data = json_data
        self.text = text

    def json(self) -> Any:
        if self._json_data is _NO_JSON_BODY:
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
        self.sent_headers: list[dict[str, str]] = []

    def _next_response(self, url: str | bytes | None = None) -> _FakeResponse:
        self.calls += 1
        if url is not None:
            self.posted_urls.append(str(url))
        # snapshot, not a reference: callers may re-stamp the session's headers between requests
        self.sent_headers.append(dict(self.headers))
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


class TestPostQueryUnusableBody:
    """Covers _post_query()'s branches for a successful response whose body is not a JSON object."""

    def test_reraises_value_error_for_a_non_json_response_body(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(200, text="not json")])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")

        with pytest.raises(ValueError, match="response has no JSON body"):
            api._post_query(session, {"query": "query { ok }"})

        assert session.calls == 1

    @pytest.mark.parametrize(
        ("json_data", "expected_type_name"),
        [(None, "NoneType"), ([{"message": "boom"}], "list"), ("nope", "str")],
        ids=["json_null", "bare_list", "bare_string"],
    )
    def test_raises_for_a_json_body_that_is_not_an_object(
        self, monkeypatch: pytest.MonkeyPatch, json_data: Any, expected_type_name: str
    ) -> None:
        """A body that parses but is not a dict must not be returned - _post_query promises a dict."""
        session = _FakeSession([_FakeResponse(200, json_data=json_data)])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")

        with pytest.raises(
            FwoImporterError, match=f"expected a JSON object as API response body, got {expected_type_name}"
        ):
            api._post_query(session, {"query": "query { ok }"})

        assert session.calls == 1

    def test_still_detects_an_expired_jwt_in_a_bare_list_body(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(200, json_data=[{"message": "Could not verify JWT: JWTExpired"}])])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")

        with pytest.raises(_JwtExpiredResponseError):
            api._post_query(session, {"query": "query { ok }"})


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


class TestProactiveJwtRefreshWiring:
    """
    Covers that the entry points really do ask for a proactive refresh before sending anything.

    TestEnsureJwtFresh exercises the helper in isolation; these pin the call sites, without which
    the proactive half of the JWT handling could be dropped while every other JWT test still
    passed - the reactive retry covers expiry-after-the-fact entirely on its own.
    """

    @staticmethod
    def _stub_refresh(monkeypatch: pytest.MonkeyPatch, api: FwoApi, session: _FakeSession) -> list[int]:
        """Records the session's request count at refresh time, so the ordering can be asserted."""
        refreshed_at_request: list[int] = []

        def _refresh_jwt() -> bool:
            refreshed_at_request.append(session.calls)
            api.fwo_jwt = "fresh-jwt"
            return True

        monkeypatch.setattr(api, "_try_refresh_jwt", _refresh_jwt)
        return refreshed_at_request

    def test_call_refreshes_an_almost_expired_jwt_before_sending_the_query(
        self, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        session = _FakeSession([_FakeResponse(200, json_data={"data": {"ok": True}})])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, _make_jwt(exp=time.time() + (JWT_REFRESH_MARGIN_SECONDS / 2)), "refresh-token")
        refreshed_at_request = self._stub_refresh(monkeypatch, api, session)

        api.call("query { ok }")

        # refreshed before the query went out - not reactively, after it came back rejected
        assert refreshed_at_request == [0]
        assert session.calls == 1
        assert session.sent_headers[0]["Authorization"] == "Bearer fresh-jwt"

    def test_call_leaves_a_jwt_far_from_expiry_alone(self, monkeypatch: pytest.MonkeyPatch) -> None:
        jwt = _make_jwt(exp=time.time() + 10_000)
        session = _FakeSession([_FakeResponse(200, json_data={"data": {"ok": True}})])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, jwt, "refresh-token")
        refreshed_at_request = self._stub_refresh(monkeypatch, api, session)

        api.call("query { ok }")

        # the call site must go through _ensure_jwt_fresh(), not refresh on every single call
        assert refreshed_at_request == []
        assert session.sent_headers[0]["Authorization"] == f"Bearer {jwt}"

    def test_call_endpoint_refreshes_an_almost_expired_jwt_before_sending_the_request(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        session = _FakeSession([_FakeResponse(200, json_data={"ok": True})])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, _make_jwt(exp=time.time() + (JWT_REFRESH_MARGIN_SECONDS / 2)), "refresh-token")
        refreshed_at_request = self._stub_refresh(monkeypatch, api, session)

        api.call_endpoint("GET", "SomeEndpoint")

        # refreshed before the request went out - not reactively, after it came back as a 401
        assert refreshed_at_request == [0]
        assert session.calls == 1
        assert session.sent_headers[0]["Authorization"] == "Bearer fresh-jwt"

    def test_call_endpoint_leaves_a_jwt_far_from_expiry_alone(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        jwt = _make_jwt(exp=time.time() + 10_000)
        session = _FakeSession([_FakeResponse(200, json_data={"ok": True})])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, jwt, "refresh-token")
        refreshed_at_request = self._stub_refresh(monkeypatch, api, session)

        api.call_endpoint("GET", "SomeEndpoint")

        # the call site must go through _ensure_jwt_fresh(), not refresh on every single call
        assert refreshed_at_request == []
        assert session.sent_headers[0]["Authorization"] == f"Bearer {jwt}"


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


class TestCallChunkedRetriesOnJwtExpiry:
    """
    Covers _call_chunked()'s reactive retry when the JWT expires mid-loop.

    Regression coverage for a bug where a JWT-expiry error raised out of _call_chunked() and
    caught by call()'s top-level retry re-entered call() with query_variables already mutated
    down to just the chunk that failed - silently dropping every remaining chunk instead of
    resuming the loop.
    """

    _EXPIRED_BODY: ClassVar[dict[str, Any]] = {"errors": [{"message": "Could not verify JWT: JWTExpired"}]}

    @staticmethod
    def _chunk_response(affected_rows: int) -> _FakeResponse:
        return _FakeResponse(
            200,
            json_data={"data": {"insert_item": {"affected_rows": affected_rows, "returning": [{"id": affected_rows}]}}},
        )

    @staticmethod
    def _query_info() -> dict[str, Any]:
        """1500 elements over a chunk size of 1000 => two chunks (1000, then 500)."""
        # built fresh per test: _call_chunked() writes back into chunking_info while it runs
        return {
            "query_name": "insertItems",
            "chunking_info": {
                "needs_chunking": True,
                "adjusted_chunk_size": 1000,
                "chunkable_variables": ["items"],
                "total_elements": 1500,
            },
        }

    def test_resumes_the_loop_and_processes_every_remaining_chunk(self, monkeypatch: pytest.MonkeyPatch) -> None:
        # The JWT expires on the very first chunk; the retry must still process the second chunk.
        session = _FakeSession(
            [
                _FakeResponse(200, json_data=self._EXPIRED_BODY),  # chunk 1, first attempt: expired
                self._chunk_response(1000),  # chunk 1, retry: succeeds
                self._chunk_response(500),  # chunk 2: succeeds
            ]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: True)
        api.query_info = self._query_info()
        query_variables = {"items": list(range(1500))}

        result = api._call_chunked(session, "mutation insertItems($items: [Int!]) { ... }", query_variables)

        # all three posts happened: failed chunk 1, retried chunk 1, chunk 2 - nothing was skipped
        assert session.calls == 3
        assert result["data"]["insert_item"]["affected_rows"] == 1500
        assert len(result["data"]["insert_item"]["returning"]) == 2

    def test_raises_when_the_jwt_refresh_itself_fails_mid_chunk(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(200, json_data=self._EXPIRED_BODY)])
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: False)
        api.query_info = self._query_info()
        query_variables = {"items": list(range(1500))}

        with pytest.raises(FwoImporterError, match="JWT expired during chunked call"):
            api._call_chunked(session, "mutation insertItems($items: [Int!]) { ... }", query_variables)

        assert session.calls == 1

    def test_raises_without_a_whole_call_retry_when_the_retried_chunk_expires_again(
        self, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        # 1500 elements over a chunk size of 1000 => two chunks. Chunk 1 succeeds; chunk 2 expires
        # on both its first attempt and its post-refresh retry. That second expiry must fail this
        # chunk outright instead of escaping as _JwtExpiredResponseError - by then query_variables
        # has been narrowed down to just chunk 2's 500 items, so letting it reach call()'s
        # whole-call retry would resend only those 500 items as if they were the entire request,
        # silently dropping chunk 1's already-processed 1000 items from the reported result.
        session = _FakeSession(
            [
                self._chunk_response(1000),  # chunk 1: succeeds
                _FakeResponse(200, json_data=self._EXPIRED_BODY),  # chunk 2, first attempt: expired
                _FakeResponse(200, json_data=self._EXPIRED_BODY),  # chunk 2, retry: expired again
            ]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "jwt-secret", "refresh-token")
        monkeypatch.setattr(api, "_try_refresh_jwt", lambda: True)
        api.query_info = self._query_info()
        query_variables = {"items": list(range(1500))}

        with pytest.raises(FwoImporterError, match="JWT expired again immediately after refresh"):
            api._call_chunked(session, "mutation insertItems($items: [Int!]) { ... }", query_variables)

        # no fourth post: the failure must not trigger call()'s whole-call retry
        assert session.calls == 3

    def test_stamps_the_refreshed_jwt_onto_the_session_before_retrying_the_chunk(
        self, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        # Unlike call() and call_endpoint(), which rebuild their headers by recursing into
        # themselves, the chunked retry reuses the session _send_query already stamped with the
        # now-stale Authorization header. Re-stamping it is therefore the only thing that puts the
        # refreshed token on the retried POST - without it the retry is rejected as expired again.
        session = _FakeSession(
            [
                _FakeResponse(200, json_data=self._EXPIRED_BODY),  # chunk 1, first attempt: expired
                self._chunk_response(1000),  # chunk 1, retry: succeeds
                self._chunk_response(500),  # chunk 2: succeeds
            ]
        )
        _patch_session(monkeypatch, session)
        api = FwoApi(BASE_URL, "stale-jwt", "refresh-token")
        # mirrors _send_query, which stamps the session before handing it to _call_chunked
        session.headers.update(api._build_request_headers())

        def _refresh_jwt() -> bool:
            api.fwo_jwt = "fresh-jwt"
            return True

        monkeypatch.setattr(api, "_try_refresh_jwt", _refresh_jwt)
        api.query_info = self._query_info()
        query_variables = {"items": list(range(1500))}

        api._call_chunked(session, "mutation insertItems($items: [Int!]) { ... }", query_variables)

        assert session.sent_headers[0]["Authorization"] == "Bearer stale-jwt"
        # the retried chunk, and every chunk after it, must go out with the refreshed token
        assert session.sent_headers[1]["Authorization"] == "Bearer fresh-jwt"
        assert session.sent_headers[2]["Authorization"] == "Bearer fresh-jwt"
