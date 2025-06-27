from graphql import ArgumentNode, BooleanValueNode, IntValueNode, ListValueNode, ObjectValueNode, OperationDefinitionNode, StringValueNode, VariableNode, parse, OperationType
from importer.fwo_api_oo import FwoApi
from importer.models.networkobject import NetworkObject
from importer.models.rulebase import Rulebase
from importer.models.serviceobject import ServiceObject
from importer.models.rule import RuleNormalized
from .mock_config import MockFwConfigNormalizedBuilder

TABLE_IDENTIFIERS = {
    "stm_change_type": "change_type_id",
    "stm_usr_typ": "usr_typ_id",
    "stm_svc_typ": "svc_typ_id",
    "stm_obj_typ": "obj_typ_id",
    "stm_action": "action_id",
    "stm_track": "track_id",
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
    ]
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
                arg_name = next((a.value.name.value for a in sel.arguments if a.name.value == "objects"), None)
                objects = variables.get(arg_name, [])
                if table not in self.tables:
                    self.tables[table] = {}
                returning = []
                for obj in objects:
                    pk = len(self.tables[table]) + 1
                    obj = dict(obj)  # copy
                    if table not in ["objgrp", "svcgrp", "usergrp"]: # using pair of reference ids as primary key
                        obj[TABLE_IDENTIFIERS.get(table, f"{table}_id")] = pk
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
        if not isinstance(where_node.value, ObjectValueNode):
            raise ValueError("Expected ObjectValueNode for where clause value.")
        where_value: ObjectValueNode = where_node.value

        for field in where_value.fields:
            key = field.name.value
            value = field.value

            if key == "_and":
                if not isinstance(value, ListValueNode):
                    raise ValueError("_and must be a list")
                if not all(self._row_matches_where(row, v, variables) for v in value.values):
                    return False
            elif key == "_or":
                if not isinstance(value, ListValueNode):
                    raise ValueError("_or must be a list")
                if not any(self._row_matches_where(row, v, variables) for v in value.values):
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
                    else:
                        raise ValueError(f"Unsupported operator '{op}' in where clause.")
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

        def obj_dict_from_row(row):
            dict = {}
            for key, value in row.items():
                if key == 'obj_id':
                    continue
                if key == 'obj_color_id':
                    dict['obj_color'] = import_state.lookupColorStr(value)
                elif key == 'obj_typ_id':
                    dict['obj_typ'] = self.tables['stm_obj_typ'].get(value, {}).get('obj_typ_name', 'unknown')
                dict[key] = value
            return dict
        def service_dict_from_row(row):
            dict = {}
            for key, value in row.items():
                if key == 'svc_id':
                    continue
                if key == 'svc_color_id':
                    dict['svc_color'] = import_state.lookupColorStr(value)
                elif key == 'svc_typ_id':
                    dict['svc_typ'] = self.tables['stm_svc_typ'].get(value, {}).get('svc_typ_name', 'unknown')
                dict[key] = value
            return dict
        def user_dict_from_row(row):
            dict = {}
            for key, value in row.items():
                if key == 'user_id':
                    continue
                dict[key] = value
            return dict
        def rulebase_dict_from_row(row):
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
            dict = {}
            for key, value in row.items():
                if key == 'rule_id':
                    continue
                if key == 'mgm_id':
                    dict['mgm_uid'] = mgm_uid
                dict[key] = value
            return dict
        for table_name, rows in self.tables.items():
            if table_name == "object":
                for row in rows.values():
                    # create new dict without the primary key
                    obj = NetworkObject.parse_obj(obj_dict_from_row(row))
                    config.network_objects[row['obj_uid']] = obj
            elif table_name == "service":
                for row in rows.values():
                    # create new dict without the primary key
                    svc = ServiceObject.parse_obj(service_dict_from_row(row))
                    config.service_objects[row['svc_uid']] = svc
            elif table_name == "usr":
                for row in rows.values():
                    # create new dict without the primary key
                    user = user_dict_from_row(row)
                    config.users[row['user_uid']] = user
            elif table_name == "rulebase":
                for row in rows.values():
                    # create new dict without the primary key
                    rulebase = rulebase_dict_from_row(row)
                    config.rulebases.append(Rulebase.parse_obj(rulebase))
            elif table_name == "rule":
                for row in rows.values():
                    # create new dict without the primary key
                    rule = rule_dict_from_row(row)
                    # find the rulebase by uid
                    rulebase = next((rb for rb in config.rulebases if import_state.RulebaseMap[rb.uid] == row['rulebase_id']), None)
                    if rulebase:
                        rulebase.Rules[row['rule_uid']] = RuleNormalized.parse_obj(rule)

        return config

    def get_table(self, table_name):
        """
        Returns the table data for the given table name.
        """
        return self.tables.get(table_name, {})
