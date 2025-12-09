from fwo_log import FWOLogger
from typing import Any

from models.import_state import ImportState


"""
    normalize all gateway details
"""
def normalize_gateways (native_config: dict[str, Any], import_state: ImportState, normalized_config: dict[str, Any]):
    normalized_config['gateways'] = []
    normalize_rulebase_links (native_config, normalized_config)
    normalize_interfaces (native_config, import_state, normalized_config)
    normalize_routing (native_config, import_state, normalized_config)


def normalize_rulebase_links (native_config: dict[str, Any], normalized_config: dict[str, Any]):
    gw_range = range(len(native_config['gateways']))
    for gw_id in gw_range:
        gw_uid = native_config['gateways'][gw_id]['uid']
        if not gw_in_normalized_config(normalized_config, gw_uid):
            gw_normalized = create_normalized_gateway(native_config, gw_id)
            normalized_config['gateways'].append(gw_normalized)
        for gw_normalized in normalized_config['gateways']:
            if gw_normalized['Uid'] == gw_uid:
                gw_normalized['RulebaseLinks'] = get_normalized_rulebase_link(native_config, gw_id)
                break


def get_normalized_rulebase_link(native_config: dict[str, Any], gw_id: int) -> list[dict[str, Any]]:
    links = native_config.get('gateways', {})[gw_id].get('rulebase_links')
    for link in links:
        if 'type' in link:
            link['link_type'] = link['type']
            del link['type']
        else:
            FWOLogger.warning('No type in rulebase link: ' + str(link))

        # Remove from_rulebase_uid and from_rule_uid if link_type is initial
        if link['link_type'] == 'initial':
            if link['from_rulebase_uid'] is not None:
                link['from_rulebase_uid'] = None
            if link['from_rule_uid'] is not None:
                link['from_rule_uid'] = None
    return links


def create_normalized_gateway(native_config: dict[str, Any], gw_id: int) -> dict[str, Any]:
    gw: dict[str, Any] = {}
    gw['Uid'] = native_config['gateways'][gw_id]['uid']
    gw['Name'] = native_config['gateways'][gw_id]['name']
    gw['Interfaces'] = []
    gw['Routing'] = []
    gw['RulebaseLinks'] = []
    return gw
            

def normalize_interfaces (native_config: dict[str, Any], import_state: ImportState, normalized_config: dict[str, Any]):
    # TODO: Implement this
    pass


def normalize_routing (native_config: dict[str, Any], import_state: ImportState, normalized_config: dict[str, Any]):
    # TODO: Implement this
    pass


def gw_in_normalized_config(normalized_config: dict[str, Any], gw_uid: str) -> bool:
    for gw in normalized_config['gateways']:
        if gw['Uid'] == gw_uid:
            return True
    return False
