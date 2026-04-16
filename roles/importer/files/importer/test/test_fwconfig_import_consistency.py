from model_controllers.check_consistency import FwConfigImportCheckConsistency
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.networkobject import NetworkObject
from netaddr import IPNetwork
from test.data.mock_objects import MockObjectsFactory
from test.utils.config_builder import FwConfigBuilder


class TestCheckConsistencyColors:
    def test_check_network_object_color_consistency_valid(
        self,
        import_state_controller: ImportStateController,
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

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=False)

        assert len(consistency_checker.issues) == 0

    def test_check__network_object_color_consistency_invalid_no_fix(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        nw_obj.obj_color = "nonexistent_color"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=False)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {
            "unresolvableColorRefs": {"nwObjColors": ["nonexistent_color"], "svcColors": [], "userColors": []}
        }

    def test_check_network_object_color_consistency_invalid_with_fix(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        nw_obj.obj_color = "nonexistent_color"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=True)
        configured_color = config.network_objects[nw_obj.obj_uid].obj_color

        assert len(consistency_checker.issues) == 0
        assert configured_color == "black"  # default color assigned when fix=True

    def test_check_service_object_color_consistency_valid(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        nw_obj.obj_color = "red"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=False)

        assert len(consistency_checker.issues) == 0

    def test_check__service_object_color_consistency_invalid_no_fix(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = fwconfig_builder.add_standard_service_object(config)
        svc_obj.svc_color = "nonexistent_color"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=False)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {
            "unresolvableColorRefs": {"nwObjColors": [], "svcColors": ["nonexistent_color"], "userColors": []}
        }

    def test_check_service_object_color_consistency_invalid_with_fix(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = fwconfig_builder.add_standard_service_object(config)
        svc_obj.svc_color = "nonexistent_color"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=True)
        configured_color = config.service_objects[svc_obj.svc_uid].svc_color

        assert len(consistency_checker.issues) == 0
        assert configured_color == "black"  # default color assigned when fix=True


class TestCheckConsistencyNetworkObjects:
    def test_check_network_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_color_consistency(config=config, fix=False)

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_invalid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        # Introduce an inconsistency by referencing a non-existent network object

        nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        nw_obj.obj_typ = "DoesNotExist"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableNwObjTypes": ["DoesNotExist"]}

    def test_check_network_object_consistency_with_single_host_in_group(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        fwconfig_builder.add_standard_network_group_object(config, [nw_obj])

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_with_multiple_hosts_in_group(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj1 = fwconfig_builder.add_standard_network_host_object(config)
        nw_obj2 = fwconfig_builder.add_standard_network_host_object(config)
        fwconfig_builder.add_standard_network_group_object(config, obj_members=[nw_obj1, nw_obj2])
        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_with_group_referencing_itself(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_group_obj = fwconfig_builder.add_standard_network_group_object(config, obj_members=[])
        nw_group_obj.obj_member_refs = nw_group_obj.obj_uid  # Circular reference

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"circularNwObjRefs": [nw_group_obj.obj_uid]}

    def test_check_network_object_consistency_with_two_groups_circular_reference(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        group_a = fwconfig_builder.add_standard_network_group_object(config, obj_members=[])
        group_b = fwconfig_builder.add_standard_network_group_object(config, obj_members=[])

        group_a.obj_member_refs = group_b.obj_uid
        group_b.obj_member_refs = group_a.obj_uid

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 1
        assert set(consistency_checker.issues["circularNwObjRefs"]) == {group_a.obj_uid, group_b.obj_uid}

    def test_check_network_object_consistency_with_empty_group(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        fwconfig_builder.add_standard_network_group_object(config, obj_members=[])

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 0

    def test_check_network_object_consistency_with_group_referencing_nonexistent_object(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_group_obj = fwconfig_builder.add_standard_network_group_object(config, obj_members=[])
        nw_group_obj.obj_member_refs = "NonExistentHost"

        manager_controller = FwConfigManagerListController()
        manager = MockObjectsFactory.get_standard_fwconfig_manager(config)
        manager_controller.add_manager(manager)
        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableNwObjRefs": ["NonExistentHost"]}

    def test_check_network_object_consistency_with_mixed_valid_and_invalid_references(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        valid_nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        group_obj = fwconfig_builder.add_standard_network_group_object(config, obj_members=[valid_nw_obj])
        group_obj.obj_member_refs += "|InvalidHost"  # pyright: ignore[reportOperatorIssue]

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )
        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableNwObjRefs": ["InvalidHost"]}

    def test_check_network_object_consistency_none_group_without_ip(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        nw_obj = fwconfig_builder.add_standard_network_host_object(config)
        nw_obj.obj_ip = None
        nw_obj.obj_ip_end = None

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )
        consistency_checker.check_network_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"non-group network object with undefined IP addresse(s)": [nw_obj]}


class TestCheckConsistencyServiceObjects:
    def test_check_service_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = fwconfig_builder.add_standard_service_object(config)
        fwconfig_builder.add_standard_service_group_object(config, [svc_obj])

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )

        assert len(consistency_checker.issues) == 0

    def test_check_service_object_consistency_unresolvable_object(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_group_obj = fwconfig_builder.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = "NonExistentService"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableSvcObjRefs": ["NonExistentService"]}

    def test_check_service_object_consistency_invalid_type(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = fwconfig_builder.add_standard_service_object(config)
        svc_obj.svc_typ = "DoesNotExist"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableSvcObjTypes": ["DoesNotExist"]}

    def test_check_service_object_consistency_with_multiple_members_in_group(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        fwconfig_builder.add_standard_service_object(config)
        fwconfig_builder.add_standard_service_object(config)

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 0

    def test_check_service_object_consistency_with_group_referencing_itself(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_group_obj = fwconfig_builder.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = svc_group_obj.svc_uid  # Circular reference

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"circularSvcObjRefs": [svc_group_obj.svc_uid]}

    def test_check_service_object_consistency_with_two_groups_circular_reference(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        group_a = fwconfig_builder.add_standard_service_group_object(config)
        group_b = fwconfig_builder.add_standard_service_group_object(config)

        group_a.svc_member_refs = group_b.svc_uid
        group_b.svc_member_refs = group_a.svc_uid

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 1
        assert set(consistency_checker.issues["circularSvcObjRefs"]) == {group_a.svc_uid, group_b.svc_uid}

    def test_check_service_object_consistency_with_empty_group(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_group_obj = fwconfig_builder.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = ""

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 0

    def test_check_service_object_consistency_with_mixed_valid_and_invalid_references(
        self,
        import_state_controller: ImportStateController,
        fwconfig_builder: FwConfigBuilder,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        svc_obj = fwconfig_builder.add_standard_service_object(config)

        svc_group_obj = fwconfig_builder.add_standard_service_group_object(config)
        svc_group_obj.svc_member_refs = f"{svc_obj.svc_uid}|InvalidService"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableSvcObjRefs": ["InvalidService"]}


class TestCheckUserObjectConsistency:
    def test_check_user_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=1,
            user_group_object_count=2,
            user_group_object_member_count=2,
        )

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )
        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 0

    def test_check_user_object_unresolvable_object(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
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

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_user_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableUserObjRefs": ["DoesNotExist"]}

    def test_check_user_object_circular_reference(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=1,
            user_group_object_count=2,
            user_group_object_member_count=2,
        )

        group = {
            "user_typ": "group",
            "user_uid": "GroupWithCircularRef",
            "user_name": "GroupWithCircularRef",
            "user_member_names": "GroupWithCircularRef",
            "user_member_refs": "GroupWithCircularRef",
        }

        config.users["GroupWithCircularRef"] = group

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_user_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"circularUserObjRefs": ["GroupWithCircularRef"]}

    def test_check_user_object_with_two_groups_circular_reference(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=1,
            user_group_object_count=2,
            user_group_object_member_count=2,
        )

        group_a = {
            "user_typ": "group",
            "user_uid": "GroupA",
            "user_name": "GroupA",
            "user_member_names": "GroupB",
            "user_member_refs": "GroupB",
        }

        group_b = {
            "user_typ": "group",
            "user_uid": "GroupB",
            "user_name": "GroupB",
            "user_member_names": "GroupA",
            "user_member_refs": "GroupA",
        }

        config.users["GroupA"] = group_a
        config.users["GroupB"] = group_b

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_user_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 1
        assert set(consistency_checker.issues["circularUserObjRefs"]) == {"GroupA", "GroupB"}


class TestRulebaseConsistency:
    def test_check_rulebase_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_service_object_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )
        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_consistency_with_empty_config(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        empty_config = fwconfig_builder.build_empty_config()
        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_rulebase_consistency(config=empty_config, fix_inconsistencies=False)

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_consistency_with_unresolvable_tracks(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        rule_uid = next(iter(config.rulebases[0].rules.keys()))
        config.rulebases[0].rules[rule_uid].rule_track = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_rulebase_consistency(config=config, fix_inconsistencies=False)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableRuleTracks": ["NonExistent"]}

    def test_check_rulebase_consistency_with_unresolvable_actions(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        rule_uid = next(iter(config.rulebases[0].rules.keys()))
        config.rulebases[0].rules[rule_uid].rule_action = "NonExistent"  # pyright: ignore[reportAttributeAccessIssue]

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_rulebase_consistency(config=config, fix_inconsistencies=False)

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableRuleActions": ["NonExistent"]}


class TestRulebaseLinkConsistency:
    def test_check_rulebase_link_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_rulebase_link_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )

        assert len(consistency_checker.issues) == 0

    def test_check_rulebase_link_consistency_with_broken_links(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )
        config.gateways[0].RulebaseLinks[0].from_rule_uid = "NonExistentRuleUid"
        config.gateways[0].RulebaseLinks[0].from_rulebase_uid = "NonExistentFromRulebaseUID"
        config.gateways[0].RulebaseLinks[0].to_rulebase_uid = "NonExistentToRulebaseUID"

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_rulebase_link_consistency(
            config=config, global_config=None, fix_inconsistencies=False
        )

        assert len(consistency_checker.issues) == 2
        assert len(consistency_checker.issues["unresolvableRulebaseLinksRulebases"]) == 2
        assert len(consistency_checker.issues["unresolvableRulebaseLinksRules"]) == 1


class TestZoneObjectConsistency:
    def test_check_zone_object_consistency_valid(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
        )

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )
        consistency_checker.check_zone_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )
        assert len(consistency_checker.issues) == 0

    def test_check_zone_object_consistency_with_unresolvable_object_src(
        self,
        import_state_controller: ImportStateController,
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

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )
        consistency_checker.check_zone_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableZoneObjRefs": ["NonExistent"]}

    def test_check_zone_object_consistency_with_unresolvable_object_dst(
        self,
        import_state_controller: ImportStateController,
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

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )
        consistency_checker.check_zone_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 1
        assert consistency_checker.issues == {"unresolvableZoneObjRefs": ["NonExistent"]}


class TestFullConfigConsistencyCheck:
    def test_full_config_consistency_check(
        self,
        fwconfig_builder: FwConfigBuilder,
        import_state_controller: ImportStateController,
    ):
        config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_group_object_count=2,
            user_group_object_member_count=2,
            user_object_count=5,
        )

        consistency_checker = FwConfigImportCheckConsistency(
            import_state=import_state_controller.state,
        )

        consistency_checker.check_zone_object_consistency(
            config=config, global_config=None, fix_unresolvable_refs=False
        )

        assert len(consistency_checker.issues) == 0
