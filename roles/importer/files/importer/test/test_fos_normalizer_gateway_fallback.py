from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fw_modules.fortiosmanagementREST.fos_normalizer import normalize_config
from model_controllers.import_state_controller import ImportStateController


class TestFosNormalizerGatewayFallback:
    def test_normalize_config_uses_management_name_when_devices_missing(
        self,
        import_state_controller: ImportStateController,
    ):
        mgm_details = import_state_controller.state.mgm_details
        mgm_details.name = "unit-test-gateway"
        mgm_details.devices = []

        normalized = normalize_config(FortiOSConfig(), mgm_details)

        assert normalized.gateways[0].Name == mgm_details.name
