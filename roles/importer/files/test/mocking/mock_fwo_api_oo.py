from typing import List
from graphql import ArgumentNode, BooleanValueNode, IntValueNode, ListValueNode, ObjectFieldNode, ObjectValueNode, OperationDefinitionNode, StringValueNode, VariableNode, parse, OperationType
from fwo_api_oo import FwoApi
from models.networkobject import NetworkObject
from models.rulebase import Rulebase
from models.serviceobject import ServiceObject
from models.rule import RuleNormalized
from importer import fwo_const
from .mock_config import MockFwConfigNormalizedBuilder

TABLE_IDENTIFIERS = {
    "stm_change_type": "change_type_id",
    "stm_usr_typ": "usr_typ_id",
    "stm_svc_typ": "svc_typ_id",
    "stm_obj_typ": "obj_typ_id",
    "stm_action": "action_id",
    "stm_track": "track_id",
    "stm_color": "color_id",
    "stm_dev_typ": "dev_typ_id",
    "object": "obj_id",
    "service": "svc_id",
    "usr": "user_id",
    "rulebase": "id",
}

STM_TABLES = {
    "stm_change_type": [
        {"change_type_id": 1, "change_type_name": "factory settings"},
        {"change_type_id": 2, "change_type_name": "initial import"},
        {"change_type_id": 3, "change_type_name": "in operation"}
    ],
    "stm_usr_typ": [
        {"usr_typ_id": 1, "usr_typ_name": "group"},
        {"usr_typ_id": 2, "usr_typ_name": "simple"}
    ],
    "stm_svc_typ": [
        {"svc_typ_id": 1, "svc_typ_name": "simple", "svc_typ_comment": "standard services"},
        {"svc_typ_id": 2, "svc_typ_name": "group", "svc_typ_comment": "groups of services"},
        {"svc_typ_id": 3, "svc_typ_name": "rpc", "svc_typ_comment": "special services, here: RPC"}
    ],
    "stm_obj_typ": [
        {"obj_typ_id": i, "obj_typ_name": name} for i, name in enumerate([
            'network', 'group', 'host', 'machines_range', 'dynamic_net_obj',
            'sofaware_profiles_security_level', 'gateway', 'cluster_member',
            'gateway_cluster', 'domain', 'group_with_exclusion', 'ip_range',
            'uas_collection', 'sofaware_gateway', 'voip_gk', 'gsn_handover_group',
            'voip_sip', 'simple-gateway', 'external-gateway', 'voip',
            'access-role'
        ], start=1)
    ],
    "stm_action": [
        {"action_id": i, "action_name": name, "allowed": allowed} for i, (name, allowed) in enumerate([
            ('accept', True), ('drop', False), ('deny', False), ('access', True),
            ('client encrypt', True), ('client auth', True), ('reject', False),
            ('encrypt', True), ('user auth', True), ('session auth', True),
            ('permit', True), ('permit webauth', True), ('redirect', True),
            ('map', True), ('permit auth', True), ('tunnel l2tp', True),
            ('tunnel vpn-group', True), ('tunnel vpn', True),
            ('actionlocalredirect', True), ('inner layer', True),
            # NAT actions
            ('NAT src', False), ('NAT src, dst', False), 
            ('NAT src, dst, svc', False), ('NAT dst', False),
            ('NAT dst, svc', False), ('NAT svc', False),
            ('NAT src, svc', False), ('NAT', False),
            ('inform', True)
        ], start=1)
    ],
    "stm_track": [
        {"track_id": i, "track_name": name} for i, name in enumerate([
            'log', 'none', 'alert', 'userdefined', 'mail', 'account',
            'userdefined 1', 'userdefined 2', 'userdefined 3', 'snmptrap',
            # junos
            'log count', 'count', 'log alert', 'log alert count',
            'log alert count alarm', 'log count alarm', 'count alarm',
            # fortinet
            'all', 'all start', 'utm', 'network log',
            'utm start', 'detailed log'
        ], start=1)
    ],
    "stm_dev_typ": [
        {"dev_typ_id": i, "dev_typ_name": name, "dev_typ_version": version,
         "dev_typ_manufacturer": manufacturer, "dev_typ_predef_svc": predef_svc,
         "dev_typ_is_mgmt": is_mgmt, "dev_typ_is_multi_mgmt": is_multi_mgmt,
         "is_pure_routing_device": is_pure_routing_device}
        for i, (name, version, manufacturer, predef_svc, is_mgmt, is_multi_mgmt, is_pure_routing_device) in enumerate([
            ('Netscreen', '5.x-6.x', 'Netscreen', '', True, False, False),
            ('FortiGateStandalone', '5ff', 'Fortinet', '', True, False, False),
            ('Barracuda Firewall Control Center', 'Vx', 'phion', '', True, False, False),
            ('phion netfence', '3.x', 'phion', '', False, False, False),
            ('Check Point R5x-R7x', '', 'Check Point', '', True, False, False),
            ('JUNOS', '10-21', 'Juniper',
             'any;0;0;65535;;junos-predefined-service;simple;', True, False, False),
            ('Check Point R8x', '', 'Check Point', '', True, False, False),
            ('FortiGate', '5ff', 'Fortinet', '', False, False, False),
            ('FortiADOM', '5ff', 'Fortinet', '', True, False, False),
            ('FortiManager', '5ff', 'Fortinet', '', True, True, False),
            ('Check Point MDS R8x', '', 'Check Point', '', True, True, False),
            ('Cisco Firepower Management Center', '7ff', 'Cisco', '', True, True, False),
            ('Cisco Firepower Domain', '7ff', 'Cisco', '', False, True, False),
            ('Cisco Firepower Gateway', '7ff', 'Cisco', '', False, False, False),
            ('DummyRouter Management', '1', 'DummyRouter', '', False, True, True),
            ('DummyRouter Gateway', '1', 'DummyRouter', '', False, False, True),
            ('Azure', '2022ff', 'Microsoft', '', False, True, False),
            ('Azure Firewall', '2022ff', 'Microsoft', '', False, False, False),
            ('Palo Alto Firewall', '2023ff', 'Palo Alto', '', False, True, False),
            ('Palo Alto Panorama', '2023ff', 'Palo Alto', '', True, True, False),
            ('Palo Alto Management', '2023ff', 'Palo Alto',
             '', False, True, False),
            ('FortiOS Management', 'REST', 'Fortinet',
             '', False, True, False),
            ('Fortinet FortiOS Gateway', 'REST',
             'Fortinet', '', False, False, False),
            ('NSX DFW Gateway','REST','VMWare','',
             False,False,False),
            ('NSX','REST','VMWare','',
             False,True,False),
            ('FortiOS Management','REST','Fortinet','',
             False,True,False),
            ('Fortinet FortiOS Gateway','REST','Fortinet','',
             False,False,False),
            ('NSX DFW Gateway','REST','VMWare','',
             False,False,False),
            ('NSX','REST','VMWare','',
             False,True,False),
            ('FortiOS Management','REST','Fortinet','',
             False,True,False),
            ('Fortinet FortiOS Gateway','REST','Fortinet','',
             False,False,False)
        ], start=1)
    ],
    "stm_color": [
        {"color_id": 0, "color_name": "black"},
        {"color_id": 1, "color_name": "blue"},
    ],
}

INSERT_UNIQUE_PK_CONSTRAINTS = {
    "rule_from": ["rule_id", "obj_id", "user_id"],
    "rule_to": ["rule_id", "obj_id", "user_id"],
    "rule_svc": ["rule_id", "svc_id"],
    "rule_nwobj_resolved": ["rule_id", "obj_id", "user_id"],
    "rule_svc_resolved": ["rule_id", "svc_id"],
    "rule_user_resolved": ["rule_id", "user_id"],
    "objgrp": ["objgrp_id", "objgrp_member_id"],
    "svcgrp": ["svcgrp_id", "svcgrp_member_id"],
    "usergrp": ["usergrp_id", "usergrp_member_id"],
    "objgrp_flat": ["objgrp_flat_id", "objgrp_flat_member_id"],
    "svcgrp_flat": ["svcgrp_flat_id", "svcgrp_flat_member_id"],
    "usergrp_flat": ["usergrp_flat_id", "usergrp_flat_member_id"],
}

def object_to_dict(obj, max_depth=10):
    """
    Converts an object to a dictionary, recursively converting nested objects.
    """
    if max_depth < 0:
        return str(obj)  # Avoid infinite recursion
    dict = {}
    dict['type'] = type(obj).__name__
    if hasattr(obj, 'name'):
        name = obj.name.value if hasattr(obj.name, 'value') else obj.name
        dict['name'] = name
    if hasattr(obj, 'value'):
        dict['value'] = object_to_dict(obj.value, max_depth - 1)
    elif hasattr(obj, 'fields'):
        dict['fields'] = [object_to_dict(field, max_depth - 1) for field in obj.fields]
    elif hasattr(obj, 'arguments'):
        dict['arguments'] = [object_to_dict(arg, max_depth - 1) for arg in obj.arguments]
    elif hasattr(obj, 'values'):
        dict['values'] = [object_to_dict(value, max_depth - 1) for value in obj.values]
    return dict

class MockFwoApi(FwoApi):
    """
    A mock FwoApi that simulates Hasura GraphQL mutations/queries using in-memory tables.
    """

    def __init__(self, ApiUri=None, Jwt=None):
        super().__init__(ApiUri, Jwt)
        self.tables = {}  # {table_name: {pk: row_dict}}
        # Initialize with some standard tables
        for table_name, rows in STM_TABLES.items():
            self.tables[table_name] = {row[TABLE_IDENTIFIERS[table_name]]: row for row in rows}

    def call(self, query, queryVariables="", debug_level=0, analyze_payload=False):
        ast = parse(query)
        # Find the first operation definition
        op_def = next((d for d in ast.definitions if hasattr(d, "operation")), None)
        if op_def is None:
            raise NotImplementedError("No operation definition found in query.")
        
        if not isinstance(op_def, OperationDefinitionNode):
            raise NotImplementedError("Only operation definitions supported in mock.")
        
        if op_def.operation == OperationType.MUTATION:
            return self._handle_mutation(op_def, queryVariables)
        elif op_def.operation == OperationType.QUERY:
            return self._handle_query(op_def, queryVariables)
        else:
            raise NotImplementedError("Only query and mutation supported in mock.")

    def _handle_mutation(self, op_def, variables):
        result = {"data": {}}
        for sel in op_def.selection_set.selections:
            field = sel.name.value
            # pprint.pprint(object_to_dict(sel))
            if field.startswith("insert_"):
                table = field[len("insert_"):]
                # Find the argument name for objects
                try:
                    arg_name = next((a.value.name.value for a in sel.arguments if a.name.value == "objects"), None)
                    objects = variables.get(arg_name, [])
                except AttributeError:
                    objects = [{
                        field.name.value: variables.get(field.value.name.value, None) for field in sel.arguments[0].value.fields
                    }]
                if table not in self.tables:
                    self.tables[table] = {}
                returning = []
                for obj in objects:
                    pk = len(self.tables[table]) + 1
                    obj = dict(obj)  # copy
                    if table not in ["objgrp", "svcgrp", "usergrp", "objgrp_flat", "svcgrp_flat", "usergrp_flat"]: # using pair of reference ids as primary key
                        obj[TABLE_IDENTIFIERS.get(table, f"{table}_id")] = pk
                    if any(("create" in key for key in obj.keys())):
                        obj["removed"] = None
                        obj["active"] = True
                    if table == "rulebase":
                        obj.pop("rules", None)  # remove rules from rulebase insert. why are they even there?
                        if any((row["mgm_id"] == obj["mgm_id"] and 
                                row["uid"] == obj["uid"] for row in self.tables.get("rulebase", {}).values())):
                            continue  # mock on_conflict unique_rulebase_mgm_id_uid constraint
                    if table in INSERT_UNIQUE_PK_CONSTRAINTS:
                        # Check for unique constraints
                        unique_keys = INSERT_UNIQUE_PK_CONSTRAINTS[table]
                        if any(all(row.get(key) == obj.get(key) for key in unique_keys)
                               for row in self.tables[table].values()):
                            raise ValueError(f"Unique constraint violation for {table} with keys {unique_keys}.")
                    self.tables[table][pk] = obj
                    returning.append(obj)
                result["data"][field] = {
                    "affected_rows": len(objects),
                    "returning": returning
                }
            elif field.startswith("update_"):
                table = field[len("update_"):]
                returning = []
                affected = 0
                # Find argument names for where and _set
                where_arg = next((a for a in sel.arguments if a.name.value == "where"), None)
                set_arg = next((a for a in sel.arguments if a.name.value == "_set"), None)
                if where_arg and set_arg:
                    # pprint.pprint(object_to_dict(where_arg))
                    for pk, row in self.tables.get(table, {}).items():
                        if self._row_matches_where(row, where_arg, variables):
                            self._update_row(row, set_arg, variables)
                            returning.append(row)
                            affected += 1
                result["data"][field] = {
                    "affected_rows": affected,
                    "returning": returning
                }
            elif field.startswith("delete_"):
                table = field[len("delete_"):]
                returning = []
                affected = 0
                # Find argument name for where
                where_arg = next((a for a in sel.arguments if a.name.value == "where"), None)
                if where_arg:
                    to_delete = []
                    for pk, row in self.tables.get(table, {}).items():
                        if self._row_matches_where(row, where_arg, variables):
                            returning.append(row)
                            to_delete.append(pk)
                            affected += 1
                    for pk in to_delete:
                        del self.tables[table][pk]
                result["data"][field] = {
                    "affected_rows": affected,
                    "returning": returning
                }
        return result


    def _get_value_from_node(self, value_node, variables):
        """Resolve a value from an AST node, using variables if needed."""
        if isinstance(value_node, VariableNode):
            return variables.get(value_node.name.value)
        elif isinstance(value_node, StringValueNode):
            return value_node.value
        elif isinstance(value_node, IntValueNode):
            return int(value_node.value)
        elif isinstance(value_node, BooleanValueNode):
            return value_node.value
        elif isinstance(value_node, ListValueNode):
            return [self._get_value_from_node(v, variables) for v in value_node.values]
        elif isinstance(value_node, ObjectValueNode):
            return {f.name.value: self._get_value_from_node(f.value, variables) for f in value_node.fields}
        else:
            if hasattr(value_node, "value"):
                return value_node.value
            raise ValueError("Unsupported AST node type: {}".format(type(value_node)))


    def _row_matches_where(self, row, where_node: ArgumentNode, variables):
        """
        Recursively checks if a row matches the where clause AST.
        Supports _and, _or, _not, and flat field checks.
        """
        where_value: ObjectValueNode
        if isinstance(where_node, ObjectValueNode):
            where_value = where_node
        elif isinstance(where_node.value, ObjectValueNode):
            try:
                where_value = where_node.value
            except AttributeError:
                where_value = where_node.fields[0]

        for field in where_value.fields:
            key = field.name.value
            value = field.value

            if key == "_and":
                if not isinstance(value, ListValueNode):
                    raise ValueError("_and must be a list")
                if not all(self._row_matches_where(row, v, variables) for v in value.values):
                    return False
            elif key == "_or":
                if isinstance(value, ListValueNode):
                    if not any(self._row_matches_where(row, v, variables) for v in value.values): # type: ignore
                        return False
                elif isinstance(value, VariableNode):
                    or_values = variables.get(value.name.value, [])
                    if not isinstance(or_values, list):
                        raise ValueError(f"Expected list for '_or' variable '{value.name.value}', got {type(or_values)}.")
                    if not self._row_matches_where_bool_exp(row, or_values):
                        return False
            elif key == "_not":
                if self._row_matches_where(row, value, variables):
                    return False
            else:
                # Flat field check
                if key not in row:
                    raise ValueError(f"Field '{key}' not present in row (possible reference or invalid field).")
                if not isinstance(value, ObjectValueNode):
                    raise ValueError(f"Non-flat where clause for field '{key}'.")
                for op_field in value.fields:
                    op = op_field.name.value
                    op_val = self._get_value_from_node(op_field.value, variables)
                    if op == "_eq":
                        if row.get(key) != op_val:
                            return False
                    elif op == "_in":
                        if row.get(key) not in op_val:
                            return False
                    elif op == "_is_null":
                        if op_val and row.get(key) is not None:
                            return False
                        elif not op_val and row.get(key) is None:
                            return False
                    elif op == "_neq":
                        if row.get(key) == op_val:
                            return False
                    else:
                        raise ValueError(f"Unsupported operator '{op}' in where clause.")
        return True

    def _row_matches_where_bool_exp(self, row, or_values):
        """
        Checks if a row matches any of the boolean expressions in or_values.
        """
        for v in or_values: # v = {'_and': [{'field1: {'_eq': 'value1'}}, {'field2': {'_eq': 'value2'}}]}
            fields = v['_and'] # [{'field1': {'_eq': 'value1'}}, {'field2': {'_eq': 'value2'}}]
            for field in fields: # {'field1': {'_eq': 'value1'}}
                field_name, value = next(iter(field.items())) # name = 'field1', value = {'_eq': 'value1'}
                key, value = next(iter(value.items())) # key = '_eq', value = 'value1'
                if key == "_eq":
                    if row.get(field_name) != value:
                        return False
                else:
                    raise ValueError(f"Unsupported operator '{key}' in where clause.")
        return True
                    




    def _update_row(self, row, set_node: ArgumentNode, variables):
        """
        Updates a row based on the _set argument node.
        """
        if not isinstance(set_node.value, ObjectValueNode):
            raise ValueError("Expected ObjectValueNode for _set clause value.")
        set_value: ObjectValueNode = set_node.value

        for field in set_value.fields:
            key = field.name.value
            value = self._get_value_from_node(field.value, variables)
            if key not in row:
                raise ValueError(f"Field '{key}' not present in row (invalid field).")
            # Update the row with the new value
            row[key] = value
    
    def _get_returning_fields(self, sel):
        """
        Extracts the returning fields mapping from the selection set.
        Returns a dict: {requested_field: actual_field}
        """
        for subsel in getattr(sel, "selection_set", []).selections if hasattr(sel, "selection_set") and sel.selection_set else []:
            if subsel.name.value == "returning":
                # If aliasing is used, subsel.alias.value is the requested field, subsel.name.value is the actual field
                return {
                    (f.alias.value if f.alias else f.name.value): f.name.value
                    for f in getattr(subsel, "selection_set", []).selections if hasattr(subsel, "selection_set") and subsel.selection_set
                }
        return None  # None means return full row

    def _map_returning_fields(self, row, returning_fields):
        """
        Maps the row dict to the requested returning fields.
        """
        if not returning_fields:
            return row
        return {req: row.get(actual) for req, actual in returning_fields.items()}

    def _handle_query(self, op_def, variables):
        result = {"data": {}}
        for sel in op_def.selection_set.selections:
            field = sel.name.value
            table = field
            rows = list(self.tables.get(table, {}).values())
            result["data"][field] = rows
        return result

    def build_config_from_db(self, import_state, mgm_uid, gateways):
        """
        Builds a configuration object from the in-memory tables.
        This is a mock implementation that simulates fetching data from a database.
        """
        config = MockFwConfigNormalizedBuilder.empty_config()

        config.gateways = gateways
        import_id = import_state.ImportId

        def obj_dict_from_row(row):
            if row['obj_create'] > import_id or row.get('removed') and row['removed'] <= import_id:
                return None
            dict = {}
            for key, value in row.items():
                if key == 'obj_id':
                    continue
                if key == 'obj_color_id':
                    dict['obj_color'] = import_state.lookupColorStr(value)
                elif key == 'obj_typ_id':
                    dict['obj_typ'] = self.tables['stm_obj_typ'].get(value, {}).get('obj_typ_name', 'unknown')
                else:
                    dict[key] = value
            return dict
        def service_dict_from_row(row):
            if row['svc_create'] > import_id or row.get('removed') and row['removed'] <= import_id:
                return None
            dict = {}
            for key, value in row.items():
                if key == 'svc_id':
                    continue
                if key == 'svc_color_id':
                    dict['svc_color'] = import_state.lookupColorStr(value)
                elif key == 'svc_typ_id':
                    dict['svc_typ'] = self.tables['stm_svc_typ'].get(value, {}).get('svc_typ_name', 'unknown')
                elif key == 'ip_proto_id':
                    dict['ip_proto'] = row['ip_proto_id']
                else:
                    dict[key] = value
            return dict
        def user_dict_from_row(row):
            if row['user_create'] > import_id or row.get('removed') and row['removed'] <= import_id:
                return None
            dict = {}
            for key, value in row.items():
                if key in ['user_id', 'mgm_id', 'user_create', 'user_last_seen', 'active', 'removed']:
                    continue
                elif key == 'usr_typ_id':
                    dict['user_typ'] = self.tables['stm_usr_typ'].get(value, {}).get('usr_typ_name', 'unknown')
                elif value is None:
                    continue
                else:
                    dict[key] = value
            return dict
        def rulebase_dict_from_row(row):
            if row.get('created') and row['created'] > import_id or row.get('removed') and row['removed'] <= import_id:
                return None
            dict = {}
            for key, value in row.items():
                if key == 'id':
                    continue
                if key == 'mgm_id':
                    dict['mgm_uid'] = mgm_uid
                dict[key] = value
            dict['rules'] = {}
            return dict
        def rule_dict_from_row(row):
            if row['rule_create'] > import_id or row.get('removed') and row['removed'] <= import_id:
                return None
            dict = {}
            for key, value in row.items():
                if key == 'rule_id':
                    continue
                if key == 'mgm_id':
                    dict['mgm_uid'] = mgm_uid
                dict[key] = value
            return dict
        for table_name, rows in self.tables.items():
            for row in rows.values():
                if row.get('active', True) is False:
                    continue
                if table_name == "object":
                    # create new dict without the primary key
                    obj = obj_dict_from_row(row)
                    if obj:
                        config.network_objects[row['obj_uid']] = NetworkObject.parse_obj(obj)
                elif table_name == "service":
                    # create new dict without the primary key
                    svc = service_dict_from_row(row)
                    if svc:
                        config.service_objects[row['svc_uid']] = ServiceObject.parse_obj(svc)
                elif table_name == "usr":
                    # create new dict without the primary key
                    user = user_dict_from_row(row)
                    if user:
                        config.users[row['user_uid']] = user
                elif table_name == "rulebase":
                    # create new dict without the primary key
                    rulebase = rulebase_dict_from_row(row)
                    if rulebase:
                        config.rulebases.append(Rulebase.parse_obj(rulebase))
                elif table_name == "rule":
                    # create new dict without the primary key
                    rule = rule_dict_from_row(row)
                    # find the rulebase by uid
                    rulebase = next((rb for rb in config.rulebases if next((rb_db["uid"] for rb_db in self.tables.get("rulebase", {}).values() if rb_db["id"] == row['rulebase_id']), None) == rb.uid), None)
                    if rulebase and rule:
                        rulebase.Rules[row['rule_uid']] = RuleNormalized.parse_obj(rule)

        return config

    def get_table(self, table_name):
        """
        Returns the table data for the given table name.
        """
        return self.tables.get(table_name, {})

    def get_nwobj_uid(self, obj_id):
        """
        Returns the UID of a network object by its ID.
        """
        nwobj_uid = self.tables.get("object", {}).get(obj_id, {}).get("obj_uid", None)
        if nwobj_uid is None:
            raise Exception(f"Network object ID {obj_id} not found in database.")
        return nwobj_uid
    
    def get_svc_uid(self, svc_id):
        """
        Returns the UID of a service object by its ID.
        """
        svc_uid = self.tables.get("service", {}).get(svc_id, {}).get("svc_uid", None)
        if svc_uid is None:
            raise Exception(f"Service ID {svc_id} not found in database.")
        return svc_uid
    
    def get_user_uid(self, user_id):
        """
        Returns the UID of a user by its ID.
        """
        user_uid = self.tables.get("usr", {}).get(user_id, {}).get("user_uid", None)
        if user_uid is None:
            raise Exception(f"User ID {user_id} not found in database.")
        return user_uid
    
    def get_rule_uid(self, rule_id):
        """
        Returns the UID of a rule by its ID.
        """
        rule_uid = self.tables.get("rule", {}).get(rule_id, {}).get("rule_uid", None)
        if rule_uid is None:
            raise Exception(f"Rule ID {rule_id} not found in database.")
        return rule_uid

    def get_nwobj_member_mappings(self):
        member_uids_db = {}
        for objgrp in self.get_table("objgrp").values():
            if not objgrp.get("active", True):
                continue
            objgrp_id = objgrp["objgrp_id"]
            uid = self.get_nwobj_uid(objgrp_id)
            if uid not in member_uids_db:
                member_uids_db[uid] = set()
            member_id = objgrp["objgrp_member_id"]
            member_uid_db = self.get_nwobj_uid(member_id)
            member_uids_db[uid].add(member_uid_db)
        return member_uids_db

    def get_svc_member_mappings(self):
        member_uids_db = {}
        for svcgrp in self.get_table("svcgrp").values():
            if not svcgrp.get("active", True):
                continue
            svcgrp_id = svcgrp["svcgrp_id"]
            uid = self.get_svc_uid(svcgrp_id)
            if uid not in member_uids_db:
                member_uids_db[uid] = set()
            member_id = svcgrp["svcgrp_member_id"]
            member_uid_db = self.get_svc_uid(member_id)
            member_uids_db[uid].add(member_uid_db)
        return member_uids_db
    
    def get_user_member_mappings(self):
        member_uids_db = {}
        for usergrp in self.get_table("usergrp").values():
            if not usergrp.get("active", True):
                continue
            usergrp_id = usergrp["usergrp_id"]
            uid = self.get_user_uid(usergrp_id)
            if uid not in member_uids_db:
                member_uids_db[uid] = set()
            member_id = usergrp["usergrp_member_id"]
            member_uid_db = self.get_user_uid(member_id)
            member_uids_db[uid].add(member_uid_db)
        return member_uids_db
    
    def get_nwobj_flat_member_mappings(self):
        flat_member_uids_db = {}
        for objgrp_flat in self.get_table("objgrp_flat").values():
            if not objgrp_flat.get("active", True):
                continue
            objgrp_flat_id = objgrp_flat["objgrp_flat_id"]
            uid = self.get_nwobj_uid(objgrp_flat_id)
            if uid not in flat_member_uids_db:
                flat_member_uids_db[uid] = set()
            member_id = objgrp_flat["objgrp_flat_member_id"]
            member_uid_db = self.get_nwobj_uid(member_id)
            flat_member_uids_db[uid].add(member_uid_db)

        return flat_member_uids_db
    
    def get_svc_flat_member_mappings(self):
        flat_member_uids_db = {}
        for svcgrp_flat in self.get_table("svcgrp_flat").values():
            if not svcgrp_flat.get("active", True):
                continue
            svcgrp_flat_id = svcgrp_flat["svcgrp_flat_id"]
            uid = self.get_svc_uid(svcgrp_flat_id)
            if uid not in flat_member_uids_db:
                flat_member_uids_db[uid] = set()
            member_id = svcgrp_flat["svcgrp_flat_member_id"]
            member_uid_db = self.get_svc_uid(member_id)
            flat_member_uids_db[uid].add(member_uid_db)

        return flat_member_uids_db
    
    def get_user_flat_member_mappings(self):
        flat_member_uids_db = {}
        for usergrp_flat in self.get_table("usergrp_flat").values():
            if not usergrp_flat.get("active", True):
                continue
            usergrp_flat_id = usergrp_flat["usergrp_flat_id"]
            uid = self.get_user_uid(usergrp_flat_id)
            if uid not in flat_member_uids_db:
                flat_member_uids_db[uid] = set()
            member_id = usergrp_flat["usergrp_flat_member_id"]
            member_uid_db = self.get_user_uid(member_id)
            flat_member_uids_db[uid].add(member_uid_db)

        return flat_member_uids_db
    
    def get_rule_from_mappings(self):
        rule_froms_db = {}
        for rule_from in self.get_table("rule_from").values():
            if not rule_from.get("active", True):
                continue
            rule_id = rule_from["rule_id"]
            uid = self.get_rule_uid(rule_id)
            if uid not in rule_froms_db:
                rule_froms_db[uid] = set()
            obj_id = rule_from["obj_id"]
            obj_uid = self.get_nwobj_uid(obj_id)
            member_string_db = obj_uid
            user_id = rule_from["user_id"]
            if user_id is not None:
                user_uid = self.get_user_uid(user_id)
                member_string_db += fwo_const.user_delimiter + user_uid
            
            rule_froms_db[uid].add(member_string_db)
        return rule_froms_db
    
    def get_rule_to_mappings(self):
        rule_tos_db = {}
        for rule_to in self.get_table("rule_to").values():
            if not rule_to.get("active", True):
                continue
            rule_id = rule_to["rule_id"]
            uid = self.get_rule_uid(rule_id)
            if uid not in rule_tos_db:
                rule_tos_db[uid] = set()
            obj_id = rule_to["obj_id"]
            obj_uid = self.get_nwobj_uid(obj_id)
            member_string_db = obj_uid
            user_id = rule_to["user_id"]
            if user_id is not None:
                user_uid = self.get_user_uid(user_id)
                member_string_db += fwo_const.user_delimiter + user_uid
            
            rule_tos_db[uid].add(member_string_db)
        return rule_tos_db
    
    def get_rule_svc_mappings(self):
        rule_svcs_db = {}
        for rule_svc in self.get_table("rule_service").values():
            if not rule_svc.get("active", True):
                continue
            rule_id = rule_svc["rule_id"]
            uid = self.get_rule_uid(rule_id)
            if uid not in rule_svcs_db:
                rule_svcs_db[uid] = set()
            svc_id = rule_svc["svc_id"]
            svc_uid = self.get_svc_uid(svc_id)
            rule_svcs_db[uid].add(svc_uid)
        return rule_svcs_db
    
    def get_rule_nwobj_resolved_mappings(self):
        rule_nwobj_resolved_db = {}
        for rule_nwobj in self.get_table("rule_nwobj_resolved").values():
            if not rule_nwobj.get("active", True):
                continue
            rule_id = rule_nwobj["rule_id"]
            uid = self.get_rule_uid(rule_id)
            if uid not in rule_nwobj_resolved_db:
                rule_nwobj_resolved_db[uid] = set()
            obj_id = rule_nwobj["obj_id"]
            obj_uid = self.get_nwobj_uid(obj_id)
            rule_nwobj_resolved_db[uid].add(obj_uid)
        return rule_nwobj_resolved_db
    
    def get_rule_svc_resolved_mappings(self):
        rule_svc_resolved_db = {}
        for rule_svc_resolved in self.get_table("rule_svc_resolved").values():
            if not rule_svc_resolved.get("active", True):
                continue
            rule_id = rule_svc_resolved["rule_id"]
            uid = self.get_rule_uid(rule_id)
            if uid not in rule_svc_resolved_db:
                rule_svc_resolved_db[uid] = set()
            svc_id = rule_svc_resolved["svc_id"]
            svc_uid = self.get_svc_uid(svc_id)
            rule_svc_resolved_db[uid].add(svc_uid)
        return rule_svc_resolved_db
    
    def store_latest_config(self, config, import_state):
        """
        Stores the latest configuration in the mock database.
        """
        if not "latest_config" in self.tables:
            self.tables["latest_config"] = {}
        import_id = import_state.ImportId
        mgm_id = import_state.MgmDetails.Id
        self.tables["latest_config"][import_id] = {
            "import_id": import_id,
            "mgm_id": mgm_id,
            "config": config.json(),
        }