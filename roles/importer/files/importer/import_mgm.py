#!/usr/bin/python3
# master plan import target design

# add main importer loop in pyhton (also able to run distributed)
#   run import loop every x seconds (adjust sleep time per management depending on the change frequency )
#      import a single management (if no import for it is running)
#         lock mgmt for import via FWORCH API call, generating new import_id y
#         check if we need to import (md5, better api call if anything has changed since last import)
#         get config
#         enrich config (if needed)
#         write config via FWORCH API to import_tables --> can we do this in one call or do we need an extra call for each import_table?
#         if possible, have a trigger in table import_rule, which calls import_main stored procedure
#         otherwise: make an extra FWORCH API call to start import with import_id y
#         release mgmt for import via FWORCH API call (also removing import_id y data from import_tables?)

import common
import re
import logging
import pdb
import argparse
import time
import requests.packages.urllib3
import requests
import json
import os
import sys
sys.path.append(r"/usr/local/fworch/importer")
import fwo_api
# use CACTUS::FWORCH::import;
# use CACTUS::read_config;

# sub parse_config {
# parsing rulebases
    # foreach my $rulebase (@rulebase_name_ar)
    # 	my $rulebase_name_sanitized = join('__', split /\//, $rulebase);
    # 	$cmd = "$parser_py -m $mgm_name -i $import_id -r \"$rulebase\" -f \"$object_file\" -d $debug_level > \"$output_dir/${rulebase_name_sanitized}_rulebase.csv\"";
    # 	$return_code = system($cmd);
# parsing users
#	system("$parser_py -m $mgm_name -i $import_id -u -f \"$object_file\" -d $debug_level > \"$output_dir/${mgm_name}_users.csv\"")
# parsing svc objects
#	system("$parser_py -m $mgm_name -i $import_id -s -f \"$object_file\" -d $debug_level > \"$output_dir/${mgm_name}_services.csv\"")
# parsing nw objects
#	system("$parser_py -m $mgm_name -i $import_id -n -f \"$object_file\" -d $debug_level > \"$output_dir/${mgm_name}_netzobjekte.csv\"")

# sub copy_config_from_mgm_to_iso {
    # $lib_path = "$base_path/checkpointR8x";
    # $get_config_bin = "$lib_path/get_config.py";
    # $enrich_config_bin = "$lib_path/enrich_config.py";
    # $get_cmd = "$python_bin $get_config_bin -a $api_hostname -w '$workdir/$CACTUS::FWORCH::ssh_id_basename' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -o '$cfg_dir/$obj_file_base' -d $debug_level";
    # $enrich_cmd = "$python_bin $enrich_config_bin -a $api_hostname -w '$workdir/$CACTUS::FWORCH::ssh_id_basename' -l '$rulebase_names' -u $api_user $api_port_setting $ssl_verify $domain_setting -c '$cfg_dir/$obj_file_base' -d $debug_level";

# set basic parameters (read from import.conf)
    # my $fworch_workdir	= &read_config('fworch_workdir') . "/$mgm_id";
    # my $archive_dir	= &read_config('archive_dir');
    # my $cfg_dir	= "${fworch_workdir}/cfg";
    # my $audit_log_file = "auditlog.export";
    # my $bin_path	= &read_config('simple_bin_dir') . "/";
    # my $save_import_results_to_file = &read_config('save_import_results_to_file');
    # my $new_md5sum = 1;
    # my $stored_md5sum_of_last_import = 2;
    # my $rulebases;

parser = argparse.ArgumentParser(
    description='Read configuration from FW management via API calls')
parser.add_argument('-m', '--mgm_id', metavar='management_id',
                    required=True, help='FWORCH DB ID of the management server to import')
parser.add_argument('-c', '--clear', metavar='clear_management', default=False,
                    help='If set the import will delete all data for the given management instead of importing')
parser.add_argument('-f', '--force', metavar='force_import', default=False,
                    help='If set the import will be attempted even if there seem to be no changes.')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                    help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0')
parser.add_argument('-x', '--proxy', metavar='proxy_string', default='',
                    help='proxy server string to use, e.g. 1.2.3.4:8080; default=empty')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='',
                    help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-i', '--limit', metavar='api_limit', default='500',
                    help='The maximal number of returned results per HTTPS Connection; default=500')
parser.add_argument('-t', '--testing', metavar='version_testing',
                    default='off', help='Version test, [off|<version number>]; default=off')
parser.add_argument('-o', '--out', metavar='output_file',
                    default=False, help='filename to write output in json format to, "False" if not writing to file')

args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only

#      import a single management (if no import for it is running)
#         lock mgmt for import via FWORCH API call, generating new import_id y

# get import info
# ($error_count_local, $error_str_local, $mgm_name, $dev_typ_id, $obj_file_base,$obj_file,$user_file_base,$user_file,$rule_file_base,$rule_file,
# 		$csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $csv_auditlog_file,
# 		$ssh_hostname,$ssh_user,$ssh_private_key,$ssh_public_key,$hersteller,$is_netscreen, $config_files, $ssh_port, $config_path_on_mgmt) =
# 		&get_import_infos_for_mgm($mgm_id, $fworch_workdir, $cfg_dir);

# jwt = fwo_api.login("fwo_import_user", "cactus1", "localhost")

user_management_api_base_url = 'https://localhost:8888/'
method = 'AuthenticateUser' 

jwt = fwo_api.login("user1_demo", "cactus1", user_management_api_base_url, method)

fwo_api_base_url = 'https://localhost:9443/api/v1/graphql'
query_variables={"mgmId": int(args.mgm_id)}

check_import_lock = """query runningImportForManagement($mgmId: Int!) {
  import_control(where: {mgm_id: {_eq: $mgmId}, stop_time: {_is_null: true}}) {
    control_id
  }
}"""
running_import_id = fwo_api.call(fwo_api_base_url, jwt, check_import_lock, query_variables=query_variables, role='importer'); 

lock_mutation = "mutation lockImport($mgmId: Int!) { insert_import_control(objects: {mgm_id: $mgmId}) { returning { control_id } } }"
import_id = fwo_api.call(fwo_api_base_url, jwt, lock_mutation, query_variables=query_variables, role='importer'); 

if import_id < 0:
    logging.exception(
        "failed to get import lock for management id " + str(args.mgm_id))
    sys.exit(1)
else:
    start_time = int(time.time())
    print(logging.info(
        "start import of management {mgm_id} import_id={import_id}"))
        # $error_count_global = &error_handler_add($current_import_id, $error_level = 3, "set-import-lock-failed", !defined($current_import_id), $error_count_global);
        # $error_count_global = &error_handler_add($current_import_id, $error_level = 2, "import-already-running: $mgm_name (ID: $mgm_id)",
        # 	$import_was_already_running, $error_count_global);

#         check if we need to import (md5, better api call if anything has changed since last import)
#         get config
#         enrich config (if needed)

        # if (!$initial_import_flag) {
        # 	$prev_imp_id	= exec_pgsql_cmd_return_value("SELECT get_last_import_id_for_mgmt($mgm_id)");
        # 	$prev_imp_time	= exec_pgsql_cmd_return_value("SELECT start_time FROM import_control WHERE control_id=$prev_imp_id AND successful_import");

        # 1) read names of rulebases of each device from database
        # copy config data from management system to fworch import system
        # &CACTUS::FWORCH::import::parser::copy_config_from_mgm_to_iso

        # check if config has changed
    # 2) parse config
    # CACTUS:: FWORCH: : import: : parser: : parse_config ($obj_file, $rule_file, $user_file, $rulebases, $fworch_workdir, $debug_level, $mgm_name, $cfg_dir,
    #     $current_import_id, "$cfg_dir/$audit_log_file", $prev_imp_time, $fullauditlog, $debug_level);
    # remove_import_lock($current_import_id)
    # set_last_change_time($last_change_time_of_config, $current_import_id);  # Zeit eintragen, zu der die letzte Aenderung an der Config vorgenommen wurde (tuning)
    # if ($clear_whole_mgm_config)
    #    xxx
    # if (-e $csv_usr_file) {& iconv_2_utf8($csv_usr_file, $fworch_workdir); }		# utf-8 conversion of user data
    # fill_import_tables_from_csv($dev_typ_id, $csv_zone_file, $csv_obj_file, $csv_svc_file, $csv_usr_file, $rulebases, $fworch_workdir, $csv_auditlog_file);

    # if (!exec_pgsql_cmd_return_value("SET client_min_messages TO NOTICE; SELECT import_all_main($current_import_id)")) {$error_count_local = 1;
    #    exec_pgsql_cmd_return_value("SET client_min_messages TO DEBUG1; SELECT import_all_main($current_import_id)");
    # exec_pgsql_cmd_return_value("SELECT show_change_summary($current_import_id)");
    # exec_pgsql_cmd_no_result("UPDATE management SET last_import_md5_complete_config='$new_md5sum' WHERE mgm_id=$mgm_id"); }
    if (error_count_global):
        logging.warning ("Import of management $mgm_name (mgm_id=$mgm_id, import_id=$current_import_id): FOUND $error_count_global error(s)\n")
    else:
        logging.debug("Import of management $mgm_name (mgm_id={mgm_id}, import_id={current_import_id}), total time: ") 
            # . sprintf("%.2fs", (time() - $first_start_time)) . "): no errors found" . (($changes_found)?"": ", no changes found") . "\n");
            # exec_pgsql_cmd_no_result($sql_cmd); # if no error occured - set import_control.successful_import to true
         # exec_pgsql_cmd_no_result("DELETE FROM import_control WHERE control_id=$current_import_id"); # remove imports with unchanged data
         # exec_pgsql_cmd_no_result("UPDATE management SET last_import_md5_complete_config='$new_md5sum' WHERE mgm_id=$mgm_id");
        # 'UPDATE import_control SET successful_import=TRUE' . (($changes_found)? ', changes_found=TRUE': '') . ' WHERE control_id=' . $current_import_id;
    # Cleanup and statistics
    #&exec_pgsql_cmd_no_result("SELECT remove_import_lock($current_import_id)");   # this sets import_control.stop_time to now()
    #&clean_up_fworch_db($current_import_id);
    # `cp -f $fworch_workdir/cfg/*.cfg /var/itsecorg/fw-config/`; # special backup for several configs - dos-box

logging.debug ( "import duration: " + str(int(time.time()) - start_time) + "s" )
sys.exit(0)
