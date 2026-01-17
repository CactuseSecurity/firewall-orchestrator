from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfigmanager import FwConfigManager
from unit_tests.utils.config_builder import FwConfigBuilder


class TestCheckConsistencyNetworkObjects:
    def test_check_consistency_network_objects_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        empty_manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[config],
            domain_name="",
            domain_uid="",
            manager_name="",
        )
        manager_controller.add_manager(empty_manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0
