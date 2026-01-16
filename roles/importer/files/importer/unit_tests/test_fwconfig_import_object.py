from typing import Any, cast

import pytest
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoDuplicateKeyViolationError, FwoImporterError
from fwo_log import ChangeLogger
from model_controllers.fwconfig_import_object import FwConfigImportObject
from model_controllers.import_state_controller import ImportStateController
from pytest_mock import MockerFixture


class PartialDict(dict[str, Any]):
    def __eq__(self, other: object) -> bool:
        try:
            if not isinstance(other, dict):
                return False

            # Checks if this dict's keys/values are a subset of 'other'
            other_dict: dict[str, Any] = cast(dict[str, Any], other)
            return all(other_dict.get(k) == v for k, v in self.items())
        except Exception:
            return False


class TestFwConfigImportObjectAddChangelogObjs:
    def test_add_changelog_objects(
        self, fwconfig_import_object: FwConfigImportObject, api_call: FwoApiCall, mocker: MockerFixture
    ):
        # Arrange
        api_call.call = mocker.Mock(
            return_value={
                "data": {
                    "add_nwobj_changelog": {
                        "nwobj_changelog": {
                            "id": 1,
                        }
                    },
                    "add_svc_changelog": {
                        "svc_changelog": {
                            "id": 2,
                        }
                    },
                }
            }
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
                "nwObjChanges": [
                    PartialDict(
                        {
                            "change_action": "I",
                            "change_type_id": 3,
                            "new_obj_id": 1,
                            "old_obj_id": None,
                            "unique_name": "1",
                        }
                    ),
                    PartialDict(
                        {
                            "change_action": "D",
                            "change_type_id": 3,
                            "new_obj_id": None,
                            "old_obj_id": 2,
                            "unique_name": "2",
                        }
                    ),
                ],
                "svcObjChanges": [
                    PartialDict(
                        {
                            "change_action": "I",
                            "change_type_id": 3,
                            "new_svc_id": 3,
                            "old_svc_id": None,
                            "unique_name": "3",
                        }
                    ),
                    PartialDict(
                        {
                            "change_action": "D",
                            "change_type_id": 3,
                            "new_svc_id": None,
                            "old_svc_id": 4,
                            "unique_name": "4",
                        }
                    ),
                ],
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
        self, fwconfig_import_object: FwConfigImportObject, import_state_controller: ImportStateController
    ):
        # Act
        nwobjs_changed, svcobjs_changed = fwconfig_import_object.prepare_changelog_objects(
            nw_obj_ids_added=[{"obj_id": 1}],
            nw_obj_ids_removed=[{"obj_id": 2}],
            svc_obj_ids_added=[{"svc_id": 3}],
            svc_obj_ids_removed=[{"svc_id": 4}],
        )

        # Assert
        assert nwobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 3,
                    "new_obj_id": 1,
                    "old_obj_id": None,
                    "unique_name": "1",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 3,
                    "new_obj_id": None,
                    "old_obj_id": 2,
                    "unique_name": "2",
                }
            ),
        ]
        assert svcobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 3,
                    "new_svc_id": 3,
                    "old_svc_id": None,
                    "unique_name": "3",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 3,
                    "new_svc_id": None,
                    "old_svc_id": 4,
                    "unique_name": "4",
                }
            ),
        ]

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
        assert nwobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 2,
                    "new_obj_id": 1,
                    "old_obj_id": None,
                    "unique_name": "1",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 2,
                    "new_obj_id": None,
                    "old_obj_id": 2,
                    "unique_name": "2",
                }
            ),
        ]
        assert svcobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 2,
                    "new_svc_id": 3,
                    "old_svc_id": None,
                    "unique_name": "3",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 2,
                    "new_svc_id": None,
                    "old_svc_id": 4,
                    "unique_name": "4",
                }
            ),
        ]

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
        assert nwobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 2,
                    "new_obj_id": 1,
                    "old_obj_id": None,
                    "unique_name": "1",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 2,
                    "new_obj_id": None,
                    "old_obj_id": 2,
                    "unique_name": "2",
                }
            ),
        ]
        assert svcobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 2,
                    "new_svc_id": 3,
                    "old_svc_id": None,
                    "unique_name": "3",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 2,
                    "new_svc_id": None,
                    "old_svc_id": 4,
                    "unique_name": "4",
                }
            ),
        ]

    def test_prepare_changelog_objects_with_logged_changes(
        self, fwconfig_import_object: FwConfigImportObject, import_state_controller: ImportStateController
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
        assert nwobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 3,
                    "new_obj_id": 1,
                    "old_obj_id": None,
                    "unique_name": "1",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 3,
                    "new_obj_id": None,
                    "old_obj_id": 2,
                    "unique_name": "2",
                }
            ),
            PartialDict(
                {
                    "change_action": "C",
                    "change_type_id": 3,
                    "new_obj_id": 10,
                    "old_obj_id": 1,
                    "unique_name": "10",
                }
            ),
        ]
        assert svcobjs_changed == [
            PartialDict(
                {
                    "change_action": "I",
                    "change_type_id": 3,
                    "new_svc_id": 3,
                    "old_svc_id": None,
                    "unique_name": "3",
                }
            ),
            PartialDict(
                {
                    "change_action": "D",
                    "change_type_id": 3,
                    "new_svc_id": None,
                    "old_svc_id": 4,
                    "unique_name": "4",
                }
            ),
            PartialDict(
                {
                    "change_action": "C",
                    "change_type_id": 3,
                    "new_svc_id": 30,
                    "old_svc_id": 3,
                    "unique_name": "30",
                }
            ),
        ]


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
