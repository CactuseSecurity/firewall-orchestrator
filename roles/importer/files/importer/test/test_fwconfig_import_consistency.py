import unittest.mock

import pytest
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObject
from netaddr import IPNetwork
from test.data.mock_objects import MockObjectsFactory
from test.utils.config_builder import FwConfigBuilder


@pytest.fixture
def fw_config_import_object() -> FwConfigImportObject:
    fw_config_import_object: FwConfigImportObject = unittest.mock.create_autospec(FwConfigImportObject)
    fw_config_import_object.network_object_type_map = {"network": 1, "group": 2, "host": 3, "machine_range": 4}
    fw_config_import_object.service_object_type_map = {"simple": 1, "group": 2, "rpc": 3}
    fw_config_import_object.user_object_type_map = {"group": 1, "simple": 2}
    fw_config_import_object.user_object_type_map = {"group": 1, "simple": 2}
    fw_config_import_object.get_user_obj_type_map = unittest.mock.Mock(return_value={"group": 1, "simple": 2})
    return fw_config_import_object


class TestCheckConsistencyColors:
    def test_check_network_object_color_consistency_valid(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        config.network_objects["NetworkObject1"] = NetworkObject(
            obj_uid="NetworkObject1",
            obj_name="NetworkObject1",
            obj_ip=IPNetwork("192.168.1.1/32"),
            obj_ip_end=IPNetwork("192.168.1.1/32"),
            obj_typ="network",
            obj_color="red",
        )

        manager_controller = FwConfigManagerListController()
        manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[config],
            domain_name="",
            domain_uid="",
            manager_name="",
        )
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check__network_object_color_consistency_invalid_no_fix(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = MockObjectsFactory.add_standard_network_host_object(config)
        nw_obj.obj_color = "nonexistent_color"

        manager_controller = FwConfigManagerListController()

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller, fix=False)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {
            "unresolvableColorRefs": {"nwObjColors": ["nonexistent_color"], "svcColors": [], "userColors": []}
        }

    def test_check_network_object_color_consistency_invalid_with_fix(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = MockObjectsFactory.add_standard_network_host_object(config)
        nw_obj.obj_color = "nonexistent_color"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller, fix=True)
        configured_color = config.network_objects[nw_obj.obj_uid].obj_color

        assert len(consistency_checker.issues) == 0
        assert configured_color == "black"  # default color assigned when fix=True

    def test_check_service_object_color_consistency_valid(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = MockObjectsFactory.add_standard_network_host_object(config)
        nw_obj.obj_color = "red"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check__service_object_color_consistency_invalid_no_fix(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = MockObjectsFactory.add_standard_service_object(config)
        svc_obj.svc_color = "nonexistent_color"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller, fix=False)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {
            "unresolvableColorRefs": {"nwObjColors": [], "svcColors": ["nonexistent_color"], "userColors": []}
        }

    def test_check_service_object_color_consistency_invalid_with_fix(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = MockObjectsFactory.add_standard_service_object(config)
        svc_obj.svc_color = "nonexistent_color"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller, fix=True)
        configured_color = config.service_objects[svc_obj.svc_uid].svc_color

        assert len(consistency_checker.issues) == 0
        assert configured_color == "black"  # default color assigned when fix=True


class TestCheckConsistencyNetworkObjects:
    def test_check_network_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_invalid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        # Introduce an inconsistency by referencing a non-existent network object

        nw_obj = MockObjectsFactory.add_standard_network_host_object(config)
        nw_obj.obj_typ = "DoesNotExist"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableNwObjTypes": ["DoesNotExist"]}

    def test_check_network_object_consistency_with_single_host_in_group(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = MockObjectsFactory.add_standard_network_host_object(config)
        MockObjectsFactory.add_standard_network_group_object(config, [nw_obj])

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)

        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )

        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_with_multiple_hosts_in_group(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj1 = MockObjectsFactory.add_standard_network_host_object(config, index=1)
        nw_obj2 = MockObjectsFactory.add_standard_network_host_object(config, index=2)
        MockObjectsFactory.add_standard_network_group_object(config, index=1, obj_members=[nw_obj1, nw_obj2])

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)

        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )

        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    @pytest.mark.skip(reason="Currently, circular references are not detected.")
    def test_check_network_object_consistency_with_group_referencing_itself(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_group_obj = MockObjectsFactory.add_standard_network_group_object(config, index=1, obj_members=[])
        nw_group_obj.obj_member_refs = nw_group_obj.obj_uid  # Circular reference

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )

        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"circularNwObjRefs": ["GroupObject"]}

    def test_check_network_object_consistency_with_empty_group(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        MockObjectsFactory.add_standard_network_group_object(config, index=1, obj_members=[])

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )

        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_with_group_referencing_nonexistent_object(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_group_obj = MockObjectsFactory.add_standard_network_group_object(config, index=1, obj_members=[])
        nw_group_obj.obj_member_refs = "NonExistentHost"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_network_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableNwObjRefs": ["NonExistentHost"]}

    def test_check_network_object_consistency_with_mixed_valid_and_invalid_references(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        valid_nw_obj = MockObjectsFactory.add_standard_network_host_object(config, index=1)
        group_obj = MockObjectsFactory.add_standard_network_group_object(config, index=1, obj_members=[valid_nw_obj])
        group_obj.obj_member_refs += "|InvalidHost"  # pyright: ignore[reportOperatorIssue]

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_network_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableNwObjRefs": ["InvalidHost"]}

    def test_check_network_object_consistency_none_group_without_ip(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = MockObjectsFactory.add_standard_network_host_object(config)
        nw_obj.obj_ip = None
        nw_obj.obj_ip_end = None

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )

        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_network_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"non-group network object with undefined IP addresse(s)": [nw_obj]}


class TestCheckConsistencyServiceObjects:
    def test_check_service_object_consistency_no_config(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        manager = MockObjectsFactory.get_standard_fwconfig_manager()
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_service_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = MockObjectsFactory.add_standard_service_object(config)
        MockObjectsFactory.add_standard_service_group_object(config, [svc_obj])

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_service_object_consistency_unresolvable_object(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_group_obj = MockObjectsFactory.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = "NonExistentService"

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableSvcObjRefs": ["NonExistentService"]}

    def test_check_service_object_consistency_invalid_type(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = MockObjectsFactory.add_standard_service_object(config)
        svc_obj.svc_typ = "DoesNotExist"

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableSvcObjTypes": ["DoesNotExist"]}

    def test_check_service_object_consistency_with_multiple_members_in_group(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        MockObjectsFactory.add_standard_service_object(config, index=1)
        MockObjectsFactory.add_standard_service_object(config, index=2)

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    @pytest.mark.skip(reason="Currently, circular references are not detected.")
    def test_check_service_object_consistency_with_group_referencing_itself(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_group_obj = MockObjectsFactory.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = svc_group_obj.svc_uid  # Circular reference

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"circularSvcObjRefs": ["GroupObject"]}

    def test_check_service_object_consistency_with_empty_group(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_group_obj = MockObjectsFactory.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = ""

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_service_object_consistency_with_mixed_valid_and_invalid_references(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        MockObjectsFactory.add_standard_service_object(config, index=1)

        svc_group_obj = MockObjectsFactory.add_standard_service_group_object(config, index=1)
        svc_group_obj.svc_member_refs = "ServiceObject1|InvalidService"

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_service_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableSvcObjRefs": ["InvalidService"]}


class TestCheckUserObjectConsistency:
    def test_check_user_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=1,
            user_group_object_count=2,
            user_group_object_member_count=2,
        )

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_user_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    @pytest.mark.skip(reason="Currently, unresolvable user objects are not detected.")
    def test_check_user_object_unresolvable_object(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=1,
            user_group_object_count=2,
            user_group_object_member_count=2,
        )

        # Add a user object that is not referenced anywhere
        group = {
            "user_typ": "group",
            "user_uid": "GroupWithInvalidMember",
            "user_name": "GroupWithInvalidMember",
            "user_member_names": "DoesNotExist",
            "user_member_refs": "DoesNotExist",
        }

        config.users["GroupWithInvalidMember"] = group

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_user_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableUserObjTypes": ["DoesNotExist"]}


class TestRulebaseConsistency:
    def test_check_rulebase_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_consistency_with_no_configs(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        manager = MockObjectsFactory.get_standard_fwconfig_manager()
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_consistency_with_empty_config(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        empty_config = fwconfig_builder.build_empty_config()

        manager = MockObjectsFactory.get_standard_fwconfig_manager(empty_config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_consistency_with_unresolvable_tracks(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        rule_uid = next(iter(config.rulebases[0].rules.keys()))
        config.rulebases[0].rules[rule_uid].rule_track = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableRuleTracks": ["NonExistent"]}

    def test_check_rulebase_consistency_with_unresolvable_actions(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        rule_uid = next(iter(config.rulebases[0].rules.keys()))
        config.rulebases[0].rules[rule_uid].rule_action = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableRuleActions": ["NonExistent"]}


class TestRulebaseLinkConsistency:
    def test_check_rulebase_link_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_link_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_link_consistency_with_no_configs(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        manager = MockObjectsFactory.get_standard_fwconfig_manager()
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_link_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_link_consistency_with_broken_links(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        config.gateways[0].RulebaseLinks[0].from_rule_uid = "NonExistentRuleUid"
        config.gateways[0].RulebaseLinks[0].from_rulebase_uid = "NonExistentFromRulebaseUID"
        config.gateways[0].RulebaseLinks[0].to_rulebase_uid = "NonExistentToRulebaseUID"

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_rulebase_link_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 1
        assert len(consistency_checker.issues["brokenRulebaseLinks"]) == 3


class TestZoneObjectConsistency:
    def test_check_zone_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 0

    def test_check_zone_object_consistency_with_no_config(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        manager = MockObjectsFactory.get_standard_fwconfig_manager()
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 0

    def test_check_zone_object_consistency_with_unresolvable_object_src(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        rule_uid = next(iter(config.rulebases[0].rules.keys()))
        config.rulebases[0].rules[rule_uid].rule_src_zone = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableZoneObjRefs": ["NonExistent"]}

    def test_check_zone_object_consistency_with_unresolvable_object_dst(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        rule_uid = next(iter(config.rulebases[0].rules.keys()))
        config.rulebases[0].rules[rule_uid].rule_dst_zone = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableZoneObjRefs": ["NonExistent"]}


class TestFullConfigConsistencyCheck:
    def test_full_config_consistency_check(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_group_object_count=2,
            user_group_object_member_count=2,
            user_object_count=5,
        )

        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_config_consistency(manager_controller)

        assert len(consistency_checker.issues) == 0
