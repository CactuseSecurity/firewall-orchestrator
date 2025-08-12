
from model_controllers.management_controller import ManagementController


class MockManagementController(ManagementController):
    def __init__(self, is_super_manager: bool = False):
        """
            Initializes without calling base init.
        """

        self.Id = 3
        self.Name = "Mock Management"
        self.IsSuperManager = is_super_manager