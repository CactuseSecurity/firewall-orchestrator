from fw_modules.fortiosmanagementREST import fwcommon
from test.mocking.mock_import_state import MockImportStateController


def test_ensure_device_name_uses_gateway_uid():
    import_state = MockImportStateController(stub_setCoreData=True)
    import_state.state.gateway_map = {import_state.state.mgm_details.current_mgm_id: {"gw-uid": 1}}
    import_state.state.mgm_details.devices = []

    fwcommon.ensure_device_name(import_state)

    assert import_state.state.mgm_details.devices[0]["name"] == "gw-uid"


def test_ensure_device_name_overrides_non_matching_device():
    import_state = MockImportStateController(stub_setCoreData=True)
    import_state.state.gateway_map = {import_state.state.mgm_details.current_mgm_id: {"gw-uid": 1}}
    import_state.state.mgm_details.devices = [{"name": "fortigate_demo"}]

    fwcommon.ensure_device_name(import_state)

    assert import_state.state.mgm_details.devices[0]["name"] == "gw-uid"
