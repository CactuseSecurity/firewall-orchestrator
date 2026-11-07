from __future__ import annotations

from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import pytest
from fw_modules.checkpointR8x import cp_const
from fw_modules.checkpointR8x.fwcommon import get_rules_params
from fw_modules.ciscoasa9.fwcommon import CiscoAsa9Common
from fw_modules.fortiadom5ff.fwcommon import FortiAdom5ffCommon
from fwo_exceptions import RuleHitsNotSupportedError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
    from pytest_mock import MockerFixture


def test_modules_without_rule_hits_reject_reading_them(import_state_controller: ImportStateController) -> None:
    module = CiscoAsa9Common()

    assert module.supports_rule_hits() is False
    with pytest.raises(RuleHitsNotSupportedError):
        module.get_rule_hits(import_state_controller)


def test_fortimanager_derives_hits_from_the_policy_packages(
    import_state_controller: ImportStateController, mocker: MockerFixture
) -> None:
    normalized_config = FwConfigManagerListController()
    get_config = mocker.patch("fw_modules.fortiadom5ff.fwcommon.get_config", return_value=(0, normalized_config))
    extract = mocker.patch("fw_modules.fortiadom5ff.fwcommon.extract_rule_hits", return_value=["hit"])
    import_state_controller.state.management_map = {"mock-uid": 3}
    module = FortiAdom5ffCommon()

    assert module.supports_rule_hits() is True
    assert module.get_rule_hits(import_state_controller) == ["hit"]
    assert get_config.call_args.args[0].has_empty_config()
    extract.assert_called_once_with(normalized_config, {"mock-uid": 3})


def test_regular_checkpoint_import_does_not_request_hits(import_state_controller: ImportStateController) -> None:
    import_state_controller.state.fwo_config = MagicMock(api_fetch_size=150)

    assert cp_const.with_hits is False
    assert get_rules_params(import_state_controller.state)["show-hits"] is False
