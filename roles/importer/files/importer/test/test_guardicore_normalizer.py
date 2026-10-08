import json
from pathlib import Path
from typing import Any

import pytest
from common import get_module, get_module_package_name
from fw_modules.guardicoremanagementREST import fwcommon, gc_getter
from fw_modules.guardicoremanagementREST.fwcommon import GuardicoreManagementRESTCommon
from fw_modules.guardicoremanagementREST.gc_models import GuardicoreConfig
from fw_modules.guardicoremanagementREST.gc_normalizer import normalize_config
from fwo_exceptions import FwoNativeConfigParseError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.rule import RuleAction, RuleTrack
from pytest_mock import MockerFixture

FIXTURE_FILE = Path(__file__).parent / "data" / "guardicore_native_config.json"


def load_native_config() -> dict[str, Any]:
    with FIXTURE_FILE.open(encoding="utf-8") as fixture:
        return json.load(fixture)


class TestNormalizeConfig:
    def test_normalizes_fixture(self, management_controller: ManagementController) -> None:
        normalized = normalize_config(GuardicoreConfig.model_validate(load_native_config()), management_controller)

        assert [rulebase.name for rulebase in normalized.rulebases] == ["03 Override Block", "04 Allow APP-1001"]
        links = normalized.gateways[0].RulebaseLinks
        assert [link.to_rulebase_uid for link in links] == ["03 Override Block", "04 Allow APP-1001"]
        assert normalized.gateways[0].Uid == "Mock Management"

        web_label = normalized.network_objects["label-approle-web"]
        assert web_label.obj_name == "AppRole: AR1001"
        assert web_label.obj_member_refs == "10.0.1.5/32|10.0.1.6/32"
        assert normalized.network_objects["label-approle-empty"].obj_member_refs is None
        assert normalized.network_objects["label-os-windows"].obj_member_refs == "10.0.1.5/32|10.0.1.6/32"

        allow_rule = normalized.rulebases[1].rules["rule-allow-web-db"]
        assert allow_rule.rule_src_refs == "label-approle-web"
        assert allow_rule.rule_dst_refs == "label-approle-db&label-stage-prod"
        assert allow_rule.rule_dst == "AppRole: AR1002 & Stage: Prod"
        assert normalized.network_objects["label-approle-db&label-stage-prod"].obj_member_refs == ("10.0.2.10/31")
        assert allow_rule.rule_svc == "tcp/5432"
        assert (allow_rule.rule_name, allow_rule.rule_comment) == ("APP-1001", "FWOC1")
        assert allow_rule.last_change_admin == "api-user"
        assert allow_rule.rule_src_labels == {"AppRole": "AR1001"}
        assert allow_rule.rule_dst_labels == {"AppRole": "AR1002", "Stage": "Prod"}

        override_rule = normalized.rulebases[0].rules["rule-override-block"]
        assert override_rule.rule_disabled
        assert (override_rule.rule_src_labels, override_rule.rule_dst_labels) == (None, None)
        assert (override_rule.rule_action, override_rule.rule_track) == (RuleAction.DROP, RuleTrack.ALERT)
        assert (override_rule.rule_src, override_rule.rule_dst, override_rule.rule_svc) == (
            "Any",
            "192.0.2.0/24",
            "Any",
        )

        ping_rule = normalized.rulebases[1].rules["rule-allow-ping"]
        assert ping_rule.rule_src == "Apps: all"
        assert ping_rule.rule_dst == "Private"
        assert ping_rule.rule_svc == "icmp type 8 code 0"

        assert [(label.key_name, label.value) for label in normalized.labels][:2] == [
            ("AppRole", "AR1001"),
            ("AppRole", "AR1002"),
        ]
        assert len(normalized.labels) == 5


class TestFwCommon:
    def test_get_config_uses_given_native_config(
        self, import_state_controller: ImportStateController, mocker: MockerFixture
    ) -> None:
        write_native = mocker.patch.object(fwcommon, "write_native_config_to_file")
        fetch = mocker.patch.object(gc_getter, "get_native_config")
        config_in = FwConfigManagerListController()
        config_in.native_config = load_native_config()

        result, config_out = GuardicoreManagementRESTCommon().get_config(config_in, import_state_controller)

        assert result == 0
        fetch.assert_not_called()
        write_native.assert_called_once()
        assert config_out.ManagerSet[0].manager_uid == "mock-uid"
        assert len(config_out.ManagerSet[0].configs[0].rulebases) == 2

    def test_get_config_fetches_from_api(
        self, import_state_controller: ImportStateController, mocker: MockerFixture
    ) -> None:
        mocker.patch.object(fwcommon, "write_native_config_to_file")
        fetch = mocker.patch.object(gc_getter, "get_native_config", return_value=load_native_config())
        config_in = FwConfigManagerListController()

        GuardicoreManagementRESTCommon().get_config(config_in, import_state_controller)

        fetch.assert_called_once_with("https://mock.example.com:443", "mock-user", "mock-secret")

    def test_get_config_rejects_invalid_native_config(
        self, import_state_controller: ImportStateController, mocker: MockerFixture
    ) -> None:
        mocker.patch.object(fwcommon, "write_native_config_to_file")
        config_in = FwConfigManagerListController()
        config_in.native_config = {"rules": [{"id": "r1"}]}

        with pytest.raises(FwoNativeConfigParseError):
            GuardicoreManagementRESTCommon().get_config(config_in, import_state_controller)


class TestDispatch:
    def test_device_type_resolves_to_guardicore_module(self, import_state_controller: ImportStateController) -> None:
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.device_type_name = "Guardicore Management"
        mgm_details.device_type_version = "REST"

        assert get_module_package_name(import_state_controller.state) == "guardicoremanagementREST"
        assert isinstance(get_module(import_state_controller.state), GuardicoreManagementRESTCommon)
