from fw_modules.fortiosmanagementREST import fwcommon
from model_controllers.import_state_controller import ImportStateController


class TestEnsureDeviceName:
    def test_ensure_device_name_uses_gateway_uid(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.gateway_map = {import_state.state.mgm_details.current_mgm_id: {"gw-uid": 1}}
        import_state.state.mgm_details.devices = []

        fwcommon.ensure_device_name(import_state)

        assert import_state.state.mgm_details.devices[0]["name"] == "gw-uid"

    def test_ensure_device_name_overrides_non_matching_device(
        self,
        import_state_controller: ImportStateController,
    ):
        import_state = import_state_controller
        import_state.state.gateway_map = {import_state.state.mgm_details.current_mgm_id: {"gw-uid": 1}}
        import_state.state.mgm_details.devices = [{"name": "fortigate_demo"}]

        fwcommon.ensure_device_name(import_state)

        assert import_state.state.mgm_details.devices[0]["name"] == "gw-uid"
