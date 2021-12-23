#!/usr/bin/python3

"""
create csv from itsecorg postgresql (input files for this script):

create .pgpass with content (chmod 600)
hostname:port:database:username:password
localhost:5432:isodb:dbadmin:xxx

create exp-mgm.sql:

COPY (
    SELECT mgm_id, mgm_name, ssh_hostname, ssh_port, ssh_private_key, ssh_public_key,
        ssh_user, dev_typ_id, config_path, do_not_import, force_initial_import, hide_in_gui,
        importer_hostname, mgm_comment, debug_level, mgm_create, mgm_update, last_import_md5_complete_config
    FROM management
    WHERE NOT do_not_import AND NOT hide_in_gui AND mgm_name NOT ILIKE '%zzz___%'
) TO STDOUT (FORMAT CSV, FORCE_QUOTE *);

create exp-dev.sql for FWO:

COPY (
    SELECT dev_id, dev_name, dev_typ_id, mgm_id, local_rulebase_name, global_rulebase_name, package_name, 
        dev_comment, do_not_import, force_initial_import, hide_in_gui, dev_create, dev_update
    FROM device
    WHERE NOT do_not_import AND NOT hide_in_gui
) TO STDOUT (FORMAT CSV, FORCE_QUOTE *);

create exp-dev.sql for itsecorg:

COPY (
    SELECT dev_id, dev_name, dev_typ_id, mgm_id, rulebase_name, global_rulebase_name,
        dev_comment, do_not_import, force_initial_import, hide_in_gui, dev_create, dev_update
    FROM device
    WHERE NOT do_not_import AND NOT hide_in_gui
) TO STDOUT (FORMAT CSV, FORCE_QUOTE *);

psql -U dbadmin -h localhost -d isodb -c "\i exp-mgm.sql" >mgm.sql
psql -U dbadmin -h localhost -d isodb -c "\i exp-dev.sql" >dev.sql

"""

import sys, logging
import csv, argparse
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)


parser = argparse.ArgumentParser(
    description="Export fworch configuration into encrypted json file\nsample; synopsis for ex- and import: fwo-export-config.py -o/tmp/fworch-config.graphql; <move to freshly installed FWO system without demo data>; fwo-execute-graphql.py -i/tmp/fworch-config.graphql")
parser.add_argument('-m', '--mgm_file', metavar='management_csv_file', required=True, help='filename to read management csv from')
parser.add_argument('-d', '--dev_file', metavar='device_csv_file', required=True, help='filename to read device csv from')
parser.add_argument('-o', '--outfile', metavar='output_file', required=True, help='filename to write output in json format to')


def convert_csv2graphql(csv_in, keys, types):
    result = ""
    for line in csv_in:
        if len(line)>0 and len(line) == len (keys):
            graphql_mgm = "{ "
            for i in range(len(keys)):
                input_value = line[i]
                value = None
                graphql_mgm += keys[i] + ": "
                if types[i] != 'int' and types[i] != 'bool':
                    value = '"' + input_value.replace('\n', '\\n') + '"'    # escape linebreaks in strings
                else:
                    if types[i]=='bool':
                        if input_value=='f':
                            value = 'false'
                        if input_value=='t':
                            value = 'true'
                    elif types[i]=='int':
                        if len(input_value)==0:
                            value = None
                        else:
                            value = input_value
                    else:
                        value = input_value
                if value is None:
                    value = 'null'
                graphql_mgm += value + " "
            graphql_mgm += " }"
            result += graphql_mgm
        else:
            if len(line)>0:
                logging.exception("wrong number of cs values: " + str(line) + "; for keys: " + str(keys))
                sys.exit(1)
    return "[" + result + "]"


args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

mgm_keys = ('mgm_id', 'mgm_name', 'ssh_hostname', 'ssh_port', 'ssh_private_key', 'ssh_public_key', 'ssh_user', 
    'dev_typ_id', 'config_path', 'do_not_import', 'force_initial_import', 'hide_in_gui', 'importer_hostname', 'mgm_comment', 
    'debug_level', 'mgm_create', 'mgm_update', 'last_import_md5_complete_config')
mgm_types = ('int', 'string', 'string', 'int', 'string', 'string', 'string', 
    'int', 'string', 'bool', 'bool', 'bool', 'string', 'string',
    'int', 'string', 'string', 'string' )
dev_keys = ('dev_id', 'dev_name', 'dev_typ_id', 'mgm_id', 'local_rulebase_name', 'global_rulebase_name', 'package_name',
    'dev_comment', 'do_not_import', 'force_initial_import', 'hide_in_gui', 'dev_create', 'dev_update')
dev_types = ('int', 'string', 'int', 'int', 'string', 'string', 'string',
    'string', 'bool', 'bool', 'bool', 'string', 'string')

with open(args.mgm_file) as mgmDataFile:
    mgmCSV = csv.reader(mgmDataFile)
    mgmString = convert_csv2graphql(mgmCSV, mgm_keys, mgm_types)

with open(args.dev_file) as devDataFile:
    devCSV = csv.reader(devDataFile)
    devString = convert_csv2graphql(devCSV, dev_keys, dev_types)

mutation = "mutation restoreDeviceData { insert_management ( objects: " + \
    mgmString + ") { returning { mgm_id } } " + \
    "insert_device ( objects: " + devString + \
    ") { returning { dev_id } } }\n"

with open(args.outfile, 'w') as file:
    file.write(mutation)

sys.exit(0)
