from fwo_log import getFwoLogger
import fwo_globals
from typing import Any


"""
    normalize all gateway details
"""
def normalize_gateways (nativeConfig, importState, normalizedConfig):
    if fwo_globals.debug_level>0:
        logger = getFwoLogger()
    
    normalizedConfig['gateways'] = []
    normalize_rulebase_links (nativeConfig, importState, normalizedConfig)
    normalize_interfaces (nativeConfig, importState, normalizedConfig)
    normalize_routing (nativeConfig, importState, normalizedConfig)


def normalize_rulebase_links (nativeConfig, importState, normalizedConfig):
    gwRange = range(len(nativeConfig['gateways']))
    for gwId in gwRange:
        gwUid = nativeConfig['gateways'][gwId]['uid']
        if not gw_in_normalized_config(normalizedConfig, gwUid):
            gwNormalized = create_normalized_gateway(nativeConfig, gwId)
            normalizedConfig['gateways'].append(gwNormalized)
        for gwNormalized in normalizedConfig['gateways']:
            if gwNormalized['Uid'] == gwUid:
                gwNormalized['RulebaseLinks'] = get_normalized_rulebase_link(nativeConfig, gwId)
                break


def get_normalized_rulebase_link(nativeConfig, gwId):
    links = nativeConfig.get('gateways', {})[gwId].get('rulebase_links')
    for link in links:
        if 'type' in link:
            link['link_type'] = link['type']
            del link['type']
        else:
            logger = getFwoLogger()
            logger.warning('No type in rulebase link: ' + str(link))

        # Remove from_rulebase_uid and from_rule_uid if link_type is initial
        if link['link_type'] == 'initial':
            if link['from_rulebase_uid'] is not None:
                link['from_rulebase_uid'] = None
            if link['from_rule_uid'] is not None:
                link['from_rule_uid'] = None
    return links


def create_normalized_gateway(nativeConfig, gwId) -> dict[str, Any]:
    gw = {}
    gw['Uid'] = nativeConfig['gateways'][gwId]['uid']
    gw['Name'] = nativeConfig['gateways'][gwId]['name']
    gw['Interfaces'] = []
    gw['Routing'] = []
    gw['RulebaseLinks'] = []
    return gw
            

def normalize_interfaces (nativeConfig, importState, normalizedConfig):
    # TODO: Implement this
    pass


def normalize_routing (nativeConfig, importState, normalizedConfig):
    # TODO: Implement this
    pass


def gw_in_normalized_config(normalizedConfig, gwUid) -> bool:
    for gw in normalizedConfig['gateways']:
        if gw['Uid'] == gwUid:
            return True
    return False
