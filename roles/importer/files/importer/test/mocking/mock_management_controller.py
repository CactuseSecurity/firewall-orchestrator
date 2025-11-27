
from model_controllers.management_controller import ManagementController


class MockManagementController(ManagementController):
    def __init__(self, is_super_manager: bool = False):
        """
            Initializes without calling base init.
        """

        self.mgm_id = 3
        self.name = "Mock Management"
        self.is_super_manager = is_super_manager