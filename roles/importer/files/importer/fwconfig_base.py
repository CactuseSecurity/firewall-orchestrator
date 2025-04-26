import json
import hashlib
from fwo_base import ConfFormat, ConfigAction


class FwoEncoder(json.JSONEncoder):

    def default(self, obj):

        if isinstance(obj, ConfigAction) or isinstance(obj, ConfFormat):
            return obj.name
        
        return json.JSONEncoder.default(self, obj)

def calcManagerUidHash(mgm_details):
    combination = f"""
        {replaceNoneWithEmpty(mgm_details.Hostname)}
        {replaceNoneWithEmpty(mgm_details.Port)}
        {replaceNoneWithEmpty(mgm_details.DomainUid)}
        {replaceNoneWithEmpty(mgm_details.DomainName)}
    """
    return hashlib.sha256(combination.encode()).hexdigest()


def replaceNoneWithEmpty(s):
    if s is None or s == '':
        return '<EMPTY>'
    else:
        return str(s)
    