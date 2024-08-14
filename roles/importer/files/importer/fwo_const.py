import sys
import json
from urllib.parse import urlparse
import socket

base_dir = '/usr/local/fworch'
importer_base_dir = base_dir + '/importer'
sys.path.append(importer_base_dir) # adding absolute path here once

fw_module_name = 'fwcommon'  # the module start-point for product specific code
full_config_size_limit = 5000000 # native configs greater than 5 MB will not be stored in DB
csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
apostrophe = "\""
section_header_uids=[]
nat_postfix = '_NatNwObj'
fwo_api_http_import_timeout = 14400 # 4 hours
importer_user_name = 'importer'  # todo: move to config file?
fwo_config_filename = base_dir + '/etc/fworch.json'
mainKeyFile=base_dir + '/etc/secrets/main_key'
importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'
import_tmp_path = base_dir + '/tmp/import'
fwo_config_filename = base_dir + '/etc/fworch.json'
max_recursion_level = 25 # do not call a function recursively more than this
default_section_header_text = 'section without name'
# possible ConfigFormat values: normalized|checkpoint|fortimanager|fortioOS|azure|ciscoFirePower
# legacy: barracuda|junos|netscreen

# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of max. 5 x this value - nwobj/svc/rules/...)
# the database has a limit of 255 MB per jsonb
# https://stackoverflow.com/questions/12632871/size-limit-of-json-data-type-in-postgresql
# >25.000 rules exceed this limit
max_objs_per_chunk = 1000 

# with open(fwo_config_filename, "r") as fwo_config:
#     fwo_config = json.loads(fwo_config.read())
# fwo_api_base_url = fwo_config['api_uri']

emptyNormalizedFwConfigJsonDict = {
    'network_objects': [],
    'service_objects': [],
    'users': [],
    'zone_objects': [],
    'rules': [],
    'gateways': []
}
