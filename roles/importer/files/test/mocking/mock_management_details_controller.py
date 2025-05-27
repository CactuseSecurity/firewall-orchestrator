
from importer.model_controllers.management_details_controller import ManagementDetailsController


class MockManagementDetailsController(ManagementDetailsController):
    def __init__(self):
        """
            Initializes without calling base init.
        """

        self.Id = 3