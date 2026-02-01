from fwo_base import init_service_provider
from model_controllers.fwconfig_import import FwConfigImport
from test.mocking.mock_import_state import MockImportStateController


def test_clear_management_uses_management_uid():
    service_provider = init_service_provider()
    import_state = MockImportStateController(stub_setCoreData=True)
    service_provider.get_global_state().import_state = import_state

    importer = FwConfigImport()
    config = importer.clear_management()

    assert config.ManagerSet[0].ManagerUid == import_state.state.mgm_details.uid
