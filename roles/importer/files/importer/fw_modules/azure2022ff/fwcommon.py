from fw_modules.azure2022ff import azure_getter, azure_normalizer
from fw_modules.azure2022ff.azure_models import AzureConfig
from fwo_base import write_native_config_to_file
from fwo_const import IMPORT_SKIP_UNSUPPORTED_RULES_KEY
from fwo_exceptions import FwoNativeConfigParseError
from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon
from models.fwconfigmanager import FwConfigManager
from pydantic import ValidationError


class Azure2022ffCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        return get_config(config_in, import_state)


def get_config(
    config_in: FwConfigManagerListController, import_state: ImportStateController
) -> tuple[int, FwConfigManagerListController]:
    FWOLogger.debug("starting azure2022ff/get_config")

    if has_normalized_config(config_in):
        return 0, config_in

    mgm_details = import_state.state.mgm_details
    devices = azure_getter.get_policy_devices(mgm_details, import_state.state.force_import)
    ensure_manager_set(config_in, import_state)

    if config_in.native_config_is_empty():
        native_config = azure_getter.get_native_config(mgm_details, devices)
        config_in.native_config = native_config.model_dump(by_alias=True)
    else:
        try:
            native_config = AzureConfig.model_validate(config_in.native_config)
        except ValidationError as ve:
            raise FwoNativeConfigParseError("Error while parsing Azure native config from file: " + str(ve))

    write_native_config_to_file(import_state.state, config_in.native_config)

    normalized_config = azure_normalizer.normalize_config(
        native_config, devices, mgm_details.uid, skip_unsupported_rules(import_state)
    )
    config_in.ManagerSet[0].configs = [normalized_config]
    config_in.ManagerSet[0].manager_uid = mgm_details.uid

    return 0, config_in


def skip_unsupported_rules(import_state: ImportStateController) -> bool:
    """Read the importer setting whether rules with unsupported features are skipped (default) or fail the import."""
    return import_state.api_call.get_config_value(key=IMPORT_SKIP_UNSUPPORTED_RULES_KEY) != "False"


def has_normalized_config(config_in: FwConfigManagerListController) -> bool:
    return config_in.native_config_is_empty() and any(len(manager.configs) > 0 for manager in config_in.ManagerSet)


def ensure_manager_set(config_in: FwConfigManagerListController, import_state: ImportStateController) -> None:
    if len(config_in.ManagerSet) > 0:
        return
    mgm_details = import_state.state.mgm_details
    config_in.add_manager(
        manager=FwConfigManager(
            manager_uid=mgm_details.uid,
            manager_name=mgm_details.name,
            is_super_manager=mgm_details.is_super_manager,
            sub_manager_ids=mgm_details.sub_manager_ids,
            domain_name=mgm_details.domain_name,
            domain_uid=mgm_details.domain_uid,
            configs=[],
        )
    )
