from fw_modules.guardicoremanagementREST import gc_getter, gc_normalizer
from fw_modules.guardicoremanagementREST.gc_gateway import ensure_policy_gateway
from fw_modules.guardicoremanagementREST.gc_models import GuardicoreConfig
from fwo_base import ensure_device_name, write_native_config_to_file
from fwo_exceptions import FwoNativeConfigParseError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon
from models.fwconfigmanager import FwConfigManager
from pydantic import ValidationError


class GuardicoreManagementRESTCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        mgm_details = import_state.state.mgm_details
        ensure_manager_set(config_in, import_state)
        ensure_device_name(import_state)
        if config_in.native_config_is_empty():
            base_url = gc_getter.build_api_base_url(mgm_details.hostname, mgm_details.port)
            config_in.native_config = gc_getter.get_native_config(base_url, mgm_details.import_user, mgm_details.secret)

        try:
            native_config = GuardicoreConfig.model_validate(config_in.native_config)
        except ValidationError as ve:
            raise FwoNativeConfigParseError(f"Error while parsing Guardicore native config: {ve!s}")

        ensure_policy_gateway(import_state)
        write_native_config_to_file(import_state.state, config_in.native_config)

        config_in.ManagerSet[0].configs = [gc_normalizer.normalize_config(native_config, mgm_details)]
        config_in.ManagerSet[0].manager_uid = mgm_details.uid

        return 0, config_in


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
