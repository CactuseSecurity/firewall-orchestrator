# Importing firewall configs via API

Use a tool like insomnia for testing.

Use the API to write the whole firewall config as JSON into import_config). The format is described here.
Currently CPR8x can be importer both via PERL and python importer without changing source code.

Python Import in roles/importer/files/importer/import_mgm.py:
```python
    error_count = fwo_api.import_json_config(fwo_api_base_url, jwt, args.mgm_id, { "importId": current_import_id, "mgmId": args.mgm_id, "config": config2import })
```

## JSON structure overview

### Config in table import_config
```json
{
    "import_id": 1,
    "mgm_id": 12,
    "config": config2import
}
```
### config2import
```json
{
    "network_objects": [...],
    "network_services": [...],
    "users": [...],
    "network_zones": [...],
    "rules": [...],
    "nat_rules": [...]
}
```

## Explanations
- In the following, all fields that are null, are optional.
- Note that you need some of them depending on the object type (e.g. group needs members).
- The control_id needs to be present in every single object for technical postgresql reasons.
- group delimiter is the pipe ("|")
- _refs fields contain a string with the uids of the referenced objects separated by "|"
- the order of fields is arbitrary
- _color can be any one listed in roles/database/files/csv/color.csv
- action and track types can be found (and enhanced) in roles/database/files/sql/creation/fworch-fill-stm.sql

## network_objects

```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "obj_ip": "1.5.1.1",                                    // string: ipv4 or v6 address (in CIDR format)
    "obj_typ": "host",                                      // string: see types below
    "obj_uid": "8a5bc8fb-8a10-4fd2-b4b7-825ee57bd6bd",      // string: with unique network object id
    "obj_name": "host112",                                  // string: name of network object
    "obj_color": null,                                      // string: color of object, see Explanations
    "obj_ip_end": "1.5.1.1",                                // string: last ipv4 or v6 address (for ranges)
    "obj_comment": null,                                    // string: comment
    "obj_member_names": null,                               // string: names of the group members separated by "|"
    "obj_member_refs": null,                                // string: uids of the referenced objects separated by "|"
    "obj_zone": null                                        // string: name of the object's zone (e.g. for fortinet)
}
```
- obj_typ can be any of the following (see roles/database/files/sql/creation/fworch-fill-stm.sql): 
  network, group, host, machines_range, dynamic_net_obj, sofaware_profiles_security_level, gateway, cluster_member, gateway_cluster, domain, group_with_exclusion, ip_range, uas_collection, sofaware_gateway, voip_gk, gsn_handover_group, voip_sip, simple-gateway


## network_services
```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "svc_name": "AOL",                                      // string: name of the service
    "svc_typ": "simple",                                    // string: type of service (see below)
    "svc_uid": "97aeb44f-9aea-11d5-bd16-0090272ccb30",      // string: unique service id
    "ip_proto": "6",                                        // integer: 0-255 procol number
    "svc_port": "5190",                                     // string: 1-65535 (or range)
    "svc_timeout": null,                                    // integer: idle timeout in seconds
    "svc_port_end": null,                                   // string: to be replaced by range in svc_port
    "svc_source_port": null,                                // string: optional source port restrictions
    "svc_source_port_end": null,                            // string: to be replaced by range in svc_source_port
    "svc_color": null,                                      // string: color of object, see Explanations
    "svc_comment": null,                                    // string: comment
    "svc_member_names": null,                               // string: names of the group members separated by "|"
    "svc_member_refs": null,                                // string: uids of the referenced service objects separated by "|"
    "rpc_nr": null                                          // string: for rpc service the rpc id
},
```
- svc_type can be any of the following: simple, group, rpc (see roles/database/files/sql/creation/fworch-fill-stm.sql)

## users

```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "user_typ": "simple",                                   // string: either "group" or "simple"
    "user_uid": "c4d28191-bd44-4d45-8887-df94f594a8ef",     // string: unique user id
    "user_name": "IA_User1",                                // string: user name
    "user_member_names": null,                              // string: names of the group members separated by "|"
    "user_member_refs": null,                               // string: uids of the referenced users separated by "|"
    "user_color": null,                                     // string: color of object, see Explanations
    "user_comment": null,                                   // string: comment
    "user_valid_until": null                                // string: user's "sell-by" date
}
```

## zones

```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "zone_name": "zone1"                                    // string: name of the zone
}
```

## rules

```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "rulebase_name": "FirstLayer shared with inline layer", // string: specifies the rulebase name (all rules are contained in a single json struct)
    "rule_num": 1,                                          // integer: rule number for ordering
    "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",     // string: unique rule id
    "rule_src": "user1@obj1|obj2",                          // string: list of source object names (if it contains user, use "@" as delimiter)
    "rule_dst": "Any",                                      // string: list of destination network object names
    "rule_svc": "Any",                                      // string: list of service names
    "rule_track": "None",                                   // string: logging options, see below
    "rule_action": "Inner Layer",                           // string: rule action options, see below
    "rule_implied": false,                                  // boolean: is it an implied (check point) rule derived from settings
    "rule_src_neg": false,                                  // boolean: is the source field negated
    "rule_dst_neg": false,                                  // boolean: is the destination field negated
    "rule_svc_neg": false,                                  // boolean: is the service field negated
    "rule_disabled": false,                                 // boolean: is the whole rule disabled
    "rule_src_refs": "9aa8e391-f811-4067-86f3-82646bd47d47@9aa8e391-f811-4067-86f3-82646bd47d40|2aa8e391-f811-4067-86f3-82646bd47d41", // string: source references
    "rule_dst_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",// string: destination references
    "rule_svc_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",// string: service references
    "rule_time": null,                                      // string: any time restrictions of the rule
    "rule_installon": null,                                 // string: list of gateways this rule should be applied to
    "parent_rule_uid": null,                                // string: for layers, the uid of the rule of layer above
    "rule_ruleid": null,                                    // string: rule id (unique within gateway, but not globally)
    "rule_name": null,                                      // string: optional name of the rule
    "rule_comment": null,                                   // string: optional rule comment
    "rule_head_text": null,                                 // string: for section headers this is the field to use
    "rule_from_zone": null,                                 // string: source zone (if applicable) of the rule
    "rule_to_zone": null                                    // string: destination zone (if applicable) of the rule
}
```
- rule_track can be any of log, none, alert, userdefined, mail, account, userdefined 1, userdefined 2, userdefined 3, snmptrap, log count, count, log alert, log alert count, log alert count alarm, log count alarm, count alarm, all, all start, utm, utm start, network log
- rule_action can be any of accept, drop, deny, access, client encrypt, client auth, reject, encrypt, user auth, session auth, permit, permit webauth, redirect, map, permit auth, tunnel l2tp, tunnel vpn-group, tunnel vpn, actionlocalredirect, inner layer

## nat_rules

```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "rulebase_name": "global nat rules",                    // string: specifies the nat rulebase name (all nat rules are contained in a single json struct)
    "rule_num": 1,                                          // integer: nat rule number for ordering
    "rule_uid": "bcc044f6-2a4f-459b-b78c-9e7afee92621",     // string: unique rule id
    "rule_src_xlate": "ip_4.5.6.7,ip_4.5.6.1|ip_2,ip2_xlate",// string: pairs (comma-separated) of source translations
    "rule_src_xlate_refs": "97aeb369-9aea-11d5-bd16-0090272ccb35,97aeb369-9aea-11d5-bd16-0090272ccb34|97aeb369-9aea-11d5-bd16-0090272ccb33,97aeb369-9aea-11d5-bd16-0090272ccb32",// string: references of translation sources
    "rule_dst_xlate": "ip1.3.45.5,original",                // string: pairs (comma-separated) of destination translations
    "rule_dst_xlate_refs": "97aeb369-9aea-11d5-bd16-0090272ccb31,97aeb369-9aea-11d5-bd16-0090272ccb31",// string: pairs (comma-separated) of destination translation references
    "rule_svc_xlate": "tcp_1234,tcp_4711",                  // string: pairs (comma-separated) of service (port) translations
    "rule_svc_xlate_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30,97aeb369-9aea-11d5-bd16-0090272ccb32", // string: pairs (comma-separated) of destination translation references
    "rule_disabled": false,                                 // boolean: is nat rule disabled
    "rule_installon": null,                                 // string: list of gateways this nat rule should be applied to
    "rule_ruleid": null,                                    // string: id (unique within gateway, but not globally)
    "rule_name": null,                                      // string: optional name of the nat rule
    "rule_comment": null,                                   // string: optional nat rule comment
    "rule_head_text": null,                                 // string: for section headers this is the field to use
    "rule_from_zone": null,                                 // string: source zone (if applicable) of the nat rule
    "rule_to_zone": null                                    // string: destination zone (if applicable) of the nat rule
}
```
- "original" is the keyword for no translation - a special object needs to be created

## Envisioned Future Changes
- network_services.ip_proto should be integer instead of string
- network_objects.obj_ip_end can be null for single ip objects instead of repeating the same ip
- The following fields might be dropped in later versions:
  - network_objects.obj_sw
  - network_objects.obj_location
  - network_objects.obj_member_names - could be derived from obj_member_refs
  - network_objects.last_change_admin
  - network_objects.last_change_time
  - network_services.svc_prod_specific - seems to be just a redundant copy of svc_typ
  - network_services.last_change_admin
  - network_services.last_change_time
  - network_services.svc_port_end - range could be entered as string "112-123"
  - network_services.svc_source_port_end - range could be entered as string "112-123"
  - users.last_change_admin
  - rules.rule_last_change_admin
