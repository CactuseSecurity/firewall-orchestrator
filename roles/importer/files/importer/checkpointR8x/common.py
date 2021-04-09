import logging

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
section_header_uids=[]

# the following is the static across all installations unique any obj uid 
# cannot fetch the Any object via API (<=1.7) at the moment
# therefore we have a workaround adding the object manually (as svc and nw)
any_obj_uid = "97aeb369-9aea-11d5-bd16-0090272ccb30"
# todo: read this from config (vom API 1.6 on it is fetched)


# todo: save the initial value, reset initial value at the end
def set_log_level(log_level, debug_level):
    logger = logging.getLogger(__name__)
    # todo: use log_level to define non debug logging
    #       use debug_level to define different debug levels
    if debug_level == 1:
        logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    elif debug_level == 2:
        logging.basicConfig(filename='/var/tmp/fworch_get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    elif debug_level == 3:
        logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
        logging.basicConfig(filename='/var/tmp/fworch_get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
    logger.debug ("debug_level: "+ str(debug_level) )


def get_ip_of_obj(obj):
    if 'ipv4-address' in obj:
        ip_addr = obj['ipv4-address']
    elif 'ipv6-address' in obj:
        ip_addr = obj['ipv6-address']
    elif 'subnet4' in obj:
        ip_addr = obj['subnet4'] + '/' + str(obj['mask-length4'])
    elif 'subnet6' in obj:
        ip_addr = obj['subnet6'] + '/' + str(obj['mask-length6'])
    elif 'obj_typ' in obj and obj['obj_typ'] == 'group':
        ip_addr = ''
    else:
        ip_addr = '0.0.0.0/0'
    return ip_addr


# logging config
# debug_level = 0

# if debug_level == 1:
#     logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
# elif debug_level == 2:
#     logging.basicConfig(filename='/var/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
# elif debug_level == 3:
#     logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
#     logging.basicConfig(filename='/var/tmp/get_config_cp_r8x_api.debug', filemode='a', level=logging.DEBUG, format='%(asctime)s - %(levelname)s - %(message)s')
