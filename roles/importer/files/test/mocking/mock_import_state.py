from importer.model_controllers.import_state_controller import ImportStateController
from importer.model_controllers.import_statistics_controller import ImportStatisticsController

try:
    from mock_management_details_controller import MockManagementDetailsController
except ModuleNotFoundError:
    from test.mocking.mock_management_details_controller import MockManagementDetailsController


class MockImportStateController(ImportStateController):
    """
        Mock class for ImportState.
    """

    def __init__(self):
        """
            Initializes without calling base init. This avoids the necessity to provide JWT and management details.
        """

        self.DebugLevel = 0
        self.Stats = ImportStatisticsController()
        self.call_log = []
        self.stub_responses = {}
        self.ImportVersion = 9
        self.MgmDetails = MockManagementDetailsController()

        
    def call(self, *args, **kwargs):

        self.call_log.append((args, kwargs))
        key = (args, frozenset(kwargs.items()))
        return self.stub_responses.get(key, None)


    def setup_response(self, args, kwargs, response):
        
        key = (args, frozenset(kwargs.items()))
        self.stub_responses[key] = response

