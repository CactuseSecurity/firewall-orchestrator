"""
import_rule_hits: rule hit import run of a single management (requested by the middleware scheduler).

The regular import does not write last hits; this run reads the last hit of all rules per gateway from the
firewall management and stores the changed ones, holding the import lock of the management meanwhile
(import_control row of type rule hit, which never counts as configuration change).

Expects service_provider to be initialized and the import state to be registered.
"""

from __future__ import annotations

import traceback
from typing import TYPE_CHECKING

import fwo_const
from common import get_module, is_responsible_importer
from fwo_exceptions import FwoApiFailedLockImportError, ShutdownRequestedError
from fwo_log import FWOLogger
from model_controllers.rule_hit_import import RuleHitImport
from services.service_provider import ServiceProvider

if TYPE_CHECKING:
    from fwo_api_call import FwoApiCall
    from model_controllers.import_state_controller import ImportStateController

RULE_HIT_IMPORT_ALERT_SEVERITY = 2


def import_rule_hits(api_call: FwoApiCall) -> bool:
    """
    Run the requested rule hit import of the management of the registered import state.

    The request is removed once this importer is responsible for the management, also if the run is skipped or
    fails: the hits are refreshed with the next scheduled request at the latest.

    Args:
        api_call: FWO API connection of the importer.

    Returns:
        True if the rule hits were imported, False if the run was skipped or failed.

    """
    import_state = ServiceProvider().get_global_state().import_state
    mgm_details = import_state.state.mgm_details
    if not is_responsible_importer(mgm_details.importer_hostname):
        FWOLogger.debug(f"rule hit import - this host is not responsible for management {mgm_details.mgm_id}")
        import_state.state.responsible_for_importing = False
        return False

    api_call.delete_rule_hit_import_request(mgm_details.mgm_id)
    if mgm_details.import_disabled:
        FWOLogger.info(f"rule hit import - import disabled for management {mgm_details.mgm_id}, skipping")
        return False

    fw_module = get_module(import_state.state)
    if not fw_module.supports_rule_hits():
        FWOLogger.debug(f"rule hit import - management {mgm_details.mgm_id} cannot read rule hits, skipping")
        return False

    try:
        import_state.state.import_id = api_call.set_import_lock(
            mgm_details, is_initial_import=False, import_type_id=fwo_const.IMPORT_TYPE_RULE_HIT
        )
    except FwoApiFailedLockImportError:
        return False

    return _run_locked_rule_hit_import(import_state, api_call)


def _run_locked_rule_hit_import(import_state: ImportStateController, api_call: FwoApiCall) -> bool:
    """Read and store the rule hits while holding the import lock, then release the lock."""
    mgm_details = import_state.state.mgm_details
    exception: Exception | None = None
    try:
        FWOLogger.info(
            f"starting rule hit import of management {mgm_details.name} ({mgm_details.mgm_id!s}), "
            f"import_id={import_state.state.import_id!s}"
        )
        fw_module = get_module(import_state.state)
        rule_hits = fw_module.get_rule_hits(import_state)
        RuleHitImport(import_state).write_rule_hits(rule_hits)
    except (KeyboardInterrupt, ShutdownRequestedError):
        import_state.delete_import()  # nothing to roll back: hits written so far are valid
        raise
    except Exception as e:
        exception = e
        FWOLogger.error(f"rule hit import of management {mgm_details.mgm_id} failed: {traceback.format_exc()!s}")

    api_call.unlock_import(import_state.state, success=exception is None)
    if exception is not None:
        api_call.set_alert(
            import_id=import_state.state.import_id,
            title="rule hit import error",
            mgm_id=mgm_details.mgm_id,
            severity=RULE_HIT_IMPORT_ALERT_SEVERITY,
            description=f"rule hit import failed: {type(exception).__name__}: {exception!s}",
            source="import",
            alert_code=fwo_const.ALERT_CODE_IMPORT_ERROR_UNSPECIFIC,
            mgm_details=mgm_details,
        )
        return False
    return True
