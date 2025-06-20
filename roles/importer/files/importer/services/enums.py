from enum import Enum

class Lifetime(Enum):
    SINGLETON = "singleton"
    TRANSIENT = "transient"

class Services(Enum):
    UID2ID_MAPPER = "uid2id_mapper"
    GROUP_FLATS_MAPPER = "group_flats_mapper"
    GLOBAL_STATE = "global_state"
    FW_CONFIG_IMPORT_GATEWAY = "fwconfig_import_gateway"

