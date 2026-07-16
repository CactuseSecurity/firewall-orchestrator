from fwo_log import FWOLogger
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from model_controllers.import_state_controller import ImportStateController
from models.fw_common import FwCommon


class Azure2022ffCommon(FwCommon):
    def get_config(
        self, config_in: FwConfigManagerListController, import_state: ImportStateController
    ) -> tuple[int, FwConfigManagerListController]:

        return get_config(config_in, import_state)


def get_config(
    config_in: FwConfigManagerListController, _import_state: ImportStateController
) -> tuple[int, FwConfigManagerListController]:
    FWOLogger.debug("starting azure2022ff/get_config")

    if config_in.has_empty_config() or config_in.contains_only_native():
        raise NotImplementedError(
            "Azure 2022 ff import currently only supports normalized config parsing, not native config retrieval from FW-Manager."
        )

    return 0, config_in
