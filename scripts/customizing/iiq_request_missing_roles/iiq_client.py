import sys
import json
import requests
from copy import deepcopy
from scripts.customizing.fwo_custom_lib.basic_helpers import get_logger


class IIQClient:
    def __init__(self, hostname, user, password, app_name, user_prefix, stage='', debug=0, logger=None):
        self.hostname = hostname
        self.user = user
        self.password = password
        self.app_name = app_name
        self.user_prefix = user_prefix
        self.stage = stage
        self.debug = debug
        self.logger = logger or get_logger()
        self.uri_path_start = "/zentralefunktionen/identityiq"
        self.uri_path_end_users = "/scim/v1/Users"
        self.uri_path_end_roles = "/scim/v1/Roles"
        self._init_placeholders()
        self.request_body_template = self._build_request_body_template()

    def _init_placeholders(self):
        self.org_id_placeholder = "{orgid}"
        user_id_placeholder = f"{self.user_prefix}-Kennung"
        self.user_id_placeholder = f"{{{user_id_placeholder} des technischen Users IIQ}}"
        self.boit_user_id_placeholder = f"{{{self.user_id_placeholder} des BO-IT}}"
        self.role_business_type = "Geschäftsfunktion"
        self.role_workplace_type = "Arbeitsplatzfunktion"
        self.role_technical_type = "Technische Funktion"
        self.app_name_origin_placeholder = "{Anwendungsname laut Alfabet-ORIGIN}"
        self.app_name_placeholder = "{Anwendungsname laut Alfabet}"
        self.app_name_upper_placeholder = "{Anwendungsname laut Alfabet-UPPER}"
        self.app_id_placeholder = "{AppID}"
        self.role_name_suffix_prefix = "fw_rulemgt_"
        # this is not accepted by IIQ: self.role_name_suffix_prefix = "fw_rulemgmt_"
        # response: Der AF-Name rva_02268_fw_rulemgmt_app_5014 ist für die Quelle FWO nicht erlaubt.","Workflow wurde nicht gestartet."],"status":"ERROR"


    def _build_request_body_template(self):
        request_body_template = {
            "requesterName": self.user_id_placeholder,
            "requesterComment": "Anlegen von Rollen für Zugriff auf NeMo (Modellierung Kommunikationsprofil, Beantragung und Rezertifizierung von Firewall-Regeln)",
            "source": "FWO",
            "objectModelList": [],
            "connectMapList": [],
            "startWorkflow": False
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
                    "afNibaEu": self.user_prefix
                },
                {
                    "objectType": self.role_business_type,
                    "gfType": "rvg",
                    "gfNameSuffix": f"{self.role_name_suffix_prefix}{self.app_name_placeholder}",
                    "gfOrgId": self.org_id_placeholder,
                    "gfDesc": f"Die {self.role_business_type} ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "gfDescription": f"Die {self.role_business_type} ist erforderlich zur Beantragung, Änderung und Rezertifizierung von Firewall Regeln für die Anwendung {self.app_name_origin_placeholder} in der Firewall Orchestrierung Anwendungen auf der PROD Umgebung. Berechtigte können in FWO Anträge für die Anlage, Änderung, Löschung und Rezertifizierung von Firewall Regeln für die Anwendung stellen.",
                    "gfAnsprechpartnerName": self.boit_user_id_placeholder
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
                    "tfAlfabetId": self.app_id_placeholder
                }
            ]
        )

        request_body_template["connectMapList"].extend([
                { "objectType": self.role_workplace_type, "objectIndex": 0, "connectIndex": 1 },
                { "objectType": self.role_business_type, "objectIndex": 1, "connectIndex": 2 }
            ])

        request_body_template["connectMapList"].append( { "objectType": self.role_business_type, "objectIndex": 1, "connectName": "A_TUFIN_REQUEST", "tfApplicationName": self.app_name } )
        return request_body_template

    def send(self, body: dict|None=None, method='POST', url_path='', url_parameter='', debug=None):
        if body is None:
            body = {}
        headers = {'Content-Type': 'application/json'}
        url = "https://" + self.hostname + url_path + url_parameter
        debug_level = self.debug if debug is None else debug

        if debug_level>7:
            print('url: ' + url)
            print('method: ' + method)
            print('iiq_user: ' + self.user)
            print('headers: ' + str(headers))
            print('body: ' + str(body))
            print('------------------------------------')

        if method=='POST':
            response = requests.post(url, json=body, auth=(self.user, self.password), headers=headers, verify=True)
        elif method=='GET':
            response = requests.get(url, auth=(self.user, self.password), headers=headers, verify=True)
        else:
            self.logger.error(f"unsupported method {method} in send")
            sys.exit(1)

        return response

    def get_org_id(self, tiso, debug=None):
        debug_level = self.debug if debug is None else debug
        url_path = self.uri_path_start + self.stage + self.uri_path_end_users
        url_parameter= f"?filter=userName%20eq%20%22{tiso}%22&attributes=urn:ietf:params:scim:schemas:sailpoint:1.0:User:parent_org_id"
        org_id = None
        response = self.send(method='GET', url_path= url_path, url_parameter=url_parameter, debug=debug_level)

        if response.ok:
            resp = json.loads(response.text)
            try:
                org_id = resp['Resources'][0]['urn:ietf:params:scim:schemas:sailpoint:1.0:User']['parent_org_id']
            except KeyError:
                org_id = None
        else:
            self.logger.warning(f"did not get an OrgId for TISO {tiso}, response code: {str(response.status_code)}")

        return org_id

    def request_group_creation(self, app_prefix, app_id, org_id, tiso, name, stats, debug=None, run_workflow=False):
        debug_level = self.debug if debug is None else debug
        iiq_req_body_local = deepcopy(self.request_body_template)

        iiq_req_body_local["requesterName"] = iiq_req_body_local["requesterName"].replace(self.user_id_placeholder, self.user)
        for object_model in iiq_req_body_local["objectModelList"]:
            for key in object_model:
                if type(object_model[key]) is str:
                    object_model[key] = object_model[key].replace(self.app_name_placeholder, app_prefix.lower() + "_" + app_id)
                    object_model[key] = object_model[key].replace(self.app_name_upper_placeholder, app_prefix + "_" + app_id)
                    object_model[key] = object_model[key].replace(self.app_name_origin_placeholder, name)
                    object_model[key] = object_model[key].replace(self.boit_user_id_placeholder, tiso)
                    object_model[key] = object_model[key].replace(self.user_id_placeholder, self.user)
                    object_model[key] = object_model[key].replace(self.org_id_placeholder, org_id)
                    object_model[key] = object_model[key].replace(self.app_id_placeholder, app_prefix + "-" + app_id)

        app_text = f"{app_prefix}_{app_id}"

        if run_workflow:   # in test environment we do not want any real WF to start
            iiq_req_body_local['startWorkflow'] = True
            if debug_level>2:
                self.logger.debug(f"run_workflow={str(run_workflow)}, actually stating workflow")
        else:
            if debug_level>2:
                self.logger.debug(f"run_workflow={str(run_workflow)}, only simulating")

        response = self.send(body=iiq_req_body_local,
            url_path= self.uri_path_start + self.stage + "/workflow/v1/ModellingGeneral/createRequest",
            debug=debug_level)

        if not response.ok:
            print("ERROR: " + str(response.text))
            self.update_stats(stats, "apps_with_request_errors", app_text)
            return

        self._write_group_creation_stats(response, app_text, stats, debug_level)

    def app_functions_exist_in_iiq(self, app_prefix, app_id, stats, debug=None):
        debug_level = self.debug if debug is None else debug
        if debug_level>2:
            self.logger.debug(f"start getting roles for app {app_id} ... ")

        match_string1 = f'\"_fw_rulemgt_{app_prefix.lower()}_{app_id}\"'    # v1
        match_string2 = f'\"_fw_rulemgmt_{app_prefix.lower()}{app_id}\"'    # v2

        match_found = self._check_for_app_pattern_in_iiq_roles(app_prefix, app_id, match_string1, stats, debug_level) \
               or \
               self._check_for_app_pattern_in_iiq_roles(app_prefix, app_id, match_string2, stats, debug_level)

        if debug_level>2 and match_found:
            self.logger.debug(f"found existing roles for app {app_id}. Filter strings {match_string1} or {match_string2} matched.")
        if debug_level>1 and not match_found:
            self.logger.debug(f"found no existing roles for app {app_id}. Filter strings: {match_string1} and {match_string2}")
            
        return match_found

    def _check_for_app_pattern_in_iiq_roles(self, app_prefix, app_id, match_string, stats, debug):
        url_path = self.uri_path_start + self.stage + self.uri_path_end_roles
        url_parameter = f"?filter=urn:ietf:params:scim:schemas:sailpoint:1.0:Role:displayableName co {match_string} and urn:ietf:params:scim:schemas:sailpoint:1.0:Role:type.name eq \"business\""
        response = self.send(
            method='GET',
            url_path=url_path,
            url_parameter=url_parameter, debug=debug)
        result = {}
        if response.ok:
            response_json = json.loads(response.text)
            if 'totalResults' in response_json:
                if response_json['totalResults']>0 and 'Resources' in response_json:
                    result = f"A_{app_prefix}_{app_id}_FW_RULEMGT"
                    if debug>4:
                        self.logger.debug(f"found existing roles for app {app_id}: {str(response_json['Resources'])}. filter string: {match_string}")
                elif debug>4:
                    self.logger.debug(f"found no existing roles for app {app_id}. filter string: {match_string}")
        else:
            self.logger.debug(f"error while getting roles for app {app_id}. filter string: {match_string}, status_code: {str(response.status_code)}")

        if result == {}:
            return False

        if debug>2:
            self.logger.debug(f"roles for app {app_prefix}-{app_id} already exist - skipping role request creation")
        self.update_stats(stats, "existing_technical_functions", result)
        return True

    def _write_group_creation_stats(self, response, app_text, stats, debug):
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
            print("unknown result: " + str(response.text))
            self.update_stats(stats, "apps_with_unexpected_request_errors", app_text)

        if debug>6:
            print ("full iiq response text: " + response.text)
        if debug>7:
            print(".", end="", flush=True)

    def update_stats(self, stats, fieldname, field_value):
        stats[fieldname].append(field_value)
        stats[f"{fieldname}_count"] = stats[f"{fieldname}_count"] + 1
