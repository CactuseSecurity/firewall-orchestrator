
from models.gateway import Gateway
from typing import List
import hashlib
from models.gateway import Gateway
from models.management_details import ManagementDetails
from models.gateway import Gateway

class GatewayController(Gateway):

    def __init__(self, gw: Gateway):
        self.Gateway = gw
     
    @classmethod
    def buildGatewayList(cls, mgmDetails: ManagementDetails) -> List['Gateway']:
        devs = []
        for dev in mgmDetails.Devices:
            # check if gateway import is enabled
            if 'do_not_import' in dev and dev['do_not_import']: # TODO: get this key from the device
                continue
            devs.append(Gateway(Name = dev['name'], Uid = f"{dev['name']}/{cls.calcManagerUidHash(mgmDetails)}"))
        return devs


    @classmethod
    def calcManagerUidHash(cls, mgm_details):
        combination = f"""
            {cls.replaceNoneWithEmpty(mgm_details.Hostname)}
            {cls.replaceNoneWithEmpty(mgm_details.Port)}
            {cls.replaceNoneWithEmpty(mgm_details.DomainUid)}
            {cls.replaceNoneWithEmpty(mgm_details.DomainName)}
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
            return (
                self.Name == other.Name and
                self.Uid == other.Uid and
                self.Routing == other.Routing and
                self.Interfaces == other.Interfaces and
                self.RulebaseLinks == other.RulebaseLinks and
                self.EnforcedNatPolicyUids == other.EnforcedNatPolicyUids and
                self.EnforcedPolicyUids == other.EnforcedPolicyUids and
                self.GlobalPolicyUid == other.GlobalPolicyUid and
                self.ImportDisabled == other.ImportDisabled and
                self.ShowInUI == other.ShowInUI
            )
        return NotImplemented
