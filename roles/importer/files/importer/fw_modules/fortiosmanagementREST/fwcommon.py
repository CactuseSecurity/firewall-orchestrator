from fw_modules.fortiosmanagementREST import fos_getter, fos_normalizer
from fw_modules.fortiosmanagementREST.fos_models import FortiOSConfig
from fwo_base import ensure_device_name, write_native_config_to_file
from fwo_exceptions import FwoNativeConfigParseError
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon
from models.fwconfigmanager import FwConfigManager
from pydantic import ValidationError


class FortiosManagementRESTCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        ensure_manager_set(config_in, import_state)
        ensure_device_name(import_state)
        if config_in.native_config_is_empty():
            # get native config via REST API
            fm_api_url = (
                f"https://{import_state.state.mgm_details.hostname}:{import_state.state.mgm_details.port!s}/api/v2"
            )
            sid = import_state.state.mgm_details.secret
            native_config = fos_getter.get_native_config(fm_api_url, sid)
            config_in.native_config = native_config.model_dump(by_alias=True)
        else:
            # parse native config from config file
            try:
                native_config = FortiOSConfig.model_validate(config_in.native_config, by_alias=True)
            except ValidationError as ve:
                raise FwoNativeConfigParseError("Error while parsing FortiOS native config from file: " + str(ve))

        write_native_config_to_file(import_state.state, config_in.native_config)

        normalized_config = fos_normalizer.normalize_config(native_config, mgm_details=import_state.state.mgm_details)

        config_in.ManagerSet[0].configs = [normalized_config]
        config_in.ManagerSet[0].manager_uid = import_state.state.mgm_details.uid

        return 0, config_in


def ensure_manager_set(config_in: FwConfigManagerListController, import_state: ImportStateController) -> None:
    if len(config_in.ManagerSet) > 0:
        return
    config_in.add_manager(
        manager=FwConfigManager(
            manager_uid=import_state.state.mgm_details.uid,
            manager_name=import_state.state.mgm_details.name,
            is_super_manager=import_state.state.mgm_details.is_super_manager,
            sub_manager_ids=import_state.state.mgm_details.sub_manager_ids,
            domain_name=import_state.state.mgm_details.domain_name,
            domain_uid=import_state.state.mgm_details.domain_uid,
            configs=[],
        )
    )
