#!/usr/bin/python3

import logging
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

