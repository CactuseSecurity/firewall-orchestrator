from asyncio.log import logger
from fwo_log import getFwoLogger
import json
import cp_const
import fwo_const
import fwo_globals
from fwo_const import list_delimiter, default_section_header_text
from fwo_base import sanitize
from fwo_exception import ImportRecursionLimitReached
from models.rulebase import Rulebase
from models.rule import Rule


"""
    normalize all gateway details
"""
def normalizeGateways (nativeConfig, importState, normalizedConfig):
    if fwo_globals.debug_level>0:
        logger = getFwoLogger()
    
    normalizedConfig['gateways'] = []
    normalizeRulebaseLinks (nativeConfig, importState, normalizedConfig)
    normalizeInterfaces (nativeConfig, importState, normalizedConfig)
    normalizeRouting (nativeConfig, importState, normalizedConfig)


def normalizeRulebaseLinks (nativeConfig, importState, normalizedConfig):
    rbLinkRange = range(len(nativeConfig['gateways']))
    for rbLink_id in rbLinkRange:
        gwUid = nativeConfig['gateways'][rbLink_id]['uid']

def normalizeInterfaces (nativeConfig, importState, normalizedConfig):
    pass

def normalizeRouting (nativeConfig, importState, normalizedConfig):
    pass
