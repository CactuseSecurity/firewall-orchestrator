
from models.gateway import Gateway
from  typing_extensions import List
import hashlib
from models.gateway import Gateway

class GatewayController():

    gateway: Gateway

    def __init__(self, gw: Gateway):
        self.Gateway = gw
     
    @classmethod
    def buildGatewayList(cls, mgmDetails: dict) -> List['Gateway']:
        gws = []
        for gw in mgmDetails['devices']:
            # check if gateway import is enabled
            if 'do_not_import' in gw and gw['do_not_import']:
                continue
            gws.append(Gateway(Name = gw['name'], Uid = f"{gw['name']}/{cls.calcManagerUidHash(mgmDetails)}"))
        return gws


    @classmethod
    def calcManagerUidHash(cls, mgm_details):
        combination = f"""
            {cls.replaceNoneWithEmpty(mgm_details['hostname'])}
            {cls.replaceNoneWithEmpty(mgm_details['port'])}
            {cls.replaceNoneWithEmpty(mgm_details['domainUid'])}
            {cls.replaceNoneWithEmpty(mgm_details['configPath'])}
        """
        return hashlib.sha256(combination.encode()).hexdigest()

    @staticmethod
    def replaceNoneWithEmpty(s):
        if s is None or s == '':
            return '<EMPTY>'
        else:
            return str(s)

    def __eq__(self, other):
        if isinstance(other, Gateway):
            return self. gateway == other
        return False
