from __future__ import annotations

from typing import Any
from unittest.mock import MagicMock, patch

import pytest
from fw_modules.fortiadom5ff import fmgr_getter, fwcommon
from fwo_exceptions import FwApiCallFailedError, FwLogoutFailedError

FM_URL = "https://fmg.invalid/jsonrpc"
SID = "sid-1"


def import_state_mock() -> MagicMock:
    import_state: MagicMock = MagicMock()
    import_state.state.mgm_details.build_fw_api_string.return_value = FM_URL
    return import_state


def test_failed_logout_keeps_the_fetched_config() -> None:
    config_in: MagicMock = MagicMock()
    with (
        patch.object(fwcommon, "get_sid", return_value=SID),
        patch.object(fwcommon, "_fetch_native_config") as fetch,
        patch.object(fmgr_getter, "logout", side_effect=FwLogoutFailedError("timeout")) as logout,
        patch.object(fwcommon, "write_native_config_to_file") as write,
    ):
        fwcommon.get_native_config(config_in, import_state_mock())

    fetch.assert_called_once()
    logout.assert_called_once_with(FM_URL, SID)
    write.assert_called_once()


def test_failed_fetch_still_logs_out_and_keeps_its_error() -> None:
    def fail_fetch(*_args: Any) -> None:
        raise FwApiCallFailedError("fetch failed")

    with (
        patch.object(fwcommon, "get_sid", return_value=SID),
        patch.object(fwcommon, "_fetch_native_config", side_effect=fail_fetch),
        patch.object(fmgr_getter, "logout", side_effect=FwLogoutFailedError("timeout")) as logout,
        patch.object(fwcommon, "write_native_config_to_file") as write,
        pytest.raises(FwApiCallFailedError),
    ):
        fwcommon.get_native_config(MagicMock(), import_state_mock())

    logout.assert_called_once_with(FM_URL, SID)
    write.assert_not_called()
