import time
from copy import deepcopy
from typing import Any

import fwo_const
import fwo_globals
from fw_modules.checkpointR8x import cp_const, cp_gateway, cp_getter, cp_network, cp_rule, cp_service
from fwo_base import ConfigAction
from fwo_exceptions import FwLoginFailedError, FwoImporterError, ImportInterruptionError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from model_controllers.management_controller import ManagementController
from models.fw_common import FwCommon
from models.fwconfig_normalized import FwConfigNormalized
from models.fwconfigmanagerlist import FwConfigManager
from models.import_state import ImportState
from utils.conversion_utils import convert_list_to_dict


class CheckpointR8xCommon(FwCommon):
    def has_config_changed(
        self, full_config: FwConfigManagerListController, import_state: ImportStateController, force: bool = False
    ) -> bool:
        if full_config:  # a config was passed in (read from file), so we assume that an import has to be done (simulating changes here)
            return True

        session_id: str = cp_getter.login(import_state.mgm_details)

        if import_state.last_successful_import is None or import_state.last_successful_import == "" or force:
            # if no last import time found or given or if force flag is set, do full import
            result = True
        else:  # otherwise search for any changes since last import
            result = (
                cp_getter.get_changes(
                    session_id,
                    import_state.mgm_details.hostname,
                    str(import_state.mgm_details.port),
                    import_state.last_successful_import,
                )
                != 0
            )

        cp_getter.logout(import_state.mgm_details.buildFwApiString(), session_id)

        return result > 0

    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        return get_config(config_in, import_state)


def get_config(
    config_in: FwConfigManagerListController, import_state: ImportStateController
) -> tuple[int, FwConfigManagerListController]:
    FWOLogger.debug("starting checkpointR8x/get_config")

    parsing_config_only = (
        not config_in.has_empty_config()
    )  # no native config was passed in, so getting it from FW-Manager

    if not parsing_config_only:  # get config from cp fw mgr
        starttime = int(time.time())
        initialize_native_config(config_in, import_state.state)

        start_time_temp = int(time.time())
        FWOLogger.debug("checkpointR8x/get_config/getting objects ...")

        if config_in.native_config is None:
            raise FwoImporterError("native_config is None in get_config")

        # IMPORTANT: cp api is expected to preserve order of refs in group objects (unlike refs in rules, which are sorted later)
        result_get_objects = get_objects(config_in.native_config, import_state.state)
        if result_get_objects > 0:
            raise FwLoginFailedError("checkpointR8x/get_config/error while gettings objects")
        FWOLogger.debug("checkpointR8x/get_config/fetched objects in " + str(int(time.time()) - start_time_temp) + "s")

        start_time_temp = int(time.time())
        FWOLogger.debug("checkpointR8x/get_config/getting rules ...")
        result_get_rules = get_rules(config_in.native_config, import_state.state)
        if result_get_rules > 0:
            raise FwLoginFailedError("checkpointR8x/get_config/error while gettings rules")
        FWOLogger.debug("checkpointR8x/get_config/fetched rules in " + str(int(time.time()) - start_time_temp) + "s")

        duration = int(time.time()) - starttime
        FWOLogger.debug("checkpointR8x/get_config - fetch duration: " + str(duration) + "s")

    if config_in.contains_only_native():
        sid: str = cp_getter.login(import_state.state.mgm_details)
        normalized_config = normalize_config(import_state.state, config_in, parsing_config_only, sid)
        FWOLogger.info("completed getting config")
        return 0, normalized_config
    # we already have a native config (from file import)
    return 0, config_in


def initialize_native_config(config_in: FwConfigManagerListController, import_state: ImportState) -> None:
    """
    Create domain structure in nativeConfig
    """
    manager_details_list = create_ordered_manager_list(import_state)
    if config_in.native_config is None:
        raise FwoImporterError("native_config is None in initialize_native_config")
    config_in.native_config.update({"domains": []})
    for manager_details in manager_details_list:
        config_in.native_config["domains"].append(
            {
                "domain_name": manager_details.domain_name,
                "domain_uid": manager_details.domain_uid,
                "is-super-manager": manager_details.is_super_manager,
                "management_name": manager_details.name,
                "management_uid": manager_details.uid,
                "objects": [],
                "rulebases": [],
                "nat_rulebases": [],
                "gateways": [],
            }
        )


def normalize_config(
    import_state: ImportState, config_in: FwConfigManagerListController, parsing_config_only: bool, sid: str
) -> FwConfigManagerListController:
    native_and_normalized_config_dict_list: list[dict[str, Any]] = []

    if config_in.native_config is None:
        raise FwoImporterError("Did not get a native config to normalize.")

    if "domains" not in config_in.native_config:
        FWOLogger.error("No domains found in native config. Cannot normalize config.")
        raise FwoImporterError("No domains found in native config. Cannot normalize config.")

    # in case of mds, first nativ config domain is global
    is_global_loop_iteration = False
    native_config_global: dict[str, Any] | None = None
    normalized_config_global = {}
    if config_in.native_config["domains"][0]["is-super-manager"]:
        native_config_global = config_in.native_config["domains"][0]
        is_global_loop_iteration = True

    for native_conf in config_in.native_config["domains"]:
        normalized_config_dict = deepcopy(fwo_const.EMPTY_NORMALIZED_FW_CONFIG_JSON_DICT)
        normalize_single_manager_config(
            native_conf,
            native_config_global,
            normalized_config_dict,
            normalized_config_global,
            import_state,
            parsing_config_only,
            sid,
            is_global_loop_iteration,
        )

        native_and_normalized_config_dict_list.append({"native": native_conf, "normalized": normalized_config_dict})

        if is_global_loop_iteration:
            normalized_config_global = normalized_config_dict
            is_global_loop_iteration = False

    for native_and_normalized_config_dict in native_and_normalized_config_dict_list:
        normalized_config = FwConfigNormalized(
            action=ConfigAction.INSERT,
            network_objects=convert_list_to_dict(
                native_and_normalized_config_dict["normalized"]["network_objects"], "obj_uid"
            ),
            service_objects=convert_list_to_dict(
                native_and_normalized_config_dict["normalized"]["service_objects"], "svc_uid"
            ),
            zone_objects=convert_list_to_dict(
                native_and_normalized_config_dict["normalized"]["zone_objects"], "zone_name"
            ),
            rulebases=native_and_normalized_config_dict["normalized"]["policies"],
            gateways=native_and_normalized_config_dict["normalized"]["gateways"],
        )
        manager = FwConfigManager(
            manager_name=native_and_normalized_config_dict["native"]["management_name"],
            manager_uid=native_and_normalized_config_dict["native"]["management_uid"],
            is_super_manager=native_and_normalized_config_dict["native"]["is-super-manager"],
            sub_manager_ids=[],
            domain_name=native_and_normalized_config_dict["native"]["domain_name"],
            domain_uid=native_and_normalized_config_dict["native"]["domain_uid"],
            configs=[normalized_config],
        )
        config_in.ManagerSet.append(manager)

    return config_in


def normalize_single_manager_config(
    native_config: dict[str, Any],
    native_config_global: dict[str, Any] | None,
    normalized_config_dict: dict[str, Any],
    normalized_config_global: dict[str, Any],
    import_state: ImportState,
    parsing_config_only: bool,
    sid: str,
    is_global_loop_iteration: bool,
):
    cp_network.normalize_network_objects(
        native_config, normalized_config_dict, import_state.import_id, mgm_id=import_state.mgm_details.mgm_id
    )
    FWOLogger.info("completed normalizing network objects")
    cp_service.normalize_service_objects(native_config, normalized_config_dict, import_state.import_id)
    FWOLogger.info("completed normalizing service objects")
    cp_gateway.normalize_gateways(native_config, import_state, normalized_config_dict)
    cp_rule.normalize_rulebases(
        native_config,
        native_config_global,
        import_state,
        normalized_config_dict,
        normalized_config_global,
        is_global_loop_iteration,
    )
    if not parsing_config_only:  # get config from cp fw mgr
        cp_getter.logout(import_state.mgm_details.build_fw_api_string(), sid)
    FWOLogger.info("completed normalizing rulebases")


def get_rules(native_config: dict[str, Any], import_state: ImportState) -> int:
    """
    Main function to get rules. Divided into smaller sub-tasks for better readability and maintainability.
    """
    show_params_policy_structure: dict[str, Any] = {
        "limit": import_state.fwo_config.api_fetch_size,
        "details-level": "full",
    }

    global_assignments, global_policy_structure, global_domain, global_sid = None, None, None, None
    manager_details_list = create_ordered_manager_list(import_state)
    for manager_index, manager_details in enumerate(manager_details_list):
        cp_manager_api_base_url = import_state.mgm_details.build_fw_api_string()

        if manager_details.is_super_manager:
            global_assignments, global_policy_structure, global_domain, global_sid = handle_super_manager(
                manager_details, cp_manager_api_base_url, show_params_policy_structure
            )

        sid: str = cp_getter.login(manager_details)
        policy_structure: list[dict[str, Any]] = []
        cp_getter.get_policy_structure(
            cp_manager_api_base_url,
            sid,
            show_params_policy_structure,
            manager_details,
            policy_structure=policy_structure,
        )

        process_devices(
            manager_details,
            policy_structure,
            global_assignments,
            global_policy_structure,
            global_domain,
            global_sid,
            cp_manager_api_base_url,
            sid,
            native_config["domains"][
                manager_index
            ],  # globalSid should not be None but is when the first manager is not supermanager
            native_config["domains"][0],
            import_state,
        )
        native_config["domains"][manager_index].update({"policies": policy_structure})

    return 0


def create_ordered_manager_list(import_state: ImportState) -> list[ManagementController]:
    """
    Creates list of manager details, supermanager is first
    """
    manager_details_list: list[ManagementController] = [deepcopy(import_state.mgm_details)]
    if import_state.mgm_details.is_super_manager:
        manager_details_list.extend([deepcopy(sub_manager) for sub_manager in import_state.mgm_details.sub_managers])  # type: ignore TODO: why we are adding submanagers as ManagementController?
    return manager_details_list


def handle_super_manager(
    manager_details: ManagementController, cp_manager_api_base_url: str, show_params_policy_structure: dict[str, Any]
) -> tuple[list[Any], list[Any] | None, Any | None, str]:
    # global assignments are fetched from mds domain
    mds_sid: str = cp_getter.login(manager_details)
    global_policy_structure: list[Any] | None = []
    global_domain = None
    global_assignments = cp_getter.get_global_assignments(
        cp_manager_api_base_url, mds_sid, show_params_policy_structure
    )
    global_sid = ""
    # import global policies if at least one global assignment exists

    if len(global_assignments) > 0:
        if "global-domain" in global_assignments[0] and "uid" in global_assignments[0]["global-domain"]:
            global_domain = global_assignments[0]["global-domain"]["uid"]

            # policy structure is fetched from global domain
            manager_details.domain_uid = global_domain
            global_sid: str = cp_getter.login(manager_details)
            cp_getter.get_policy_structure(
                cp_manager_api_base_url,
                global_sid,
                show_params_policy_structure,
                manager_details,
                policy_structure=global_policy_structure,
            )
        else:
            raise FwoImporterError(f"Unexpected global assignments: {global_assignments!s}")

    if len(global_policy_structure) == 0:
        global_policy_structure = None

    return global_assignments, global_policy_structure, global_domain, global_sid


def process_devices(
    manager_details: ManagementController,
    policy_structure: list[dict[str, Any]],
    global_assignments: list[Any] | None,
    global_policy_structure: list[dict[str, Any]] | None,
    global_domain: str | None,
    global_sid: str | None,
    cp_manager_api_base_url: str,
    sid: str,
    native_config_domain: dict[str, Any],
    native_config_global_domain: dict[str, Any],
    import_state: ImportState,
) -> None:
    for device in manager_details.devices:
        device_config = initialize_device_config(device)
        if not device_config:
            continue

        ordered_layer_uids, policy_dict = get_ordered_layer_uids(
            policy_structure, device_config, manager_details.get_domain_string()
        )
        if not ordered_layer_uids or not policy_dict:
            FWOLogger.warning(f"No ordered layers found for device: {device_config['name']}")
            native_config_domain["gateways"].append(device_config)
            continue

        global_ordered_layer_count = 0
        if import_state.mgm_details.is_super_manager:
            global_ordered_layer_count = handle_global_rulebase_links(
                manager_details,
                import_state,
                device_config,
                global_assignments,
                global_policy_structure,
                global_domain,
                global_sid,
                ordered_layer_uids,
                native_config_global_domain,
                cp_manager_api_base_url,
            )
        else:
            define_initial_rulebase(device_config, ordered_layer_uids, is_global=False)

        add_ordered_layers_to_native_config(
            ordered_layer_uids,
            get_rules_params(import_state),
            cp_manager_api_base_url,
            sid,
            native_config_domain,
            device_config,
            is_global=False,
            global_ordered_layer_count=global_ordered_layer_count,
        )

        fetched_nat_rulebases: list[str] = []
        fetched_but_empty_nat_rulebases: list[str] = []
        handle_nat_rules(
            policy_dict,
            device_config,
            native_config_domain,
            sid,
            import_state,
            fetched_nat_rulebases,
            fetched_but_empty_nat_rulebases,
        )

        native_config_domain["gateways"].append(device_config)


def initialize_device_config(device: dict[str, Any]) -> dict[str, Any]:
    if "name" in device and "uid" in device:
        return {"name": device["name"], "uid": device["uid"], "rulebase_links": []}
    raise FwoImporterError(f"Device missing name or uid: {device}")


def handle_global_rulebase_links(
    manager_details: ManagementController,
    import_state: ImportState,
    device_config: dict[str, Any],
    global_assignments: list[Any] | None,
    global_policy_structure: list[dict[str, Any]] | None,
    global_domain: str | None,
    global_sid: str | None,
    ordered_layer_uids: list[str],
    native_config_global_domain: dict[str, Any],
    cp_manager_api_base_url: str,
) -> int:
    """
    Searches for global access policy for current device policy,
    adds global ordered layers and defines global rulebase link
    """
    if global_assignments is None:
        raise FwoImporterError("Global assignments is None in handle_global_rulebase_links")

    if global_policy_structure is None:
        raise FwoImporterError("Global policy structure is None in handle_global_rulebase_links")

    for global_assignment in global_assignments:
        if global_assignment["dependent-domain"]["uid"] != manager_details.get_domain_string():
            continue
        for global_policy in global_policy_structure:
            if global_policy["name"] == global_assignment["global-access-policy"]:
                # no global NAT, so global_policy_dict not used
                global_ordered_layer_uids, _global_policy_dict = get_ordered_layer_uids(
                    [global_policy], device_config, global_domain
                )
                if not global_ordered_layer_uids:
                    FWOLogger.warning(f"No access layer for global policy: {global_policy['name']}")
                    break

                global_ordered_layer_count = len(global_ordered_layer_uids)
                global_policy_rulebases_uid_list = add_ordered_layers_to_native_config(
                    global_ordered_layer_uids,
                    get_rules_params(import_state),
                    cp_manager_api_base_url,
                    global_sid,
                    native_config_global_domain,
                    device_config,
                    is_global=True,
                    global_ordered_layer_count=global_ordered_layer_count,
                )
                define_global_rulebase_link(
                    device_config,
                    global_ordered_layer_uids,
                    ordered_layer_uids,
                    native_config_global_domain,
                    global_policy_rulebases_uid_list,
                )

                return global_ordered_layer_count

    return 0


def define_global_rulebase_link(
    device_config: dict[str, Any],
    global_ordered_layer_uids: list[str],
    ordered_layer_uids: list[str],
    native_config_global_domain: dict[str, Any],
    global_policy_rulebases_uid_list: list[str],
):
    """
    Links initial and placeholder rule for global rulebases
    """
    define_initial_rulebase(device_config, global_ordered_layer_uids, is_global=True)

    # parse global rulebases, find place-holders and link local rulebases
    placeholder_link_index = 0
    for global_rulebase_uid in global_policy_rulebases_uid_list:
        placeholder_rule_uid = ""
        for rulebase in native_config_global_domain["rulebases"]:
            if rulebase["uid"] == global_rulebase_uid:
                placeholder_rule_uid, placeholder_rulebase_uid = cp_getter.get_placeholder_in_rulebase(rulebase)

                if placeholder_rule_uid:
                    ordered_layer_uid = ""
                    # we might find more than one placeholder, may be unequal to number of domain ordered layers
                    if len(ordered_layer_uids) > placeholder_link_index:
                        ordered_layer_uid = ordered_layer_uids[placeholder_link_index]

                    device_config["rulebase_links"].append(
                        {
                            "from_rulebase_uid": placeholder_rulebase_uid,
                            "from_rule_uid": None,
                            "to_rulebase_uid": ordered_layer_uid,
                            "type": "domain",
                            "is_global": False,
                            "is_initial": False,
                            "is_section": False,
                        }
                    )

                    placeholder_link_index += 1


def define_initial_rulebase(device_config: dict[str, Any], ordered_layer_uids: list[str], is_global: bool):
    device_config["rulebase_links"].append(
        {
            "from_rulebase_uid": None,
            "from_rule_uid": None,
            "to_rulebase_uid": ordered_layer_uids[0],
            "type": "ordered",
            "is_global": is_global,
            "is_initial": True,
            "is_section": False,
        }
    )


def get_rules_params(import_state: ImportState) -> dict[str, Any]:
    return {
        "limit": import_state.fwo_config.api_fetch_size,
        "use-object-dictionary": cp_const.use_object_dictionary,
        "details-level": "standard",
        "show-hits": cp_const.with_hits,
    }


def handle_nat_rules(
    policy_dict: dict[str, Any],
    device_config: dict[str, Any],
    native_config_domain: dict[str, Any],
    sid: str,
    import_state: ImportState,
    fetched_nat_rulebases: list[str],
    fetched_but_empty_nat_rulebases: list[str],
):
    """Get nat rulebases, name and uid get _nat prefix and link to access rulebase"""
    if policy_dict["uid"] not in fetched_nat_rulebases + fetched_but_empty_nat_rulebases:
        show_params_rules: dict[str, Any] = {
            "limit": import_state.fwo_config.api_fetch_size,
            "use-object-dictionary": cp_const.use_object_dictionary,
            "details-level": "standard",
            "package": policy_dict["name"],
        }
        FWOLogger.debug(f"Getting NAT rules for package: {policy_dict['name']}", 4)
        nat_rules = cp_getter.get_nat_rules_from_api_as_dict(
            policy_dict,
            import_state.mgm_details.build_fw_api_string(),
            sid,
            show_params_rules,
            native_config_domain,
        )
        if nat_rules["chunks"]:
            native_config_domain["nat_rulebases"].append(nat_rules)
            fetched_nat_rulebases.append(policy_dict["uid"])  # uid without _nat postfix
        else:
            fetched_but_empty_nat_rulebases.append(policy_dict["uid"])  # uid without _nat postfix

    if policy_dict["uid"] in fetched_nat_rulebases:
        link_nat_rulebase_sections(policy_dict["uid"], native_config_domain["nat_rulebases"], device_config)


def link_nat_rulebase_sections(policy_dict_uid: str, nat_rulebases: list[Any], device_config: dict[str, Any]):
    current_nat_rulebase_uid = policy_dict_uid + "_nat"
    device_config["rulebase_links"].append(
        {
            "from_rulebase_uid": policy_dict_uid,
            "from_rule_uid": None,
            "to_rulebase_uid": current_nat_rulebase_uid,
            "type": "nat",
            "is_global": False,
            "is_initial": False,
            "is_section": False,
        }
    )
    for nat_rulebase in nat_rulebases:
        if current_nat_rulebase_uid == nat_rulebase["uid"]:
            for nat_rulebase_chunk in nat_rulebase["chunks"]:
                if "rulebase" in nat_rulebase_chunk:
                    define_nat_section_chain(current_nat_rulebase_uid, nat_rulebase_chunk, device_config)


def define_nat_section_chain(
    current_nat_rulebase_uid: str, nat_rulebase_chunk: dict[str, Any], device_config: dict[str, Any]
):
    for nat_section in nat_rulebase_chunk["rulebase"]:
        if nat_section["type"] == "nat-section":
            device_config["rulebase_links"].append(
                {
                    "from_rulebase_uid": current_nat_rulebase_uid,
                    "from_rule_uid": None,
                    "to_rulebase_uid": nat_section["uid"],
                    "type": "concatenated",
                    "is_global": False,
                    "is_initial": False,
                    "is_section": True,
                }
            )
            current_nat_rulebase_uid = nat_section["uid"]


def add_ordered_layers_to_native_config(
    ordered_layer_uids: list[str],
    show_params_rules: dict[str, Any],
    cp_manager_api_base_url: str,
    sid: str | None,
    native_config_domain: dict[str, Any],
    device_config: dict[str, Any],
    is_global: bool,
    global_ordered_layer_count: int,
) -> list[str]:
    """
    Fetches ordered layers and links them
    """
    policy_rulebases_uid_list = []
    for ordered_layer_index, ordered_layer_uid in enumerate(ordered_layer_uids):
        show_params_rules.update({"uid": ordered_layer_uid})

        policy_rulebases_uid_list = cp_getter.get_rulebases(
            cp_manager_api_base_url,
            sid,
            show_params_rules,
            native_config_domain,
            device_config,
            policy_rulebases_uid_list,
            is_global=is_global,
            access_type="access",
            rulebase_uid=ordered_layer_uid,
        )

        # link to next ordered layer
        # in case of mds: domain ordered layers are linked once there is no global ordered layer counterpart
        if (is_global or ordered_layer_index >= global_ordered_layer_count - 1) and (
            ordered_layer_index < len(ordered_layer_uids) - 1
        ):
            device_config["rulebase_links"].append(
                {
                    "from_rulebase_uid": ordered_layer_uid,
                    "from_rule_uid": None,
                    "to_rulebase_uid": ordered_layer_uids[ordered_layer_index + 1],
                    "type": "ordered",
                    "is_global": is_global,
                    "is_initial": False,
                    "is_section": False,
                }
            )

    return policy_rulebases_uid_list


def get_ordered_layer_uids(
    policy_structure: list[dict[str, Any]], device_config: dict[str, Any], domain: str | None
) -> tuple[list[str], dict[str, Any]]:
    """
    Get UIDs of ordered layers for policy of device
    """
    ordered_layer_uids: list[str] = []
    policy_dict: dict[str, str] = {}
    failsafe_multiple_policies_per_device = False
    for policy in policy_structure:
        found_target_in_policy = False
        for target in policy["targets"]:
            if target["uid"] == device_config["uid"] or target["uid"] == "all":
                found_target_in_policy = True
                check_if_multiple_policies_per_device(failsafe_multiple_policies_per_device, device_config["uid"])
                failsafe_multiple_policies_per_device = True
        if found_target_in_policy:
            append_access_layer_uid(policy, domain, ordered_layer_uids)
            policy_dict = policy

    return ordered_layer_uids, policy_dict


def check_if_multiple_policies_per_device(failsafe_multiple_policies_per_device: bool, device_config_uid: str):
    if failsafe_multiple_policies_per_device:
        raise FwoImporterError("multiple policies for device " + device_config_uid)


def append_access_layer_uid(policy: dict[str, Any], domain: str | None, ordered_layer_uids: list[str]) -> None:
    ordered_layer_uids.extend(
        [
            access_layer["uid"]
            for access_layer in policy["access-layers"]
            if access_layer["domain"] == domain or domain == ""
        ]
    )


def get_objects(native_config_dict: dict[str, Any], import_state: ImportState) -> int:
    show_params_objs = {"limit": import_state.fwo_config.api_fetch_size}
    manager_details_list = create_ordered_manager_list(import_state)

    # loop over sub-managers in case of mds
    manager_index = 0
    for manager_details in manager_details_list:
        if manager_details.import_disabled and not import_state.force_import:
            continue

        is_stand_alone_manager = len(manager_details_list) == 1
        if manager_details.is_super_manager or is_stand_alone_manager:
            obj_type_array = cp_const.api_obj_types
        else:
            obj_type_array = cp_const.local_api_obj_types

        if manager_details.is_super_manager:
            # for super managers we need to get both the global domain data and the Check Point Data (perdefined objects)

            # Check Point Data (perdefined objects)
            manager_details.domain_name = ""
            manager_details.domain_uid = ""  # Check Point Data
            get_objects_per_domain(
                manager_details,
                native_config_dict["domains"][0],
                obj_type_array,
                show_params_objs,
                is_stand_alone_manager=is_stand_alone_manager,
            )

            # global domain containing the manually added global objects
            manager_details.domain_name = "Global"
            manager_details.domain_uid = "Global"
            get_objects_per_domain(
                manager_details,
                native_config_dict["domains"][0],
                obj_type_array,
                show_params_objs,
                is_stand_alone_manager=is_stand_alone_manager,
            )
        else:
            get_objects_per_domain(
                manager_details,
                native_config_dict["domains"][manager_index],
                obj_type_array,
                show_params_objs,
                is_stand_alone_manager=is_stand_alone_manager,
            )

        manager_index += 1
    return 0


def get_objects_per_domain(
    manager_details: ManagementController,
    native_domain: dict[str, Any],
    obj_type_array: list[str],
    show_params_objs: dict[str, Any],
    is_stand_alone_manager: bool = True,
) -> None:
    sid = cp_getter.login(manager_details)
    cp_url = manager_details.build_fw_api_string()
    for obj_type in obj_type_array:
        object_table = get_objects_per_type(obj_type, show_params_objs, sid, cp_url)
        add_special_objects_to_global_domain(object_table, obj_type, sid, cp_api_url=cp_url)
        if not is_stand_alone_manager and not manager_details.is_super_manager:
            remove_predefined_objects_for_domains(object_table)
        native_domain["objects"].append(object_table)


def remove_predefined_objects_for_domains(object_table: dict[str, Any]) -> None:
    if (
        "chunks" in object_table
        and "type" in object_table
        and object_table["type"] in cp_const.types_to_remove_globals_from
    ):
        return

    for chunk in object_table["chunks"]:
        if "objects" in chunk:
            for obj in chunk["objects"]:
                domain_type = obj.get("domain", {}).get("domain-type", "")
                if domain_type != "domain":
                    chunk["objects"].remove(obj)


def get_objects_per_type(
    obj_type: str, show_params_objs: dict[str, Any], sid: str, cp_manager_api_base_url: str
) -> dict[str, Any]:
    if fwo_globals.shutdown_requested:
        raise ImportInterruptionError("Shutdown requested during object retrieval.")
    if obj_type in cp_const.obj_types_full_fetch_needed:
        show_params_objs.update({"details-level": cp_const.details_level_group_objects})
    else:
        show_params_objs.update({"details-level": cp_const.details_level_objects})
    object_table: dict[str, Any] = {"type": obj_type, "chunks": []}
    current = 0
    total = current + 1
    show_cmd = "show-" + obj_type
    FWOLogger.debug("obj_type: " + obj_type, 6)

    while current < total:
        show_params_objs["offset"] = current
        objects = cp_getter.cp_api_call(cp_manager_api_base_url, show_cmd, show_params_objs, sid)
        if fwo_globals.shutdown_requested:
            raise ImportInterruptionError("Shutdown requested during object retrieval.")

        object_table["chunks"].append(objects)
        if "total" in objects and "to" in objects:
            total = objects["total"]
            current = objects["to"]
            FWOLogger.debug(obj_type + " current:" + str(current) + " of a total " + str(total), 6)
        else:
            current = total

    return object_table


def add_special_objects_to_global_domain(
    object_table: dict[str, Any], obj_type: str, sid: str, cp_api_url: str
) -> None:
    """
    Appends special objects Original, Any, None and Internet to global domain
    """
    # getting Original (NAT) object (both for networks and services)
    orig_obj = cp_getter.get_object_details_from_api(cp_const.original_obj_uid, sid=sid, apiurl=cp_api_url)["chunks"][0]
    any_obj = cp_getter.get_object_details_from_api(cp_const.any_obj_uid, sid=sid, apiurl=cp_api_url)["chunks"][0]
    none_obj = cp_getter.get_object_details_from_api(cp_const.none_obj_uid, sid=sid, apiurl=cp_api_url)["chunks"][0]
    internet_obj = cp_getter.get_object_details_from_api(cp_const.internet_obj_uid, sid=sid, apiurl=cp_api_url)[
        "chunks"
    ][0]

    if obj_type == "networks":
        object_table["chunks"].append(orig_obj)
        object_table["chunks"].append(any_obj)
        object_table["chunks"].append(none_obj)
        object_table["chunks"].append(internet_obj)
    if obj_type == "services-other":
        object_table["chunks"].append(orig_obj)
        object_table["chunks"].append(any_obj)
        object_table["chunks"].append(none_obj)
