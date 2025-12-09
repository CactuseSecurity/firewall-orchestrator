import json
from typing import Any

from fwo_log import FWOLogger


def extract_access_role_user(src: dict[str, Any]) -> tuple[str, str, str, str | None, str | None]:
    """Extract user data from access-role type source object."""
    user_name = src["name"]
    user_uid = src["uid"]
    user_typ = "group"
    user_comment = src.get("comments")
    user_color = src.get("color")
    if "users" in src:
        user_typ = "simple"
    return user_name, user_uid, user_typ, user_comment, user_color


def extract_legacy_user_at_location(src: dict[str, Any]) -> tuple[str, str | None, str, str | None, str | None]:
    """Extract user data from LegacyUserAtLocation type source object."""
    user_str = src["name"]
    user_ar = user_str.split("@")
    user_name = user_ar[0]
    user_uid = src.get("userGroup")
    user_typ = "group"
    user_comment = src.get("comments")
    user_color = src.get("color")
    return user_name, user_uid, user_typ, user_comment, user_color


def normalize_user_data(user_comment: str | None, user_color: str | None) -> tuple[str | None, str]:
    """Normalize user comment and color values."""
    if user_comment == "":
        user_comment = None
    if user_color is None:
        user_color = "black"
    return user_comment, user_color


def process_typed_source_object(src: dict[str, Any], users: dict[str, Any]) -> None:
    """Process a source object that has a type field."""
    if src["type"] == "access-role":
        user_name, user_uid, user_typ, user_comment, user_color = extract_access_role_user(src)
    elif src["type"] == "LegacyUserAtLocation":
        user_name, user_uid, user_typ, user_comment, user_color = extract_legacy_user_at_location(src)
    else:
        return

    user_comment, user_color = normalize_user_data(user_comment, user_color)

    users.update(
        {
            user_name: {
                "user_uid": user_uid,
                "user_typ": user_typ,
                "user_comment": user_comment,
                "user_color": user_color,
            }
        }
    )


def process_untyped_source_object(src: dict[str, Any], users: dict[str, Any]) -> None:
    """Process a source object that lacks a type field."""
    FWOLogger.warning("found src user without type field: " + json.dumps(src))
    if "name" in src and "uid" in src:
        users.update({src["name"]: {"user_uid": src["uid"], "user_typ": "simple"}})


def process_standard_rule(rule: dict[str, Any], users: dict[str, Any]) -> None:
    """Process a standard rule to extract user information."""
    if "type" not in rule or rule["type"] == "place-holder":
        return

    for src in rule["source"]:
        if "type" in src:
            process_typed_source_object(src, users)
        else:
            process_untyped_source_object(src, users)


def collect_users_from_rule(rule: dict[str, Any], users: dict[str, Any]) -> None:
    """Collect user information from a single rule."""
    if "rule-number" in rule:  # standard rule
        process_standard_rule(rule, users)
    else:  # section
        collect_users_from_rulebase(rule["rulebase"], users)


# collect_users writes user info into global users dict
def collect_users_from_rulebase(rulebase: dict[str, Any], users: dict[str, Any]) -> None:
    if "rulebase_chunks" in rulebase:
        for chunk in rulebase["rulebase_chunks"]:
            if "rulebase" in chunk:
                for rule in chunk["rulebase"]:
                    collect_users_from_rule(rule, users)
    else:
        for rule in rulebase:
            collect_users_from_rule(rule, users)  # type: ignore #TODO refactor this


# the following is only used within new python-only importer:
def parse_user_objects_from_rulebase(rulebase: dict[str, Any], users: dict[str, Any], import_id: str) -> None:
    collect_users_from_rulebase(rulebase, users)
    for user_name in users:
        # TODO: get user info via API
        _ = get_user_uid_from_cp_api(user_name)
        # finally add the import id
        users[user_name]["control_id"] = import_id


def get_user_uid_from_cp_api(user_name: str) -> str:
    # show-object with UID
    # dummy implementation returning the name as uid
    return user_name


def normalize_users_legacy() -> None:
    raise NotImplementedError
