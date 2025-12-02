
from typing import Any


BASE_DIR = '/usr/local/fworch'
IMPORTER_BASE_DIR = BASE_DIR + '/importer'

FW_MODULE_NAME = 'fwcommon'  # the module start-point for product specific code
FULL_CONFIG_SIZE_LIMIT = 5000000 # native configs greater than 5 MB will not be stored in DB
LIST_DELIMITER = '|'
USER_DELIMITER = "@"
ANY_IP_IPV4 = '0.0.0.0/0'
DUMMY_IP = '0.0.0.0/32'
DEFAULT_COLOR = 'black'
NAT_POSTFIX = '_NatNwObj'
FWO_API_HTTP_IMPORT_TIMEOUT = 14400 # 4 hours
IMPORTER_USER_NAME = 'importer'  # TODO: move to config file?
FWO_CONFIG_FILENAME = BASE_DIR + '/etc/fworch.json'
MAIN_KEY_FILE=BASE_DIR + '/etc/secrets/main_key'
IMPORTER_PWD_FILE = BASE_DIR + '/etc/secrets/importer_pwd'
IMPORT_TMP_PATH = BASE_DIR + '/tmp/import'
DEFAULT_SECTION_HEADER_TEXT = 'section without name'
GRAPHQL_QUERY_PATH = BASE_DIR + '/fwo-api-calls/'

# possible ConfigFormat values: normalized|checkpoint|fortimanager|fortioOS|azure|ciscoFirePower
# legacy: barracuda|junos|netscreen

# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of max. 5 x this value - nwobj/svc/rules/...)
# the database has a limit of 255 MB per jsonb
# https://stackoverflow.com/questions/12632871/size-limit-of-json-data-type-in-postgresql
# >25.000 rules exceed this limit
API_CALL_CHUNK_SIZE = 1000
RULE_NUM_NUMERIC_STEPS = 1024.0

EMPTY_NORMALIZED_FW_CONFIG_JSON_DICT: dict[str, list[Any]] = { #TYPING: DO NOT USE THIS!!!!
    'network_objects': [],
    'service_objects': [],
    'user_objects': [],
    'zone_objects': [],
    'rules': [],
    'gateways': []
}
