from contextlib import AbstractContextManager
from typing import TYPE_CHECKING, Protocol, cast

import pytest
from fw_modules.azure2022ff.fwcommon import Azure2022ffCommon
from fw_modules.generic.fwcommon import GenericFirewallCommon
from model_controllers.fwconfigmanagerlist_controller import FwConfigManagerListController
from models.fw_common import FwCommon
from models.fwconfig_normalized import FwConfigNormalized

if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController


class PytestApi(Protocol):
    def raises(
        self,
        expected_exception: type[Exception],
        *,
        match: str,
    ) -> AbstractContextManager[Exception]: ...


NormalizedOnlyImporterCase = tuple[FwCommon, type[Exception], str]

NORMALIZED_ONLY_IMPORTERS: tuple[NormalizedOnlyImporterCase, ...] = (
    (GenericFirewallCommon(), ValueError, "requires a normalized config"),
    (Azure2022ffCommon(), NotImplementedError, "only supports normalized config parsing"),
)
IMPORT_STATE = cast("ImportStateController", object())
PYTEST = cast("PytestApi", pytest)


def assert_get_config_rejected(
    fw_common: FwCommon,
    exception_type: type[Exception],
    message: str,
    config: FwConfigManagerListController,
) -> None:
    with PYTEST.raises(exception_type, match=message):
        fw_common.get_config(config, IMPORT_STATE)


def test_get_config_accepts_normalized_config() -> None:
    for fw_common, _, _ in NORMALIZED_ONLY_IMPORTERS:
        config = FwConfigManagerListController.generate_empty_config()
        manager = config.get_first_manager()
        assert manager is not None
        manager.configs.append(FwConfigNormalized())

        status, result = fw_common.get_config(config, IMPORT_STATE)

        assert status == 0
        assert result is config


def test_get_config_rejects_empty_config() -> None:
    for fw_common, exception_type, message in NORMALIZED_ONLY_IMPORTERS:
        config = FwConfigManagerListController.generate_empty_config()

        assert_get_config_rejected(fw_common, exception_type, message, config)


def test_get_config_rejects_native_only_config() -> None:
    for fw_common, exception_type, message in NORMALIZED_ONLY_IMPORTERS:
        config = FwConfigManagerListController.generate_empty_config()
        config.native_config = {"native": "config"}

        assert_get_config_rejected(fw_common, exception_type, message, config)
