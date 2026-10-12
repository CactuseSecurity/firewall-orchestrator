from __future__ import annotations

from typing import TYPE_CHECKING, Any, cast

import fwo_api
import fwo_http_retry
import pytest
import requests
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_config import TlsIdentity
from fwo_exceptions import FwoImporterError, ShutdownRequestedError
from orphaned_imports import (
    OrphanCleanupResult,
    import_left_open,
    roll_back_orphaned_imports,
    try_roll_back_orphaned_imports,
)

if TYPE_CHECKING:
    from unittest.mock import MagicMock

    from pytest_mock import MockerFixture

THIS_HOST = "importer-a"
ORPHAN_A: dict[str, Any] = {"control_id": 11, "mgm_id": 1, "start_time": "2026-10-12T07:00:00"}
ORPHAN_B: dict[str, Any] = {"control_id": 12, "mgm_id": 2, "start_time": "2026-10-12T07:30:00"}
ROLLED_BACK: dict[str, Any] = {"data": {"delete_import_control": {"affected_rows": 1}}}
API_URL = "https://fworch.example/api/v1/graphql"
HTTP_SERVICE_UNAVAILABLE = 503


class _UnavailableResponse:
    status_code = HTTP_SERVICE_UNAVAILABLE


# a real session whose every POST is answered by apache's 503, so FwoApi starts retrying
class _UnavailableSession(requests.Session):
    def post(self, *_args: Any, **_kwargs: Any) -> requests.Response:
        return cast("requests.Response", _UnavailableResponse())


def _test_tls_identity(_config_file: str) -> TlsIdentity:
    return TlsIdentity("/tmp/client.crt", "/tmp/client.key", "/tmp/ca.crt")  # noqa: S108


def _shutdown_during_wait(_seconds: float) -> None:
    # what the SIGTERM handler does when the signal arrives while a retry waits
    raise ShutdownRequestedError


def _orphans(*rows: dict[str, Any]) -> dict[str, Any]:
    return {"data": {"import_control": list(rows)}}


def _call(api_call: FwoApiCall) -> MagicMock:
    return cast("MagicMock", api_call.call)


class TestRollBackOrphanedImports:
    def test_nothing_to_roll_back(self, api_call: FwoApiCall) -> None:
        _call(api_call).return_value = _orphans()

        assert roll_back_orphaned_imports(api_call) == OrphanCleanupResult(complete=True, rolled_back_mgm_ids=[])
        _call(api_call).assert_called_once()
        cast("MagicMock", api_call.create_data_issue).assert_not_called()

    def test_query_only_selects_config_imports_of_this_host(self, api_call: FwoApiCall, mocker: MockerFixture) -> None:
        mocker.patch("orphaned_imports.gethostname", return_value=THIS_HOST)
        _call(api_call).return_value = _orphans()

        roll_back_orphaned_imports(api_call)

        assert _call(api_call).call_args.kwargs["query_variables"] == {
            "importTypeId": 1,
            "importerHostname": THIS_HOST,
        }

    def test_every_orphan_is_rolled_back(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = [_orphans(ORPHAN_A, ORPHAN_B), ROLLED_BACK, ROLLED_BACK]

        assert roll_back_orphaned_imports(api_call) == OrphanCleanupResult(complete=True, rolled_back_mgm_ids=[1, 2])

        rolled_back = [c.kwargs["query_variables"]["importId"] for c in _call(api_call).call_args_list[1:]]
        assert rolled_back == [11, 12]

    def test_failed_rollback_is_reported_and_the_others_still_run(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = [_orphans(ORPHAN_A, ORPHAN_B), {"errors": [{"message": "x"}]}, ROLLED_BACK]

        assert roll_back_orphaned_imports(api_call) == OrphanCleanupResult(complete=False, rolled_back_mgm_ids=[2])
        assert _call(api_call).call_count == 3

    def test_exception_on_one_orphan_does_not_stop_the_others(
        self, api_call: FwoApiCall, mocker: MockerFixture
    ) -> None:
        mocker.patch("orphaned_imports.FWOLogger.error")
        _call(api_call).side_effect = [_orphans(ORPHAN_A, ORPHAN_B), FwoImporterError("boom"), ROLLED_BACK]

        assert roll_back_orphaned_imports(api_call) == OrphanCleanupResult(complete=False, rolled_back_mgm_ids=[2])
        assert _call(api_call).call_count == 3

    def test_a_data_issue_is_created_per_rolled_back_import(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = [_orphans(ORPHAN_A, ORPHAN_B), {"errors": [{"message": "x"}]}, ROLLED_BACK]

        roll_back_orphaned_imports(api_call)

        create_data_issue = cast("MagicMock", api_call.create_data_issue)
        create_data_issue.assert_called_once()
        assert create_data_issue.call_args.kwargs["mgm_id"] == ORPHAN_B["mgm_id"]
        assert create_data_issue.call_args.kwargs["severity"] == 1
        description: str = create_data_issue.call_args.kwargs["description"]
        assert description == (
            f"import {ORPHAN_B['control_id']} of this management was left unfinished "
            "(the importer process ended or lost the api during the import) and has been rolled back"
        )

    def test_the_warning_does_not_blame_a_previous_process(self, api_call: FwoApiCall, mocker: MockerFixture) -> None:
        mock_warning = mocker.patch("orphaned_imports.FWOLogger.warning")
        _call(api_call).side_effect = [_orphans(ORPHAN_A), ROLLED_BACK]

        roll_back_orphaned_imports(api_call)

        message: str = mock_warning.call_args.args[0]
        assert "previous" not in message
        assert "it ended or lost the api during the import" in message

    def test_a_failed_data_issue_does_not_fail_the_rollback(self, api_call: FwoApiCall, mocker: MockerFixture) -> None:
        mock_error = mocker.patch("orphaned_imports.FWOLogger.error")
        _call(api_call).side_effect = [_orphans(ORPHAN_A), ROLLED_BACK]
        cast("MagicMock", api_call.create_data_issue).side_effect = FwoImporterError("api down")

        assert roll_back_orphaned_imports(api_call) == OrphanCleanupResult(complete=True, rolled_back_mgm_ids=[1])
        mock_error.assert_called_once()

    def test_shutdown_while_creating_the_data_issue_is_not_swallowed(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = [_orphans(ORPHAN_A), ROLLED_BACK]
        cast("MagicMock", api_call.create_data_issue).side_effect = ShutdownRequestedError()

        with pytest.raises(ShutdownRequestedError):
            roll_back_orphaned_imports(api_call)

    def test_shutdown_during_a_rollback_is_not_swallowed(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = [_orphans(ORPHAN_A), ShutdownRequestedError()]

        with pytest.raises(ShutdownRequestedError):
            roll_back_orphaned_imports(api_call)


class TestTryRollBackOrphanedImports:
    def test_api_error_is_logged_and_reported_as_pending(self, api_call: FwoApiCall, mocker: MockerFixture) -> None:
        _call(api_call).side_effect = FwoImporterError("api down")
        mock_error = mocker.patch("orphaned_imports.FWOLogger.error")

        assert try_roll_back_orphaned_imports(api_call) == OrphanCleanupResult(complete=False, rolled_back_mgm_ids=[])
        mock_error.assert_called_once()

    def test_shutdown_is_not_swallowed(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = ShutdownRequestedError()

        with pytest.raises(ShutdownRequestedError):
            try_roll_back_orphaned_imports(api_call)

    def test_shutdown_while_the_real_api_retries_is_not_swallowed(self, monkeypatch: pytest.MonkeyPatch) -> None:
        monkeypatch.setattr(requests, "Session", _UnavailableSession)
        monkeypatch.setattr(fwo_api, "read_tls_identity", _test_tls_identity)
        monkeypatch.setattr(fwo_api, "FWO_API_RETRY_DELAYS_SECONDS", (1,))
        monkeypatch.setattr(fwo_http_retry, "_sleep", _shutdown_during_wait)

        with pytest.raises(ShutdownRequestedError):
            try_roll_back_orphaned_imports(FwoApiCall(FwoApi(API_URL, "jwt")))


class TestImportLeftOpen:
    def test_no_lock_taken_needs_no_query(self, api_call: FwoApiCall) -> None:
        assert import_left_open(api_call, -1) is False
        _call(api_call).assert_not_called()

    def test_released_lock(self, api_call: FwoApiCall) -> None:
        _call(api_call).return_value = _orphans()

        assert import_left_open(api_call, ORPHAN_A["control_id"]) is False
        assert _call(api_call).call_args.kwargs["query_variables"] == {"importId": ORPHAN_A["control_id"]}

    def test_lock_still_held_is_reported(self, api_call: FwoApiCall, mocker: MockerFixture) -> None:
        mock_warning = mocker.patch("orphaned_imports.FWOLogger.warning")
        _call(api_call).return_value = _orphans({"control_id": ORPHAN_A["control_id"]})

        assert import_left_open(api_call, ORPHAN_A["control_id"]) is True
        assert "still holds its management's lock" in mock_warning.call_args.args[0]

    @pytest.mark.parametrize("outcome", [FwoImporterError("api down"), {"errors": [{"message": "x"}]}])
    def test_unknown_state_counts_as_open(self, outcome: object, api_call: FwoApiCall, mocker: MockerFixture) -> None:
        mocker.patch("orphaned_imports.FWOLogger.warning")
        if isinstance(outcome, Exception):
            _call(api_call).side_effect = outcome
        else:
            _call(api_call).return_value = outcome

        assert import_left_open(api_call, ORPHAN_A["control_id"]) is True

    def test_shutdown_is_not_swallowed(self, api_call: FwoApiCall) -> None:
        _call(api_call).side_effect = ShutdownRequestedError()

        with pytest.raises(ShutdownRequestedError):
            import_left_open(api_call, ORPHAN_A["control_id"])
