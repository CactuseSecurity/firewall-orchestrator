from urllib.parse import urlparse

base_dir = '/usr/local/fworch'
importer_base_dir = base_dir + '/importer'

debug_level=0
fw_module_name = 'fwcommon'  # the module start-point for product specific code
full_config_size_limit = 5000000 # native configs greater than 5 MB will not be stored in DB
csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
user_delimiter = "@"
apostrophe = "\""
section_header_uids=[]
any_ip_ipv4 = '0.0.0.0/0'
dummy_ip = '0.0.0.0/32'
defaultColor = 'black'
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
graphqlQueryPath = base_dir + "/fwo-api-calls/";

# possible ConfigFormat values: normalized|checkpoint|fortimanager|fortioOS|azure|ciscoFirePower
# legacy: barracuda|junos|netscreen

# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of max. 5 x this value - nwobj/svc/rules/...)
# the database has a limit of 255 MB per jsonb
# https://stackoverflow.com/questions/12632871/size-limit-of-json-data-type-in-postgresql
# >25.000 rules exceed this limit
max_objs_per_chunk = 1000
api_call_chunk_size = 1000
rule_num_numeric_steps = 1024.0

emptyNormalizedFwConfigJsonDict = {
    'network_objects': [],
    'service_objects': [],
    'user_objects': [],
    'zone_objects': [],
    'rules': [],
    'gateways': []
}
