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
importer_pwd_file = base_dir + '/etc/secrets/importer_pwd'
import_tmp_path = base_dir + '/tmp/import'
fwo_config_filename = base_dir + '/etc/fworch.json'
# fwo_api_verify_certs = False    # default value for certificate verification
# fwo_api_suppress_cert_warnings = True    # default value for suppressing certificate warnings
# fwo_default_proxy_settings = { "no_proxy": "localhost,127.0.0.1,::", "http_proxy": "", "https_proxy": "" }

# global_fwo_api_verify_certs=None
# global_fwo_api_suppress_cert_warnings=None
# global_debug_level=0
# global_fwo_default_proxy_settings = {}

# def setGlobalValues (
#         proxy=None,
#         verify_certificates=None, 
#         suppress_cert_warnings=None,
#         debug_level = 0,
#         fwo_api_url = None,
#         jwt=None
#     ):
#     global global_fwo_api_verify_certs
#     global global_fwo_api_suppress_cert_warnings
#     global global_fwo_default_proxy_settings
#     global global_debug_level
#     global_fwo_api_verify_certs = verify_certificates
#     global_fwo_api_suppress_cert_warnings = suppress_cert_warnings
#     global_debug_level=debug_level
    
#     if proxy is None:
#         proxy = {}
#     else:
#         global_fwo_default_proxy_settings = { "no_proxy": "localhost,127.0.0.1,::", "http_proxy": proxy, "https_proxy": proxy }
        
#         # if fwo_api host == local importer host or fwo_api host resolved = localhost: add hostname of fwo_api host to no_proxy exceptions
#         api_url = urlparse(fwo_api_base_url)
#         api_hostname = api_url.hostname
#         api_ip = socket.gethostbyname(api_hostname)
#         if api_hostname == 'localhost' or api_ip == '127.0.0.1' or api_ip == '::':
#             global_fwo_default_proxy_settings['no_proxy'] += ',' + api_hostname

#         local_importer_hostname = socket.gethostname()
#         importer_ip = socket.gethostbyname(local_importer_hostname)
#         if importer_ip == api_ip or local_importer_hostname == api_hostname:
#             global_fwo_default_proxy_settings['no_proxy'] += ',' + local_importer_hostname
    # if proxy is None:
    #     global_fwo_default_proxy_settings = fwo_api.get_config_value(fwo_api_base_url, jwt, key='importFwProxy')


# def initialize_db(name):
#     if (this.db_name is None):
#         # also in local function scope. no scope specifier like global is needed
#         this.db_name = name
#         # also the name remains free for local use
#         db_name = "Locally scoped db_name variable. Doesn't do anything here."
#     else:
#         msg = "Database is already initialized to {0}."
#         raise RuntimeError(msg.format(this.db_name))


# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of max. 5 x this value - nwobj/svc/rules/...)
# the database has a limit of 255 MB per jsonb
# https://stackoverflow.com/questions/12632871/size-limit-of-json-data-type-in-postgresql
# >25.000 rules exceed this limit
max_objs_per_chunk = 1000 

# with open(fwo_config_filename, "r") as fwo_config:
#     fwo_config = json.loads(fwo_config.read())
# fwo_api_base_url = fwo_config['api_uri']
