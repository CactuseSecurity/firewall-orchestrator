from fw_modules.fortiosmanagementREST import fwcommon
from states.import_state import ImportState


class TestEnsureDeviceName:
    def test_ensure_device_name_uses_gateway_uid(
        self,
        import_state: ImportState,
    ):
        import_state.gateway_map = {import_state.mgm_details.current_mgm_id: {"gw-uid": 1}}
        import_state.mgm_details.devices = []

        fwcommon.ensure_device_name(import_state)

        assert import_state.mgm_details.devices[0]["name"] == "gw-uid"

    def test_ensure_device_name_overrides_non_matching_device(
        self,
        import_state: ImportState,
    ):
        import_state.gateway_map = {import_state.mgm_details.current_mgm_id: {"gw-uid": 1}}
        import_state.mgm_details.devices = [{"name": "fortigate_demo"}]

        fwcommon.ensure_device_name(import_state)

        assert import_state.mgm_details.devices[0]["name"] == "gw-uid"
