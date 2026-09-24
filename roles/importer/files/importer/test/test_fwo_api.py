# pyright: reportPrivateUsage=false
from __future__ import annotations

import json
from typing import TYPE_CHECKING, Any, cast

import fwo_api as fwo_api_module
import pytest
import requests
from fwo_api import REDACTED_VALUE, FwoApi
from fwo_config import TlsIdentity
from fwo_const import API_CALL_CHUNK_SIZE
from fwo_exceptions import (
    FwoApiLoginFailedError,
    FwoApiServiceUnavailableError,
    FwoApiTimeoutError,
    FwoImporterError,
)
from services.enums import Lifetime, Services

if TYPE_CHECKING:
    from pathlib import Path

    from pytest_mock import MockerFixture
    from services.service_provider import ServiceProvider

API_URL = "https://fworch.example/api/v1/graphql"
MIDDLEWARE_URL = "https://fworch.example/middleware/"
TEST_JWT = "jwt-secret"
SIMPLE_QUERY = "query getStuff { stuff { id } }"
CHUNKED_MUTATION = "mutation insertRules($rules: [rule_insert_input!]!) { insert_rule(objects: $rules) { affected_rows returning { rule_id } } }"
HTTP_UNAUTHORIZED = 401
HTTP_BAD_GATEWAY = 502
HTTP_SERVICE_UNAVAILABLE = 503
HTTP_INTERNAL_ERROR = 500
HTTP_OK = 200


class _FakeResponse:
    def __init__(self, status_code: int = HTTP_OK, payload: Any = None, text: str = "") -> None:
        self.status_code = status_code
        self._payload = payload
        self.text = text

    def json(self) -> Any:
        if self._payload is None:
            raise ValueError("no json")
        return self._payload

    def raise_for_status(self) -> None:
        if self.status_code >= HTTP_INTERNAL_ERROR or self.status_code == HTTP_UNAUTHORIZED:
            error = requests.exceptions.HTTPError(f"http {self.status_code}")
            error.response = cast("requests.Response", self)
            raise error


# subclasses the real session so the context manager protocol and the headers attribute keep working
class _FakeSession(requests.Session):
    def __init__(self, responses: list[_FakeResponse] | None = None, exception: Exception | None = None) -> None:
        super().__init__()
        self._responses = responses or []
        self._exception = exception
        self.requests: list[tuple[str, str, Any]] = []

    def _respond(self, method: str, url: str | bytes, **kwargs: Any) -> requests.Response:
        self.requests.append((method, str(url), kwargs.get("data", kwargs.get("json"))))
        if self._exception is not None:
            raise self._exception
        return cast("requests.Response", self._responses.pop(0))

    def post(self, url: str | bytes, *_args: Any, **kwargs: Any) -> requests.Response:
        return self._respond("POST", url, **kwargs)

    def get(self, url: str | bytes, *_args: Any, **kwargs: Any) -> requests.Response:
        return self._respond("GET", url, **kwargs)

    def put(self, url: str | bytes, *_args: Any, **kwargs: Any) -> requests.Response:
        return self._respond("PUT", url, **kwargs)

    def delete(self, url: str | bytes, *_args: Any, **kwargs: Any) -> requests.Response:
        return self._respond("DELETE", url, **kwargs)

    def patch(self, url: str | bytes, *_args: Any, **kwargs: Any) -> requests.Response:
        return self._respond("PATCH", url, **kwargs)


def _read_test_tls_identity(_config_file: str) -> TlsIdentity:
    return TlsIdentity("/tmp/client.crt", "/tmp/client.key", "/tmp/ca.crt")  # noqa: S108


def _patch_session(monkeypatch: pytest.MonkeyPatch, session: _FakeSession) -> None:
    monkeypatch.setattr(requests, "Session", lambda: session)
    monkeypatch.setattr(fwo_api_module, "read_tls_identity", _read_test_tls_identity)


def _register_fwo_config(service_provider: ServiceProvider) -> None:
    service_provider.register(
        Services.FWO_CONFIG, lambda: {"user_management_api_base_url": MIDDLEWARE_URL}, Lifetime.SINGLETON
    )


class TestFwoApiCall:
    def test_call_posts_query_with_importer_headers_and_client_identity(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(payload={"data": {"stuff": []}})])
        _patch_session(monkeypatch, session)

        result = FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY, {"id": 1})

        assert result == {"data": {"stuff": []}}
        method, url, data = session.requests[0]
        assert (method, url) == ("POST", API_URL)
        assert json.loads(data) == {"query": SIMPLE_QUERY, "variables": {"id": 1}}
        assert session.headers["Authorization"] == f"Bearer {TEST_JWT}"
        assert session.headers["x-hasura-role"] == "importer"
        assert session.cert == ("/tmp/client.crt", "/tmp/client.key")  # noqa: S108
        assert session.verify == "/tmp/ca.crt"  # noqa: S108

    def test_call_without_variables_sends_empty_variables(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(payload={"data": {}})])
        _patch_session(monkeypatch, session)

        FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)

        assert json.loads(session.requests[0][2])["variables"] == {}

    def test_call_analyzes_small_payload_without_chunking(self, monkeypatch: pytest.MonkeyPatch) -> None:
        session = _FakeSession([_FakeResponse(payload={"data": {"insert_rule": {"affected_rows": 1}}})])
        _patch_session(monkeypatch, session)
        api = FwoApi(API_URL, TEST_JWT)

        result = api.call(CHUNKED_MUTATION, {"rules": [{"uid": "r1"}]}, analyze_payload=True)

        assert result["data"]["insert_rule"]["affected_rows"] == 1
        assert len(session.requests) == 1
        assert api.query_info["query_name"] == "insertRules"

    def test_call_chunks_large_list_variables_and_merges_results(self, monkeypatch: pytest.MonkeyPatch) -> None:
        total_rules = API_CALL_CHUNK_SIZE + 1
        first_chunk = {"data": {"insert_rule": {"affected_rows": API_CALL_CHUNK_SIZE, "returning": [{"rule_id": 1}]}}}
        second_chunk = {"data": {"insert_rule": {"affected_rows": 1, "returning": [{"rule_id": 2}]}}}
        session = _FakeSession([_FakeResponse(payload=first_chunk), _FakeResponse(payload=second_chunk)])
        _patch_session(monkeypatch, session)
        api = FwoApi(API_URL, TEST_JWT)
        rules = [{"uid": f"r{index}"} for index in range(total_rules)]

        result = api.call(CHUNKED_MUTATION, {"rules": rules}, analyze_payload=True)

        assert len(session.requests) == 2
        assert len(json.loads(session.requests[0][2])["variables"]["rules"]) == API_CALL_CHUNK_SIZE
        assert len(json.loads(session.requests[1][2])["variables"]["rules"]) == 1
        assert result["data"]["insert_rule"]["affected_rows"] == total_rules
        assert result["data"]["insert_rule"]["returning"] == [{"rule_id": 1}, {"rule_id": 2}]
        assert api.query_info == {}

    def test_call_maps_http_503_to_service_unavailable(self, monkeypatch: pytest.MonkeyPatch) -> None:
        _patch_session(monkeypatch, _FakeSession([_FakeResponse(status_code=HTTP_SERVICE_UNAVAILABLE)]))

        with pytest.raises(FwoApiServiceUnavailableError):
            FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)

    def test_call_maps_http_502_to_timeout(self, monkeypatch: pytest.MonkeyPatch) -> None:
        _patch_session(monkeypatch, _FakeSession([_FakeResponse(status_code=HTTP_BAD_GATEWAY)]))

        with pytest.raises(FwoApiTimeoutError):
            FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)

    def test_call_reraises_other_http_errors(self, monkeypatch: pytest.MonkeyPatch) -> None:
        _patch_session(monkeypatch, _FakeSession([_FakeResponse(status_code=HTTP_INTERNAL_ERROR)]))

        with pytest.raises(requests.exceptions.HTTPError):
            FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)

    def test_call_reraises_connection_errors_without_response(self, monkeypatch: pytest.MonkeyPatch) -> None:
        _patch_session(monkeypatch, _FakeSession(exception=requests.exceptions.ConnectionError("refused")))

        with pytest.raises(requests.exceptions.ConnectionError):
            FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)

    def test_call_reraises_importer_errors_unchanged(self, monkeypatch: pytest.MonkeyPatch) -> None:
        _patch_session(monkeypatch, _FakeSession(exception=FwoImporterError("importer broke")))

        with pytest.raises(FwoImporterError, match="importer broke"):
            FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)

    def test_call_wraps_unexpected_errors(self, monkeypatch: pytest.MonkeyPatch) -> None:
        _patch_session(monkeypatch, _FakeSession(exception=KeyError("boom")))

        with pytest.raises(FwoImporterError, match="Unexpected error during API call"):
            FwoApi(API_URL, TEST_JWT).call(SIMPLE_QUERY)


class TestFwoApiChunkedResponseHandling:
    def test_first_response_initializes_return_object(self) -> None:
        response = {"data": {"insert_rule": {"affected_rows": 3}}}

        assert FwoApi(API_URL, TEST_JWT)._handle_chunked_calls_response({}, response) is response

    def test_errors_in_later_chunk_raise(self) -> None:
        with pytest.raises(FwoImporterError, match="error while handling chunked call"):
            FwoApi(API_URL, TEST_JWT)._handle_chunked_calls_response({"data": {}}, {"errors": [{"message": "bad"}]})

    def test_later_chunk_without_data_in_return_object_is_only_logged(self, mocker: MockerFixture) -> None:
        mock_warning = mocker.patch("fwo_api.FWOLogger.warning")
        return_object: dict[str, Any] = {"errors": []}
        response: dict[str, Any] = {
            "data": {"a": {"returning": []}, "b": {"affected_rows": 0}, "c": {"affected_rows": 2}}
        }

        result = FwoApi(API_URL, TEST_JWT)._handle_chunked_calls_response(return_object, response)

        assert result is return_object
        assert mock_warning.call_count == 2

    def test_list_responses_are_summed_and_returning_data_accumulated(self) -> None:
        return_object: dict[str, Any] = {"data": {"insert_rule": None}}
        new_data = [{"affected_rows": 2, "returning": [{"id": 1}]}, {"affected_rows": 3}]

        FwoApi(API_URL, TEST_JWT)._handle_chunked_calls_response_with_return_data(
            return_object, "insert_rule", new_data
        )

        assert return_object["data"]["insert_rule"]["affected_rows"] == 5
        assert return_object["data"]["insert_rule"]["returning"] == [[{"id": 1}]]

    def test_dict_response_without_returning_keeps_existing_returning(self) -> None:
        return_object: dict[str, Any] = {"data": {"update_rule": {"affected_rows": 1, "returning": [{"id": 1}]}}}

        FwoApi(API_URL, TEST_JWT)._handle_chunked_calls_response_with_return_data(
            return_object, "update_rule", {"affected_rows": 4}
        )

        assert return_object["data"]["update_rule"] == {"affected_rows": 5, "returning": [{"id": 1}]}

    def test_update_query_variables_by_chunk_consumes_chunkable_lists(self) -> None:
        api = FwoApi(API_URL, TEST_JWT)
        api.query_info = {"chunking_info": {"adjusted_chunk_size": 2}}
        query_variables: dict[str, list[Any]] = {"a": [1, 2, 3], "b": [4]}
        chunkable_variables: dict[str, list[Any]] = {"a": [1, 2, 3], "b": [4]}

        processed = api._update_query_variables_by_chunk(query_variables, chunkable_variables)

        assert processed == 3
        assert query_variables == {"a": [1, 2], "b": [4]}
        assert chunkable_variables == {"a": [3], "b": []}

    def test_call_chunked_without_variables_returns_empty_result(self) -> None:
        api = FwoApi(API_URL, TEST_JWT)
        api.query_info = {"query_name": "noop", "chunking_info": {"chunkable_variables": [], "total_elements": 0}}

        assert api._call_chunked(requests.Session(), SIMPLE_QUERY) == {}


class TestFwoApiCallEndpoint:
    @pytest.mark.parametrize("method", ["GET", "post", "Put", "DELETE", "patch"])
    def test_call_endpoint_dispatches_http_method_and_returns_json(
        self, method: str, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        session = _FakeSession([_FakeResponse(payload={"ok": True})])
        _patch_session(monkeypatch, session)

        result = FwoApi(API_URL, TEST_JWT).call_endpoint(method, "/User", {"id": 1})

        assert result == {"ok": True}
        assert session.requests[0] == (method.upper(), MIDDLEWARE_URL + "User", {"id": 1})
        assert session.headers["Authorization"] == f"Bearer {TEST_JWT}"

    def test_call_endpoint_returns_text_for_non_json_response(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        _patch_session(monkeypatch, _FakeSession([_FakeResponse(text="plain")]))

        assert FwoApi(API_URL, TEST_JWT).call_endpoint("GET", "Status") == "plain"

    def test_call_endpoint_rejects_unsupported_method(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        _patch_session(monkeypatch, _FakeSession())

        with pytest.raises(FwoImporterError, match="Unsupported HTTP method: HEAD"):
            FwoApi(API_URL, TEST_JWT).call_endpoint("HEAD", "User")

    @pytest.mark.parametrize(
        ("status_code", "expected_error"),
        [
            (HTTP_UNAUTHORIZED, FwoApiLoginFailedError),
            (HTTP_SERVICE_UNAVAILABLE, FwoApiServiceUnavailableError),
            (HTTP_BAD_GATEWAY, FwoApiTimeoutError),
        ],
    )
    def test_call_endpoint_maps_http_status_codes(
        self,
        status_code: int,
        expected_error: type[Exception],
        monkeypatch: pytest.MonkeyPatch,
        service_provider: ServiceProvider,
    ) -> None:
        _register_fwo_config(service_provider)
        _patch_session(monkeypatch, _FakeSession([_FakeResponse(status_code=status_code)]))

        with pytest.raises(expected_error):
            FwoApi(API_URL, TEST_JWT).call_endpoint("GET", "User")

    def test_call_endpoint_wraps_request_exceptions(
        self, monkeypatch: pytest.MonkeyPatch, service_provider: ServiceProvider
    ) -> None:
        _register_fwo_config(service_provider)
        _patch_session(monkeypatch, _FakeSession([_FakeResponse(status_code=HTTP_INTERNAL_ERROR)]))

        with pytest.raises(FwoImporterError, match="Middleware API request failed"):
            FwoApi(API_URL, TEST_JWT).call_endpoint("GET", "User")


class TestFwoApiLogHelpers:
    def test_show_api_call_info_truncates_large_payloads(self) -> None:
        payload = {"query": "q" * 2000, "variables": {"secret": "value"}}

        message = FwoApi(API_URL, TEST_JWT).show_api_call_info(API_URL, payload, {}, typ="error")

        assert message.startswith("error while sending api_call to url ")
        assert "[snip]" in message
        assert "total query size=" in message

    def test_show_import_api_call_info_truncates_and_appends_query_info(self) -> None:
        api = FwoApi(API_URL, TEST_JWT)
        api.query_info = {"query_name": "bigQuery"}
        payload = {"query": "q" * 2000, "variables": {"secret": "value"}}

        message = api.show_import_api_call_info(API_URL, payload, {}, typ="error", show_query_info=True)

        assert message.startswith("error while sending api_call to url ")
        assert "[snip]" in message
        assert "Query Info:" in message
        assert "bigQuery" in message
        assert "value" not in message
        assert REDACTED_VALUE in message

    def test_summarize_query_variables_without_variables(self) -> None:
        assert FwoApi.summarize_query_variables({}) == "none"


class TestFwoApiGraphqlFiles:
    def test_get_graphql_code_joins_files_and_strips_line_breaks(self, tmp_path: Path) -> None:
        first_file = tmp_path / "first.graphql"
        second_file = tmp_path / "second.graphql"
        first_file.write_text("query a {\n  a\n}\n", encoding="utf-8")
        second_file.write_text("fragment b on B {\r\n b }ä", encoding="utf-8")

        code = FwoApi.get_graphql_code([str(first_file), str(second_file)])

        assert "\n" not in code
        assert "\r" not in code
        assert "ä" not in code
        assert "query a" in code
        assert "fragment b on B" in code

    def test_get_graphql_code_raises_for_missing_file(self, tmp_path: Path) -> None:
        with pytest.raises(FileNotFoundError):
            FwoApi.get_graphql_code([str(tmp_path / "missing.graphql")])

    def test_read_clean_text_from_file_removes_non_printable_chars(self, tmp_path: Path) -> None:
        text_file = tmp_path / "text.txt"
        text_file.write_text("abcä\u0001def", encoding="utf-8")

        assert FwoApi._read_clean_text_from_file(str(text_file)) == "abcdef"
