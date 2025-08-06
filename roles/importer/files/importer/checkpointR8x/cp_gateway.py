from fwo_log import getFwoLogger
import fwo_globals


"""
    normalize all gateway details
"""
def normalizeGateways (nativeConfig, importState, normalizedConfig):
    if debug_level>0:
        logger = getFwoLogger()
    
    normalizedConfig['gateways'] = []
    normalizeRulebaseLinks (nativeConfig, importState, normalizedConfig)
    normalizeInterfaces (nativeConfig, importState, normalizedConfig)
    normalizeRouting (nativeConfig, importState, normalizedConfig)


def normalizeRulebaseLinks (nativeConfig, importState, normalizedConfig):
    gwRange = range(len(nativeConfig['gateways']))
    for gwId in gwRange:
        gwUid = nativeConfig['gateways'][gwId]['uid']
        if not gwInNormalizedConfig(normalizedConfig, gwUid):
            gwNormalized = createNormalizedGateway(nativeConfig, gwId)
            normalizedConfig['gateways'].append(gwNormalized)
        for gwNormalized in normalizedConfig['gateways']:
            if gwNormalized['Uid'] == gwUid:
                gwNormalized['RulebaseLinks'] = getNormalizedRulebaseLink(nativeConfig, gwId)
                break

def getNormalizedRulebaseLink(nativeConfig, gwId):
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
            if link['from_rulebase_uid'] != None:
                link['from_rulebase_uid'] = None
            if link['from_rule_uid'] != None:
                link['from_rule_uid'] = None
    return links

def createNormalizedGateway(nativeConfig, gwId):
    gw = {}
    gw['Uid'] = nativeConfig['gateways'][gwId]['uid']
    gw['Name'] = nativeConfig['gateways'][gwId]['name']
    gw['Interfaces'] = []
    gw['Routing'] = []
    gw['RulebaseLinks'] = []
    return gw
            

def normalizeInterfaces (nativeConfig, importState, normalizedConfig):
    # TODO: Implement this
    pass

def normalizeRouting (nativeConfig, importState, normalizedConfig):
    # TODO: Implement this
    pass

def gwInNormalizedConfig(normalizedConfig, gwUid):
    for gw in normalizedConfig['gateways']:
        if gw['Uid'] == gwUid:
            return True
    return False
