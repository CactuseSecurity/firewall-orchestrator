import unittest.mock

import pytest
from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObject
from models.serviceobject import ServiceObject
from netaddr import IPNetwork
from unit_tests.utils.config_builder import FwConfigBuilder


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

        config.network_objects["NetworkObject1"] = NetworkObject(
            obj_uid="NetworkObject1",
            obj_name="NetworkObject1",
            obj_ip=IPNetwork("192.168.1.1/32"),
            obj_ip_end=IPNetwork("192.168.1.1/32"),
            obj_typ="host",
            obj_color="nonexistent_color",
        )

        manager_controller = FwConfigManagerListController()
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

        config.network_objects["NetworkObject1"] = NetworkObject(
            obj_uid="NetworkObject1",
            obj_name="NetworkObject1",
            obj_ip=IPNetwork("192.168.1.1/32"),
            obj_ip_end=IPNetwork("192.168.1.1/32"),
            obj_typ="host",
            obj_color="nonexistent_color",
        )

        manager_controller = FwConfigManagerListController()
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
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller, fix=True)
        configured_color = config.network_objects["NetworkObject1"].obj_color

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

        config.service_objects["ServiceObject1"] = ServiceObject(
            svc_uid="ServiceObject1",
            svc_name="ServiceObject1",
            svc_port=80,
            svc_port_end=80,
            svc_color="red",
            svc_typ="simple",
        )

        manager_controller = FwConfigManagerListController()
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

        config.service_objects["ServiceObject1"] = ServiceObject(
            svc_uid="ServiceObject1",
            svc_name="ServiceObject1",
            svc_port=80,
            svc_port_end=80,
            svc_typ="simple",
            svc_color="nonexistent_color",
        )

        manager_controller = FwConfigManagerListController()
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

        config.service_objects["ServiceObject1"] = ServiceObject(
            svc_uid="ServiceObject1",
            svc_name="ServiceObject1",
            svc_port=80,
            svc_port_end=80,
            svc_typ="simple",
            svc_color="nonexistent_color",
        )

        manager_controller = FwConfigManagerListController()
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
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_color_consistency(config=manager_controller, fix=True)
        configured_color = config.service_objects["ServiceObject1"].svc_color

        assert len(consistency_checker.issues) == 0
        assert configured_color == "black"  # default color assigned when fix=True


class TestCheckConsistencyNetworkObjects:
    def test_check_network_object_consistency_valid(
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
        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_invalid(
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
        # Introduce an inconsistency by referencing a non-existent network object
        config.network_objects["non_existent_network_obj"] = NetworkObject(
            obj_uid="non_existent_network_obj",
            obj_name="Non Existent Network Object",
            obj_ip=IPNetwork("0.0.0.0/32"),
            obj_ip_end=IPNetwork("0.0.0.0/32"),
            obj_typ="DoesNotExist",
            obj_color="red",
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

        config.network_objects["HostObject"] = NetworkObject(
            obj_uid="HostObject",
            obj_name="HostObject",
            obj_ip=IPNetwork("0.0.0.0/32"),
            obj_ip_end=IPNetwork("0.0.0.0/32"),
            obj_typ="host",
            obj_color="red",
        )

        config.network_objects["GroupObject"] = NetworkObject(
            obj_uid="GroupObject",
            obj_name="GroupObject",
            obj_typ="group",
            obj_color="red",
            obj_member_refs="HostObject",
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

        config.network_objects["HostObject1"] = NetworkObject(
            obj_uid="HostObject1",
            obj_name="HostObject1",
            obj_ip=IPNetwork("0.0.0.0/32"),
            obj_ip_end=IPNetwork("0.0.0.0/32"),
            obj_typ="host",
            obj_color="red",
        )

        config.network_objects["HostObject2"] = NetworkObject(
            obj_uid="HostObject2",
            obj_name="HostObject2",
            obj_ip=IPNetwork("0.0.0.1/32"),
            obj_ip_end=IPNetwork("0.0.0.1/32"),
            obj_typ="host",
            obj_color="red",
        )

        config.network_objects["GroupObject"] = NetworkObject(
            obj_uid="GroupObject",
            obj_name="GroupObject",
            obj_typ="group",
            obj_color="red",
            obj_member_refs="HostObject1|HostObject2",
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

        config.network_objects["GroupObject"] = NetworkObject(
            obj_uid="GroupObject",
            obj_name="GroupObject",
            obj_typ="group",
            obj_color="red",
            obj_member_refs="GroupObject",
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

        config.network_objects["EmptyGroupObject"] = NetworkObject(
            obj_uid="EmptyGroupObject",
            obj_name="EmptyGroupObject",
            obj_typ="group",
            obj_color="red",
            obj_member_refs="",
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

        consistency_checker.maps = fw_config_import_object

        consistency_checker.check_network_object_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_with_group_referencing_nonexistent_object(
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
        config.network_objects["GroupObject"] = NetworkObject(
            obj_uid="GroupObject",
            obj_name="GroupObject",
            obj_typ="group",
            obj_color="red",
            obj_member_refs="NonExistentHost",
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
        manager_controller = FwConfigManagerListController()
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        config.network_objects["ValidHost"] = NetworkObject(
            obj_uid="ValidHost",
            obj_name="ValidHost",
            obj_ip=IPNetwork("0.0.0.0/32"),
            obj_ip_end=IPNetwork("0.0.0.0/32"),
            obj_typ="host",
            obj_color="red",
        )
        config.network_objects["MixedGroup"] = NetworkObject(
            obj_uid="MixedGroup",
            obj_name="MixedGroup",
            obj_typ="group",
            obj_color="red",
            obj_member_refs="ValidHost|InvalidHost",
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

        config.network_objects["HostObjectNoIp"] = NetworkObject(
            obj_uid="HostObjectNoIp",
            obj_name="HostObjectNoIp",
            obj_typ="host",
            obj_color="red",
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

        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_network_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {
            "non-group network object with undefined IP addresse(s)": [
                NetworkObject(
                    obj_uid="HostObjectNoIp",
                    obj_name="HostObjectNoIp",
                    obj_ip=None,
                    obj_ip_end=None,
                    obj_color="red",
                    obj_typ="host",
                    obj_member_refs=None,
                    obj_member_names=None,
                    obj_comment=None,
                )
            ]
        }


class TestCheckConsistencyServiceObjects:
    def test_check_service_object_consistency_no_config(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        empty_manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[],
            domain_name="",
            domain_uid="",
            manager_name="",
        )
        manager_controller.add_manager(empty_manager)

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

        config.service_objects["ServiceObject1"] = ServiceObject(
            svc_uid="ServiceObject1",
            svc_name="ServiceObject1",
            svc_typ="simple",
            svc_color="red",
            svc_port=80,
            svc_port_end=80,
        )

        config.service_objects["ServiceObject2"] = ServiceObject(
            svc_uid="ServiceObject2",
            svc_name="ServiceObject2",
            svc_typ="group",
            svc_color="red",
            svc_member_refs="ServiceObject1",
            svc_member_names="ServiceObject1",
        )

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

        config.service_objects["ServiceObject1"] = ServiceObject(
            svc_uid="ServiceObject1",
            svc_name="ServiceObject1",
            svc_typ="group",
            svc_color="red",
            svc_member_refs="NonExistentService",
        )

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

        config.service_objects["InvalidTypeService"] = ServiceObject(
            svc_uid="InvalidTypeService",
            svc_name="InvalidTypeService",
            svc_typ="DoesNotExist",
            svc_color="red",
            svc_port=80,
            svc_port_end=80,
        )

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

        config.service_objects["Service1"] = ServiceObject(
            svc_uid="Service1",
            svc_name="Service1",
            svc_typ="simple",
            svc_color="red",
            svc_port=80,
            svc_port_end=80,
        )

        config.service_objects["Service2"] = ServiceObject(
            svc_uid="Service2",
            svc_name="Service2",
            svc_typ="simple",
            svc_color="red",
            svc_port=443,
            svc_port_end=443,
        )

        config.service_objects["ServiceGroup"] = ServiceObject(
            svc_uid="ServiceGroup",
            svc_name="ServiceGroup",
            svc_typ="group",
            svc_color="red",
            svc_member_refs="Service1|Service2",
            svc_member_names="Service1|Service2",
        )

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

        config.service_objects["GroupObject"] = ServiceObject(
            svc_uid="GroupObject",
            svc_name="GroupObject",
            svc_typ="group",
            svc_color="red",
            svc_member_refs="GroupObject",
            svc_member_names="GroupObject",
        )

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

        config.service_objects["EmptyGroupObject"] = ServiceObject(
            svc_uid="EmptyGroupObject",
            svc_name="EmptyGroupObject",
            svc_typ="group",
            svc_color="red",
            svc_member_refs="",
            svc_member_names="",
        )

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

        config.service_objects["ValidService"] = ServiceObject(
            svc_uid="ValidService",
            svc_name="ValidService",
            svc_typ="simple",
            svc_color="red",
            svc_port=80,
            svc_port_end=80,
        )

        config.service_objects["MixedGroup"] = ServiceObject(
            svc_uid="MixedGroup",
            svc_name="MixedGroup",
            svc_typ="group",
            svc_color="red",
            svc_member_refs="ValidService|InvalidService",
            svc_member_names="ValidService|InvalidService",
        )

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

        manager = FwConfigManager(
            manager_uid="GroupWithInvalidMember",
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

        consistency_checker.check_rulebase_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_consistency_with_no_configs(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[],
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

        manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[empty_config],
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

        rule_uid = list(config.rulebases[0].rules.keys())[0]
        config.rulebases[0].rules[rule_uid].rule_track = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

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

        rule_uid = list(config.rulebases[0].rules.keys())[0]
        config.rulebases[0].rules[rule_uid].rule_action = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

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

        consistency_checker.check_rulebase_link_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_link_consistency_with_no_configs(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
    ):
        manager_controller = FwConfigManagerListController()

        manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[],
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

        consistency_checker.check_rulebase_link_consistency(config=manager_controller)

        assert len(consistency_checker.issues) == 0


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

        rule_uid = list(config.rulebases[0].rules.keys())[0]
        config.rulebases[0].rules[rule_uid].rule_dst_zone = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

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
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 0

    def test_check_zone_object_consistency_with_no_config(
        self,
        import_state_controller: ImportStateController,
        fw_config_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        manager_controller = FwConfigManagerListController()

        empty_manager = FwConfigManager(
            manager_uid="mgr1",
            is_super_manager=True,
            sub_manager_ids=[],
            configs=[],
            domain_name="",
            domain_uid="",
            manager_name="",
        )
        manager_controller.add_manager(empty_manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_details=import_state_controller,
            config_list=manager_controller,
        )
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 0

    def test_check_zone_object_consistency_with_unresolvable_object(
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
        consistency_checker.maps = fw_config_import_object
        consistency_checker.check_zone_object_consistency(config=manager_controller)
        assert len(consistency_checker.issues) == 1
