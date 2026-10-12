"""
Rolls back firewall config imports which an importer process left unfinished.

An import holds its management's lock - an import_control row without stop_time - until it
completes or is rolled back. A process that is killed (SIGKILL once TimeoutStopSec expires, the OOM
killer, a power loss), or that loses the api during its own rollback, leaves that row behind. The
unique index uidx_import_control_only_one_null_stop_time_per_mgm_when_null then blocks every further
import of the management until someone rolls it back in the UI.

Only imports of managements this host is responsible for (importer_hostname) are touched, so the
imports of other importer hosts are left alone. An import that was running on this host when the
importer service (re)started, or when a cycle ended after an api outage, is rolled back - including
a manual import_mgm.py run.
"""

from __future__ import annotations

import traceback
from dataclasses import dataclass
from socket import gethostname
from typing import TYPE_CHECKING, Any

import fwo_const
from fwo_api import FwoApi
from fwo_exceptions import ShutdownRequestedError
from fwo_log import FWOLogger

if TYPE_CHECKING:
    from fwo_api_call import FwoApiCall


@dataclass(frozen=True)
class OrphanCleanupResult:
    """Outcome of an orphan cleanup: complete if every orphan was rolled back, and whose they were."""

    complete: bool
    rolled_back_mgm_ids: list[int]


def roll_back_orphaned_imports(fwo_api_call: FwoApiCall) -> OrphanCleanupResult:
    """Rolls back every unfinished config import of this host's managements."""
    query = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/getOrphanedImports.graphql"])
    result = fwo_api_call.call(
        query,
        query_variables={
            "importTypeId": fwo_const.CONFIG_IMPORT_TYPE_ID,
            "importerHostname": gethostname(),
        },
    )
    orphans: list[dict[str, Any]] = result["data"]["import_control"]
    if not orphans:
        return OrphanCleanupResult(complete=True, rolled_back_mgm_ids=[])
    rollback_mutation = FwoApi.get_graphql_code([fwo_const.GRAPHQL_QUERY_PATH + "import/rollbackImport.graphql"])
    rolled_back_mgm_ids = [
        mgm_id
        for mgm_id in (_try_roll_back(fwo_api_call, rollback_mutation, orphan) for orphan in orphans)
        if mgm_id is not None
    ]
    return OrphanCleanupResult(
        complete=len(rolled_back_mgm_ids) == len(orphans), rolled_back_mgm_ids=rolled_back_mgm_ids
    )


def try_roll_back_orphaned_imports(fwo_api_call: FwoApiCall) -> OrphanCleanupResult:
    """Like roll_back_orphaned_imports, but logs a failure and reports it as incomplete - except a shutdown."""
    try:
        return roll_back_orphaned_imports(fwo_api_call)
    except ShutdownRequestedError:
        raise
    except Exception:
        FWOLogger.error(f"could not roll back orphaned imports - retrying next cycle: {traceback.format_exc()}")
        return OrphanCleanupResult(complete=False, rolled_back_mgm_ids=[])


def _try_roll_back(fwo_api_call: FwoApiCall, rollback_mutation: str, orphan: dict[str, Any]) -> int | None:
    """
    Rolls back one orphan and returns its management id; a failure is logged and reported as None
    so the others still run.
    """
    try:
        rolled_back = _roll_back(fwo_api_call, rollback_mutation, orphan)
    except ShutdownRequestedError:
        raise
    except Exception:
        FWOLogger.error(f"could not roll back orphaned import {orphan['control_id']}: {traceback.format_exc()}")
        return None
    if not rolled_back:
        return None
    _report_rolled_back(fwo_api_call, orphan)
    return orphan["mgm_id"]


def _roll_back(fwo_api_call: FwoApiCall, rollback_mutation: str, orphan: dict[str, Any]) -> bool:
    import_id = orphan["control_id"]
    result = fwo_api_call.call(rollback_mutation, query_variables={"importId": import_id})
    if "errors" in result:
        FWOLogger.error(
            f"could not roll back orphaned import {import_id} of management {orphan['mgm_id']}: {result['errors']}"
        )
        return False
    FWOLogger.warning(
        f"rolled back import {import_id} of management {orphan['mgm_id']}, started {orphan['start_time']}, "
        "which the importer process left unfinished (it ended or lost the api during the import)"
    )
    return True


def _report_rolled_back(fwo_api_call: FwoApiCall, orphan: dict[str, Any]) -> None:
    """
    Records the rollback as a data issue of the management, so it is visible in the UI - a management
    whose import keeps getting the importer killed shows up there. A failure is only logged: the
    rollback itself succeeded.
    """
    try:
        fwo_api_call.create_data_issue(
            mgm_id=orphan["mgm_id"],
            severity=fwo_const.ORPHANED_IMPORT_DATA_ISSUE_SEVERITY,
            description=(
                f"import {orphan['control_id']} of this management was left unfinished "
                "(the importer process ended or lost the api during the import) and has been rolled back"
            ),
        )
    except ShutdownRequestedError:
        raise
    except Exception:
        FWOLogger.error(
            f"could not create the data issue for rolled back import {orphan['control_id']}: {traceback.format_exc()}"
        )
