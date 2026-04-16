import copy
from typing import Any

import pytest
from fwo_api_call import FwoApiCall
from fwo_const import LIST_DELIMITER
from fwo_exceptions import FwoDuplicateKeyViolationError, FwoImporterError
from fwo_log import ChangeLogger, FWOLogger
from model_controllers.fwconfig_import_object import FwConfigImportObject, Type
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
from pytest_mock import MockerFixture
from services.uid2id_mapper import Uid2IdMapper
from states.global_state import GlobalState
from states.import_state import ImportState
from states.management_state import ManagementState
from test.data.mock_objects import MockObjectsFactory
from test.utils.config_builder import FwConfigBuilder
from test.utils.test_utils import mock_get_graphql_code


class TestFwConfigImportObjectAddChangelogObjs:
    def test_add_changelog_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        global_state: GlobalState,
        import_state: ImportState,
    ):
        # Arrange
        api_call.call = mocker.Mock(
            return_value=MockObjectsFactory.get_standard_changelog_return_value(),
        )

        # Act
        fwconfig_import_object.add_changelog_objs(
            global_state=global_state,
            import_state=import_state,
            nwobj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        expected_change_type_id = (
            2 if import_state.is_initial_import or global_state.fwo_config_controller.fwo_config.clear else 3
        )
        api_call.call.assert_any_call(
            mocker.ANY,
            query_variables={
                "nwObjChanges": MockObjectsFactory.get_changelog_object_insert_delete(expected_change_type_id),
                "svcObjChanges": MockObjectsFactory.get_changelog_svc_objects_insert_delete(expected_change_type_id),
            },
            analyze_payload=True,
        )

    def test_add_changelog_objects_with_errors(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        global_state: GlobalState,
        import_state: ImportState,
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(return_value={"errors": [{"message": "Some error occurred"}]})

        # Act
        fwconfig_import_object.add_changelog_objs(
            global_state=global_state,
            import_state=import_state,
            nwobj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        mock_logger.assert_called_once_with(
            "error while adding changelog entries for objects: [{'message': 'Some error occurred'}]"
        )

    def test_add_changelog_objects_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        global_state: GlobalState,
        import_state: ImportState,
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(side_effect=Exception("API call failed"))

        # Act
        fwconfig_import_object.add_changelog_objs(
            global_state=global_state,
            import_state=import_state,
            nwobj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert mock_logger.call_count == 1
        assert str(mock_logger.call_args[0][0]).startswith(
            "fatal error while adding changelog entries for objects: Traceback (most recent call last):"
        )


class TestFwConfigImportObjectPrepareChangelogObjects:
    def test_prepare_changelog_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        global_state: GlobalState,
        import_state: ImportState,
    ):
        # Arrange
        expected_change_type_id = (
            2 if import_state.is_initial_import or global_state.fwo_config_controller.fwo_config.clear else 3
        )

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            global_state=global_state,
            import_state=import_state,
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete(expected_change_type_id)
        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete(expected_change_type_id)

    def test_prepare_changelog_objects_initial_import(
        self, fwconfig_import_object: FwConfigImportObject, global_state: GlobalState, import_state: ImportState
    ):
        # Arrange
        import_state.is_initial_import = True

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            global_state=global_state,
            import_state=import_state,
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert

        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete(2)
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete(2)

    def test_prepare_changelog_objects_clearing_import(
        self, fwconfig_import_object: FwConfigImportObject, global_state: GlobalState, import_state: ImportState
    ):
        # Arrange
        global_state.fwo_config_controller.fwo_config.clear = True

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            global_state=global_state,
            import_state=import_state,
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete(2)
        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete(2)

    def test_prepare_changelog_objects_with_logged_changes(
        self, fwconfig_import_object: FwConfigImportObject, global_state: GlobalState, import_state: ImportState
    ):
        # Arrange
        change_logger = ChangeLogger()
        change_logger.changed_object_id_map = {1: 10}
        change_logger.changed_service_id_map = {3: 30}
        expected_change_type_id = (
            2 if import_state.is_initial_import or global_state.fwo_config_controller.fwo_config.clear else 3
        )

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            global_state=global_state,
            import_state=import_state,
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete_change(expected_change_type_id)
        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete_change(
            expected_change_type_id
        )


class TestFwConfigImportObjectLookupProtoNameToId:
    def test_lookup_proto_name_to_id_unknown_name(
        self,
        import_state: ImportState,
    ):
        # Arrange
        proto_name = "tcp"

        # Act and Assert
        with pytest.raises(FwoImporterError):
            import_state.lookup_protocol_id(proto_name)

    def test_lookup_proto_name_to_id_known_name(
        self,
        import_state: ImportState,
    ):
        # Arrange
        proto_name = "icmp"
        expected_proto_id = 1
        import_state.protocol_map = {
            "icmp": 1,
            "tcp": 6,
            "udp": 17,
        }

        # Act
        proto_id = import_state.lookup_protocol_id(proto_name)
        # Assert
        assert proto_id == expected_proto_id


class TestFwConfigImportObjectLookupUserType:
    def test_lookup_user_type_unknown(
        self,
        import_state: ImportState,
    ):
        # Arrange
        user_type_str = "some-user-type"

        # Act and Assert
        with pytest.raises(FwoImporterError):
            import_state.lookup_user_obj_type_id(user_type_str)

    def test_lookup_user_type_known(
        self,
        import_state: ImportState,
    ):
        # Arrange
        user_type_str = "imported"
        expected_user_type = 2
        import_state.user_obj_type_map = {
            "admin": 1,
            "imported": 2,
            "readonly": 3,
        }

        # Act
        user_type = import_state.lookup_user_obj_type_id(user_type_str)

        # Assert
        assert user_type == expected_user_type


class TestFwConfigImportObjectLookupSvcType:
    def test_lookup_svc_type_unknown(
        self,
        import_state: ImportState,
    ):
        # Arrange
        svc_type_str = "some-svc-type"

        # Act and Assert
        with pytest.raises(FwoImporterError):
            import_state.lookup_service_obj_type_id(svc_type_str)

    def test_lookup_svc_type_known(
        self,
        import_state: ImportState,
    ):
        # Arrange
        svc_type_str = "imported"
        expected_svc_type = 2
        import_state.service_obj_type_map = {
            "builtin": 1,
            "imported": 2,
            "custom": 3,
        }

        # Act
        svc_type = import_state.lookup_service_obj_type_id(svc_type_str)

        # Assert
        assert svc_type == expected_svc_type


class TestFwConfigImportObjectWriteMemberUpdates:
    def test_write_member_updates(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        import_state: ImportState,
    ):
        # Arrange
        prefix = "nwobj"
        api_call.call = mocker.Mock(
            return_value={
                "data": {
                    f"insert_{prefix}": {
                        "affected_rows": 1,
                    },
                    f"insert_{prefix}_flat": {
                        "affected_rows": 1,
                    },
                }
            }
        )

        # Act
        fwconfig_import_object.write_member_updates(
            import_state=import_state,
            new_group_member_flats=[
                {
                    "admin": 1,
                }
            ],
            new_group_members=[
                {
                    "admin": 1,
                }
            ],
            prefix=prefix,
        )

        # Assert
        api_call.call.assert_any_call(
            mocker.ANY,
            query_variables={
                "groups": [
                    {
                        "admin": 1,
                    }
                ],
                "groupFlats": [
                    {
                        "admin": 1,
                    }
                ],
            },
            analyze_payload=True,
        )

    def test_write_member_updates_no_updates(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        import_state: ImportState,
    ):
        # Arrange
        prefix = "nwobj"
        api_call.call = mocker.Mock(
            return_value={
                "data": {
                    f"insert_{prefix}": {
                        "affected_rows": 0,
                    },
                    f"insert_{prefix}_flat": {
                        "affected_rows": 0,
                    },
                }
            }
        )

        # Act
        fwconfig_import_object.write_member_updates(
            import_state=import_state,
            new_group_member_flats=[],
            new_group_members=[],
            prefix=prefix,
        )

        # Assert
        api_call.call.assert_any_call(
            mocker.ANY,
            query_variables={
                "groups": [],
                "groupFlats": [],
            },
            analyze_payload=True,
        )

    def test_write_member_updates_with_error(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        import_state: ImportState,
    ):
        # Arrange
        prefix = "nwobj"
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(
            return_value={
                "errors": [
                    {
                        "message": "Some error occurred",
                    }
                ]
            }
        )

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.write_member_updates(
                import_state=import_state,
                new_group_member_flats=[],
                new_group_members=[],
                prefix=prefix,
            )

        # Assert
        assert mock_logger.call_count == 2
        assert (
            mock_logger.call_args_list[0][0][0] == "fwo_api:addGroupMemberships: [{'message': 'Some error occurred'}]"
        )
        assert str(mock_logger.call_args_list[1][0][0]).startswith("failed to write new objects: Traceback")

    def test_write_member_updates_with_duplicate_error(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        import_state: ImportState,
    ):
        # Arrange
        prefix = "nwobj"
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(
            return_value={
                "errors": [
                    "duplicate",
                ]
            }
        )

        # Act
        with pytest.raises(FwoDuplicateKeyViolationError):
            fwconfig_import_object.write_member_updates(
                import_state=import_state,
                new_group_member_flats=[],
                new_group_members=[],
                prefix=prefix,
            )

        # Assert
        assert mock_logger.call_count == 2
        assert mock_logger.call_args_list[0][0][0] == "fwo_api:addGroupMemberships: ['duplicate']"
        assert str(mock_logger.call_args_list[1][0][0]).startswith("failed to write new objects: Traceback")
        assert "duplicate" in str(mock_logger.call_args_list[1][0][0])

    def test_write_member_updates_with_wrong_return_format(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
        import_state: ImportState,
    ):
        # Arrange
        prefix = "nwobj"
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(return_value={"data": []})

        # Act
        with pytest.raises(TypeError):
            fwconfig_import_object.write_member_updates(
                import_state=import_state,
                new_group_member_flats=[],
                new_group_members=[],
                prefix=prefix,
            )

        # Assert
        assert mock_logger.call_count == 1
        assert str(mock_logger.call_args_list[0][0][0]).startswith("failed to write new objects: Traceback")
        assert "TypeError" in str(mock_logger.call_args_list[0][0][0])


class TestFwConfigImportObjectCollectGroupMembers:
    def test_collect_group_members_empty(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        new_group_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_group_members(
            import_state=import_state,
            management_state=management_state,
            current_config_objects={},
            group_id=1,
            prefix="nwobj",
            member_uids=[],
            new_group_members=new_group_members,
            obj_type=Type.NETWORK_OBJECT,
            prev_config_objects={},
            prev_member_uids=[],
        )

        # Assert
        assert new_group_members == []

    def test_collect_group_members_no_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        new_group_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_group_members(
            import_state=import_state,
            management_state=management_state,
            current_config_objects={"1": {"id": 1, "uid": "1"}},
            group_id=1,
            prefix="nwobj",
            member_uids=["1"],
            new_group_members=new_group_members,
            obj_type=Type.NETWORK_OBJECT,
            prev_config_objects={"1": {"id": 1, "uid": "1"}},
            prev_member_uids=["1"],
        )

        # Assert
        assert new_group_members == []

    def test_collect_group_members_with_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        prefix = "nwobj"
        new_group_members: list[dict[str, Any]] = []
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=2)
        import_state.import_id = 5

        # Act
        fwconfig_import_object.collect_group_members(
            management_state=management_state,
            import_state=import_state,
            current_config_objects={
                "1": {"id": 1, "uid": "1"},
                "2": {"id": 2, "uid": "2"},
            },
            group_id=1,
            prefix=prefix,
            member_uids=["1", "2"],
            new_group_members=new_group_members,
            obj_type=Type.NETWORK_OBJECT,
            prev_config_objects={
                "1": {"id": 1, "uid": "1"},
                "3": {"id": 3, "uid": "3"},
            },
            prev_member_uids=["1", "3"],
        )

        # Assert
        assert new_group_members == [
            {f"{prefix}_id": 1, f"{prefix}_member_id": 2, "import_created": 5, "import_last_seen": 5}
        ]


class TestFwConfigImportObjectCollectFlatGroupMembers:
    def test_collect_flat_group_members_empty(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        new_group_member_flats: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_flat_group_members(
            management_state=management_state,
            import_state=import_state,
            current_config_objects={},
            group_id=1,
            prefix="nwobj",
            new_group_member_flats=new_group_member_flats,
            obj_type=Type.NETWORK_OBJECT,
            prev_config_objects={},
            flat_member_uids=[],
            prev_flat_member_uids=[],
        )

        # Assert
        assert new_group_member_flats == []

    def test_collect_flat_group_members_no_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        new_group_member_flats: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_flat_group_members(
            management_state=management_state,
            import_state=import_state,
            current_config_objects={"1": {"id": 1, "uid": "1"}},
            group_id=1,
            prefix="nwobj",
            new_group_member_flats=new_group_member_flats,
            obj_type=Type.NETWORK_OBJECT,
            prev_config_objects={"1": {"id": 1, "uid": "1"}},
            flat_member_uids=["1"],
            prev_flat_member_uids=["1"],
        )

        # Assert
        assert new_group_member_flats == []

    def test_collect_flat_group_members_with_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        prefix = "nwobj"
        new_group_member_flats: list[dict[str, Any]] = []
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=2)
        import_state.import_id = 5

        # Act
        fwconfig_import_object.collect_flat_group_members(
            management_state=management_state,
            import_state=import_state,
            current_config_objects={
                "1": {"id": 1, "uid": "1"},
                "2": {"id": 2, "uid": "2"},
            },
            group_id=1,
            prefix=prefix,
            new_group_member_flats=new_group_member_flats,
            obj_type=Type.NETWORK_OBJECT,
            prev_config_objects={
                "1": {"id": 1, "uid": "1"},
                "3": {"id": 3, "uid": "3"},
            },
            flat_member_uids=["1", "2"],
            prev_flat_member_uids=["1", "3"],
        )

        # Assert
        assert new_group_member_flats == [
            {f"{prefix}_flat_id": 1, f"{prefix}_flat_member_id": 2, "import_created": 5, "import_last_seen": 5}
        ]


class TestFwConfigImportObjectAddGroupMemberships:
    def test_add_group_memberships_no_changes(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        management_state.normalized_config = FwConfigNormalized()
        new_group_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=FwConfigNormalized(),
        )

        # Assert
        assert new_group_members == []

    def test_add_group_memberships_not_a_group(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
        mocker: MockerFixture,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=1,
        )
        management_state.previous_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=1,
        )
        fwconfig_import_object.write_member_updates = mocker.Mock()

        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_typ = "not-a-group"
        for obj in management_state.previous_config.network_objects.values():
            obj.obj_typ = "not-a-group"

        # Act
        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=management_state.previous_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_not_called()

    def test_add_group_memberships_no_group_changes(
        self,
        import_state: ImportState,
        management_state: ManagementState,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=1,
        )
        management_state.previous_config = copy.deepcopy(management_state.normalized_config)
        fwconfig_import_object.write_member_updates = mocker.Mock()

        # Act
        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=management_state.previous_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_not_called()

    def test_add_group_memberships_with_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
        mocker: MockerFixture,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=1,
        )
        import_state.import_id = 5
        management_state.previous_config = copy.deepcopy(management_state.normalized_config)
        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        fwconfig_import_object.write_member_updates = mocker.Mock()
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=["old-member-uid"])

        # Act
        management_state.group_flats_mapper.init_config(management_state.normalized_config, import_state.super_config)

        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=management_state.previous_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
            import_state,
            [
                {
                    "import_created": 5,
                    "import_last_seen": 5,
                    "objgrp_id": 1,
                    "objgrp_member_id": 1,
                },
            ],
            [
                {
                    "import_created": 5,
                    "import_last_seen": 5,
                    "objgrp_flat_id": 1,
                    "objgrp_flat_member_id": 1,
                }
            ],
            "objgrp",
        )

    def test_add_group_memberships_self_reference_group(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=1,
            service_object_count=0,
            include_gateway=False,
        )
        management_state.previous_config = fwconfig_builder.build_empty_config(management_state.uid2id_mapper)
        group_obj = next(iter(management_state.normalized_config.network_objects.values()))
        group_obj.obj_member_refs = group_obj.obj_uid
        import_state.import_id = 7
        fwconfig_import_object.write_member_updates = mocker.Mock()
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        management_state.group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[group_obj.obj_uid])

        # Act
        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=management_state.previous_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
            import_state,
            [
                {
                    "import_created": 7,
                    "import_last_seen": 7,
                    "objgrp_id": 1,
                    "objgrp_member_id": 1,
                }
            ],
            [
                {
                    "import_created": 7,
                    "import_last_seen": 7,
                    "objgrp_flat_id": 1,
                    "objgrp_flat_member_id": 1,
                }
            ],
            "objgrp",
        )

    def test_add_group_memberships_member_changed(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = copy.deepcopy(management_state.normalized_config)
        group_uid, member_uid = list(management_state.normalized_config.network_objects.keys())[:2]
        management_state.normalized_config.network_objects[group_uid].obj_member_refs = member_uid
        prev_config.network_objects[group_uid].obj_member_refs = member_uid
        management_state.normalized_config.network_objects[member_uid].obj_typ = "host"
        prev_config.network_objects[member_uid].obj_typ = "host"
        management_state.normalized_config.network_objects[member_uid].obj_name = "changed-member"

        def fake_get_id(uid: str, _before_update: bool = False):
            return 1 if uid == group_uid else 2

        import_state.import_id = 9
        fwconfig_import_object.write_member_updates = mocker.Mock()
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        management_state.group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[member_uid])
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[member_uid])

        # Act
        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
            import_state,
            [
                {
                    "import_created": 9,
                    "import_last_seen": 9,
                    "objgrp_id": 1,
                    "objgrp_member_id": 2,
                }
            ],
            [
                {
                    "import_created": 9,
                    "import_last_seen": 9,
                    "objgrp_flat_id": 1,
                    "objgrp_flat_member_id": 2,
                }
            ],
            "objgrp",
        )

    def test_add_group_memberships_group_changed_adds_all_members_and_flats(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=3,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = copy.deepcopy(management_state.normalized_config)
        group_uid, member_uid_one, member_uid_two = list(management_state.normalized_config.network_objects.keys())[:3]
        management_state.normalized_config.network_objects[
            group_uid
        ].obj_member_refs = f"{member_uid_one}{LIST_DELIMITER}{member_uid_two}"
        prev_config.network_objects[group_uid].obj_member_refs = member_uid_one
        management_state.normalized_config.network_objects[member_uid_one].obj_typ = "host"
        management_state.normalized_config.network_objects[member_uid_two].obj_typ = "host"
        prev_config.network_objects[member_uid_one].obj_typ = "host"
        prev_config.network_objects[member_uid_two].obj_typ = "host"

        def fake_get_id(uid: str, _before_update: bool = False):
            mapping = {
                group_uid: 1,
                member_uid_one: 2,
                member_uid_two: 3,
            }
            return mapping[uid]

        import_state.import_id = 11
        fwconfig_import_object.write_member_updates = mocker.Mock()
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        management_state.group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=[member_uid_one, member_uid_two]
        )

        # Act
        fwconfig_import_object.add_group_memberships(
            import_state=import_state,
            management_state=management_state,
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
            import_state,
            [
                {
                    "import_created": 11,
                    "import_last_seen": 11,
                    "objgrp_id": 1,
                    "objgrp_member_id": 2,
                },
                {
                    "import_created": 11,
                    "import_last_seen": 11,
                    "objgrp_id": 1,
                    "objgrp_member_id": 3,
                },
            ],
            [
                {
                    "import_created": 11,
                    "import_last_seen": 11,
                    "objgrp_flat_id": 1,
                    "objgrp_flat_member_id": 2,
                },
                {
                    "import_created": 11,
                    "import_last_seen": 11,
                    "objgrp_flat_id": 1,
                    "objgrp_flat_member_id": 3,
                },
            ],
            "objgrp",
        )


class TestFwConfigImportObjectFindRemovedObjects:
    def test_find_removed_objects_not_a_group(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        management_state.previous_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=3,
        )
        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_typ = "not-a-group"
        for obj in management_state.previous_config.network_objects.values():
            obj.obj_typ = "not-a-group"
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.find_removed_objects(
            management_state=management_state,
            current_config_objects=management_state.normalized_config.network_objects,
            prev_config_objects=management_state.previous_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(management_state.previous_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == []
        assert removed_members == []

    def test_find_removed_objects_no_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        management_state.previous_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)

        # Act
        fwconfig_import_object.find_removed_objects(
            management_state=management_state,
            current_config_objects=management_state.normalized_config.network_objects,
            prev_config_objects=management_state.previous_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(management_state.previous_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == []
        assert removed_members == []

    def test_find_removed_objects_with_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        management_state.previous_config = copy.deepcopy(management_state.normalized_config)
        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        for obj in management_state.previous_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])

        # Act
        fwconfig_import_object.find_removed_objects(
            management_state=management_state,
            current_config_objects=management_state.normalized_config.network_objects,
            prev_config_objects=management_state.previous_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(management_state.previous_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == [
            {
                "_and": [
                    {
                        "nwobj_flat_id": {
                            "_eq": 1,
                        },
                    },
                    {
                        "nwobj_flat_member_id": {
                            "_eq": 1,
                        },
                    },
                ]
            },
        ]
        assert removed_members == [
            {
                "_and": [
                    {
                        "nwobj_id": {
                            "_eq": 1,
                        },
                    },
                    {
                        "nwobj_member_id": {
                            "_eq": 1,
                        },
                    },
                ]
            },
        ]

    def test_find_removed_objects_no_member_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        management_state.previous_config = copy.deepcopy(management_state.normalized_config)
        last_obj_uid = list(management_state.normalized_config.network_objects.keys())[-1]
        for obj in list(management_state.normalized_config.network_objects.values())[:-1]:
            obj.obj_member_refs = last_obj_uid
        for obj in list(management_state.previous_config.network_objects.values())[:-1]:
            obj.obj_member_refs = last_obj_uid
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=last_obj_uid)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[last_obj_uid])

        management_state.group_flats_mapper.init_config(management_state.normalized_config, None)

        # Act
        fwconfig_import_object.find_removed_objects(
            management_state=management_state,
            current_config_objects=management_state.normalized_config.network_objects,
            prev_config_objects=management_state.previous_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(management_state.previous_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == []
        assert removed_members == []


class TestFwConfigImportObjectRemoveOutdatedMemberships:
    def test_remove_outdated_memberships_no_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = FwConfigBuilder().build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        prev_config = copy.deepcopy(management_state.normalized_config)
        global_state.fwo_api_call.call = mocker.Mock()
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            import_state=import_state,
            management_state=management_state,
            fwo_api_call=global_state.fwo_api_call,
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        global_state.fwo_api_call.call.assert_not_called()

    def test_remove_outdated_memberships_with_changes_and_success(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        prev_config = copy.deepcopy(management_state.normalized_config)
        for obj in prev_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"
        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"

        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "update_objgrp_flat": {
                        "affected_rows": 1,
                    },
                    "update_objgrp": {
                        "affected_rows": 1,
                    },
                }
            }
        )
        import_state.import_id = 5

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            import_state=import_state,
            management_state=management_state,
            fwo_api_call=global_state.fwo_api_call,
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        global_state.fwo_api_call.call.assert_called_once()
        call_args = global_state.fwo_api_call.call.call_args
        assert call_args.kwargs["query_variables"] == {
            "importId": 5,
            "removedMembers": [
                {"_and": [{"objgrp_id": {"_eq": 1}}, {"objgrp_member_id": {"_eq": 1}}]},
                {"_and": [{"objgrp_id": {"_eq": 1}}, {"objgrp_member_id": {"_eq": 1}}]},
            ],
            "removedFlats": [
                {"_and": [{"objgrp_flat_id": {"_eq": 1}}, {"objgrp_flat_member_id": {"_eq": 1}}]},
                {"_and": [{"objgrp_flat_id": {"_eq": 1}}, {"objgrp_flat_member_id": {"_eq": 1}}]},
            ],
        }

    def test_remove_outdated_memberships_with_changes_and_errors(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        management_state.previous_config = copy.deepcopy(management_state.normalized_config)
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        for obj in management_state.previous_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"

        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "errors": [
                    {
                        "message": "Some error occurred",
                    }
                ]
            }
        )
        import_state.import_id = 5
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            import_state=import_state,
            management_state=management_state,
            prev_config=management_state.previous_config,
            typ=Type.NETWORK_OBJECT,
            fwo_api_call=global_state.fwo_api_call,
        )

        # Assert
        global_state.fwo_api_call.call.assert_called_once()
        mock_logger.assert_called_once()
        assert (
            mock_logger.call_args[0][0]
            == "fwo_api:importNwObject - error in removeOutdatedObjgrpMemberships: [{'message': 'Some error occurred'}]"
        )

    def test_remove_outdated_memberships_with_changes_and_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
        )
        management_state.previous_config = copy.deepcopy(management_state.normalized_config)
        for obj in management_state.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        for obj in management_state.previous_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "error": "Some unexpected error occurred",
            }
        )
        import_state.import_id = 5
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            import_state=import_state,
            management_state=management_state,
            prev_config=management_state.previous_config,
            typ=Type.NETWORK_OBJECT,
            fwo_api_call=global_state.fwo_api_call,
        )

        # Assert
        global_state.fwo_api_call.call.assert_called_once()
        mock_logger.assert_called_once()
        assert str(mock_logger.call_args[0][0]).startswith(
            "failed to remove outdated group memberships for Type.NETWORK_OBJECT: Traceback"
        )

    def test_remove_outdated_memberships_group_removed(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.previous_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=2,
            service_object_count=0,
            include_gateway=False,
        )
        group_uid, member_uid = list(management_state.previous_config.network_objects.keys())[:2]
        management_state.previous_config.network_objects[group_uid].obj_member_refs = member_uid
        management_state.previous_config.network_objects[member_uid].obj_typ = "host"
        management_state.normalized_config = FwConfigNormalized()

        def fake_get_id(uid: str, _before_update: bool = False):
            return 10 if uid == group_uid else 20

        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[member_uid])
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "update_objgrp": {"affected_rows": 1},
                    "update_objgrp_flat": {"affected_rows": 1},
                }
            }
        )
        import_state.import_id = 6

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            import_state=import_state,
            management_state=management_state,
            prev_config=management_state.previous_config,
            typ=Type.NETWORK_OBJECT,
            fwo_api_call=global_state.fwo_api_call,
        )

        # Assert
        call_args = global_state.fwo_api_call.call.call_args
        assert call_args.kwargs["query_variables"] == {
            "importId": 6,
            "removedMembers": [{"_and": [{"objgrp_id": {"_eq": 10}}, {"objgrp_member_id": {"_eq": 20}}]}],
            "removedFlats": [{"_and": [{"objgrp_flat_id": {"_eq": 10}}, {"objgrp_flat_member_id": {"_eq": 20}}]}],
        }

    def test_remove_outdated_memberships_group_changed_removes_all_prev_members_and_flats(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
        global_state: GlobalState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=3,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = copy.deepcopy(management_state.normalized_config)
        group_uid, member_uid_one, member_uid_two = list(prev_config.network_objects.keys())[:3]
        prev_config.network_objects[group_uid].obj_member_refs = f"{member_uid_one}{LIST_DELIMITER}{member_uid_two}"
        management_state.normalized_config.network_objects[group_uid].obj_member_refs = member_uid_one
        prev_config.network_objects[member_uid_one].obj_typ = "host"
        prev_config.network_objects[member_uid_two].obj_typ = "host"
        management_state.normalized_config.network_objects[member_uid_one].obj_typ = "host"
        management_state.normalized_config.network_objects[member_uid_two].obj_typ = "host"

        def fake_get_id(uid: str, _before_update: bool = False):
            mapping = {
                group_uid: 100,
                member_uid_one: 200,
                member_uid_two: 300,
            }
            return mapping[uid]

        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=[member_uid_one, member_uid_two]
        )
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "update_objgrp": {"affected_rows": 2},
                    "update_objgrp_flat": {"affected_rows": 2},
                }
            }
        )
        import_state.import_id = 12

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            import_state=import_state,
            management_state=management_state,
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
            fwo_api_call=global_state.fwo_api_call,
        )

        # Assert
        call_args = global_state.fwo_api_call.call.call_args
        assert call_args.kwargs["query_variables"] == {
            "importId": 12,
            "removedMembers": [
                {"_and": [{"objgrp_id": {"_eq": 100}}, {"objgrp_member_id": {"_eq": 200}}]},
                {"_and": [{"objgrp_id": {"_eq": 100}}, {"objgrp_member_id": {"_eq": 300}}]},
            ],
            "removedFlats": [
                {"_and": [{"objgrp_flat_id": {"_eq": 100}}, {"objgrp_flat_member_id": {"_eq": 200}}]},
                {"_and": [{"objgrp_flat_id": {"_eq": 100}}, {"objgrp_flat_member_id": {"_eq": 300}}]},
            ],
        }


class TestFwConfigImportObjectGetPrefix:
    def test_get_prefix_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        expected_prefix = "objgrp"

        # Act
        prefix = fwconfig_import_object.get_prefix(Type.NETWORK_OBJECT)

        # Assert
        assert prefix == expected_prefix

    def test_get_prefix_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        expected_prefix = "svcgrp"

        # Act
        prefix = fwconfig_import_object.get_prefix(Type.SERVICE_OBJECT)

        # Assert
        assert prefix == expected_prefix

    def test_get_prefix_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        expected_prefix = "usergrp"

        # Act
        prefix = fwconfig_import_object.get_prefix(Type.USER)

        # Assert
        assert prefix == expected_prefix


class TestFwConfigImportObjectGetPrevFlats:
    def test_get_prev_flats_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[1, 2, 3])

        # Act
        flats = fwconfig_import_object.get_prev_flats(management_state, Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert flats == [1, 2, 3]

    def test_get_prev_flats_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.prev_group_flats_mapper.get_service_object_flats = mocker.Mock(return_value=[4, 5, 6])

        # Act
        flats = fwconfig_import_object.get_prev_flats(management_state, Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert flats == [4, 5, 6]

    def test_get_prev_flats_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.prev_group_flats_mapper.get_user_flats = mocker.Mock(return_value=[7, 8, 9])

        # Act
        flats = fwconfig_import_object.get_prev_flats(management_state, Type.USER, "some-uid")

        # Assert
        assert flats == [7, 8, 9]


class TestFwConfigImportObjectGetFlats:
    def test_get_flats_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[1, 2, 3])

        # Act
        flats = fwconfig_import_object.get_flats(management_state, Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert flats == [1, 2, 3]

    def test_get_flats_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.group_flats_mapper.get_service_object_flats = mocker.Mock(return_value=[4, 5, 6])

        # Act
        flats = fwconfig_import_object.get_flats(management_state, Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert flats == [4, 5, 6]

    def test_get_flats_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.group_flats_mapper.get_user_flats = mocker.Mock(return_value=[7, 8, 9])

        # Act
        flats = fwconfig_import_object.get_flats(management_state, Type.USER, "some-uid")

        # Assert
        assert flats == [7, 8, 9]


class TestFwConfigImportObjectGetMembers:
    def test_get_members_network_object_empty(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Act
        members = fwconfig_import_object.get_members(Type.NETWORK_OBJECT, "")

        # Assert
        assert members == []

    def test_get_members_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Act
        members = fwconfig_import_object.get_members(
            Type.NETWORK_OBJECT, "some-uid" + LIST_DELIMITER + "some-user@another-uid"
        )

        # Assert
        assert members == [
            "some-uid",
            "some-user",
        ]

    def test_get_members_service_object_empty(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Act
        members = fwconfig_import_object.get_members(Type.SERVICE_OBJECT, "")

        # Assert
        assert members == []

    def test_get_members_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Act
        members = fwconfig_import_object.get_members(
            Type.SERVICE_OBJECT, "some-uid" + LIST_DELIMITER + "some-user@another-uid"
        )

        # Assert
        assert members == [
            "some-uid",
            "some-user@another-uid",
        ]


class TestFwConfigImportObjectGetRefs:
    def test_get_refs_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper, network_object_count=1)
        obj = next(iter(config.network_objects.values()))
        obj.obj_member_refs = "some-uid" + LIST_DELIMITER + "some-user@another-uid"

        # Act
        refs = fwconfig_import_object.get_refs(Type.NETWORK_OBJECT, obj)

        # Assert
        assert refs == "some-uid" + LIST_DELIMITER + "some-user@another-uid"

    def test_get_refs_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper, service_object_count=1)
        obj = next(iter(config.service_objects.values()))
        obj.svc_member_refs = "some-uid" + LIST_DELIMITER + "some-user@another-uid"

        # Act
        refs = fwconfig_import_object.get_refs(Type.SERVICE_OBJECT, obj)

        # Assert
        assert refs == "some-uid" + LIST_DELIMITER + "some-user@another-uid"

    def test_get_refs_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Act
        refs = fwconfig_import_object.get_refs(
            Type.USER, {"user_member_refs": "some-uid" + LIST_DELIMITER + "some-user@another-uid"}
        )

        # Assert
        assert refs == "some-uid" + LIST_DELIMITER + "some-user@another-uid"


class TestFwConfigImportObjectIsGroup:
    def test_is_group_true_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper, network_object_count=1)
        obj = next(iter(config.network_objects.values()))
        obj.obj_typ = "group"

        # Act
        is_group = fwconfig_import_object.is_group(Type.NETWORK_OBJECT, obj)

        # Assert
        assert is_group is True

    def test_is_group_false_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper, network_object_count=1)
        obj = next(iter(config.network_objects.values()))
        obj.obj_typ = "not-a-group"

        # Act
        is_group = fwconfig_import_object.is_group(Type.NETWORK_OBJECT, obj)

        # Assert
        assert is_group is False

    def test_is_group_true_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper, service_object_count=1)
        obj = next(iter(config.service_objects.values()))
        obj.svc_typ = "group"

        # Act
        is_group = fwconfig_import_object.is_group(Type.SERVICE_OBJECT, obj)

        # Assert
        assert is_group is True

    def test_is_group_false_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper, service_object_count=1)
        obj = next(iter(config.service_objects.values()))
        obj.svc_typ = "not-a-group"

        # Act
        is_group = fwconfig_import_object.is_group(Type.SERVICE_OBJECT, obj)

        # Assert
        assert is_group is False

    def test_is_group_true_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        obj = {"user_typ": "group"}

        # Act
        is_group = fwconfig_import_object.is_group(Type.USER, obj)

        # Assert
        assert is_group is True

    def test_is_group_false_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        obj = {"user_typ": "not-a-group"}

        # Act
        is_group = fwconfig_import_object.is_group(Type.USER, obj)

        # Assert
        assert is_group is False


class TestFwConfigImportObjectGetLocalId:
    def test_get_local_id_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=42)

        # Act
        local_id = fwconfig_import_object.get_local_id(management_state, Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert local_id == 42

    def test_get_local_id_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.uid2id_mapper.get_service_object_id = mocker.Mock(return_value=43)

        # Act
        local_id = fwconfig_import_object.get_local_id(management_state, Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert local_id == 43

    def test_get_local_id_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.uid2id_mapper.get_user_id = mocker.Mock(return_value=44)

        # Act
        local_id = fwconfig_import_object.get_local_id(management_state, Type.USER, "some-uid")

        # Assert
        assert local_id == 44


class TestFwConfigImportObjectGetId:
    def test_get_id_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=42)

        # Act
        obj_id = fwconfig_import_object.get_id(management_state, Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert obj_id == 42

    def test_get_id_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.uid2id_mapper.get_service_object_id = mocker.Mock(return_value=43)

        # Act
        obj_id = fwconfig_import_object.get_id(management_state, Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert obj_id == 43

    def test_get_id_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.uid2id_mapper.get_user_id = mocker.Mock(return_value=44)

        # Act
        obj_id = fwconfig_import_object.get_id(management_state, Type.USER, "some-uid")

        # Assert
        assert obj_id == 44


class TestFwConfigImportObjectGetConfigObjects:
    def test_get_config_objects_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.get_config_objects(
                management_state=management_state,
                typ=Type.NETWORK_OBJECT,
                prev_config=FwConfigNormalized(),
            )

    def test_get_config_objects_network_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, network_object_count=2
        )
        management_state.previous_config, __ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, network_object_count=1
        )

        # Act
        prev_objs, current_objs = fwconfig_import_object.get_config_objects(
            management_state=management_state,
            typ=Type.NETWORK_OBJECT,
            prev_config=management_state.previous_config,
        )

        # Assert
        assert prev_objs == management_state.previous_config.network_objects
        assert current_objs == management_state.normalized_config.network_objects

    def test_get_config_objects_service_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, service_object_count=2
        )
        management_state.previous_config, __ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, service_object_count=1
        )

        # Act
        prev_objs, current_objs = fwconfig_import_object.get_config_objects(
            management_state=management_state,
            typ=Type.SERVICE_OBJECT,
            prev_config=management_state.previous_config,
        )

        # Assert
        assert prev_objs == management_state.previous_config.service_objects
        assert current_objs == management_state.normalized_config.service_objects

    def test_get_config_objects_user_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, user_object_count=2
        )
        management_state.previous_config, __ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, user_object_count=1
        )

        # Act
        prev_objs, current_objs = fwconfig_import_object.get_config_objects(
            management_state=management_state,
            typ=Type.USER,
            prev_config=management_state.previous_config,
        )

        # Assert
        assert prev_objs == management_state.previous_config.users
        assert current_objs == management_state.normalized_config.users


class TestFwConfigImportObjectPrepareNewZones:
    def test_prepare_new_zones_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_zones(
                import_state=import_state,
                management_state=management_state,
                new_zone_names=["zone1", "zone2"],
            )

    def test_prepare_new_zones(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(management_state.uid2id_mapper)
        new_zone_names = ["zone1", "zone2"]
        for zone_name in new_zone_names:
            management_state.normalized_config.zone_objects[zone_name] = {
                "zone_name": zone_name,
            }
        import_state.import_id = 5

        # Act
        new_zones = fwconfig_import_object.prepare_new_zones(
            import_state=import_state,
            management_state=management_state,
            new_zone_names=new_zone_names,
        )

        # Assert
        assert new_zones == [
            {
                "mgm_id": 1,
                "zone_create": 5,
                "zone_last_seen": 5,
                "zone_name": "zone1",
            },
            {
                "mgm_id": 1,
                "zone_create": 5,
                "zone_last_seen": 5,
                "zone_name": "zone2",
            },
        ]


class TestFwConfigImportObjectPrepareNewUserobjs:
    def test_prepare_new_userobjs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_userobjs(
                import_state=import_state,
                management_state=management_state,
                new_user_uids=["user1", "user2"],
            )

    def test_prepare_new_userobjs(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, user_object_count=2
        )
        new_user_uids = list(management_state.normalized_config.users.keys())[:2]
        import_state.import_id = 5

        # Act
        new_userobjs = fwconfig_import_object.prepare_new_userobjs(
            import_state=import_state,
            management_state=management_state,
            new_user_uids=new_user_uids,
        )

        # Assert
        assert new_userobjs == [
            {
                "mgm_id": 1,
                "user_create": 5,
                "user_last_seen": 5,
                "user_name": f"user-{new_user_uids[0]}",
                "user_uid": new_user_uids[0],
                "usr_typ_id": 2,
            },
            {
                "mgm_id": 1,
                "user_create": 5,
                "user_last_seen": 5,
                "user_name": f"user-{new_user_uids[1]}",
                "user_uid": new_user_uids[1],
                "usr_typ_id": 2,
            },
        ]


class TestFwConfigImportObjectPrepareNewSvcobjs:
    def test_prepare_new_svcobjs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_svcobjs(
                import_state=import_state,
                management_state=management_state,
                new_svcobj_uids=["svc1", "svc2"],
            )

    def test_prepare_new_svcobjs(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, service_object_count=2
        )
        new_svc_uids = list(management_state.normalized_config.service_objects.keys())[:2]
        import_state.import_id = 5

        # Act
        new_svcobjs = fwconfig_import_object.prepare_new_svcobjs(
            import_state=import_state,
            management_state=management_state,
            new_svcobj_uids=new_svc_uids,
        )

        # Assert
        assert new_svcobjs == [
            ServiceObjectForImport(
                svc_object=management_state.normalized_config.service_objects[uid],
                mgm_id=1,
                import_id=import_state.import_id,
                color_id=import_state.lookup_color_id(
                    management_state.normalized_config.service_objects[uid].svc_color
                ),
                typ_id=import_state.lookup_service_obj_type_id(
                    management_state.normalized_config.service_objects[uid].svc_typ
                ),
            ).to_dict()
            for uid in new_svc_uids
        ]


class TestFwConfgImportObjectPrepareNewNwobjs:
    def test_prepare_new_nwobjs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_nwobjs(
                import_state=import_state,
                management_state=management_state,
                new_nwobj_uids=["nwobj1", "nwobj2"],
            )

    def test_prepare_new_nwobjs(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper, network_object_count=2
        )
        new_nwobj_uids = list(management_state.normalized_config.network_objects.keys())[:2]
        import_state.import_id = 5

        # Act
        new_nwobjs = fwconfig_import_object.prepare_new_nwobjs(
            import_state=import_state,
            management_state=management_state,
            new_nwobj_uids=new_nwobj_uids,
        )

        # Assert
        assert new_nwobjs == [
            NetworkObjectForImport(
                nw_object=management_state.normalized_config.network_objects[uid],
                mgm_id=1,
                import_id=import_state.import_id,
                color_id=import_state.lookup_color_id(
                    management_state.normalized_config.network_objects[uid].obj_color
                ),
                typ_id=import_state.lookup_network_obj_type_id(
                    management_state.normalized_config.network_objects[uid].obj_typ
                ),
            ).to_dict()
            for uid in new_nwobj_uids
        ]


class TestFwConfigImportObjectUpdateObjectsViaApi:
    def test_update_objects_via_api_no_management_id(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        import_state.lookup_management_id = mocker.Mock(return_value=None)
        global_state.fwo_api_call.call = mocker.Mock()

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.update_objects_via_api(
                import_state=import_state,
                management_state=management_state,
                fwo_api_call=global_state.fwo_api_call,
                new_nw_object_uids=[],
                new_svc_obj_uids=[],
                new_user_uids=[],
                new_zone_names=[],
                removed_nw_object_uids=[],
                removed_svc_object_uids=[],
                removed_user_uids=[],
                removed_zone_names=[],
                single_manager=fw_config_manager,
            )

        # Assert
        global_state.fwo_api_call.call.assert_not_called()

    def test_update_objects_via_api_with_errors(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):

        management_state.normalized_config = FwConfigNormalized()
        # Arrange
        import_state.lookup_management_id = mocker.Mock(return_value=1)
        mock_get_graphql_code(mocker, "importObjectsMutation")
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "errors": [
                    {
                        "message": "Some error occurred",
                    }
                ]
            }
        )

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.update_objects_via_api(
                new_nw_object_uids=[],
                new_svc_obj_uids=[],
                new_user_uids=[],
                new_zone_names=[],
                removed_nw_object_uids=[],
                removed_svc_object_uids=[],
                removed_user_uids=[],
                removed_zone_names=[],
                single_manager=fw_config_manager,
                import_state=import_state,
                management_state=management_state,
                fwo_api_call=global_state.fwo_api_call,
            )

        # Assert
        global_state.fwo_api_call.call.assert_called_once()

    def test_update_objects_via_api_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        import_state: ImportState,
        management_state: ManagementState,
        global_state: GlobalState,
    ):
        # Arrange
        import_state.lookup_management_id = mocker.Mock(return_value=1)
        mock_get_graphql_code(mocker, "importObjectsMutation")
        global_state.fwo_api_call.call = mocker.Mock(side_effect=Exception("Unexpected error occurred"))

        management_state.normalized_config = FwConfigNormalized()
        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.update_objects_via_api(
                new_nw_object_uids=[],
                new_svc_obj_uids=[],
                new_user_uids=[],
                new_zone_names=[],
                removed_nw_object_uids=[],
                removed_svc_object_uids=[],
                removed_user_uids=[],
                removed_zone_names=[],
                single_manager=fw_config_manager,
                import_state=import_state,
                management_state=management_state,
                fwo_api_call=global_state.fwo_api_call,
            )

        # Assert
        global_state.fwo_api_call.call.assert_called_once()

    def test_update_objects_via_api_with_wrong_response_format(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        import_state: ImportState,
        management_state: ManagementState,
        global_state: GlobalState,
    ):

        # Arrange
        import_state.lookup_management_id = mocker.Mock(return_value=1)
        mock_get_graphql_code(mocker, "importObjectsMutation")
        global_state.fwo_api_call.call = mocker.Mock(return_value={"unexpected_key": {}})

        management_state.normalized_config = FwConfigNormalized()

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.update_objects_via_api(
                new_nw_object_uids=[],
                new_svc_obj_uids=[],
                new_user_uids=[],
                new_zone_names=[],
                removed_nw_object_uids=[],
                removed_svc_object_uids=[],
                removed_user_uids=[],
                removed_zone_names=[],
                single_manager=fw_config_manager,
                import_state=import_state,
                management_state=management_state,
                fwo_api_call=global_state.fwo_api_call,
            )

        # Assert
        global_state.fwo_api_call.call.assert_called_once()

    def test_update_objects_via_api(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        import_state: ImportState,
        management_state: ManagementState,
        global_state: GlobalState,
    ):
        # Arrange
        FWOLogger.instance.debug_level = 9
        import_state.lookup_management_id = mocker.Mock(return_value=1)
        global_state.fwo_api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "insert_object": {"affected_rows": 1, "returning": [{"id": 1}]},
                    "insert_service": {"affected_rows": 1, "returning": [{"id": 2}]},
                    "insert_usr": {"affected_rows": 1, "returning": [{"id": 3}]},
                    "insert_zone": {"affected_rows": 1, "returning": [{"id": 4}]},
                    "update_object": {"affected_rows": 1, "returning": [{"id": 5}]},
                    "update_service": {"affected_rows": 1, "returning": [{"id": 6}]},
                    "update_usr": {"affected_rows": 1, "returning": [{"id": 7}]},
                    "update_zone": {"affected_rows": 1, "returning": [{"id": 8}]},
                }
            }
        )
        mock_get_graphql_code(mocker, "importObjectsMutation")

        management_state.normalized_config = FwConfigNormalized()

        # Act
        (
            new_nwobj_ids,
            new_nwsvc_ids,
            new_user_ids,
            new_zone_ids,
            removed_nwobj_ids,
            removed_nwsvc_ids,
            removed_user_ids,
            removed_zone_ids,
        ) = fwconfig_import_object.update_objects_via_api(
            new_nw_object_uids=[],
            new_svc_obj_uids=[],
            new_user_uids=[],
            new_zone_names=[],
            removed_nw_object_uids=[],
            removed_svc_object_uids=[],
            removed_user_uids=[],
            removed_zone_names=[],
            single_manager=fw_config_manager,
            import_state=import_state,
            management_state=management_state,
            fwo_api_call=global_state.fwo_api_call,
        )

        # Assert
        assert new_nwobj_ids == [{"id": 1}]
        assert new_nwsvc_ids == [{"id": 2}]
        assert new_user_ids == [{"id": 3}]
        assert new_zone_ids == [{"id": 4}]
        assert removed_nwobj_ids == [{"id": 5}]
        assert removed_nwsvc_ids == [{"id": 6}]
        assert removed_user_ids == [{"id": 7}]
        assert removed_zone_ids == [{"id": 8}]


class TestFwConfigImportObjectUpdateObjectDiffs:
    def test_update_object_diffs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fw_config_manager: FwConfigManager,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.update_object_diffs(
                global_state=global_state,
                import_state=import_state,
                management_state=management_state,
                single_manager=fw_config_manager,
            )

    def test_update_object_diffs_uses_previous_config_as_global_fallback(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange
        management_state.previous_config = FwConfigNormalized()
        management_state.normalized_config = FwConfigNormalized()
        import_state.previous_super_config = None
        import_state.super_config = FwConfigNormalized()

        management_state.uid2id_mapper.update_network_object_mapping = mocker.Mock()
        management_state.uid2id_mapper.update_service_object_mapping = mocker.Mock()
        management_state.uid2id_mapper.update_user_mapping = mocker.Mock()
        management_state.uid2id_mapper.update_zone_mapping = mocker.Mock()
        management_state.uid2id_mapper.add_network_object_mappings = mocker.Mock()
        management_state.uid2id_mapper.add_service_object_mappings = mocker.Mock()
        management_state.uid2id_mapper.add_user_mappings = mocker.Mock()
        management_state.uid2id_mapper.add_zone_mappings = mocker.Mock()

        management_state.group_flats_mapper.init_config = mocker.Mock()
        management_state.prev_group_flats_mapper.init_config = mocker.Mock()

        fwconfig_import_object.remove_outdated_memberships = mocker.Mock()
        fwconfig_import_object.add_group_memberships = mocker.Mock()
        fwconfig_import_object.add_changelog_objs = mocker.Mock()
        fwconfig_import_object.update_time_objs_via_api = mocker.Mock()
        fwconfig_import_object.update_objects_via_api = mocker.Mock(return_value=([], [], [], [], [], [], [], []))

        # Act
        fwconfig_import_object.update_object_diffs(
            global_state=global_state,
            import_state=import_state,
            management_state=management_state,
            single_manager=fw_config_manager,
        )

        # Assert
        management_state.prev_group_flats_mapper.init_config.assert_called_once_with(
            management_state.previous_config,
            management_state.previous_config,
        )

    def test_update_object_diffs_changes_and_filters(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        fwconfig_builder: FwConfigBuilder,
        global_state: GlobalState,
        import_state: ImportState,
        management_state: ManagementState,
    ):
        # Arrange:
        management_state.previous_config, _ = fwconfig_builder.build_config(
            management_state.uid2id_mapper,
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=10,
            zone_object_count=3,
        )

        curr_config = copy.deepcopy(management_state.previous_config)

        first_nwobj_uid = next(iter(management_state.previous_config.network_objects.keys()))
        first_svcobj_uid = next(iter(management_state.previous_config.service_objects.keys()))
        first_userobj_uid = next(iter(management_state.previous_config.users.keys()))
        first_zone_name = next(iter(management_state.previous_config.zone_objects.keys()))

        first_nwobj = curr_config.network_objects[first_nwobj_uid]
        first_svcobj = curr_config.service_objects[first_svcobj_uid]
        first_userobj = curr_config.users[first_userobj_uid]
        first_zoneobj = curr_config.zone_objects[first_zone_name]

        first_nwobj.obj_name = "modified-name"
        first_svcobj.svc_name = "modified-name"
        first_userobj["user_name"] = "modified-name"
        first_zoneobj["zone_name"] = "modified-name"

        management_state.normalized_config = curr_config
        import_state.previous_super_config = copy.deepcopy(management_state.previous_config)
        import_state.super_config = copy.deepcopy(management_state.normalized_config)

        management_state.uid2id_mapper.update_network_object_mapping = mocker.Mock()
        management_state.uid2id_mapper.update_service_object_mapping = mocker.Mock()
        management_state.uid2id_mapper.update_user_mapping = mocker.Mock()
        management_state.uid2id_mapper.update_zone_mapping = mocker.Mock()
        management_state.uid2id_mapper.add_network_object_mappings = mocker.Mock()
        management_state.uid2id_mapper.add_service_object_mappings = mocker.Mock()
        management_state.uid2id_mapper.add_user_mappings = mocker.Mock()
        management_state.uid2id_mapper.add_zone_mappings = mocker.Mock()

        fwconfig_import_object.remove_outdated_memberships = mocker.Mock()
        fwconfig_import_object.add_group_memberships = mocker.Mock()
        fwconfig_import_object.add_changelog_objs = mocker.Mock()

        fwconfig_import_object.update_objects_via_api = mocker.Mock(
            return_value=(
                [{"obj_id": 1}, {"obj_id": 2}],
                [{"svc_id": 3}],
                [],
                [],
                [{"obj_id": 10}, {"obj_id": 11}],
                [{"svc_id": 30}],
                [],
                [],
            )
        )

        def fake_create_change_id_maps(
            self: ChangeLogger,
            _uid2id_mapper: Uid2IdMapper,
            _changed_nw_objs: list[str],
            _changed_svcs: list[str],
            _removed_nw_objs: list[dict[str, Any]],
            _removed_nw_svcs: list[dict[str, Any]],
        ):
            self.changed_object_id_map = {10: 1}
            self.changed_service_id_map = {30: 3}

        mocker.patch.object(ChangeLogger, "create_change_id_maps", new=fake_create_change_id_maps)

        stats = import_state.statistics_controller
        stats.increment_network_object_add_count = mocker.Mock()
        stats.increment_network_object_delete_count = mocker.Mock()
        stats.increment_network_object_change_count = mocker.Mock()
        stats.increment_service_object_add_count = mocker.Mock()
        stats.increment_service_object_delete_count = mocker.Mock()
        stats.increment_service_object_change_count = mocker.Mock()

        # Act
        fwconfig_import_object.update_object_diffs(
            global_state=global_state,
            import_state=import_state,
            management_state=management_state,
            single_manager=fw_config_manager,
        )

        # Assert: update_objects_via_api called with expected uid sets
        args, _ = fwconfig_import_object.update_objects_via_api.call_args
        assert args[0] is import_state
        assert args[1] is management_state
        assert args[2] is fw_config_manager
        assert set(args[3]) == {first_nwobj_uid}
        assert set(args[4]) == {first_svcobj_uid}
        assert set(args[5]) == {first_userobj_uid}
        assert set(args[6]) == {first_zone_name}
        assert set(args[7]) == {first_nwobj_uid}
        assert set(args[8]) == {first_svcobj_uid}
        assert set(args[9]) == {first_userobj_uid}
        assert set(args[10]) == {first_zone_name}

        assert fwconfig_import_object.remove_outdated_memberships.call_count == 3
        assert fwconfig_import_object.add_group_memberships.call_count == 3

        fwconfig_import_object.add_changelog_objs.assert_called_once_with(
            global_state,
            import_state,
            [{"obj_id": 2}],
            [],
            [{"obj_id": 11}],
            [],
        )

        stats.increment_network_object_add_count.assert_called_once_with(1)
        stats.increment_network_object_delete_count.assert_called_once_with(1)
        stats.increment_network_object_change_count.assert_called_once_with(1)
        stats.increment_service_object_add_count.assert_called_once_with(0)
        stats.increment_service_object_delete_count.assert_called_once_with(0)
        stats.increment_service_object_change_count.assert_called_once_with(1)
