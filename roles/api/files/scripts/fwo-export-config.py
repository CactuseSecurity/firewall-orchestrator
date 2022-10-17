#!/usr/bin/python3
#  export-fworch-config.py: export the full config of the product itself for later import
#  does not contain any firewall config data, just the device config plus fworch user config

# todo: remove redundant code
    # todo: use a single source for fwo_api between this script and importer
    # todo: use a single source for graphql queries between importer, config im/exporter, C#

# todo: get more config data
    # get user related data:
        # ldap servers
        # tenants
        # uiusers including roles & groups & tenants

# todo: encrypt config before writing to file

# todo: adapt device data:
    # todo: replace importer_hostname with target machine
    # todo: replace ssh_private_key with key of target machine

# do not forget to reset the sequences after importing 
# SELECT setval(pg_get_serial_sequence('device', 'dev_id'), coalesce(max(dev_id), 0)+1 , false) FROM device;
# SELECT setval(pg_get_serial_sequence('management', 'mgm_id'), coalesce(max(mgm_id), 0)+1 , false) FROM management;    

import sys, logging, re
import json, requests, requests.packages, argparse
base_dir = "/usr/local/fworch"
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir)
import common_scripts as common_scripts, fwo_api

parser = argparse.ArgumentParser(
    description="Export fworch configuration into encrypted json file\nsample; synopsis for ex- and import: fwo-export-config.py -o/tmp/fworch-config.graphql; <move to freshly installed FWO system without demo data>; fwo-execute-graphql.py -i/tmp/fworch-config.graphql")
parser.add_argument('-o', '--out', metavar='output_file', required=True, help='filename to write output in json format to')
parser.add_argument('-u', '--user', metavar='user_name', default='admin', help='username for getting fworch config (default=admin')
parser.add_argument('-p', '--password', metavar='password_file', default=base_dir + '/etc/secrets/ui_admin_pwd', help='username for getting fworch config (default=$FWORCH_HOME/etc/secrets/ui_admin_pwd')
parser.add_argument('-d', '--debug', metavar='debug_level', default='0',
                    help='Debug Level: 0=off, 1=send debug to console, 2=send debug to file, 3=keep temporary config files; default=0')
parser.add_argument('-s', '--ssl', metavar='ssl_verification_mode', default='',
                    help='[ca]certfile, if value not set, ssl check is off"; default=empty/off')
parser.add_argument('-f', '--format', metavar='output_format', default='graphql',
                    help='specify output format [json|graphql(default)]')


def convert_jsonString2graphql(json_in):
    json_transformed = ' '.join(json_in.split()) # replace multiple spaces with single space
    json_transformed = json_transformed.translate(str.maketrans("", "", "\n")) # remove all line breaks
    lines = json_transformed.split(",")  # only one key/value pair per line
    result = []
    for line in lines:
        pos = line.find(':') # find first ":"
        if pos>=0:
            if pos>0:
                left = line[:pos].translate(str.maketrans('', '', '"'))  # remove " around field name
                right = line[pos+1:]
            else:  # first char is :
                left = ''
                right = line[1:]
            result.append(left + ':' + right)
    return ','.join(result)


args = parser.parse_args()
if len(sys.argv) == 1:
    parser.print_help(sys.stderr)
    sys.exit(1)

fwo_config_filename = base_dir + '/etc/fworch.json'
if args.ssl == '' or args.ssl == 'off':
    requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only
debug_level = int(args.debug)
common_scripts.set_log_level(log_level=debug_level, debug_level=debug_level)

# read fwo config (API URLs)
with open(fwo_config_filename, "r") as fwo_config:
    fwo_config = json.loads(fwo_config.read())
user_management_api_base_url = fwo_config['middleware_uri']
fwo_api_base_url = fwo_config['api_uri']

method = 'api/AuthenticationToken/Get'
ssl_mode = args.ssl

# authenticate to get JWT
with open(args.password, 'r') as file:
    exporter_pwd = file.read().replace('\n', '')

jwt = fwo_api.login(args.user, exporter_pwd, user_management_api_base_url,
                        method, ssl_verification=ssl_mode)

config_json = {}
# get device details

mgm_query = """
    query getFullDeviceDetails {
        management {
            mgm_id
            mgm_name
            ssh_hostname
            ssh_port
            import_credential_id
            import_credential {
                user: username
                secret
                sshPublicKey: public_key
                credential_name
                is_key_pair
                id
            }
            dev_typ_id
            config_path
            do_not_import
            force_initial_import
            hide_in_gui
            importer_hostname
            mgm_comment
            debug_level
            mgm_create
            mgm_update
            last_import_md5_complete_config
        }
        device {
            dev_id
            dev_name
            dev_typ_id
            mgm_id
            local_rulebase_name
            global_rulebase_name
            package_name
            dev_comment
            do_not_import
            force_initial_import
            hide_in_gui
            dev_create
            dev_update
        }
    }
    """
api_call_result = fwo_api.call(fwo_api_base_url, jwt, mgm_query, query_variables={}, role='admin')
if 'data' in api_call_result:
    config_json.update({ 'device_configuration': api_call_result['data'] })
else:
    logging.error('did not succeed in getting device details from API')
    sys.exit(1)

if not re.compile(args.format+"$").match(args.out ):
    outfile = args.out + '.' + args.format
else:
    outfile = args.out

with open(outfile, 'w') as file:
    if args.format == 'json':
        file.write(str(api_call_result['data']))
    elif args.format == 'graphql':
        # graphql_query = {
        #     "query_string": """mutation restoreDeviceData($managementObjects, $deviceObjects) { 
        #             insert_management ( objects: $managementObjects ) { returning { mgm_id } } 
        #             insert_device ( objects: $deviceObjects ) { returning { dev_id } }
        #         }""",
        #     "query_variables": {
        #         "managementObjects": config_json['device_configuration']['management'], 
        #         "deviceObjects": config_json['device_configuration']['device']
        #     }
        # }
        # file.write(json.dumps(graphql_query))

        config_string = "mutation restoreDeviceData { insert_management ( objects: " + \
            convert_jsonString2graphql(json.dumps(config_json['device_configuration']['management'], indent=3)) + \
            ") { returning { mgm_id } } " + \
            "insert_device ( objects: " + \
            convert_jsonString2graphql(json.dumps(config_json['device_configuration']['device'], indent=3)) + \
            ") { returning { dev_id } } }\n"
        file.write(config_string)

sys.exit(0)
