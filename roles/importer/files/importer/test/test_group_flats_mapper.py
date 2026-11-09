from fwo_const import LIST_DELIMITER
from pytest_mock import MockerFixture
from services.group_flats_mapper import CONFIG_NOT_SET_MESSAGE, GroupFlatsMapper
from test.utils.config_builder import FwConfigBuilder


class TestGroupFlatsMapperConfigNotSet:
    def test_get_network_object_flats_without_config_returns_empty_set(self, mocker: MockerFixture) -> None:
        mapper = GroupFlatsMapper()
        mock_log_error = mocker.patch.object(mapper, "log_error")

        flats = mapper.get_network_object_flats(["some-uid"])

        assert flats == set()
        mock_log_error.assert_called_once_with(f"{CONFIG_NOT_SET_MESSAGE} - networks")

    def test_get_service_object_flats_without_config_returns_empty_set(self, mocker: MockerFixture) -> None:
        mapper = GroupFlatsMapper()
        mock_log_error = mocker.patch.object(mapper, "log_error")

        flats = mapper.get_service_object_flats(["some-uid"])

        assert flats == set()
        mock_log_error.assert_called_once_with(f"{CONFIG_NOT_SET_MESSAGE} - services")

    def test_get_user_flats_without_config_returns_empty_set(self, mocker: MockerFixture) -> None:
        mapper = GroupFlatsMapper()
        mock_log_error = mocker.patch.object(mapper, "log_error")

        flats = mapper.get_user_flats(["some-uid"])

        assert flats == set()
        mock_log_error.assert_called_once_with(f"{CONFIG_NOT_SET_MESSAGE} - users")


class TestGroupFlatsMapperDuplicateMembers:
    def test_get_network_object_flats_deduplicates_members(self, fwconfig_builder: FwConfigBuilder) -> None:
        # Arrange: outer group contains inner group and member, inner group contains member twice
        config = fwconfig_builder.build_empty_config()
        outer_group = fwconfig_builder.add_network_object(config)
        inner_group = fwconfig_builder.add_network_object(config)
        member = fwconfig_builder.add_network_object(config)
        outer_group.obj_typ = "group"
        outer_group.obj_member_refs = f"{inner_group.obj_uid}{LIST_DELIMITER}{member.obj_uid}"
        inner_group.obj_typ = "group"
        inner_group.obj_member_refs = f"{member.obj_uid}{LIST_DELIMITER}{member.obj_uid}"
        member.obj_typ = "host"
        member.obj_member_refs = None
        mapper = GroupFlatsMapper()
        mapper.init_config(config)

        # Act
        flats = mapper.get_network_object_flats([outer_group.obj_uid, inner_group.obj_uid])

        # Assert
        assert isinstance(flats, set)
        assert flats == {outer_group.obj_uid, inner_group.obj_uid, member.obj_uid}

    def test_get_service_object_flats_deduplicates_members(self, fwconfig_builder: FwConfigBuilder) -> None:
        # Arrange: outer group contains inner group and member, inner group contains member twice
        config = fwconfig_builder.build_empty_config()
        outer_group = fwconfig_builder.add_service_object(config)
        inner_group = fwconfig_builder.add_service_object(config)
        member = fwconfig_builder.add_service_object(config)
        outer_group.svc_member_refs = f"{inner_group.svc_uid}{LIST_DELIMITER}{member.svc_uid}"
        inner_group.svc_member_refs = f"{member.svc_uid}{LIST_DELIMITER}{member.svc_uid}"
        member.svc_typ = "simple"
        member.svc_member_refs = None
        mapper = GroupFlatsMapper()
        mapper.init_config(config)

        # Act
        flats = mapper.get_service_object_flats([outer_group.svc_uid, inner_group.svc_uid])

        # Assert
        assert isinstance(flats, set)
        assert flats == {outer_group.svc_uid, inner_group.svc_uid, member.svc_uid}

    def test_get_user_flats_deduplicates_members(self, fwconfig_builder: FwConfigBuilder) -> None:
        # Arrange: outer group contains inner group and member, inner group contains member twice
        config = fwconfig_builder.build_empty_config()
        member = fwconfig_builder.add_user_object(config)
        inner_group = fwconfig_builder.add_user_group_object(config, member_count=0)
        outer_group = fwconfig_builder.add_user_group_object(config, member_count=0)
        inner_group["user_member_refs"] = f"{member['user_uid']}{LIST_DELIMITER}{member['user_uid']}"
        outer_group["user_member_refs"] = f"{inner_group['user_uid']}{LIST_DELIMITER}{member['user_uid']}"
        mapper = GroupFlatsMapper()
        mapper.init_config(config)

        # Act
        flats = mapper.get_user_flats([outer_group["user_uid"], inner_group["user_uid"]])

        # Assert
        assert isinstance(flats, set)
        assert flats == {outer_group["user_uid"], inner_group["user_uid"], member["user_uid"]}
