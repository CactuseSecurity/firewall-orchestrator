from fwo_log import getFwoLogger
from typing import Any

from model_controllers.import_state_controller import ImportStateController


"""
    normalize all gateway details
"""
def normalize_gateways (nativeConfig: dict[str, Any], importState: ImportStateController, normalizedConfig: dict[str, Any]):
    normalizedConfig['gateways'] = []
    normalize_rulebase_links (nativeConfig, importState, normalizedConfig)
    normalize_interfaces (nativeConfig, importState, normalizedConfig)
    normalize_routing (nativeConfig, importState, normalizedConfig)


def normalize_rulebase_links (nativeConfig: dict[str, Any], importState: ImportStateController, normalizedConfig: dict[str, Any]):
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


def get_normalized_rulebase_link(nativeConfig: dict[str, Any], gwId: int) -> list[dict[str, Any]]:
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


def create_normalized_gateway(nativeConfig: dict[str, Any], gwId: int) -> dict[str, Any]:
    gw: dict[str, Any] = {}
    gw['Uid'] = nativeConfig['gateways'][gwId]['uid']
    gw['Name'] = nativeConfig['gateways'][gwId]['name']
    gw['Interfaces'] = []
    gw['Routing'] = []
    gw['RulebaseLinks'] = []
    return gw
            

def normalize_interfaces (nativeConfig: dict[str, Any], importState: ImportStateController, normalizedConfig: dict[str, Any]):
    # TODO: Implement this
    pass


def normalize_routing (nativeConfig: dict[str, Any], importState: ImportStateController, normalizedConfig: dict[str, Any]):
    # TODO: Implement this
    pass


def gw_in_normalized_config(normalizedConfig: dict[str, Any], gwUid: str) -> bool:
    for gw in normalizedConfig['gateways']:
        if gw['Uid'] == gwUid:
            return True
    return False
