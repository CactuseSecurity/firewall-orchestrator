import csv
import re
import sys
import traceback
from netaddr import IPAddress, IPNetwork


DEFAULT_VALID_APP_ID_PREFIXES = ['app-', 'com-']
DEFAULT_OWNER_HEADER_PATTERNS = {
    "name": r'.*?:\s*Name',
    "app_id": r'.*?:\s*Alfabet-ID$',
    "owner_tiso": r'.*?:\s*TISO',
    "owner_kwita": r'.*?:\s*kwITA'
}
DEFAULT_IP_HEADER_PATTERNS = {
    "app_id": r'.*?:\s*Alfabet-ID$',
    "ip": r'.*?:\s*IP'
}


def build_dn(user_id, ldap_path, logger):
    dn = ""
    if len(user_id) > 0:
        if '{USERID}' in ldap_path:
            dn = ldap_path.replace('{USERID}', user_id)
        else:
            logger.error("could not find {USERID} parameter in ldap_path " + ldap_path)
    return dn


def read_app_data_from_csv(csv_file_name: str, logger, column_patterns=None):
    try:
        header_patterns = {**DEFAULT_OWNER_HEADER_PATTERNS, **(column_patterns or {})}
        with open(csv_file_name, newline='') as csv_file_handle:
            reader = csv.reader(csv_file_handle)
            headers = next(reader)  # Get header row first

            name_pattern = re.compile(header_patterns["name"])
            app_id_pattern = re.compile(header_patterns["app_id"])
            owner_tiso_pattern = re.compile(header_patterns["owner_tiso"])
            owner_kwita_pattern = re.compile(header_patterns["owner_kwita"])

            app_name_column = next(i for i, h in enumerate(headers) if name_pattern.match(h))
            app_id_column = next(i for i, h in enumerate(headers) if app_id_pattern.match(h))
            app_owner_tiso_column = next(i for i, h in enumerate(headers) if owner_tiso_pattern.match(h))
            app_owner_kwita_column = next(i for i, h in enumerate(headers) if owner_kwita_pattern.match(h))

            apps_from_csv = list(reader)  # Read remaining rows
    except Exception:
        logger.error("error while trying to read csv file '" + csv_file_name + "', exception: " + str(traceback.format_exc()))
        sys.exit(1)

    return apps_from_csv, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column


def parse_app_line(line, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column, app_list, count_skips, ldap_path, import_source_string, owner_cls, logger, debug_level):
    app_id = line[app_id_column]
    if app_id.lower().startswith('app-') or app_id.lower().startswith('com-'):
        app_name = line[app_name_column]
        app_main_user = line[app_owner_tiso_column]
        main_user_dn = build_dn(app_main_user, ldap_path, logger)
        kwita = line[app_owner_kwita_column]
        if kwita is None or kwita == '' or kwita.lower() == 'false':
            recert_period_days = 365
        else:
            recert_period_days = 182
        if main_user_dn == '' and debug_level > 0:
            logger.warning('adding app without main user: ' + app_id)
        app_list.append(owner_cls(app_id_external=app_id, name=app_name, main_user=main_user_dn, 
                                  recert_period_days=recert_period_days, days_until_first_recert=recert_period_days, recert_active=False, import_source=import_source_string))
    else:
        if debug_level > 1:
            logger.info(f'ignoring line from csv file: {app_id} - inconclusive appId')
        count_skips += 1
    return count_skips


def extract_app_data_from_csv(csv_file: str, app_list: list, ldap_path, import_source_string, owner_cls, logger, debug_level, 
                              base_dir='.', recert_active_app_list=None, column_patterns=None): 

    if recert_active_app_list is None:
        recert_active_app_list = []

    apps_from_csv = []
    csv_file_path = base_dir + '/' + csv_file  # add directory to csv files

    apps_from_csv, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column = read_app_data_from_csv(csv_file_path, logger, column_patterns)

    count_skips = 0
    # append all owners from CSV
    for line in apps_from_csv:
        count_skips = parse_app_line(line, app_name_column, app_id_column, app_owner_tiso_column, app_owner_kwita_column, app_list, count_skips, ldap_path, import_source_string, owner_cls, logger, debug_level)
    if debug_level > 0:
        logger.info(f"{str(csv_file_path)}: #total lines {str(len(apps_from_csv))}, skipped: {str(count_skips)}")

    if len(recert_active_app_list) > 0:
        # activate recertification for apps in recert_active_app_list
        for app in app_list:
            if app.app_id_external in recert_active_app_list:
                app.recert_active = True
                app.days_until_first_recert = app.recert_period_days    # settings initial recertification to standard period of days
            else:
                app.recert_active = False


def read_ip_data_from_csv(csv_filename, logger, column_patterns=None):
    try:
        header_patterns = {**DEFAULT_IP_HEADER_PATTERNS, **(column_patterns or {})}
        with open(csv_filename, newline='', encoding='utf-8') as csv_file:
            reader = csv.reader(csv_file)
            headers = next(reader)  # Get header row first

            app_id_pattern = re.compile(header_patterns["app_id"])
            ip_pattern = re.compile(header_patterns["ip"])

            app_id_column_no = next(i for i, h in enumerate(headers) if app_id_pattern.match(h))
            ip_column_no = next(i for i, h in enumerate(headers) if ip_pattern.match(h))

            ip_data = list(reader)  # Read remaining rows
    except Exception as e:
        logger.error("error while trying to read csv file '%s', exception: %s", csv_filename, e)
        sys.exit(1)

    return ip_data, app_id_column_no, ip_column_no


def parse_ip(line, app_id, ip_column_no, app_dict, count_skips, app_ip_cls, logger, debug_level):
    app_server_ip_string = line[ip_column_no]
    if app_server_ip_string is not None and app_server_ip_string != "":
        try:
            ip_range = IPNetwork(app_server_ip_string)
        except Exception:
            if debug_level > 1:
                logger.warning(f'error parsing IP/network {app_server_ip_string} for app {app_id}, skipping this entry')
            count_skips += 1
            return count_skips
        if ip_range.size > 1:
            ip_type = "network"
        else:
            ip_type = "host"

        ip_start = IPAddress(ip_range.first)
        ip_end = IPAddress(ip_range.last)
        ip_obj_name = f"{ip_type}_{app_server_ip_string}".replace('/', '_')
        app_server_ip = app_ip_cls(app_id_external=app_id, ip_start=ip_start, ip_end=ip_end, type=ip_type, name=ip_obj_name)
        if app_server_ip not in app_dict[app_id].app_servers:
            app_dict[app_id].app_servers.append(app_server_ip)
    else:
        count_skips += 1

    return count_skips


def parse_single_ip_line(line, app_id_column_no, ip_column_no, app_dict, valid_app_id_prefixes, app_ip_cls, logger, debug_level):
    count_skips = 0
    if len(line) - 1 < app_id_column_no:
        return 1

    app_id: str = line[app_id_column_no]
    app_id_prefix = app_id.split('-')[0].lower() + '-'

    if len(valid_app_id_prefixes) == 0 or app_id_prefix in valid_app_id_prefixes:
        if app_id in app_dict.keys():
            count_skips = parse_ip(line, app_id, ip_column_no, app_dict, count_skips, app_ip_cls, logger, debug_level)
        else:
            if debug_level > 1:
                logger.debug(f'ignoring line from csv file as the app_id is not part of the app_list: {app_id} inactive?')
            return 1
    else:
        if debug_level > 1:
            logger.info(f'ignoring line from csv file: {app_id} - inconclusive appId')
        return 1
    return count_skips


def extract_ip_data_from_csv(csv_filename: str, app_dict, app_ip_cls, logger, debug_level, base_dir, valid_app_id_prefixes=None, column_patterns=None): 

    if valid_app_id_prefixes is None:
        valid_app_id_prefixes = DEFAULT_VALID_APP_ID_PREFIXES

    ip_data = []
    csv_file_path = base_dir + '/' + csv_filename  # add directory to csv files

    ip_data, app_id_column_no, ip_column_no = read_ip_data_from_csv(csv_file_path, logger, column_patterns)

    count_skips = 0
    for line in ip_data:
        count_skips += parse_single_ip_line(line, app_id_column_no, ip_column_no, app_dict, valid_app_id_prefixes, app_ip_cls, logger, debug_level)
    if debug_level > 0:
        logger.info(f"{str(csv_file_path)}: #total lines {str(len(ip_data))}, skipped: {str(count_skips)}")
