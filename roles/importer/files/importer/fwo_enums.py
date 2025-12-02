from enum import Enum

class ConfigAction(Enum):
    INSERT = 'INSERT'
    UPDATE = 'UPDATE'
    DELETE = 'DELETE'

class ConfFormat(Enum):
    NORMALIZED = 'NORMALIZED'
    
    CHECKPOINT = 'CHECKPOINT'
    FORTINET = 'FORTINET'
    FORTIMANAGER = 'FORTIMANAGER'
    PALOALTO = 'PALOALTO'
    CISCOFIREPOWER = 'CISCOFIREPOWER'

    NORMALIZED_LEGACY = 'NORMALIZED_LEGACY'

    CHECKPOINT_LEGACY = 'CHECKPOINT_LEGACY'
    FORTINET_LEGACY = 'FORTINET_LEGACY'
    PALOALTO_LEGACY = 'PALOALTO_LEGACY'
    CISCOFIREPOWER_LEGACY = 'CISCOFIREPOWER_LEGACY'

    @staticmethod
    def IsLegacyConfigFormat(conf_format_string: str) -> bool:
        return ConfFormat(conf_format_string) in [ConfFormat.NORMALIZED_LEGACY, ConfFormat.CHECKPOINT_LEGACY, 
                                    ConfFormat.CISCOFIREPOWER_LEGACY, ConfFormat.FORTINET_LEGACY, 
                                    ConfFormat.PALOALTO_LEGACY]
