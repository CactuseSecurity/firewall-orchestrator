from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fw_modules.fortiosmanagementREST.fos_normalizer import normalize_config
from fwo_log import FWOLogger
from test.mocking.mock_management_controller import MockManagementController


def test_normalize_config_uses_management_name_when_devices_missing():
    FWOLogger(0)
    mgm_details = MockManagementController()
    mgm_details.devices = []

    normalized = normalize_config(FortiOSConfig(), mgm_details)

    assert normalized.gateways[0].Name == mgm_details.name
