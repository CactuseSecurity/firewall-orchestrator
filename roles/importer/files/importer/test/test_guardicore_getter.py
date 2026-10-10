import unittest.mock
from typing import Any

import pytest
import requests
from fw_modules.guardicoremanagementREST import gc_const, gc_getter
from fwo_exceptions import FwApiCallFailedError, FwLoginFailedError
from pytest_mock import MockerFixture


def build_response(payload: object, status_code: int = 200) -> unittest.mock.MagicMock:
    response = unittest.mock.MagicMock()
    response.json.return_value = payload
    response.status_code = status_code
    if status_code >= 400:
        response.raise_for_status.side_effect = requests.exceptions.HTTPError(f"status {status_code}")
    return response


class TestLogin:
    def test_returns_access_token(self) -> None:
        session = unittest.mock.MagicMock()
        session.post.return_value = build_response({"access_token": "tok", "username": "u"})
        assert gc_getter.login(session, "https://gc", "u", "p") == "tok"
        assert session.post.call_args.args[0] == "https://gc" + gc_const.GC_AUTH_ENDPOINT

    def test_rejected_login_raises(self) -> None:
        session = unittest.mock.MagicMock()
        session.post.return_value = build_response({}, status_code=403)
        with pytest.raises(FwLoginFailedError):
            gc_getter.login(session, "https://gc", "u", "p")

    def test_missing_token_raises(self) -> None:
        session = unittest.mock.MagicMock()
        session.post.return_value = build_response({"id": "x"})
        with pytest.raises(FwLoginFailedError):
            gc_getter.login(session, "https://gc", "u", "p")


class TestPaging:
    def test_build_api_base_url(self) -> None:
        assert gc_getter.build_api_base_url("gc.example.com", 8443) == "https://gc.example.com:8443"
        assert gc_getter.build_api_base_url("gc.example.com", None) == "https://gc.example.com"

    def test_reads_all_pages(self, mocker: MockerFixture) -> None:
        mocker.patch.object(gc_const, "GC_PAGE_SIZE", 2)
        session = unittest.mock.MagicMock()
        session.get.side_effect = [
            build_response({"objects": [{"id": 1}, {"id": 2}], "total_count": 3}),
            build_response({"objects": [{"id": 3}, "noise"], "total_count": 3}),
        ]
        objects = gc_getter.get_all_objects(session, "https://gc/x", "start_at", "max_results", {"expand": "labels"})
        assert [obj["id"] for obj in objects] == [1, 2, 3]
        second_params: dict[str, Any] = session.get.call_args_list[1].kwargs["params"]
        assert second_params == {"start_at": 2, "max_results": 2, "expand": "labels"}

    def test_stops_on_short_page_without_total(self) -> None:
        session = unittest.mock.MagicMock()
        session.get.return_value = build_response({"objects": [{"id": 1}]})
        assert gc_getter.get_all_objects(session, "https://gc/x") == [{"id": 1}]

    def test_endless_paging_is_stopped(self, mocker: MockerFixture) -> None:
        mocker.patch.object(gc_const, "GC_PAGE_SIZE", 1)
        mocker.patch.object(gc_const, "GC_MAX_PAGES", 3)
        session = unittest.mock.MagicMock()
        session.get.return_value = build_response({"objects": [{"id": 1}]})
        with pytest.raises(FwApiCallFailedError):
            gc_getter.get_all_objects(session, "https://gc/x")

    def test_invalid_page_raises(self) -> None:
        session = unittest.mock.MagicMock()
        session.get.return_value = build_response({"error": "x"})
        with pytest.raises(FwApiCallFailedError):
            gc_getter.get_page(session, "https://gc/x", {})
        session.get.return_value = build_response({}, status_code=500)
        with pytest.raises(FwApiCallFailedError):
            gc_getter.get_page(session, "https://gc/x", {})

    def test_get_total(self) -> None:
        assert gc_getter.get_total({"total": 4}) == 4
        assert gc_getter.get_total({"total": "4"}) is None


class TestGetNativeConfig:
    def test_fetches_all_object_types(self, mocker: MockerFixture) -> None:
        session = unittest.mock.MagicMock()
        session.__enter__.return_value = session
        session.headers = {}
        mocker.patch.object(gc_getter.requests, "Session", return_value=session)
        mocker.patch.object(gc_getter, "login", return_value="tok")
        get_all = mocker.patch.object(gc_getter, "get_all_objects", side_effect=[[{"id": "l"}], [], [{"id": "a"}], []])

        native_config = gc_getter.get_native_config("https://gc", "u", "p")

        assert native_config == {"labels": [{"id": "l"}], "label_groups": [], "assets": [{"id": "a"}], "rules": []}
        assert session.headers["Authorization"] == "Bearer tok"
        assert get_all.call_args_list[2].kwargs == {
            "offset_param": "start_at",
            "limit_param": "max_results",
            "extra_params": {"expand": "labels"},
        }
