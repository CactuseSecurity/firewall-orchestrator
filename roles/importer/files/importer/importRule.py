from typing import List
import datetime
import time
from enum import Enum, auto
from fwo_api import call
from fwoBaseImport import ImportState

class RuleType(Enum):
    ACCESS = auto()
    NAT = auto()
    ACCESSANDNAT = auto()
    SECTIONHEADER = auto()


# normalized rule object without database IDs (pure string refs)
#  
class NormalizedRule():
    Uid: str
    Name: str
    Number: int
    Type: RuleType
    Disabled: bool
    SourceNeg: bool
    Source: List[str]
    SourceRef: List[str]
    DestinationNeg: bool
    Destination: List[str]
    DestinationRef: List[str]
    ServiceNeg: bool
    Service: List[str]
    ServiceRef: List[str]
    Action: str
    Track: str
    InstallOn: List[str]
    Time:List[str]
    CustomFields:dict
    Implied: bool
    LastHit: datetime
    Comment: str
    FromZone: str
    ToZone: str


    def __init__(self,  Uid: str,  Name: str, Number: int, Type: RuleType, Disabled: bool,
        SourceNeg: bool, Source: List[str], SourceRef: List[str], 
        DestinationNeg: bool, Destination: List[str], DestinationRef: List[str],
        ServiceNeg: bool, Service: List[str], ServiceRef: List[str],
        Action: str, Track: str, InstallOn: List[str], Time:List[str], CustomFields:dict,
        Implied: bool, LastHit: datetime,
        Comment: str, FromZone: str, ToZone: str):

        self.Uid = Uid
        self.Name = Name
        self.Number = Number
        self.RuleType = Type
        self.Disabled = Disabled
        self.SourceNeg = SourceNeg
        self.Source = Source
        self.SourceRef = SourceRef
        self.DestinationNeg = DestinationNeg
        self.Destination = Destination
        self.DestinationRef = DestinationRef
        self.ServiceNeg = ServiceNeg
        self.Service = Service
        self.ServiceRef = ServiceRef
        self.Action = Action
        self.Track = Track
        self.InstallOn = InstallOn
        self.Time = Time
        self.CustomFields = CustomFields
        self.Implied = Implied 
        self.LastHit = LastHit
        self.Comment = Comment
        self.FromZone = FromZone
        self.ToZone = ToZone

    def __str__(self):
        return f"{str(self.Name)}({self.Uid})"


# rule object including database ref IDs ready for API import
class Rule(NormalizedRule):
    Id: int
    MgmId: int
    NumberFloat: float
    ActionId: int
    TrackId: int
    SectionHeader: str
    Created: int
    Deleted: int
    CorrespondigNATRule: int


    def __init__(self,  Uid: str,  Name: str, Number: int, Type: RuleType, Disabled: bool,
        SourceNeg: bool, Source: List[str], SourceRef: List[str], 
        DestinationNeg: bool, Destination: List[str], DestinationRef: List[str],
        ServiceNeg: bool, Service: List[str], ServiceRef: List[str],
        Action: str, Track: str, InstallOn: List[str], Time:List[str], CustomFields:dict,
        Implied: bool, LastHit: datetime,
        Comment: str,
        FromZone: str, ToZone: str,
        MgmId: int, refLists: dict
    ):

        self.Uid = Uid
        self.Name = Name
        self.Number = Number
        self.RuleType = Type
        self.Disabled = Disabled
        self.SourceNeg = SourceNeg
        self.Source = Source
        self.SourceRef = SourceRef
        self.DestinationNeg = DestinationNeg
        self.Destination = Destination
        self.DestinationRef = DestinationRef
        self.ServiceNeg = ServiceNeg
        self.Service = Service
        self.ServiceRef = ServiceRef
        self.Action = Action
        self.Track = Track
        self.InstallOn = InstallOn
        self.Time = Time
        self.CustomFields = CustomFields
        self.Implied = Implied 
        self.LastHit = LastHit
        self.Comment = Comment
        self.FromZone = FromZone
        self.ToZone = ToZone
        self.FromZoneId = lookupZone(FromZone, refLists)
        self.ToZoneId = lookupZone (ToZone, refLists)
        self.MgmId = MgmId
        self.NumberFloat = NumberFloat ### TODO
        self.ActionId = lookupAction(Action, refLists)
        self.TrackId = lookupTrack(Track, refLists)

    # def importInsertRuleViaApi(self):
    #     # Id = call()
    #     # self.Id = Id
    #     pass

    def __str__(self):
        return f"{str(self.Name)}({self.Uid})"

    def insertObjectRefs(self, refLists):
        sourceIds = []
        for nwUid in self.SourceRef:
            sourceIds.append(lookupNetwork(nwUid, refLists))

        # importInsertRuleSourceRefs(sourceIds, self.Id)

    def toImportJsonRule(self, importState: ImportState):
        ruleDict = {
            # {
            #     "mgm_id": importState.MgmDetails.Id,
            #     "rule_num": rule.rule_num,
            #     "action_id": rule.action_id,
            #     "track_id": rule.track_id,
            #     "rule_track": "log",
            #     "access_rule": True,
            #     "rule_action": "accept",
            #     "rule_src": "abc",
            #     "rule_dst": "abc",
            #     "rule_svc": "abc",
            #     "rule_create": 1,
            #     "rule_last_seen": 1,
            #     "rulebase_on_gateways": {
            #         "data": [
            #             {
            #                 "dev_id": 24,
            #                 "rulebase_id": 2
            #             }
            #         ]
            #     }
            # }        
        }
        return ruleDict


class RuleList():

    Rules: List[Rule]


    def __init__(self,  Rules: List[Rule]):
        self.Rules = Rules


    def importInsertRulesViaApi(self, importState: ImportState):

        rules = []
        for rule in self:
            rules.append(rule.toImportJsonRule())
            

        queryVariables = { "rules": [ rules ] }
        mutation = """
            mutation importInsertRule($rules: [rule_insert_input!]!) {
                insert_rule(objects: $rules) {
                    affected_rows
                }
            }
        """
        
        return call(importState.FwoConfig.FwoApiUri, importState.Jwt, mutation, query_variables=queryVariables, role='importer')


# TODO: lookup object uids (network, service) and fill rule_source, ... tables
# fill group tables
# also fill group_flat tables
# figure out how to get rule order (position) of an inserted rule in checkpoint when using show-changes API cmd
#   when using show-changes with "details-level: full", we get a position back:
#       {'uid': 'cf8c7582-fd95-464c-81a0-7297df3c5ad9', 'type': 'access-rule', 'domain': {'uid': '41e821a0-3720-11e3-aa6e-0800200c9fde', 'name': 'SMC User', 'domain-type': 'domain'}, 'position': 7, 'track': {'type': {...}, 'per-session': False, 'per-connection': False, 'accounting': False, 'enable-firewall-session': False, 'alert': 'none'}, 'layer': '0f45100c-e4ea-4dc1-bf22-74d9d98a4811', 'source': [{...}], 'source-negate': False, 'destination': [{...}], 'destination-negate': False, 'service': [{...}], 'service-negate': False, 'service-resource': '', 'vpn': [{...}], 'action': {'uid': '6c488338-8eec-4103-ad21-cd461ac2c472', 'name': 'Accept', 'type': 'RulebaseAction', 'domain': {...}, 'color': 'none', 'meta-info': {...}, 'tags': [...], 'icon': 'Actions/actionsAccept', 'comments': 'Accept', 'display-name': 'Accept', 'customFields': None}, 'action-settings': {'enable-identity-captive-portal': False}, 'content': [{...}], 'content-negate': False, 'content-direction': 'any', 'time': [{...}], 'custom-fields': {'field-1': '', 'field-2': '', 'field-3': ''}, 'meta-info': {'lock': 'unlocked', 'validation-state': 'ok', 'last-modify-time': {...}, 'last-modifier': 'tim-admin', 'creation-time': {...}, 'creator': 'tim-admin'}, 'comments': '', 'enabled': True, 'install-on': [{...}], 'available-actions': {'clone': 'not_supported'}, 'tags': []}
#       need to 
#       a) be careful and not get too many changes with "full" - limit = 150
#       b) check how to interprete this position (got 7 for rule with number 9 in SmartConsole)
# CREATE OR REPLACE FUNCTION import_rules_set_rule_num_numeric (BIGINT,INTEGER) RETURNS VOID AS $$
# DECLARE
# 	i_current_control_id ALIAS FOR $1; -- ID des aktiven Imports
# 	i_dev_id ALIAS FOR $2; -- ID des zu importierenden Devices
# 	i_mgm_id INTEGER; -- ID des zugehoerigen Managements
# 	r_rule RECORD;
# 	i_prev_numeric_value BIGINT;
# 	i_next_numeric_value BIGINT;
# 	i_numeric_value BIGINT;
# 	v_rulebase_name VARCHAR;

# BEGIN
# 	RAISE DEBUG 'import_rules_set_rule_num_numeric - start';
# 	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
# 	SELECT INTO v_rulebase_name local_rulebase_name FROM device WHERE dev_id=i_dev_id;
# 	RAISE DEBUG 'import_rules_set_rule_num_numeric - mgm_id=%, dev_id=%, before inserting', i_mgm_id, i_dev_id;
# 	i_prev_numeric_value := NULL;
# 	FOR r_rule IN -- set rule_num_numeric for changed (i.e. "new") rules
# 		SELECT rule.rule_id, rule_num_numeric FROM import_rule LEFT JOIN rule USING (rule_uid) WHERE
# 			active AND
# 			import_rule.control_id = i_current_control_id AND
# 			import_rule.rulebase_name = v_rulebase_name AND
# 			rule.dev_id=i_dev_id 
# 			ORDER BY import_rule.rule_num
# 	LOOP
# 		RAISE DEBUG 'import_rules_set_rule_num_numeric loop rule %', CAST(r_rule.rule_id AS VARCHAR );
# 		IF r_rule.rule_num_numeric IS NULL THEN
# 			RAISE DEBUG 'import_rules_set_rule_num_numeric found new rule %', CAST(r_rule.rule_id AS VARCHAR );
# 			-- get numeric value of next rule:
# 			SELECT INTO i_next_numeric_value rule_num_numeric FROM rule 
# 				WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_num_numeric>i_prev_numeric_value ORDER BY rule_num_numeric LIMIT 1;
# 			RAISE DEBUG 'import_rules_set_rule_num_numeric next rule %', CAST(i_next_numeric_value AS VARCHAR);
# 			IF i_prev_numeric_value IS NULL AND i_next_numeric_value IS NULL THEN
# 				i_numeric_value := 0;
# 			ELSIF i_next_numeric_value IS NULL THEN
# 				i_numeric_value := i_prev_numeric_value + 1000;
# 			ELSIF i_prev_numeric_value IS NULL THEN
# 				i_numeric_value := i_next_numeric_value - 1000;
# 			ELSE
# 				i_numeric_value := (i_prev_numeric_value + i_next_numeric_value) / 2;
# 			END IF; 
# 			RAISE DEBUG 'import_rules_set_rule_num_numeric determined rule_num_numeric %', CAST(i_numeric_value AS VARCHAR);
# 			UPDATE rule SET rule_num_numeric = i_numeric_value WHERE rule.rule_id=r_rule.rule_id;
# 			r_rule.rule_num_numeric := i_numeric_value;
# 		END IF;
# 		i_prev_numeric_value := r_rule.rule_num_numeric;
# 	END LOOP;
# 	RAISE DEBUG 'import_rules_set_rule_num_numeric - end';
# 	RETURN;
# END;
# $$ LANGUAGE plpgsql;