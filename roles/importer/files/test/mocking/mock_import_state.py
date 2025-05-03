from importer.model_controllers.import_state_controller import ImportStateController
from importer.model_controllers.import_statistics_controller import ImportStatisticsController


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

