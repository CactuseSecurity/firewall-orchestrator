from importer.model_controllers.import_statistics_controller import ImportStatisticsController

class MockImportStateController: # TODO: use real class as base
    def __init__(self):
        self._debug_level = 0
        self._stats = ImportStatisticsController()

    @property
    def DebugLevel(self) -> int:
        return self._debug_level
    
    @DebugLevel.setter
    def DebugLevel(self, value):
        self._debug_level = value
        
    @property
    def Stats(self) -> int:
        return self._stats
    
    @Stats.setter
    def Stats(self, value):
        self._stats = value    

