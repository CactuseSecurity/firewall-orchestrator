from fw_modules.fortiosmanagementREST import fos_getter, fos_normalizer
from fwo_base import write_native_config_to_file
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon


class FortiosManagementRESTCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:
        if config_in.native_config_is_empty():
            # get native config via REST API
            fm_api_url = (
                f"https://{import_state.state.mgm_details.hostname}:{import_state.state.mgm_details.port!s}/api/v2"
            )
            sid = import_state.state.mgm_details.secret
            native_config = fos_getter.get_native_config(fm_api_url, sid)
            config_in.native_config = native_config.model_dump(by_alias=True)

        write_native_config_to_file(import_state.state, config_in.native_config)

        fos_normalizer.normalize_config(config_in, import_state)

        return 0, config_in
