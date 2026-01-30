from mocking.mock_import_state import MockImportStateController
from model_controllers.fwconfig_import_gateway import FwConfigImportGateway


class MockFwConfigImportGateway(FwConfigImportGateway):
    def __init__(self):
        super().__init__()
        self._import_details = MockImportStateController(stub_setCoreData=True)

        self._global_state.import_state = self._import_details
