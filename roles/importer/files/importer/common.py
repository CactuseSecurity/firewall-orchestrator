import logging

csv_delimiter = '%'
list_delimiter = '|'
line_delimiter = "\n"
apostrophe = "\""
section_header_uids=[]


def set_log_level(log_level, debug_level):
    # todo: save the initial value, reset initial value at the end
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


def csv_add_field(content, no_csv_delimiter=False):
    if content == '' and not no_csv_delimiter:  # do not add apostrophes for empty fields
        field_result = csv_delimiter
    else:
        # add apostrophes at beginning and end and escape any ocurrence of them within the string 
        field_result = apostrophe + content.replace('"', '\"') + apostrophe
        if not no_csv_delimiter:
            field_result += csv_delimiter
    return field_result
 