from enum import Enum

class Lifetime(Enum):
    SINGLETON = "singleton"
    TRANSIENT = "transient"
    IMPORT = "import"
    MANAGEMENT = "management" # only holds data with a scope valid for a single sub-management

class Services(Enum):
    UID2ID_MAPPER = "uid2id_mapper"
    GROUP_FLATS_MAPPER = "group_flats_mapper"
    PREV_GROUP_FLATS_MAPPER = "prev_group_flats_mapper"
    GLOBAL_STATE = "global_state"
    FW_CONFIG_IMPORT_GATEWAY = "fwconfig_import_gateway"
    FWO_CONFIG = "fwo_config"
    RULE_ORDER_SERVICE = "rule_order_service"

