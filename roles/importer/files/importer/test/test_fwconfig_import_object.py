import copy
from typing import Any
from unittest.mock import mock_open

import pytest
from fwo_api_call import FwoApiCall
from fwo_const import LIST_DELIMITER
from fwo_exceptions import FwoDuplicateKeyViolationError, FwoImporterError
from fwo_log import ChangeLogger, FWOLogger
from model_controllers.fwconfig_import_object import FwConfigImportObject, Type
from model_controllers.import_state_controller import ImportStateController
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanager import FwConfigManager
from models.networkobject import NetworkObjectForImport
from models.serviceobject import ServiceObjectForImport
from pytest_mock import MockerFixture
from services.uid2id_mapper import Uid2IdMapper
from test.data.mock_objects import MockObjectsFactory
from test.utils.config_builder import FwConfigBuilder
from test.utils.test_utils import mock_get_graphql_code


class TestFwConfigImportObjectAddChangelogObjs:
    def test_add_changelog_objects(
        self, fwconfig_import_object: FwConfigImportObject, api_call: FwoApiCall, mocker: MockerFixture
    ):
        # Arrange
        api_call.call = mocker.Mock(
            return_value=MockObjectsFactory.get_standard_changelog_return_value(),
        )

        # Act
        fwconfig_import_object.add_changelog_objs(
            nwobj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        api_call.call.assert_any_call(
            mocker.ANY,
            query_variables={
                "nwObjChanges": MockObjectsFactory.get_changelog_object_insert_delete(),
                "svcObjChanges": MockObjectsFactory.get_changelog_svc_objects_insert_delete(),
            },
            analyze_payload=True,
        )

    def test_add_changelog_objects_with_errors(
        self, fwconfig_import_object: FwConfigImportObject, api_call: FwoApiCall, mocker: MockerFixture
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(return_value={"errors": [{"message": "Some error occurred"}]})

        # Act
        fwconfig_import_object.add_changelog_objs(
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
        self, fwconfig_import_object: FwConfigImportObject, api_call: FwoApiCall, mocker: MockerFixture
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(side_effect=Exception("API call failed"))

        # Act
        fwconfig_import_object.add_changelog_objs(
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
    ):
        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete()
        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete()

    def test_prepare_changelog_objects_initial_import(
        self, fwconfig_import_object: FwConfigImportObject, import_state_controller: ImportStateController
    ):
        # Arrange
        import_state_controller.state.is_initial_import = True

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert

        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete(2)
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete(2)

    def test_prepare_changelog_objects_clearing_import(
        self, fwconfig_import_object: FwConfigImportObject, import_state_controller: ImportStateController
    ):
        # Arrange
        import_state_controller.state.is_clearing_import = True

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete(2)
        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete(2)

    def test_prepare_changelog_objects_with_logged_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        change_logger = ChangeLogger()
        change_logger.changed_object_id_map = {1: 10}
        change_logger.changed_service_id_map = {3: 30}

        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == MockObjectsFactory.get_changelog_object_insert_delete_change()
        assert svcobjs_changed == MockObjectsFactory.get_changelog_svc_objects_insert_delete_change()


class TestFwConfigImportObjectLookupProtoNameToId:
    def test_lookup_proto_name_to_id_int(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        expected_proto_id = 6

        # Act
        proto_id = fwconfig_import_object.lookup_proto_name_to_id(expected_proto_id)

        # Assert
        assert proto_id == expected_proto_id

    def test_lookup_proto_name_to_id_unknown_name(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        proto_name = "tcp"
        expected_proto_id = None

        # Act
        proto_id = fwconfig_import_object.lookup_proto_name_to_id(proto_name)

        # Assert
        assert proto_id == expected_proto_id

    def test_lookup_proto_name_to_id_known_name(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        proto_name = "icmp"
        expected_proto_id = 1
        fwconfig_import_object.protocol_map = {
            "icmp": 1,
            "tcp": 6,
            "udp": 17,
        }

        # Act
        proto_id = fwconfig_import_object.lookup_proto_name_to_id(proto_name)

        # Assert
        assert proto_id == expected_proto_id


class TestFwConfigImportObjectLookupSvcIdToUidAndPolicyName:
    def test_lookup_svc_id_to_uid_and_policy_name(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        svc_id = 42
        expected_svc_uid = "42"

        # Act
        svc_uid = fwconfig_import_object.lookup_svc_id_to_uid_and_policy_name(svc_id)

        # Assert
        assert svc_uid == expected_svc_uid


class TestFwConfigImportObjectLookupObjIdToUidAndPolicyName:
    def test_lookup_obj_id_to_uid_and_policy_name(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        obj_id = 99
        expected_obj_uid = "99"

        # Act
        obj_uid = fwconfig_import_object.lookup_obj_id_to_uid_and_policy_name(obj_id)

        # Assert
        assert obj_uid == expected_obj_uid


class TestFwConfigImportObjectLookupUserType:
    def test_lookup_user_type_unknown(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        user_type_str = "some-user-type"
        expected_user_type = -1

        # Act
        user_type = fwconfig_import_object.lookup_user_type(user_type_str)

        # Assert
        assert user_type == expected_user_type

    def test_lookup_user_type_known(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        user_type_str = "imported"
        expected_user_type = 2
        fwconfig_import_object.user_object_type_map = {
            "admin": 1,
            "imported": 2,
            "readonly": 3,
        }

        # Act
        user_type = fwconfig_import_object.lookup_user_type(user_type_str)

        # Assert
        assert user_type == expected_user_type


class TestFwConfigImportObjectLookupSvcType:
    def test_lookup_svc_type_unknown(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        svc_type_str = "some-svc-type"
        expected_svc_type = -1

        # Act
        svc_type = fwconfig_import_object.lookup_svc_type(svc_type_str)

        # Assert
        assert svc_type == expected_svc_type

    def test_lookup_svc_type_known(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        svc_type_str = "imported"
        expected_svc_type = 2
        fwconfig_import_object.service_object_type_map = {
            "builtin": 1,
            "imported": 2,
            "custom": 3,
        }

        # Act
        svc_type = fwconfig_import_object.lookup_svc_type(svc_type_str)

        # Assert
        assert svc_type == expected_svc_type


class TestFwConfigImportObjectLookupObjType:
    def test_lookup_obj_type_unknown(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        obj_type_str = "some-obj-type"
        expected_obj_type = -1

        # Act
        obj_type = fwconfig_import_object.lookup_obj_type(obj_type_str)

        # Assert
        assert obj_type == expected_obj_type

    def test_lookup_obj_type_known(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        obj_type_str = "imported"
        expected_obj_type = 2
        fwconfig_import_object.network_object_type_map = {
            "builtin": 1,
            "imported": 2,
            "custom": 3,
        }

        # Act
        obj_type = fwconfig_import_object.lookup_obj_type(obj_type_str)

        # Assert
        assert obj_type == expected_obj_type


class TestFwConfigImportObjectWriteMemberUpdates:
    def test_write_member_updates(
        self,
        fwconfig_import_object: FwConfigImportObject,
        api_call: FwoApiCall,
        mocker: MockerFixture,
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
    ):
        # Arrange
        prefix = "nwobj"
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")
        api_call.call = mocker.Mock(return_value={"data": []})

        # Act
        with pytest.raises(TypeError):
            fwconfig_import_object.write_member_updates(
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
    ):
        # Arrange
        new_group_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_group_members(
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
    ):
        # Arrange
        new_group_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_group_members(
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
    ):
        # Arrange
        prefix = "nwobj"
        new_group_members: list[dict[str, Any]] = []
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=2)
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        fwconfig_import_object.collect_group_members(
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
    ):
        # Arrange
        new_group_member_flats: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_flat_group_members(
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
    ):
        # Arrange
        new_group_member_flats: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.collect_flat_group_members(
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
    ):
        # Arrange
        prefix = "nwobj"
        new_group_member_flats: list[dict[str, Any]] = []
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=2)
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        fwconfig_import_object.collect_flat_group_members(
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
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        new_group_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=FwConfigNormalized(),
        )

        # Assert
        assert new_group_members == []

    def test_add_group_memberships_not_a_group(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=1,
        )
        prev_config, _ = fwconfig_builder.build_config(
            network_object_count=1,
        )
        fwconfig_import_object.write_member_updates = mocker.Mock()

        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_typ = "not-a-group"
        for obj in prev_config.network_objects.values():
            obj.obj_typ = "not-a-group"

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_not_called()

    def test_add_group_memberships_no_group_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=1,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        fwconfig_import_object.write_member_updates = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_not_called()

    def test_add_group_memberships_no_group_id(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=1,
        )
        prev_config, __ = fwconfig_builder.build_config(
            network_object_count=1,
        )
        fwconfig_import_object.write_member_updates = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=None)

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_not_called()

        assert mock_logger.call_count == 1
        assert (
            mock_logger.call_args[0][0]
            == f"failed to add group memberships: no id found for group uid '{next(iter(fwconfig_import_object.normalized_config.network_objects.keys()))}'"
        )

    def test_add_group_memberships_with_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=1,
        )
        fwconfig_import_object.import_state.state.import_id = 5
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        fwconfig_import_object.write_member_updates = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=["old-member-uid"]
        )

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=1,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = fwconfig_builder.build_empty_config()
        group_obj = next(iter(fwconfig_import_object.normalized_config.network_objects.values()))
        group_obj.obj_member_refs = group_obj.obj_uid
        fwconfig_import_object.import_state.state.import_id = 7
        fwconfig_import_object.write_member_updates = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        fwconfig_import_object.group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=[group_obj.obj_uid]
        )

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        group_uid, member_uid = list(fwconfig_import_object.normalized_config.network_objects.keys())[:2]
        fwconfig_import_object.normalized_config.network_objects[group_uid].obj_member_refs = member_uid
        prev_config.network_objects[group_uid].obj_member_refs = member_uid
        fwconfig_import_object.normalized_config.network_objects[member_uid].obj_typ = "host"
        prev_config.network_objects[member_uid].obj_typ = "host"
        fwconfig_import_object.normalized_config.network_objects[member_uid].obj_name = "changed-member"

        def fake_get_id(uid: str, _before_update: bool = False):
            return 1 if uid == group_uid else 2

        fwconfig_import_object.import_state.state.import_id = 9
        fwconfig_import_object.write_member_updates = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        fwconfig_import_object.group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[member_uid])
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[member_uid])

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=3,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        group_uid, member_uid_one, member_uid_two = list(
            fwconfig_import_object.normalized_config.network_objects.keys()
        )[:3]
        fwconfig_import_object.normalized_config.network_objects[
            group_uid
        ].obj_member_refs = f"{member_uid_one}{LIST_DELIMITER}{member_uid_two}"
        prev_config.network_objects[group_uid].obj_member_refs = member_uid_one
        fwconfig_import_object.normalized_config.network_objects[member_uid_one].obj_typ = "host"
        fwconfig_import_object.normalized_config.network_objects[member_uid_two].obj_typ = "host"
        prev_config.network_objects[member_uid_one].obj_typ = "host"
        prev_config.network_objects[member_uid_two].obj_typ = "host"

        def fake_get_id(uid: str, _before_update: bool = False):
            mapping = {
                group_uid: 1,
                member_uid_one: 2,
                member_uid_two: 3,
            }
            return mapping[uid]

        fwconfig_import_object.import_state.state.import_id = 11
        fwconfig_import_object.write_member_updates = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        fwconfig_import_object.group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=[member_uid_one, member_uid_two]
        )

        # Act
        fwconfig_import_object.add_group_memberships(
            obj_type=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        fwconfig_import_object.write_member_updates.assert_called_once_with(
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config, _ = fwconfig_builder.build_config(
            network_object_count=3,
        )
        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_typ = "not-a-group"
        for obj in prev_config.network_objects.values():
            obj.obj_typ = "not-a-group"
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []

        # Act
        fwconfig_import_object.find_removed_objects(
            current_config_objects=fwconfig_import_object.normalized_config.network_objects,
            prev_config_objects=prev_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(prev_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == []
        assert removed_members == []

    def test_find_removed_objects_no_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)

        # Act
        fwconfig_import_object.find_removed_objects(
            current_config_objects=fwconfig_import_object.normalized_config.network_objects,
            prev_config_objects=prev_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(prev_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == []
        assert removed_members == []

    def test_find_removed_objects_with_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        for obj in prev_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])

        # Act
        fwconfig_import_object.find_removed_objects(
            current_config_objects=fwconfig_import_object.normalized_config.network_objects,
            prev_config_objects=prev_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(prev_config.network_objects.values())).obj_uid,
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        last_obj_uid = list(fwconfig_import_object.normalized_config.network_objects.keys())[-1]
        for obj in list(fwconfig_import_object.normalized_config.network_objects.values())[:-1]:
            obj.obj_member_refs = last_obj_uid
        for obj in list(prev_config.network_objects.values())[:-1]:
            obj.obj_member_refs = last_obj_uid
        removed_flats: list[dict[str, Any]] = []
        removed_members: list[dict[str, Any]] = []
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=last_obj_uid)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=[last_obj_uid]
        )

        # Act
        fwconfig_import_object.find_removed_objects(
            current_config_objects=fwconfig_import_object.normalized_config.network_objects,
            prev_config_objects=prev_config.network_objects,
            prefix="nwobj",
            removed_flats=removed_flats,
            removed_members=removed_members,
            typ=Type.NETWORK_OBJECT,
            uid=next(iter(prev_config.network_objects.values())).obj_uid,
        )

        # Assert
        assert removed_flats == []
        assert removed_members == []


class TestFwConfigImportObjectRemoveOutdatedMemberships:
    def test_remove_outdated_memberships_no_changes(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = FwConfigBuilder().build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        fwconfig_import_object.import_state.api_call.call = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_not_called()

    def test_remove_outdated_memberships_with_changes_and_success(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        for obj in prev_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"
        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"

        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_called_once()
        call_args = fwconfig_import_object.import_state.api_call.call.call_args
        assert call_args[0][1] == {
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        for obj in prev_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"

        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "errors": [
                    {
                        "message": "Some error occurred",
                    }
                ]
            }
        )
        fwconfig_import_object.import_state.state.import_id = 5
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_called_once()
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        for obj in fwconfig_import_object.normalized_config.network_objects.values():
            obj.obj_member_refs = "new-member-uid"
        for obj in prev_config.network_objects.values():
            obj.obj_member_refs = "old-member-uid"
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=1)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[2])
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "error": "Some unexpected error occurred",
            }
        )
        fwconfig_import_object.import_state.state.import_id = 5
        mock_logger = mocker.patch("fwo_log.FWOLogger.exception")

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_called_once()
        mock_logger.assert_called_once()
        assert str(mock_logger.call_args[0][0]).startswith(
            "failed to remove outdated group memberships for Type.NETWORK_OBJECT: Traceback"
        )

    def test_remove_outdated_memberships_group_removed(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        prev_config, _ = fwconfig_builder.build_config(
            network_object_count=2,
            service_object_count=0,
            include_gateway=False,
        )
        group_uid, member_uid = list(prev_config.network_objects.keys())[:2]
        prev_config.network_objects[group_uid].obj_member_refs = member_uid
        prev_config.network_objects[member_uid].obj_typ = "host"
        fwconfig_import_object.normalized_config = FwConfigNormalized()

        def fake_get_id(uid: str, _before_update: bool = False):
            return 10 if uid == group_uid else 20

        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[member_uid])
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "update_objgrp": {"affected_rows": 1},
                    "update_objgrp_flat": {"affected_rows": 1},
                }
            }
        )
        fwconfig_import_object.import_state.state.import_id = 6

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        call_args = fwconfig_import_object.import_state.api_call.call.call_args
        assert call_args[0][1] == {
            "importId": 6,
            "removedMembers": [{"_and": [{"objgrp_id": {"_eq": 10}}, {"objgrp_member_id": {"_eq": 20}}]}],
            "removedFlats": [{"_and": [{"objgrp_flat_id": {"_eq": 10}}, {"objgrp_flat_member_id": {"_eq": 20}}]}],
        }

    def test_remove_outdated_memberships_group_changed_removes_all_prev_members_and_flats(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(
            network_object_count=3,
            service_object_count=0,
            include_gateway=False,
        )
        prev_config = copy.deepcopy(fwconfig_import_object.normalized_config)
        group_uid, member_uid_one, member_uid_two = list(prev_config.network_objects.keys())[:3]
        prev_config.network_objects[group_uid].obj_member_refs = f"{member_uid_one}{LIST_DELIMITER}{member_uid_two}"
        fwconfig_import_object.normalized_config.network_objects[group_uid].obj_member_refs = member_uid_one
        prev_config.network_objects[member_uid_one].obj_typ = "host"
        prev_config.network_objects[member_uid_two].obj_typ = "host"
        fwconfig_import_object.normalized_config.network_objects[member_uid_one].obj_typ = "host"
        fwconfig_import_object.normalized_config.network_objects[member_uid_two].obj_typ = "host"

        def fake_get_id(uid: str, _before_update: bool = False):
            mapping = {
                group_uid: 100,
                member_uid_one: 200,
                member_uid_two: 300,
            }
            return mapping[uid]

        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(side_effect=fake_get_id)
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(
            return_value=[member_uid_one, member_uid_two]
        )
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "update_objgrp": {"affected_rows": 2},
                    "update_objgrp_flat": {"affected_rows": 2},
                }
            }
        )
        fwconfig_import_object.import_state.state.import_id = 12

        # Act
        fwconfig_import_object.remove_outdated_memberships(
            prev_config=prev_config,
            typ=Type.NETWORK_OBJECT,
        )

        # Assert
        call_args = fwconfig_import_object.import_state.api_call.call.call_args
        assert call_args[0][1] == {
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
    ):
        # Arrange
        fwconfig_import_object.prev_group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[1, 2, 3])

        # Act
        flats = fwconfig_import_object.get_prev_flats(Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert flats == [1, 2, 3]

    def test_get_prev_flats_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.prev_group_flats_mapper.get_service_object_flats = mocker.Mock(return_value=[4, 5, 6])

        # Act
        flats = fwconfig_import_object.get_prev_flats(Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert flats == [4, 5, 6]

    def test_get_prev_flats_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.prev_group_flats_mapper.get_user_flats = mocker.Mock(return_value=[7, 8, 9])

        # Act
        flats = fwconfig_import_object.get_prev_flats(Type.USER, "some-uid")

        # Assert
        assert flats == [7, 8, 9]


class TestFwConfigImportObjectGetFlats:
    def test_get_flats_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.group_flats_mapper.get_network_object_flats = mocker.Mock(return_value=[1, 2, 3])

        # Act
        flats = fwconfig_import_object.get_flats(Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert flats == [1, 2, 3]

    def test_get_flats_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.group_flats_mapper.get_service_object_flats = mocker.Mock(return_value=[4, 5, 6])

        # Act
        flats = fwconfig_import_object.get_flats(Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert flats == [4, 5, 6]

    def test_get_flats_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.group_flats_mapper.get_user_flats = mocker.Mock(return_value=[7, 8, 9])

        # Act
        flats = fwconfig_import_object.get_flats(Type.USER, "some-uid")

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
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(network_object_count=1)
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
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(service_object_count=1)
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
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(network_object_count=1)
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
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(network_object_count=1)
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
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(service_object_count=1)
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
    ):
        # Arrange
        config, _ = fwconfig_builder.build_config(service_object_count=1)
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
    ):
        # Arrange
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=42)

        # Act
        local_id = fwconfig_import_object.get_local_id(Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert local_id == 42

    def test_get_local_id_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.uid2id_mapper.get_service_object_id = mocker.Mock(return_value=43)

        # Act
        local_id = fwconfig_import_object.get_local_id(Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert local_id == 43

    def test_get_local_id_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.uid2id_mapper.get_user_id = mocker.Mock(return_value=44)

        # Act
        local_id = fwconfig_import_object.get_local_id(Type.USER, "some-uid")

        # Assert
        assert local_id == 44


class TestFwConfigImportObjectGetId:
    def test_get_id_network_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.uid2id_mapper.get_network_object_id = mocker.Mock(return_value=42)

        # Act
        obj_id = fwconfig_import_object.get_id(Type.NETWORK_OBJECT, "some-uid")

        # Assert
        assert obj_id == 42

    def test_get_id_service_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.uid2id_mapper.get_service_object_id = mocker.Mock(return_value=43)

        # Act
        obj_id = fwconfig_import_object.get_id(Type.SERVICE_OBJECT, "some-uid")

        # Assert
        assert obj_id == 43

    def test_get_id_user_object(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.uid2id_mapper.get_user_id = mocker.Mock(return_value=44)

        # Act
        obj_id = fwconfig_import_object.get_id(Type.USER, "some-uid")

        # Assert
        assert obj_id == 44


class TestFwConfigImportObjectGetConfigObjects:
    def test_get_config_objects_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        fwconfig_import_object.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.get_config_objects(
                typ=Type.NETWORK_OBJECT,
                prev_config=FwConfigNormalized(),
            )

    def test_get_config_objects_network_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(network_object_count=2)
        prev_config, __ = fwconfig_builder.build_config(network_object_count=1)

        # Act
        prev_objs, current_objs = fwconfig_import_object.get_config_objects(
            typ=Type.NETWORK_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        assert prev_objs == prev_config.network_objects
        assert current_objs == fwconfig_import_object.normalized_config.network_objects

    def test_get_config_objects_service_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(service_object_count=2)
        prev_config, __ = fwconfig_builder.build_config(service_object_count=1)

        # Act
        prev_objs, current_objs = fwconfig_import_object.get_config_objects(
            typ=Type.SERVICE_OBJECT,
            prev_config=prev_config,
        )

        # Assert
        assert prev_objs == prev_config.service_objects
        assert current_objs == fwconfig_import_object.normalized_config.service_objects

    def test_get_config_objects_user_objects(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(user_object_count=2)
        prev_config, __ = fwconfig_builder.build_config(user_object_count=1)

        # Act
        prev_objs, current_objs = fwconfig_import_object.get_config_objects(
            typ=Type.USER,
            prev_config=prev_config,
        )

        # Assert
        assert prev_objs == prev_config.users
        assert current_objs == fwconfig_import_object.normalized_config.users


class TestFwConfigImportObjectPrepareNewZones:
    def test_prepare_new_zones_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        fwconfig_import_object.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_zones(
                mgm_id=1,
                new_zone_names=["zone1", "zone2"],
            )

    def test_prepare_new_zones(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config()
        new_zone_names = ["zone1", "zone2"]
        for zone_name in new_zone_names:
            fwconfig_import_object.normalized_config.zone_objects[zone_name] = {
                "zone_name": zone_name,
            }
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        new_zones = fwconfig_import_object.prepare_new_zones(
            mgm_id=1,
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
    ):
        # Arrange
        fwconfig_import_object.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_userobjs(
                mgm_id=1,
                new_user_uids=["user1", "user2"],
            )

    def test_prepare_new_userobjs(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(user_object_count=2)
        new_user_uids = list(fwconfig_import_object.normalized_config.users.keys())[:2]
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        new_userobjs = fwconfig_import_object.prepare_new_userobjs(
            mgm_id=1,
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
                "usr_typ_id": -1,
            },
            {
                "mgm_id": 1,
                "user_create": 5,
                "user_last_seen": 5,
                "user_name": f"user-{new_user_uids[1]}",
                "user_uid": new_user_uids[1],
                "usr_typ_id": -1,
            },
        ]


class TestFwConfigImportObjectPrepareNewSvcobjs:
    def test_prepare_new_svcobjs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        fwconfig_import_object.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_svcobjs(
                mgm_id=1,
                new_svcobj_uids=["svc1", "svc2"],
            )

    def test_prepare_new_svcobjs(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(service_object_count=2)
        new_svc_uids = list(fwconfig_import_object.normalized_config.service_objects.keys())[:2]
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        new_svcobjs = fwconfig_import_object.prepare_new_svcobjs(
            mgm_id=1,
            new_svcobj_uids=new_svc_uids,
        )

        # Assert
        assert new_svcobjs == [
            ServiceObjectForImport(
                svc_object=fwconfig_import_object.normalized_config.service_objects[uid],
                mgm_id=1,
                import_id=fwconfig_import_object.import_state.state.import_id,
                color_id=fwconfig_import_object.import_state.state.lookup_color_id(
                    fwconfig_import_object.normalized_config.service_objects[uid].svc_color
                ),
                typ_id=fwconfig_import_object.lookup_svc_type(
                    fwconfig_import_object.normalized_config.service_objects[uid].svc_typ
                ),
            ).to_dict()
            for uid in new_svc_uids
        ]


class TestFwConfgImportObjectPrepareNewNwobjs:
    def test_prepare_new_nwobjs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
    ):
        # Arrange
        fwconfig_import_object.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.prepare_new_nwobjs(
                mgm_id=1,
                new_nwobj_uids=["nwobj1", "nwobj2"],
            )

    def test_prepare_new_nwobjs(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange
        fwconfig_import_object.normalized_config, _ = fwconfig_builder.build_config(network_object_count=2)
        new_nwobj_uids = list(fwconfig_import_object.normalized_config.network_objects.keys())[:2]
        fwconfig_import_object.import_state.state.import_id = 5

        # Act
        new_nwobjs = fwconfig_import_object.prepare_new_nwobjs(
            mgm_id=1,
            new_nwobj_uids=new_nwobj_uids,
        )

        # Assert
        assert new_nwobjs == [
            NetworkObjectForImport(
                nw_object=fwconfig_import_object.normalized_config.network_objects[uid],
                mgm_id=1,
                import_id=fwconfig_import_object.import_state.state.import_id,
                color_id=fwconfig_import_object.import_state.state.lookup_color_id(
                    fwconfig_import_object.normalized_config.network_objects[uid].obj_color
                ),
                typ_id=fwconfig_import_object.lookup_obj_type(
                    fwconfig_import_object.normalized_config.network_objects[uid].obj_typ
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
    ):
        # Arrange
        fwconfig_import_object.import_state.state.lookup_management_id = mocker.Mock(return_value=None)
        fwconfig_import_object.import_state.api_call.call = mocker.Mock()

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
            )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_not_called()

    def test_update_objects_via_api_with_errors(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
    ):
        # Arrange
        fwconfig_import_object.import_state.state.lookup_management_id = mocker.Mock(return_value=1)
        mock_get_graphql_code(mocker, "importObjectsMutation")
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
            )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_called_once()

    def test_update_objects_via_api_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
    ):
        # Arrange
        fwconfig_import_object.import_state.state.lookup_management_id = mocker.Mock(return_value=1)
        mock_get_graphql_code(mocker, "importObjectsMutation")
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
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
            )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_called_once()

    def test_update_objects_via_api_with_wrong_response_format(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
    ):
        # Arrange
        fwconfig_import_object.import_state.state.lookup_management_id = mocker.Mock(return_value=1)
        mock_get_graphql_code(mocker, "importObjectsMutation")
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(return_value={"unexpected_key": {}})

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
            )

        # Assert
        fwconfig_import_object.import_state.api_call.call.assert_called_once()

    def test_update_objects_via_api(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
    ):
        # Arrange
        FWOLogger.instance.debug_level = 9
        m_open = mocker.patch("builtins.open", new_callable=mock_open)
        fwconfig_import_object.import_state.state.lookup_management_id = mocker.Mock(return_value=1)
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
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
        expected_path = "/usr/local/fworch/tmp/import/mgm_id_3_query_variables.json"
        m_open.assert_called_with(expected_path, "w")


class TestFwConfigImportObjectGetProtocolMap:
    def test_get_protocol_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_ip_proto": [
                        {
                            "ip_proto_name": "tcp",
                            "ip_proto_id": 6,
                        }
                    ]
                }
            }
        )
        expected_protocol_map = {
            "tcp": 6,
        }

        # Act
        protocol_map = fwconfig_import_object.get_protocol_map()

        # Assert
        assert protocol_map == expected_protocol_map

    def test_get_protocol_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act
        protocol_map = fwconfig_import_object.get_protocol_map()

        # Assert
        mock_logger.assert_called_once()
        assert protocol_map == {}


class TestFwConfigImportObjectGetUserObjTypeMap:
    def test_get_userobj_type_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_usr_typ": [
                        {
                            "usr_typ_name": "user",
                            "usr_typ_id": 1,
                        }
                    ]
                }
            }
        )
        expected_userobj_type_map = {
            "user": 1,
        }

        # Act
        userobj_type_map = fwconfig_import_object.get_user_obj_type_map()

        # Assert
        assert userobj_type_map == expected_userobj_type_map

    def test_get_userobj_type_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act
        userobj_type_map = fwconfig_import_object.get_user_obj_type_map()

        # Assert
        mock_logger.assert_called_once()
        assert userobj_type_map == {}


class TestFwConfigImportObjectGetServiceObjTypeMap:
    def test_get_serviceobj_type_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_svc_typ": [
                        {
                            "svc_typ_name": "service",
                            "svc_typ_id": 1,
                        }
                    ]
                }
            }
        )
        expected_serviceobj_type_map = {
            "service": 1,
        }

        # Act
        serviceobj_type_map = fwconfig_import_object.get_service_obj_type_map()

        # Assert
        assert serviceobj_type_map == expected_serviceobj_type_map

    def test_get_serviceobj_type_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act
        serviceobj_type_map = fwconfig_import_object.get_service_obj_type_map()

        # Assert
        mock_logger.assert_called_once()
        assert serviceobj_type_map == {}


class TestFwConfigImportObjectGetNetworkObjTypeMap:
    def test_get_networkobj_type_map(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "stm_obj_typ": [
                        {
                            "obj_typ_name": "network",
                            "obj_typ_id": 1,
                        }
                    ]
                }
            }
        )
        expected_networkobj_type_map = {
            "network": 1,
        }

        # Act
        networkobj_type_map = fwconfig_import_object.get_network_obj_type_map()

        # Assert
        assert networkobj_type_map == expected_networkobj_type_map

    def test_get_networkobj_type_map_with_exception(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
    ):
        # Arrange
        fwconfig_import_object.import_state.api_call.call = mocker.Mock(
            side_effect=Exception("Unexpected error occurred")
        )
        mock_logger = mocker.patch("fwo_log.FWOLogger.error")

        # Act
        networkobj_type_map = fwconfig_import_object.get_network_obj_type_map()

        # Assert
        mock_logger.assert_called_once()
        assert networkobj_type_map == {}


class TestFwConfigImportObjectUpdateObjectDiffs:
    def test_update_object_diffs_no_normalized_config(
        self,
        fwconfig_import_object: FwConfigImportObject,
        fw_config_manager: FwConfigManager,
    ):
        # Arrange
        fwconfig_import_object.normalized_config = None

        # Act
        with pytest.raises(FwoImporterError):
            fwconfig_import_object.update_object_diffs(
                prev_config=FwConfigNormalized(),
                prev_global_config=None,
                single_manager=fw_config_manager,
            )

    def test_update_object_diffs_changes_and_filters(
        self,
        fwconfig_import_object: FwConfigImportObject,
        mocker: MockerFixture,
        fw_config_manager: FwConfigManager,
        fwconfig_builder: FwConfigBuilder,
    ):
        # Arrange:
        prev_config, _ = fwconfig_builder.build_config(
            network_object_count=10,
            service_object_count=10,
            rulebase_count=3,
            rules_per_rulebase_count=10,
            user_object_count=10,
            zone_object_count=3,
        )

        curr_config = copy.deepcopy(prev_config)

        first_nwobj_uid = next(iter(prev_config.network_objects.keys()))
        first_svcobj_uid = next(iter(prev_config.service_objects.keys()))
        first_userobj_uid = next(iter(prev_config.users.keys()))
        first_zone_name = next(iter(prev_config.zone_objects.keys()))

        first_nwobj = curr_config.network_objects[first_nwobj_uid]
        first_svcobj = curr_config.service_objects[first_svcobj_uid]
        first_userobj = curr_config.users[first_userobj_uid]
        first_zoneobj = curr_config.zone_objects[first_zone_name]

        first_nwobj.obj_name = "modified-name"
        first_svcobj.svc_name = "modified-name"
        first_userobj["user_name"] = "modified-name"
        first_zoneobj["zone_name"] = "modified-name"

        fwconfig_import_object.normalized_config = curr_config

        fwconfig_import_object.uid2id_mapper.update_network_object_mapping = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.update_service_object_mapping = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.update_user_mapping = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.update_zone_mapping = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.add_network_object_mappings = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.add_service_object_mappings = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.add_user_mappings = mocker.Mock()
        fwconfig_import_object.uid2id_mapper.add_zone_mappings = mocker.Mock()

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

        stats = fwconfig_import_object.import_state.state.stats
        stats.increment_network_object_add_count = mocker.Mock()
        stats.increment_network_object_delete_count = mocker.Mock()
        stats.increment_network_object_change_count = mocker.Mock()
        stats.increment_service_object_add_count = mocker.Mock()
        stats.increment_service_object_delete_count = mocker.Mock()
        stats.increment_service_object_change_count = mocker.Mock()

        # Act
        fwconfig_import_object.update_object_diffs(
            prev_config=prev_config,
            prev_global_config=None,
            single_manager=fw_config_manager,
        )

        # Assert: update_objects_via_api called with expected uid sets
        args, _ = fwconfig_import_object.update_objects_via_api.call_args
        assert args[0] is fw_config_manager
        assert set(args[1]) == {first_nwobj_uid}
        assert set(args[2]) == {first_svcobj_uid}
        assert set(args[3]) == {first_userobj_uid}
        assert set(args[4]) == {first_zone_name}
        assert set(args[5]) == {first_nwobj_uid}
        assert set(args[6]) == {first_svcobj_uid}
        assert set(args[7]) == {first_userobj_uid}
        assert set(args[8]) == {first_zone_name}

        assert fwconfig_import_object.remove_outdated_memberships.call_count == 3
        assert fwconfig_import_object.add_group_memberships.call_count == 3

        fwconfig_import_object.add_changelog_objs.assert_called_once_with(
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
