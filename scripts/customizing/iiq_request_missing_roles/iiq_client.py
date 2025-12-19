import json
import sys
from copy import deepcopy
from typing import Any

import requests

from scripts.customizing.fwo_custom_lib.basic_helpers import FWOLogger, get_logger


class IIQClient:
    def __init__(
        self,
        hostname: str,
        user: str,
        password: str,
        app_name: str,
        user_prefix: str,
        stage: str = "",
        debug: int = 0,
        logger: FWOLogger | None = None,
    ) -> None:
        self.hostname: str = hostname
        self.user: str = user
        self.password: str = password
        self.app_name: str = app_name
        self.user_prefix: str = user_prefix
        self.stage: str = stage
        self.debug: int = debug
        self.logger: FWOLogger = logger or get_logger(self.debug)
        self.logger.configure_debug_level(self.debug)
        self.uri_path_start: str = "/zentralefunktionen/identityiq"
        self.uri_path_end_users: str = "/scim/v1/Users"
        self.uri_path_end_roles: str = "/scim/v1/Roles"
        self._init_placeholders()
        self.request_body_template: dict[str, Any] = self._build_request_body_template()

    def _init_placeholders(self) -> None:
        self.org_id_placeholder: str = "{orgid}"
        user_id_placeholder: str = f"{self.user_prefix}-Kennung"
        self.user_id_placeholder: str = f"{{{user_id_placeholder} des technischen Users IIQ}}"
        self.boit_user_id_placeholder: str = f"{{{self.user_id_placeholder} des BO-IT}}"
        self.role_business_type: str = "Geschäftsfunktion"
        self.role_workplace_type: str = "Arbeitsplatzfunktion"
        self.role_technical_type: str = "Technische Funktion"
        self.app_name_origin_placeholder: str = "{Anwendungsname laut Alfabet-ORIGIN}"
        self.app_name_placeholder: str = "{Anwendungsname laut Alfabet}"
        self.app_name_upper_placeholder: str = "{Anwendungsname laut Alfabet-UPPER}"
        self.app_id_placeholder: str = "{AppID}"
        self.role_name_suffix_prefix: str = "fw_rulemgt_"
        # this is not accepted by IIQ: self.role_name_suffix_prefix = "fw_rulemgmt_"
        # response: Der AF-Name rva_02268_fw_rulemgmt_app_5014 ist für die Quelle FWO nicht erlaubt.","Workflow wurde nicht gestartet."],"status":"ERROR"

    def _build_request_body_template(self) -> dict[str, Any]:
        request_body_template: dict[str, Any] = {
            "requesterName": self.user_id_placeholder,
            "requesterComment": "Anlegen von Rollen für Zugriff auf NeMo (Modellierung Kommunikationsprofil, Beantragung und Rezertifizierung von Firewall-Regeln)",
            "source": "FWO",
            "objectModelList": [],
            "connectMapList": [],
            "startWorkflow": False,
        }

        request_body_template["objectModelList"].extend(
            [
                {
                    "objectType": self.role_workplace_type,
                    "afType": "rva",
                    "afNameSuffix": f"{self.role_name_suffix_prefix}{self.app_name_placeholder}",
                    "afOrgId": self.org_id_placeholder,
                    "afDesc": f"Die {self.role_workplace_type} ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "afDescription": f"Die {self.role_workplace_type} ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "afCtlAuf": "B",
                    "afAnsprechpartnerName": self.boit_user_id_placeholder,
                    "afNibaEu": self.user_prefix,
                },
                {
                    "objectType": self.role_business_type,
                    "gfType": "rvg",
                    "gfNameSuffix": f"{self.role_name_suffix_prefix}{self.app_name_placeholder}",
                    "gfOrgId": self.org_id_placeholder,
                    "gfDesc": f"Die {self.role_business_type} ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "gfDescription": f"Die {self.role_business_type} ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "gfAnsprechpartnerName": self.boit_user_id_placeholder,
                },
                {
                    "objectType": self.role_technical_type,
                    "tfApplicationName": self.app_name,
                    "tfAdType": "Anwendungsgruppe anlegen",
                    "tfOrgId": self.org_id_placeholder,
                    "tfDesc": f"Die Berechtigung ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "tfApplicationDescription": self.app_name_origin_placeholder,
                    "tfName": f"A_{self.app_name_upper_placeholder}_FW_RULEMGT",
                    "tfAnsprechpartnerName": self.boit_user_id_placeholder,
                    "tfKkz": "K",
                    "tfAlfabetId": self.app_id_placeholder,
                },
            ]
        )

        request_body_template["connectMapList"].extend(
            [
                {"objectType": self.role_workplace_type, "objectIndex": 0, "connectIndex": 1},
                {"objectType": self.role_business_type, "objectIndex": 1, "connectIndex": 2},
            ]
        )

        request_body_template["connectMapList"].append(
            {
                "objectType": self.role_business_type,
                "objectIndex": 1,
                "connectName": "A_TUFIN_REQUEST",
                "tfApplicationName": self.app_name,
            }
        )
        return request_body_template

    def send(
        self,
        body: dict[str, Any] | None = None,
        method: str = "POST",
        url_path: str = "",
        url_parameter: str = "",
    ) -> requests.Response:
        if body is None:
            body = {}
        headers: dict[str, str] = {"Content-Type": "application/json"}
        url: str = "https://" + self.hostname + url_path + url_parameter

        if method == "POST":
            response: requests.Response = requests.post(
                url, json=body, auth=(self.user, self.password), headers=headers, verify=True
            )
        elif method == "GET":
            response = requests.get(url, auth=(self.user, self.password), headers=headers, verify=True)
        else:
            self.logger.error("unsupported method %s in send", method)
            sys.exit(1)

        return response

    def get_org_id(self, tiso: str) -> str | None:
        url_path: str = self.uri_path_start + self.stage + self.uri_path_end_users
        url_parameter: str = f"?filter=userName%20eq%20%22{tiso}%22&attributes=urn:ietf:params:scim:schemas:sailpoint:1.0:User:parent_org_id"
        org_id: str | None = None
        response = self.send(method="GET", url_path=url_path, url_parameter=url_parameter)

        if response.ok:
            resp: dict[str, Any] = json.loads(response.text)
            try:
                org_id = resp["Resources"][0]["urn:ietf:params:scim:schemas:sailpoint:1.0:User"]["parent_org_id"]
            except KeyError:
                org_id = None
        else:
            self.logger.warning(
                "did not get an OrgId for TISO %s, response code: %s",
                tiso,
                response.status_code,
            )

        return org_id

    def request_group_creation(
        self,
        app_prefix: str,
        app_id: str,
        org_id: str,
        tiso: str,
        name: str,
        stats: dict[str, Any],
        run_workflow: bool = False,
    ) -> None:
        iiq_req_body_local: dict[str, Any] = deepcopy(self.request_body_template)

        iiq_req_body_local["requesterName"] = iiq_req_body_local["requesterName"].replace(
            self.user_id_placeholder, self.user
        )
        object_model: dict[str, Any]
        for object_model in iiq_req_body_local["objectModelList"]:
            key: str
            for key in object_model:
                if type(object_model[key]) is str:
                    object_model[key] = object_model[key].replace(
                        self.app_name_placeholder, app_prefix.lower() + "_" + app_id
                    )
                    object_model[key] = object_model[key].replace(
                        self.app_name_upper_placeholder, app_prefix + "_" + app_id
                    )
                    object_model[key] = object_model[key].replace(self.app_name_origin_placeholder, name)
                    object_model[key] = object_model[key].replace(self.boit_user_id_placeholder, tiso)
                    object_model[key] = object_model[key].replace(self.user_id_placeholder, self.user)
                    object_model[key] = object_model[key].replace(self.org_id_placeholder, org_id)
                    object_model[key] = object_model[key].replace(self.app_id_placeholder, app_prefix + "-" + app_id)

        app_text: str = f"{app_prefix}_{app_id}"

        if run_workflow:  # in test environment we do not want any real WF to start
            iiq_req_body_local["startWorkflow"] = True
            self.logger.debug_if(2, f"run_workflow={run_workflow!s}, actually starting workflow")
        else:
            self.logger.debug_if(2, f"run_workflow={run_workflow!s}, only simulating")

        response: requests.Response = self.send(
            body=iiq_req_body_local,
            url_path=self.uri_path_start + self.stage + "/workflow/v1/ModellingGeneral/createRequest",
        )

        if not response.ok:
            self.update_stats(stats, "apps_with_request_errors", app_text)
            return

        self._write_group_creation_stats(response, app_text, stats)

    def app_functions_exist_in_iiq(
        self,
        app_prefix: str,
        app_id: str,
        stats: dict[str, Any],
    ) -> bool:
        self.logger.debug_if(2, f"start getting roles for app {app_id} ... ")

        match_string1: str = f'"_fw_rulemgt_{app_prefix.lower()}_{app_id}"'  # v1
        match_string2: str = f'"_fw_rulemgmt_{app_prefix.lower()}{app_id}"'  # v2

        match_found = self._check_for_app_pattern_in_iiq_roles(
            app_prefix, app_id, match_string1, stats
        ) or self._check_for_app_pattern_in_iiq_roles(app_prefix, app_id, match_string2, stats)

        if match_found:
            self.logger.debug_if(
                2, f"found existing roles for app {app_id}. Filter strings {match_string1} or {match_string2} matched."
            )
        else:
            self.logger.debug_if(
                1, f"found no existing roles for app {app_id}. Filter strings: {match_string1} and {match_string2}"
            )

        return match_found

    def _check_for_app_pattern_in_iiq_roles(
        self,
        app_prefix: str,
        app_id: str,
        match_string: str,
        stats: dict[str, Any],
    ) -> bool:
        url_path: str = self.uri_path_start + self.stage + self.uri_path_end_roles
        url_parameter: str = f'?filter=urn:ietf:params:scim:schemas:sailpoint:1.0:Role:displayableName co {match_string} and urn:ietf:params:scim:schemas:sailpoint:1.0:Role:type.name eq "business"'
        response = self.send(
            method="GET",
            url_path=url_path,
            url_parameter=url_parameter,
        )
        result: str = ""
        if response.ok:
            response_json: dict[str, Any] = json.loads(response.text)
            if "totalResults" in response_json:
                if response_json["totalResults"] > 0 and "Resources" in response_json:
                    result = f"A_{app_prefix}_{app_id}_FW_RULEMGT"
                    self.logger.debug_if(
                        4,
                        f"found existing roles for app {app_id}: {response_json['Resources']!s}. filter string: {match_string}",
                    )
                else:
                    self.logger.debug_if(4, f"found no existing roles for app {app_id}. filter string: {match_string}")
        else:
            self.logger.debug(
                "error while getting roles for app %s. filter string: %s, status_code: %s",
                app_id,
                match_string,
                response.status_code,
            )

        if result == "":
            return False

        self.logger.debug_if(2, f"roles for app {app_prefix}-{app_id} already exist - skipping role request creation")
        self.update_stats(stats, "existing_technical_functions", result)
        return True

    def _write_group_creation_stats(
        self,
        response: requests.Response,
        app_text: str,
        stats: dict[str, Any],
    ) -> None:
        if "Validierung der Auftragsdaten erfolgreich" in response.text:
            if "Workflow wurde nicht gestartet." in response.text:
                self.update_stats(stats, "apps_request_simulated", app_text)
            else:
                self.update_stats(stats, "apps_newly_requested", app_text)
        elif "die Alfabet-ID ist ung" in response.text:
            self.update_stats(stats, "apps_with_invalid_alfabet_id", app_text)
        elif "Es existiert bereits der offene Auftrag" in response.text:
            self.update_stats(stats, "apps_with_running_requests", app_text)
        else:
            self.update_stats(stats, "apps_with_unexpected_request_errors", app_text)

        if self.logger.is_debug_level(6):
            self.logger.debug("full iiq response text: %s", response.text)
        if self.logger.is_debug_level(7):
            self.logger.debug("iiq response dot")

    def update_stats(self, stats: dict[str, Any], fieldname: str, field_value: str) -> None:
        stats[fieldname].append(field_value)
        stats[f"{fieldname}_count"] = stats[f"{fieldname}_count"] + 1
