import json
import hashlib
from fwo_base import ConfFormat, ConfigAction


class FwoEncoder(json.JSONEncoder):

    def default(self, obj):

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        if isinstance(obj, Policy):
            return obj.toJson()
        
        return json.JSONEncoder.default(self, obj)

# class FwConfig(BaseModel):
#     ConfigFormat: ConfFormat
#     FwConf: dict

#     # def __init__(self, configFormat: ConfFormat=ConfFormat.NORMALIZED, config={}):
#     #     self.ConfigFormat = configFormat
#     #     self.FwConf = config

#     @classmethod
#     def fromJson(cls, jsonDict):
#         ConfigFormat = jsonDict['ConfigFormat']
#         Config = jsonDict['config']
#         return cls(ConfigFormat, Config)

#     def __str__(self):
#         return f"{self.ConfigType}({str(self.Config)})"

#     def IsLegacy(self):
#         return self.ConfigFormat in [ConfFormat.NORMALIZED_LEGACY, ConfFormat.CHECKPOINT_LEGACY, 
#                                     ConfFormat.CISCOFIREPOWER_LEGACY, ConfFormat.FORTINET_LEGACY, 
#                                     ConfFormat.PALOALTO_LEGACY]

def calcManagerUidHash(mgm_details):
    combination = f"""
        {replaceNoneWithEmpty(mgm_details['hostname'])}
        {replaceNoneWithEmpty(mgm_details['port'])}
        {replaceNoneWithEmpty(mgm_details['domainUid'])}
        {replaceNoneWithEmpty(mgm_details['configPath'])}
    """
    return hashlib.sha256(combination.encode()).hexdigest()


def replaceNoneWithEmpty(s):
    if s is None or s == '':
        return '<EMPTY>'
    else:
        return str(s)
    