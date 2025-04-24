from importer.model_controllers.import_statistics_controller import ImportStatisticsController

class MockImportStateController: # TODO: use real class as base
    DebugLevel = 0
    Stats = ImportStatisticsController()
