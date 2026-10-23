import json
from pathlib import Path
from typing import Any

import pytest
from fw_modules.azure2022ff import fwcommon
from fw_modules.azure2022ff.azure_models import AzureConfig
from fw_modules.azure2022ff.fwcommon import Azure2022ffCommon
from fwo_exceptions import FwoNativeConfigParseError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from pytest_mock import MockerFixture

NATIVE_CONFIG_FILE = Path(__file__).parent / "data" / "azure2022ff_native_config.json"


@pytest.fixture
def azure_import_state(import_state_controller: ImportStateController) -> ImportStateController:
    import_state_controller.state.mgm_details.devices = [
        {
            "name": "AzureFirewall1",
            "uid": "azfw1",
            "importDisabled": False,
            "local_rulebase_name": "FirewallPolicy_AzureFirewall1",
        },
    ]
    import_state_controller.state.force_import = False
    return import_state_controller


def test_normalized_config_is_passed_through(azure_import_state: ImportStateController) -> None:
    config = FwConfigManagerListController.generate_empty_config()
    manager = config.get_first_manager()
    assert manager is not None
    normalized = FwConfigNormalized()
    manager.configs.append(normalized)

    status, result = Azure2022ffCommon().get_config(config, azure_import_state)

    assert status == 0
    assert result is config
    assert result.ManagerSet[0].configs == [normalized]


def test_native_config_from_file_is_normalized(azure_import_state: ImportStateController) -> None:
    config = FwConfigManagerListController.generate_empty_config()
    config.native_config = json.loads(NATIVE_CONFIG_FILE.read_text())

    status, result = Azure2022ffCommon().get_config(config, azure_import_state)

    assert status == 0
    normalized = result.ManagerSet[0].configs[0]
    assert len(normalized.rulebases[0].rules) == 4
    assert normalized.gateways[0].Uid == "azfw1"
    assert result.ManagerSet[0].manager_uid == azure_import_state.state.mgm_details.uid


def test_invalid_native_config_fails(azure_import_state: ImportStateController) -> None:
    config = FwConfigManagerListController.generate_empty_config()
    config.native_config = {"firewallPolicies": [{"ruleCollectionGroups": []}]}

    with pytest.raises(FwoNativeConfigParseError):
        Azure2022ffCommon().get_config(config, azure_import_state)


def test_empty_config_fetches_from_api(mocker: MockerFixture, azure_import_state: ImportStateController) -> None:
    native_config = AzureConfig.model_validate(json.loads(NATIVE_CONFIG_FILE.read_text()))
    get_native_config = mocker.patch.object(fwcommon.azure_getter, "get_native_config", return_value=native_config)
    config = FwConfigManagerListController.generate_empty_config()

    _, result = Azure2022ffCommon().get_config(config, azure_import_state)

    get_native_config.assert_called_once()
    assert result.native_config is not None
    assert "firewallPolicies" in result.native_config
    assert len(result.ManagerSet[0].configs[0].rulebases[0].rules) == 4


def test_ensure_manager_set_adds_manager(azure_import_state: ImportStateController) -> None:
    config = FwConfigManagerListController()

    fwcommon.ensure_manager_set(config, azure_import_state)

    assert len(config.ManagerSet) == 1
    assert config.ManagerSet[0].manager_uid == azure_import_state.state.mgm_details.uid


def test_management_controller_reads_cloud_fields(management_controller: ManagementController) -> None:
    json_dict: dict[str, Any] = {
        "id": 1,
        "uid": "u",
        "name": "azure",
        "hostname": "management.azure.com",
        "port": 443,
        "devices": [],
        "deviceType": {"name": "Azure", "version": "2022ff"},
        "import_credential": {"user": "cid", "secret": "csecret"},
        "isSuperManager": False,
        "subManagers": [],
        "configPath": "rg-1",
        "importDisabled": False,
        "cloudTenantId": "tenant-1",
        "cloudSubscriptionId": "sub-1",
    }

    management = ManagementController.from_json(json_dict)

    assert management.cloud_tenant_id == "tenant-1"
    assert management.cloud_subscription_id == "sub-1"
    assert management.domain_name == "rg-1"
    assert management_controller.cloud_tenant_id is None
