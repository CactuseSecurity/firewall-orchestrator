from __future__ import annotations

import re
from pathlib import Path
from typing import TYPE_CHECKING
from unittest.mock import MagicMock

import pytest
from common import get_module
from fw_modules.checkpointR8x import cp_const
from fw_modules.checkpointR8x.fwcommon import get_rules_params
from fw_modules.ciscoasa9.fwcommon import CiscoAsa9Common
from fw_modules.fortiadom5ff.fwcommon import FortiAdom5ffCommon
from fwo_exceptions import FwoImporterError, RuleHitsNotSupportedError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
    from pytest_mock import MockerFixture

# The device type seed belongs to the source checkout and is not copied to deployed importer installations.
DEVICE_TYPE_SEED_FILE = (
    Path(__file__).resolve().parents[4] / "database" / "files" / "sql" / "creation" / "fworch-fill-stm.sql"
)


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


def test_device_types_flagged_for_rule_hits_match_the_modules(mocker: MockerFixture) -> None:
    """Reports read stm_dev_typ.dev_typ_supports_rule_hits; it must mirror supports_rule_hits() of the modules."""
    if not DEVICE_TYPE_SEED_FILE.is_file():
        pytest.skip("Device type seed requires the source checkout")
    seed = DEVICE_TYPE_SEED_FILE.read_text(encoding="utf-8")
    flag_update = re.search(r"SET dev_typ_supports_rule_hits = TRUE WHERE dev_typ_id IN \(([\d, ]+)\)", seed)
    assert flag_update is not None
    flagged_ids = {int(dev_typ_id) for dev_typ_id in flag_update.group(1).split(",")}
    device_types: list[tuple[str, str, str]] = re.findall(
        r"^\s*VALUES \((\d+),\s*'([^']*)',\s*'([^']*)',[^\n]*?,\s*(?:true|false),\s*true,\s*(?:true|false)\)",
        seed,
        re.MULTILINE,
    )
    assert len(device_types) > len(flagged_ids)

    supporting_ids: set[int] = set()
    for dev_typ_id, name, version in device_types:
        state = mocker.MagicMock()
        state.mgm_details.device_type_name = name
        state.mgm_details.device_type_version = version
        try:
            module = get_module(state)
        except FwoImporterError:
            continue
        if module.supports_rule_hits():
            supporting_ids.add(int(dev_typ_id))

    assert supporting_ids == flagged_ids
