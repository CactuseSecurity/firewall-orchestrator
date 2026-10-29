from __future__ import annotations

# resolves uid references in Check Point rules and objects against the fetched object dictionaries
from typing import Any

import fwo_const
from fw_modules.checkpointR8x import cp_const, cp_network
from fwo_log import FWOLogger


def find_element_by_uid(array: list[dict[str, Any]], uid: str | None) -> dict[str, Any] | None:
    for el in array:
        if "uid" in el and el["uid"] == uid:
            return el
    return None


def resolve_ref_from_object_dictionary(
    uid: str | None,
    obj_dict: list[dict[str, Any]],
    native_config_domain: dict[str, Any] | None = None,
    field_name: str | None = None,
) -> dict[str, Any] | None:
    if native_config_domain is None:
        native_config_domain = {}
    matched_obj = find_element_by_uid(obj_dict, uid)

    if matched_obj is None:  # object not in dict - need to fetch it from API
        if field_name != "track" and uid != "29e53e3d-23bf-48fe-b6b1-d59bd88036f9":
            # 29e53e3d-23bf-48fe-b6b1-d59bd88036f9 is a track object uid (track None) which is not in the object dictionary, but used in some rules
            if field_name is None:
                field_name = "unknown"
            if uid is None:
                uid = "unknown"
            FWOLogger.warning(f"object of type {field_name} with uid {uid} not found in object dictionary")
        return None
    # there are some objects (e.g. VoIP domains) which are not API-gettable with show-objects (only with show-object "UID")
    # these must be added to the (network) objects tables
    if cp_const.is_object_dictionary_only_obj_type(matched_obj["type"]):
        FWOLogger.info(
            f"adding {matched_obj['type']} '{matched_obj['name']}' object manually, because it is not retrieved by show objects API command"
        )
        color = matched_obj.get("color", "black")
        native_config_domain["objects"].append(
            {
                "type": matched_obj["type"],
                "chunks": [
                    {
                        "objects": [
                            {
                                "uid": matched_obj["uid"],
                                "name": matched_obj["name"],
                                "color": color,
                                "type": matched_obj["type"],
                                "domain": matched_obj["domain"],
                            }
                        ]
                    }
                ],
            }
        )

    return matched_obj


# resolving all uid references using the object dictionary
# dealing with a single chunk
def resolve_ref_list_from_object_dictionary(
    rulebase: list[dict[str, Any]] | dict[str, Any],
    value: str,
    obj_dicts: list[dict[str, Any]] | None = None,
    native_config_domain: dict[str, Any] | None = None,
) -> None:  # TODO: what is objDict: I think it should be a list of dicts
    if native_config_domain is None:
        native_config_domain = {}
    if obj_dicts is None:
        obj_dicts = []
    if isinstance(rulebase, dict) and "objects-dictionary" in rulebase:
        obj_dicts = rulebase["objects-dictionary"]
    if isinstance(rulebase, list):  # found a list of rules
        for rule in rulebase:
            if value in rule:
                categorize_value_for_resolve_ref(rule, value, obj_dicts, native_config_domain)  # type: ignore  # noqa: PGH003
            if "rulebase" in rule:
                resolve_ref_list_from_object_dictionary(
                    rule["rulebase"], value, obj_dicts=obj_dicts, native_config_domain=native_config_domain
                )
    elif "rulebase" in rulebase:
        resolve_ref_list_from_object_dictionary(
            rulebase["rulebase"], value, obj_dicts=obj_dicts, native_config_domain=native_config_domain
        )


def categorize_value_for_resolve_ref(
    rule: dict[str, Any], value: str, obj_dict: list[dict[str, Any]], native_config_domain: dict[str, Any]
) -> None:
    value_list: list[Any] = []
    if isinstance(rule[value], str):  # assuming single uid
        rule[value] = resolve_ref_from_object_dictionary(
            rule[value], obj_dict, native_config_domain=native_config_domain, field_name=value
        )
    elif "type" in rule[value]:  # e.g. track
        rule[value] = resolve_ref_from_object_dictionary(
            rule[value]["type"], obj_dict, native_config_domain=native_config_domain, field_name=value
        )
    else:  # assuming list of rules
        value_list.extend(
            [
                resolve_ref_from_object_dictionary(
                    rule_id, obj_dict, native_config_domain=native_config_domain, field_name=value
                )
                for rule_id in rule[value]
            ]
        )

        rule[value] = value_list  # replace ref list with object list


def handle_cpmi_any_object(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle CpmiAnyObject type objects."""
    color = obj.get("color", "black")

    if obj["name"] == "Any":
        return {
            "type": "hosts",
            "chunks": [
                {
                    "objects": [
                        {
                            "uid": obj["uid"],
                            "name": obj["name"],
                            "color": color,
                            "comments": "any nw object checkpoint (hard coded)",
                            "type": "network",
                            "ipv4-address": fwo_const.ANY_IP_IPV4,
                            "domain": obj["domain"],
                        }
                    ]
                }
            ],
        }
    if obj["name"] == "None":  # None service or network object
        return {
            "type": "hosts",
            "chunks": [
                {
                    "objects": [
                        {
                            "uid": obj["uid"],
                            "name": obj["name"],
                            "color": color,
                            "comments": "none nw object checkpoint (hard coded)",
                            "type": "group",
                            "domain": obj["domain"],
                        }
                    ]
                }
            ],
        }
    return {}


def handle_gateway_objects(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle various gateway and network member objects."""
    color = obj.get("color", "black")
    return {
        "type": "hosts",
        "chunks": [
            {
                "objects": [
                    {
                        "uid": obj["uid"],
                        "name": obj["name"],
                        "color": color,
                        "comments": obj["comments"],
                        "type": "host",
                        "ipv4-address": cp_network.get_ip_of_obj(obj),
                        "domain": obj["domain"],
                    }
                ]
            }
        ],
    }


def handle_global_object(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle Global type objects."""
    color = obj.get("color", "black")
    return {
        "type": "hosts",
        "chunks": [
            {
                "objects": [
                    {
                        "uid": obj["uid"],
                        "name": obj["name"],
                        "color": color,
                        "comments": obj["comments"],
                        "type": "host",
                        "ipv4-address": fwo_const.ANY_IP_IPV4,
                        "domain": obj["domain"],
                    }
                ]
            }
        ],
    }


def handle_updatable_objects(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle updatable objects and VoIP domains."""
    color = obj.get("color", "black")
    return {
        "type": "hosts",
        "chunks": [
            {
                "objects": [
                    {
                        "uid": obj["uid"],
                        "name": obj["name"],
                        "color": color,
                        "comments": obj["comments"],
                        "type": "host",
                        "domain": obj["domain"],
                    }
                ]
            }
        ],
    }


def handle_network_zone_objects(obj: dict[str, Any]) -> dict[str, Any]:
    """Handle Internet and security-zone objects."""
    color = obj.get("color", "black")
    return {
        "type": "hosts",
        "chunks": [
            {
                "objects": [
                    {
                        "uid": obj["uid"],
                        "name": obj["name"],
                        "color": color,
                        "comments": obj["comments"],
                        "type": "network",
                        "ipv4-address": fwo_const.ANY_IP_IPV4,
                        "domain": obj["domain"],
                    }
                ]
            }
        ],
    }
