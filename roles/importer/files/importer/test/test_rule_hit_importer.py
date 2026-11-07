from __future__ import annotations

from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import fwo_const
import pytest
from fwo_exceptions import FwoApiFailedLockImportError, FwoApiWriteError, ShutdownRequestedError
from rule_hit_importer import import_rule_hits

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
    from pytest_mock import MockerFixture

MGM_ID = 3
IMPORT_ID = 77
IMPORT_ERROR_ALERT_CODE = 14


@pytest.fixture
def fw_module(mocker: MockerFixture) -> MagicMock:
    module = MagicMock()
    module.supports_rule_hits.return_value = True
    module.get_rule_hits.return_value = ["hit"]
    mocker.patch("rule_hit_importer.get_module", return_value=module)
    return module


@pytest.fixture
def responsible(mocker: MockerFixture) -> MagicMock:
    return mocker.patch("rule_hit_importer.is_responsible_importer", return_value=True)


@pytest.fixture
def writer(mocker: MockerFixture) -> MagicMock:
    return mocker.patch("rule_hit_importer.RuleHitImport").return_value


@pytest.fixture
def api() -> MagicMock:
    return MagicMock()


@pytest.fixture
def api_call_with_lock(api: MagicMock) -> MagicMock:
    api.set_import_lock.return_value = IMPORT_ID
    return api


@pytest.mark.usefixtures("responsible")
def test_rule_hits_are_imported_with_rule_hit_lock(
    api_call_with_lock: MagicMock,
    fw_module: MagicMock,
    writer: MagicMock,
    import_state_controller: ImportStateController,
) -> None:
    assert import_rule_hits(api_call_with_lock) is True

    api_call_with_lock.delete_rule_hit_import_request.assert_called_once_with(MGM_ID)
    assert api_call_with_lock.set_import_lock.call_args.kwargs["import_type_id"] == fwo_const.IMPORT_TYPE_RULE_HIT
    assert import_state_controller.state.import_id == IMPORT_ID
    fw_module.get_rule_hits.assert_called_once_with(import_state_controller)
    writer.write_rule_hits.assert_called_once_with(["hit"])
    api_call_with_lock.unlock_import.assert_called_once_with(import_state_controller.state, success=True)
    api_call_with_lock.set_alert.assert_not_called()


def test_other_importer_hosts_leave_the_request(
    api: MagicMock, responsible: MagicMock, fw_module: MagicMock, import_state_controller: ImportStateController
) -> None:
    responsible.return_value = False

    assert import_rule_hits(api) is False

    api.delete_rule_hit_import_request.assert_not_called()
    fw_module.get_rule_hits.assert_not_called()
    assert import_state_controller.state.responsible_for_importing is False


@pytest.mark.usefixtures("responsible")
def test_disabled_management_is_skipped(
    api: MagicMock, fw_module: MagicMock, import_state_controller: ImportStateController
) -> None:
    import_state_controller.state.mgm_details.import_disabled = True

    assert import_rule_hits(api) is False

    api.delete_rule_hit_import_request.assert_called_once_with(MGM_ID)
    api.set_import_lock.assert_not_called()
    fw_module.get_rule_hits.assert_not_called()


@pytest.mark.usefixtures("responsible")
def test_modules_without_rule_hits_are_skipped(api: MagicMock, fw_module: MagicMock) -> None:
    fw_module.supports_rule_hits.return_value = False

    assert import_rule_hits(api) is False

    api.delete_rule_hit_import_request.assert_called_once_with(MGM_ID)
    api.set_import_lock.assert_not_called()


@pytest.mark.usefixtures("responsible")
def test_locked_management_is_skipped(api: MagicMock, fw_module: MagicMock) -> None:
    api.set_import_lock.side_effect = FwoApiFailedLockImportError("locked")

    assert import_rule_hits(api) is False

    fw_module.get_rule_hits.assert_not_called()
    api.unlock_import.assert_not_called()


@pytest.mark.usefixtures("responsible")
def test_failure_unlocks_unsuccessfully_and_alerts(
    api_call_with_lock: MagicMock,
    fw_module: MagicMock,
    writer: MagicMock,
    import_state_controller: ImportStateController,
) -> None:
    writer.write_rule_hits.side_effect = FwoApiWriteError("write failed")

    assert import_rule_hits(api_call_with_lock) is False

    api_call_with_lock.unlock_import.assert_called_once_with(import_state_controller.state, success=False)
    alert = api_call_with_lock.set_alert.call_args.kwargs
    assert alert["alert_code"] == IMPORT_ERROR_ALERT_CODE
    assert alert["mgm_id"] == MGM_ID
    assert "write failed" in alert["description"]
    fw_module.get_rule_hits.assert_called_once()


@pytest.mark.usefixtures("responsible", "fw_module")
def test_shutdown_removes_the_lock(
    api_call_with_lock: MagicMock,
    writer: MagicMock,
    import_state_controller: ImportStateController,
    mocker: MockerFixture,
) -> None:
    writer.write_rule_hits.side_effect = ShutdownRequestedError()
    delete_import = mocker.patch.object(import_state_controller, "delete_import")

    with pytest.raises(ShutdownRequestedError):
        import_rule_hits(api_call_with_lock)

    delete_import.assert_called_once()
    api_call_with_lock.unlock_import.assert_not_called()
