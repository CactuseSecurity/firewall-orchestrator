#!/usr/bin/python3

"""

This script exports all devices from an existing itsecorg installation.
It allows for migrating fortigate ssh devices into fortimanager installations

create csv from itsecorg postgresql (input files for this script):

create .pgpass with content (chmod 600)
    # hostname:port:database:username:password
    localhost:5432:isodb:dbadmin:xxx

create exp-mgm.sql:
    COPY (
        SELECT mgm_id, mgm_name, ssh_hostname, ssh_port, ssh_private_key, ssh_public_key,
            ssh_user, dev_typ_id, config_path, do_not_import, force_initial_import, hide_in_gui,
            importer_hostname, mgm_comment, mgm_create, mgm_update, last_import_md5_complete_config
        FROM management
        WHERE NOT do_not_import AND NOT hide_in_gui AND mgm_name NOT ILIKE '%zzz___%'
    ) TO STDOUT (FORMAT CSV, FORCE_QUOTE *);

# create exp-dev.sql only if you want to migrate from one FWO to another one):
#     COPY (
#         SELECT dev_id, dev_name, dev_typ_id, mgm_id, local_rulebase_name, global_rulebase_name, package_name, 
#             dev_comment, do_not_import, force_initial_import, hide_in_gui, dev_create, dev_update
#         FROM device
#         WHERE NOT do_not_import AND NOT hide_in_gui
#     ) TO STDOUT (FORMAT CSV, FORCE_QUOTE *);

create exp-dev.sql for itsecorg:
    COPY (
        SELECT dev_id, dev_name, dev_typ_id, mgm_id, dev_rulebase,
            dev_comment, do_not_import, force_initial_import, hide_in_gui, dev_create, dev_update
        FROM device
        WHERE NOT do_not_import AND NOT hide_in_gui
    ) TO STDOUT (FORMAT CSV, FORCE_QUOTE *);

# run csv export for fworch:
#     psql -U dbadmin -h localhost -d fworchdb -c "\i exp-mgm.sql" >mgm.csv
#     psql -U dbadmin -h localhost -d fworchdb -c "\i exp-dev.sql" >dev.csv

run csv export for itsecorg:
    psql -U dbadmin -h localhost -d isodb -c "\i exp-mgm.sql" >mgm.csv
    psql -U dbadmin -h localhost -d isodb -c "\i exp-dev.sql" >dev.csv

then run this script:
    fwo-export-itsecorg-devices.py -

"""

import sys, logging
import csv, argparse
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)

parser = argparse.ArgumentParser(
    description="convert database csv export of device data into graphql code for import into fworch\nsample; synopsis: fwo-convert-csv-dev-data.py -m mgm.csv -d dev.csv -o /tmp/fworch-dev-data.graphql; <can be imported into FWORCH>")
parser.add_argument('-m', '--mgm_file', metavar='management_csv_file', required=True, help='filename to read management csv from')
parser.add_argument('-d', '--dev_file', metavar='device_csv_file', required=True, help='filename to read device csv from')
parser.add_argument('-o', '--outfile', metavar='output_file', required=True, help='filename to write output in json format to')
parser.add_argument('-s', '--source', metavar='source_format', default='itsecorg', help='reading data from "itsecorg" (default) or "fworch" source')


def convert_csv2graphql(csv_in, keys, types):
    result = ""
    for line in csv_in:
        if len(line)>0 and len(line) == len (keys):
            graphql_mgm = "{ "
            for i in range(len(keys)):
                input_value = line[i]
                value = None
                graphql_mgm += keys[i] + ": "
                if types[i] != 'int' and types[i] != 'bool' and input_value != None:
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


def migrateCsvMgm(csv_in, input_type):
    for line in csv_in:
        if len(line)>0:
            dev_typ_id = int(line[8])
            # if dev_typ_id==10: # fortigate
            #     # delete line from csv_in (we will use autodiscovery for fortinet devs)
            #     csv_in = csv_in.remove(line)
            #     continue    # deal with next line
            if dev_typ_id==17:
                line[8] = 9     # dev type id for checkpoint is now 9 (not 17)


def migrateCsvDev(csv_in, input_type):
    for line in csv_in:
        if len(line)>0:
            dev_typ_id = int(line[2])
            if dev_typ_id==10: # fortigate
                # delete line from csv_in (we will use autodiscovery for fortinet devs)
                csv_in = csv_in.remove(line)
                continue    # deal with next line
            if dev_typ_id==17:
                line[2] = 9     # dev type id for checkpoint is now 9 (not 17)
            if input_type=='itsecorg':
                dev_rulebase_name = line[4]
                dev_rulebase_name_ar = '/'.split(dev_rulebase_name)
                if len(dev_rulebase_name_ar)>1: # found global rulebase
                    line[4] = dev_rulebase_name_ar[0] # local rulebase name
                    line.insert(5, dev_rulebase_name_ar[1]) # global rulebase name
                else:
                    line.insert(5, None) # global rulebase name
                line.insert(6, None) # package name - needs to be set manually


args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

mgm_keys = ('mgm_id', 'mgm_name', 'ssh_hostname', 'ssh_port', 'ssh_private_key', 'ssh_public_key', 'ssh_user', 
    'dev_typ_id', 'config_path', 'do_not_import', 'force_initial_import', 'hide_in_gui', 'importer_hostname', 'mgm_comment', 
    'mgm_create', 'mgm_update', 'last_import_md5_complete_config')
mgm_types = ('int', 'string', 'string', 'int', 'string', 'string', 'string', 
    'int', 'string', 'bool', 'bool', 'bool', 'string', 'string',
    'string', 'string', 'string' )

dev_keys = ('dev_id', 'dev_name', 'dev_typ_id', 'mgm_id', 'local_rulebase_name', 'global_rulebase_name', 'package_name',
    'dev_comment', 'do_not_import', 'force_initial_import', 'hide_in_gui', 'dev_create', 'dev_update')
dev_types = ('int', 'string', 'int', 'int', 'string', 'string', 'string',
    'string', 'bool', 'bool', 'bool', 'string', 'string')

mgmList = list()
with open(args.mgm_file) as mgmDataFile:
    mgmCSV = csv.reader(mgmDataFile)
    for line in mgmCSV:
        mgmList.append(line)

for mgm in mgmList:
    if len(mgm)>0:
        dev_typ_id = int(mgm[7])
        if dev_typ_id==10: # fortigate
            mgmList.remove(mgm) # autodiscover fortinet devices

devList = list()
with open(args.dev_file) as devDataFile:
    devCSV = csv.reader(devDataFile)
    for line in devCSV:
        devList.append(line)    
    # migrate itsecorg dev to fwo dev (dividing rulebase names)

migrateCsvMgm(mgmList,args.source)
migrateCsvDev(devList,args.source)

mgmString = convert_csv2graphql(mgmList, mgm_keys, mgm_types)
devString = convert_csv2graphql(devList, dev_keys, dev_types)
    
mutation = "mutation restoreDeviceData { insert_management ( objects: " + \
    mgmString + ") { returning { mgm_id } } " + \
    "insert_device ( objects: " + devString + \
    ") { returning { dev_id } } }\n"

with open(args.outfile, 'w') as file:
    file.write(mutation)

sys.exit(0)
