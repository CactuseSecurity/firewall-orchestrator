#!/usr/bin/python3

import sys
import json
import urllib3
import requests
import socket
import argparse
import os
import re
import git  # apt install python3-git # or: pip install git
import urllib.parse
from datetime import datetime
from pathlib import Path
from copy import deepcopy
from scripts.customizing.fwo_custom_lib.app_data_models import Owner, Appip
from scripts.customizing.fwo_custom_lib.read_app_data_csv import extract_app_data_from_csv, extract_ip_data_from_csv
from scripts.customizing.fwo_custom_lib.basic_helpers import read_custom_config, get_logger
from scripts.customizing.fwo_custom_lib.app_data_basics import transform_owner_dict_to_list, transform_app_list_to_dict


__version__ = "2025-11-20-01"
# "2025-03-24-01" adding support for getting already modelled functions
# "2025-05-20-01" renaming from ldap-import.py to request-missing-fwmgt-roles.py
# "2025-06-23-01" adding A_Tufin_Request TF for all users
# "2025-07-31-01" changing naming scheme of TF to match concept
# "2025-08-01-01" correcting function names and leaving out TF A_Tufin_Request for now 
#                 as it does cause errors (not all capitals)
# "2025-08-12-01" adding A_TUFIN_REQUEST (capital letters)
# "2025-10-10-01" fixing a) missing git clone b) iiq_request_body copy issue
# "2025-10-28-01" fixing wrong match string resulting in unneccessary attempt to create already existing roles, leading to false positive errors in statistics
# "2025-11-20-01" fixing wrong match string resulting in unneccessary attempt to create already existing roles, leading to false positive errors in statistics

csv_file_base_dir = "/usr/local/fwo-iiq/"
base_dir_etc = csv_file_base_dir + "etc/"
cmdb_repo_target_dir = base_dir_etc + "cmdb-repo"
default_config_file_name = base_dir_etc + "customizingConfig.json"


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
                    "afNameSuffix": "fw_rulemgt_{Anwendungsname laut Alfabet}",
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
                    "gfNameSuffix": f"fw_rulemgt_{self.app_name_placeholder}",
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
                    "tfAlfabetId": self.app_name_origin_placeholder
                }
            ]
        )

        request_body_template["connectMapList"].extend([
                { "objectType": self.role_workplace_type, "objectIndex": 0, "connectIndex": 1 },
                { "objectType": self.role_business_type, "objectIndex": 1, "connectIndex": 2 }
            ])

        request_body_template["connectMapList"].append( { "objectType": self.role_business_type, "objectIndex": 1, "connectName": "A_TUFIN_REQUEST", "tfApplicationName": self.app_name } )
        return request_body_template

    def send(self, body='{}', method='POST', url_path='', url_parameter='', debug=None):
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
            response = requests.post(url, data=body, auth=(self.user, self.password), headers=headers, verify=False)
        elif method=='GET':
            response = requests.get(url, auth=(self.user, self.password), headers=headers, verify=False)
        else:
            self.logger.error(f"unsupported method {method} in send")
            sys.exit(1)

        return response

    def get_org_id(self, tiso, debug=None):
        debug_level = self.debug if debug is None else debug
        url_path = self.uri_path_start + self.stage + "/scim/v1/Users"
        url_parameter= "?filter=userName%20eq%20%22" + tiso + "%22&attributes=urn:ietf:params:scim:schemas:sailpoint:1.0:User:parent_org_id"
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

        app_text = f"{app_prefix}_{app_id}"

        if run_workflow:   # in test environment we do not want any real WF to start
            iiq_req_body_local['startWorkflow'] = True
            if debug_level>2:
                self.logger.debug(f"run_workflow={str(run_workflow)}, actually stating workflow")
        else:
            if debug_level>2:
                self.logger.debug(f"run_workflow={str(run_workflow)}, only simulating")

        iiq_req_json = json.dumps(iiq_req_body_local, ensure_ascii=False).encode('utf-8')

        response = self.send(body=str(iiq_req_json),
            url_path= self.uri_path_start + self.stage + "/workflow/v1/ModellingGeneral/createRequest",
            debug=debug_level)

        if not response.ok:
            print("ERROR: " + str(response.text))
            update_stats(stats, "apps_with_request_errors", app_text)
            return

        write_group_creation_stats(response, app_text, stats, debug_level)

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
        url_path = self.uri_path_start + self.stage + "/scim/v1/Roles"
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
        update_stats(stats, "existing_technical_functions", result)
        return True


def is_valid_ipv4_address(address):
    try:
        socket.inet_pton(socket.AF_INET, address)
    except AttributeError:  # no inet_pton here, sorry
        try:
            socket.inet_aton(address)
        except socket.error:
            return False
        return address.count('.') == 3
    except socket.error:  # not a valid address
        return False

    return True


def get_owners_from_csv_files(csv_owner_file_pattern, csv_app_server_file_pattern, repo_target_dir, ldap_path, logger, debug_level):
    app_list = []
    re_owner_file_pattern = re.compile(csv_owner_file_pattern)
    for file_name in os.listdir(repo_target_dir):
        if re_owner_file_pattern.match(file_name):
            extract_app_data_from_csv(file_name, app_list, ldap_path, "import-source-dummy", Owner, logger, debug_level, base_dir=repo_target_dir)

    owner_dict = transform_app_list_to_dict(app_list)

    re_app_server_file_pattern = re.compile(csv_app_server_file_pattern)
    for file_name in os.listdir(repo_target_dir):
        if re_app_server_file_pattern.match(file_name):
            if debug_level>0:
                logger.info(f"importing IP data from file {file_name} ...")
            extract_ip_data_from_csv(file_name, owner_dict, Appip, logger, debug_level, base_dir=repo_target_dir)
    tisos = get_tisos_from_owner_dict(owner_dict)
    return owner_dict, tisos


def get_tisos_from_owner_dict(app_dict):
    tisos = {}
    for app_id in app_dict:
        owner = app_dict[app_id]
        if owner.main_user is not None and owner.main_user != "":
            tiso = owner.main_user.replace("CN=", "")
            tisos[f"{owner.name}"] = tiso
        else:
            logger.warning(f"owner {owner.name} has no main user, cannot get TISO")
    return tisos


def get_git_repo(git_repo_url, git_username, git_password, repo_target_dir):
    encoded_password = urllib.parse.quote(git_password, safe="")
    repo_url = "https://" + git_username + ":" + encoded_password + "@" + git_repo_url

    if os.path.exists(repo_target_dir):
        # If the repository already exists, open it and perform a pull
        repo = git.Repo(repo_target_dir)
        origin = repo.remotes.origin
        # for DEBUG: do not pull
        origin.pull()
    else:
        git.Repo.clone_from(repo_url, repo_target_dir)

def write_group_creation_stats(response, app_text, stats, debug):
    if "Validierung der Auftragsdaten erfolgreich" in response.text:
        if "Workflow wurde nicht gestartet." in response.text:
            update_stats(stats, "apps_request_simulated", app_text)
        else:
            update_stats(stats, "apps_newly_requested", app_text)
    elif "die Alfabet-ID ist ung" in response.text:
        update_stats(stats, "apps_with_invalid_alfabet_id", app_text)
    elif "Es existiert bereits der offene Auftrag" in response.text:
        update_stats(stats, "apps_with_running_requests", app_text)
    else:
        print("unknown result: " + str(response.text))
        update_stats(stats, "apps_with_unexpected_request_errors", app_text)

    if debug>6:
        print ("full iiq response text: " + response.text)
    if debug>7:
        print(".", end="", flush=True)


def update_stats(stats, fieldname, field_value):
    stats[fieldname].append(field_value)
    stats[f"{fieldname}_count"] = stats[f"{fieldname}_count"] + 1


def request_all_roles(owner_dict, tisos, tiso_orgids, iiq_client, stats, first, run_workflow):
    counter = 0
    # create new groups
    logger.info("creating new groups in iiq")
    for name in owner_dict:
        counter += 1
        tiso = tisos.get(name)
        org_id = tiso_orgids.get(tiso)
        if org_id is None:
            logger.warning("did not find an OrgId for owner " + name + ", skipping group creation")
            continue

        app_prefix, app_id = name.split("-")
        # get existing (already modelled) functions for this app to find out, what still needs to be changed in iiq
        if not iiq_client.app_functions_exist_in_iiq(app_prefix, app_id, stats):
            iiq_client.request_group_creation(app_prefix, app_id, org_id, tiso, name, stats, run_workflow=run_workflow)
        
        # if first parameter is set, only handle the first "first" applications, otherwise handle all
        if first > 0 and counter >= first: 
            break


def get_tisos_orgids(tisos, iiq_client, exit_after_dump=False):
    tiso_orgids = {}
    if iiq_client.debug>0:
        logger.info("getting tiso orgids from iiq")
    for tiso in set(tisos.values()):
        org_id = iiq_client.get_org_id(tiso)
        if org_id is not None:
            tiso_orgids[tiso] = org_id 
    if len(tiso_orgids.keys())==0:
        logger.error("could not resolve a single TISO OrgId, quitting ")
        sys.exit(1)
    elif iiq_client.debug>2:
        print(tiso_orgids)

    if exit_after_dump:
        for tiso_user_id in tiso_orgids:
            print(f"{tiso_user_id},{tiso_orgids[tiso_user_id]}")
        sys.exit(0)
    return tiso_orgids


def init_statistics():
    stats = {}
    stats_fields = ["apps_with_running_requests", "apps_newly_requested", "apps_request_simulated", "apps_with_invalid_alfabet_id",
        "apps_with_request_errors", "apps_with_unexpected_request_errors", "existing_technical_functions" ]
    for field in stats_fields:
        stats.update({ field: [], f"{field}_count": 0 })
    return stats

def write_stats_to_file(stats):
    log_dir = f"{csv_file_base_dir}/log"
    os.makedirs(log_dir, exist_ok=True)
    # Get current date as YYYY-MM-DD
    date_str = datetime.now().strftime("%Y-%m-%d")
    log_file = f"{log_dir}/{date_str}_iiq_request.log"
    Path(log_file).write_text(json.dumps(stats, indent=2))


if __name__ == "__main__":
    ALLOWED_STAGE_VALUES = {"prod", "test"}
    ALLOWED_RUN_VALUES = {True, False}

    logger = get_logger()

    parser = argparse.ArgumentParser(
        description='Read configuration from FW management via API calls')
    parser.add_argument('-c', '--config', default=default_config_file_name,
                        help='Filename of custom config file for modelling imports, default file='+default_config_file_name+',\
                        sample config file content: \
                        { \
                            "iiqHostname": "stest.api.example.de",} \
                            "iiqUsername": "iiq-user-id", \
                            "iiqPassword": "iiq-user-pwd", \
                            "gitRepo": "github.example.de/cmdb/app-export", \
                            "gitName": "git-user-1", \
                            "gitPassword": "gituser-1-pwd", \
                            "csvOwnerFilePattern": "NeMo_???_meta.csv", \
                            "csvAppServerFilePattern": "NeMo_???_IP_.*?.csv", \
                            "iiqAppName": "AD EXAMPLEDE", \
                            "userPrefix": "USR" \
                        } \
                        ')
    parser.add_argument('-s', "--suppress_certificate_warnings", action='store_true', default = True,
                        help = "suppress certificate warnings")
    parser.add_argument('-d', "--debug", metavar='debug_level', default = '0',
                        help = "set to >1 for debugging and to avoid CMDB git pull (due to permission conflicts in debug mode)")
    parser.add_argument('-f', "--first", metavar='handle_first_x_apps', default = '0',
                        help = "set to value greater than 0 to only handle the first x applications")
    parser.add_argument('-g', "--stage", metavar='stage_of_workflow', choices=ALLOWED_STAGE_VALUES,
                        required=True,
                        help=f"Specify the stage of your system. Allowed values: {', '.join(ALLOWED_STAGE_VALUES)}")
    parser.add_argument('-r', "--run_workflow", action="store_true", 
                        help="Should the created IIQ workflow be run? If left out, the workflow is not started (simulation only).")
    parser.add_argument('-j', "--just_dump_tiso_org_ids", action="store_true", 
                        help="Just dump the org_ids of all TISOs as CSV and then exit.")
    parser.add_argument('-o', "--import_from_folder", 
                        help = "if set, will try to read csv files from given folder instead of git repo")

    args = parser.parse_args()

    csv_owner_file_pattern = read_custom_config(args.config, 'csvOwnerFilePattern', logger)
    csv_app_server_file_pattern = read_custom_config(args.config, 'csvAppServerFilePattern', logger)

    debug = int(args.debug)

    if args.stage == 'prod':
        stage = ''  # the production instance does not need any extra strings
    else:
        stage = args.stage
    first = int(args.first)
    if args.suppress_certificate_warnings:
        urllib3.disable_warnings()

    if debug>3:
        logger.debug(f"using config file {args.config}")

    ldap_path = read_custom_config(args.config, 'ldapPath', logger)
    iiq_hostname = read_custom_config(args.config, 'iiqHostname', logger)
    iiq_user = read_custom_config(args.config, 'iiqUsername', logger)
    iiq_password = read_custom_config(args.config, 'iiqPassword', logger)
    cmdb_exports = []
    
    # get/set template parameters
    iiq_app_name =  read_custom_config(args.config, 'iiqAppName', logger)
    user_prefix =  read_custom_config(args.config, 'userPrefix', logger)
    iiq_client = IIQClient(iiq_hostname, iiq_user, iiq_password, iiq_app_name, user_prefix, stage=stage, debug=debug, logger=logger)

    if args.import_from_folder:
        csv_file_base_dir = args.import_from_folder
    else:
        git_repo_url = read_custom_config(args.config, 'cmdbGitRepoUrl', logger)
        git_username = read_custom_config(args.config, 'cmdbGitUsername', logger)
        git_password = read_custom_config(args.config, 'cmdbGitPassword', logger)
        csv_file_base_dir=cmdb_repo_target_dir
        get_git_repo(git_repo_url, git_username, git_password, cmdb_repo_target_dir)

    if debug>0:
        logger.info("getting owners from file")
    owners, tisos = get_owners_from_csv_files(csv_owner_file_pattern, csv_app_server_file_pattern, csv_file_base_dir, ldap_path, logger, debug)
    
    tiso_orgids = get_tisos_orgids(tisos, iiq_client, exit_after_dump=args.just_dump_tiso_org_ids)

    # collect all app ids
    owner_dict = [key.split("|")[0] for key in owners.keys()]

    stats = init_statistics()

    request_all_roles(owner_dict, tisos, tiso_orgids, iiq_client, stats, first, args.run_workflow)

    if debug>0:
        print ("Stats: " + json.dumps(stats, indent=3))
    
    write_stats_to_file(stats)

    sys.exit(0)
