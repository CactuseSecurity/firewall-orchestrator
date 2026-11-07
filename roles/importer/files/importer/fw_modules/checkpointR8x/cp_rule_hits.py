"""
Rule hit import for Check Point: reads the last hit of every access rule per gateway.

The regular import does not request hits. Here the access layers of each gateway's policy are fetched with
hits-settings.target set to the gateway, so Check Point reports the hits of that gateway only. Layers shared by
several gateways are therefore fetched once per gateway.
Global layers of a multi-domain server are not covered, as their hits are only counted in the domains.
"""

from __future__ import annotations

from typing import TYPE_CHECKING, Any, cast

from fw_modules.checkpointR8x import cp_getter
from fw_modules.checkpointR8x.fwcommon import (
    add_ordered_layers_to_native_config,
    create_ordered_manager_list,
    get_ordered_layer_uids,
    get_policy_for_device,
    get_rules_params,
    initialize_device_config,
)
from fwo_log import FWOLogger
from models.rule_hit import RuleHit

if TYPE_CHECKING:
    from model_controllers.management_controller import ManagementController
    from models.import_state import ImportState

ACCESS_RULE_TYPE = "access-rule"
# sections only contain rules, inline layers are fetched as rulebases of their own
MAX_SECTION_DEPTH = 100


def get_rule_hits(import_state: ImportState) -> list[RuleHit]:
    """
    Read the last hit of all access rules per gateway of the management and its domains.

    Args:
        import_state: State of the rule hit import run.

    Returns:
        One rule hit per gateway and rule that was hit at least once.

    """
    api_base_url = import_state.mgm_details.build_fw_api_string()
    rule_hits: list[RuleHit] = []
    for manager_details in create_ordered_manager_list(import_state):
        if manager_details.import_disabled and not import_state.force_import:
            continue
        if len(manager_details.devices) == 0:  # e.g. the multi-domain server itself
            continue
        with cp_getter.api_session(manager_details) as sid:
            rule_hits.extend(get_rule_hits_of_manager(manager_details, api_base_url, sid, import_state))
    return rule_hits


def get_rule_hits_of_manager(
    manager_details: ManagementController,
    api_base_url: str,
    sid: str,
    import_state: ImportState,
) -> list[RuleHit]:
    """Read the rule hits of all gateways of a single (domain) management."""
    policy_structure: list[dict[str, Any]] = []
    show_params_policy_structure: dict[str, Any] = {
        "limit": import_state.fwo_config.api_fetch_size,
        "details-level": "full",
    }
    cp_getter.get_policy_structure(
        api_base_url, sid, show_params_policy_structure, manager_details, policy_structure=policy_structure
    )
    gateways_and_servers = cp_getter.get_gateways_and_servers(sid, api_base_url)

    rule_hits: list[RuleHit] = []
    for device in manager_details.devices:
        if device["importDisabled"] and not import_state.force_import:
            continue
        policy = get_policy_for_device(device, gateways_and_servers, policy_structure)
        if policy is None:
            FWOLogger.warning(f"rule hit import - no policy found for gateway {device['uid']}, skipping")
            continue
        device_config = initialize_device_config(device)
        ordered_layer_uids = get_ordered_layer_uids(policy, device_config, manager_details.get_domain_string())
        if len(ordered_layer_uids) <= 1:
            FWOLogger.warning(f"rule hit import - no ordered layers found for gateway {device['uid']}, skipping")
            continue

        # a fresh container per gateway: layers fetched for another gateway carry that gateway's hits
        native_rulebases: dict[str, Any] = {"rulebases": [], "nat_rulebases": [], "objects": []}
        add_ordered_layers_to_native_config(
            ordered_layer_uids,
            get_rule_hits_params(import_state, device["uid"]),
            api_base_url,
            sid,
            native_rulebases,
            device_config,
            is_global=False,
            global_ordered_layer_count=0,
        )
        for rule_uid, last_hit in collect_last_hits(native_rulebases["rulebases"]).items():
            rule_hits.append(
                RuleHit(
                    rule_mgm_id=manager_details.mgm_id,
                    rule_uid=rule_uid,
                    gateway_mgm_id=manager_details.mgm_id,
                    gateway_uid=device["uid"],
                    last_hit=last_hit,
                )
            )
    return rule_hits


def get_rule_hits_params(import_state: ImportState, gateway_uid: str) -> dict[str, Any]:
    """Return the show-access-rulebase parameters requesting the hits of a single gateway."""
    params = get_rules_params(import_state)
    params.update({"show-hits": True, "hits-settings": {"target": gateway_uid}})
    return params


def collect_last_hits(native_rulebases: list[dict[str, Any]]) -> dict[str, str]:
    """
    Collect the last hit date of all access rules in the fetched rulebase chunks.

    Args:
        native_rulebases: Rulebases as fetched by cp_getter.get_rulebases (with "chunks").

    Returns:
        Rule uid to the ISO 8601 date of its last hit, for rules that were hit at least once.

    """
    last_hits: dict[str, str] = {}
    for native_rulebase in native_rulebases:
        for chunk in native_rulebase.get("chunks", []):
            _collect_last_hits_of_entries(chunk.get("rulebase", []), last_hits, depth=0)
    return last_hits


def _collect_last_hits_of_entries(entries: list[dict[str, Any]], last_hits: dict[str, str], depth: int) -> None:
    """Add the last hits of the rules in a rulebase chunk or section to last_hits."""
    if depth > MAX_SECTION_DEPTH:
        FWOLogger.warning("rule hit import - maximum section depth exceeded, ignoring deeper sections")
        return
    for entry in entries:
        if entry.get("type") == ACCESS_RULE_TYPE:
            last_hit = get_last_hit_of_rule(entry)
            if last_hit is not None and "uid" in entry:
                last_hits[entry["uid"]] = last_hit
        elif "rulebase" in entry:  # section
            _collect_last_hits_of_entries(entry["rulebase"], last_hits, depth + 1)


def get_last_hit_of_rule(native_rule: dict[str, Any]) -> str | None:
    """Return the ISO 8601 date of the rule's last hit, or None if the rule was never hit."""
    hits: Any = native_rule.get("hits")
    if not isinstance(hits, dict):
        return None
    last_date: Any = cast("dict[str, Any]", hits).get("last-date")
    if not isinstance(last_date, dict):
        return None
    iso_date: Any = cast("dict[str, Any]", last_date).get("iso-8601")
    return iso_date if isinstance(iso_date, str) else None
