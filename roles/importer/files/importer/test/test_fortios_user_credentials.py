from fw_modules.fortiosmanagementREST.fos_models import UserObjGroup, UserObjLocal

SECRET_MARKER = "fwo-sweep-credential-marker"  # noqa: S105


def test_local_user_credentials_are_dropped_when_parsing() -> None:
    user = UserObjLocal.model_validate(
        {
            "name": "alice",
            "q_origin_key": "alice",
            "id": 1,
            "status": "enable",
            "type": "password",
            "passwd": SECRET_MARKER,
            "fortitoken": SECRET_MARKER,
            "ppk-secret": SECRET_MARKER,
        }
    )

    assert user.name == "alice"
    assert SECRET_MARKER not in user.model_dump_json()


def test_guest_group_credentials_are_dropped_when_parsing() -> None:
    group = UserObjGroup.model_validate(
        {
            "name": "guests",
            "q_origin_key": "guests",
            "id": 2,
            "group-type": "guest",
            "password": SECRET_MARKER,
            "guest": [{"user-id": "guest1", "password": SECRET_MARKER}],
        }
    )

    assert group.name == "guests"
    assert SECRET_MARKER not in group.model_dump_json()
