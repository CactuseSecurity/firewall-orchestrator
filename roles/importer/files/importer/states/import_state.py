import time
import traceback
from datetime import datetime, timezone

import networking.graphql.import_state_mutations as mutations
import networking.graphql.import_state_queries as queries
from dateutil import parser
from fwo_api import FwoApi
from fwo_api_call import FwoApiCall
from fwo_exceptions import FwoImporterError
from fwo_log import FWOLogger
from model_controllers.import_statistics_controller import ImportStatisticsController
from model_controllers.management_controller import ManagementController
from models.fwconfig_normalized import FwConfigNormalized
from services.uid2id_mapper import Uid2IdMapper


class ImportState:
    import_id: int = -1

    super_config: FwConfigNormalized | None = None
    previous_super_config: FwConfigNormalized | None = None

    super_uid2id_mapper: Uid2IdMapper | None = None

    statistics_controller: ImportStatisticsController

    mgm_details: ManagementController

    data_retention_days: int
    days_since_last_full_import: int
    last_full_import_id: int
    last_full_import_date: str | None = None
    last_successful_import: str | None = None
    is_full_import: bool = False
    is_initial_import: bool = False
    responsible_for_importing: bool = True
    input_file: str | None = None

    def __init__(self, mgm_id: int, fwo_api: FwoApi, fwo_api_call: FwoApiCall, input_file: str | None = None):
        self.input_file = input_file

        self.statistics_controller: ImportStatisticsController = ImportStatisticsController()
        self.start_time: int = int(time.time())
        self.management_map: dict[str, int] = {}
        self.rulebase_to_gateway_map: dict[int, list[int]] = {}
        self.data_retention_days: int = 30

        try:  # get mgm_details (fw-type, port, ip, user credentials):
            mgm_details = ManagementController.get_mgm_details(fwo_api, mgm_id)
        except Exception as _:
            FWOLogger.error(
                f"import_management - error while getting fw management details for mgm={mgm_id}: {traceback.format_exc()!s}"
            )
            raise

        try:  # get last import data
            _, last_import_date = fwo_api_call.get_last_complete_import({"mgmId": mgm_id})
        except Exception:
            FWOLogger.error(f"import_management - error while getting last import data for mgm={mgm_id}")
            raise

        self.mgm_details = ManagementController.from_json(mgm_details)
        self.last_full_import_date = last_import_date
        self.is_initial_import = last_import_date == ""

        self.get_past_import_infos(fwo_api_call)

    def set_import_file_name(self, import_file_name: str):
        self.import_file_name = import_file_name

    def set_import_id(self, import_id: int):
        self.import_id = import_id

    def lookup_management_id(self, mgm_uid: str) -> int | None:
        if not self.management_map.get(mgm_uid, None):
            FWOLogger.error(f"fwo_api:import_latest_config - no mgm id found for current manager uid '{mgm_uid}'")
        return self.management_map.get(mgm_uid, None)

    def get_past_import_infos(self, fwo_api_call: FwoApiCall):
        try:  # get past import details (LastFullImport, ...):
            day_string = fwo_api_call.get_config_value(key="dataRetentionTime")
            if day_string:
                self.data_retention_days = int(day_string)
            self.last_full_import_id, self.last_full_import_date = fwo_api_call.get_last_complete_import(
                {"mgmId": int(self.mgm_details.mgm_id)}
            )
        except Exception:
            FWOLogger.error(
                f"import_management - error while getting past import details for mgm={self.mgm_details.mgm_id!s}: {traceback.format_exc()!s}"
            )
            raise FwoImporterError(f"Error while getting past import details: {traceback.format_exc()!s}")

        if self.last_full_import_date != "":
            self.last_successful_import = self.last_full_import_date

            # Convert the string to a datetime object
            past_date = parser.parse(self.last_full_import_date)

            # Ensure "now" is timezone-aware (UTC here)
            now = datetime.now(timezone.utc)

            # Normalize pastDate too (convert to UTC if it had a tz)
            past_date = (
                past_date.replace(tzinfo=timezone.utc)
                if past_date.tzinfo is None
                else past_date.astimezone(timezone.utc)
            )

            difference = now - past_date

            self.days_since_last_full_import = difference.days
        else:
            self.days_since_last_full_import = 0

    # getting all managements (not limitited to the current mgm_id) to support super managements
    # creates a dict with key = management.uid  and value = management.id
    def set_management_map(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(
                query=queries.GET_MANAGEMENT_MAP,
                query_variables={"mgmId": self.mgm_details.mgm_id},
            )
        except Exception:
            FWOLogger.error("Error while getting managements")
            self.management_map = {}
            raise FwoImporterError("Error while getting managements")

        m: dict[str, int] = {}
        mgm = result["data"]["management"][0]
        m.update({mgm["mgm_uid"]: mgm["mgm_id"]})
        for sub_mgr in mgm["sub_managers"]:
            m.update({sub_mgr["mgm_uid"]: sub_mgr["mgm_id"]})

        self.management_map = m

    def delete_import(self, fwo_api_call: FwoApiCall):
        try:
            result = fwo_api_call.call(
                mutations.DELETE_IMPORT,
                query_variables={"importId": self.import_id},
            )
            _ = result["data"]["delete_import_control"]["affected_rows"]
            FWOLogger.info(f"removed import with id {self.import_id!s} completely")
        except Exception:
            FWOLogger.exception("fwo_api: failed to unlock import for import id " + str(self.import_id))
