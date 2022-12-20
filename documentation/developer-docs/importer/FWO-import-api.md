# Importing firewall configs via API

## JSON structure overview

### Config in table import_config
```json
{
    "import_id": 1,
    "mgm_id": 12,
    "config": "config2import"
}
```
### config2import
```json
{
    "network_objects": [...],
    "service_objects": [...],
    "user_objects": [...],
    "zone_objects": [...],
    "rules": [...]
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

here we describe a single network object:
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


## service_objects
here we describe a single service object:

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

## user_objects

here we describe a single user object:

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

## zone_objects

here we describe a single zone object:

```json
{
    "control_id": 1,                                        // bigint: ID of the current import
    "zone_name": "zone1"                                    // string: name of the zone
}
```

## rules

here we describe a single rule:

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
    "rule_to_zone": null,                                   // string: destination zone (if applicable) of the rule
    "rule_type": "access"                                   // string: type of the nat rule: "access|combined|original|xlate", default "access"
}
```
- rule_track can be any of log, none, alert, userdefined, mail, account, userdefined 1, userdefined 2, userdefined 3, snmptrap, log count, count, log alert, log alert count, log alert count alarm, log count alarm, count alarm, all, all start, utm, utm start, network log
- rule_action can be any of accept, drop, deny, access, client encrypt, client auth, reject, encrypt, user auth, session auth, permit, permit webauth, redirect, map, permit auth, tunnel l2tp, tunnel vpn-group, tunnel vpn, actionlocalredirect, inner layer

## Putting it all together

This is a complete example of a config which may be imported:

```json
{
  "rules": [
    {
      "rulebase_name": "FirstLayer shared with inline layer",
      "control_id": 1074,
      "rule_num": 0,
      "rule_uid": "828b0f42-4b18-4352-8bdf-c9c864d692eb",
      "rule_name": null,
      "rule_comment": "test comment",
      "rule_src": "test-ext-vpn-gw|test-interop-device|BeeW10|wsus",
      "rule_dst": "sting-gw",
      "rule_svc": "IPSEC",
      "rule_time": "Any",
      "rule_from_zone": null,
      "rule_to_zone": null,
      "rule_track": "Log",
      "rule_action": "Drop",
      "rule_implied": false,
      "rule_src_neg": false,
      "rule_dst_neg": false,
      "rule_svc_neg": false,
      "rule_disabled": true,
      "rule_src_refs": "a580c5a3-379c-479b-b49d-487faba2442e|98bc04fc-b88b-4283-83ad-7b6899bc1876|2ad18398-e004-4324-af79-634be66941d6|2661ec9f-293f-4c82-8150-4bb6c883ca79",
      "rule_dst_refs": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
      "rule_svc_refs": "97aeb475-9aea-11d5-bd16-0090272ccb30",
      "rule_installon": "Policy Targets",
      "parent_rule_uid": null,
      "rule_ruleid": null,
      "rule_type": "access",
      "rule_last_change_admin": null
    }
  ],
  "user_objects": [
    {
      "user_typ": "simple",
      "user_uid": "aae47c39-f416-4b32-801d-af53adfa1939",
      "user_name": "test-user1",
      "control_id": 1074,
      "user_color": "black",
      "user_comment": ""
    },
    {
      "user_typ": "group",
      "user_uid": "227d1a80-cc1e-4cd4-9576-4d46f271402f",
      "user_name": "test-group",
      "control_id": 1074,
      "user_color": "black",
      "user_comment": ""
    }
  ],
  "network_objects": [
    {
      "obj_ip": "22.55.200.192/26",
      "obj_typ": "network",
      "obj_uid": "5368caf0-d192-457b-9c86-5d5f9e5dc199",
      "obj_name": "Net_22.55.200.192-2",
      "obj_color": "black",
      "control_id": 1074,
      "obj_ip_end": "22.55.200.192/26",
      "obj_comment": null,
      "obj_member_refs": null,
      "obj_member_names": null
    }
  ],
  "service_objects": [
    {
      "rpc_nr": null,
      "svc_typ": "simple",
      "svc_uid": "97aeb44f-9aea-11d5-bd16-0090272ccb30",
      "ip_proto": "6",
      "svc_name": "AOL",
      "svc_port": "5190",
      "svc_color": "red",
      "control_id": 1074,
      "svc_comment": "AOL Instant Messenger. Also used by: ICQ & Apple iChat",
      "svc_timeout": "3600",
      "svc_port_end": "5190",
      "svc_member_refs": null,
      "svc_member_names": null
    }
  ],
  "zone_objects": [
    {
      "zone_name": "test-zone",
      "zone_uid": "98aeb44f-9aea-11d5-bd16-0090272ccb30",
      "control_id": 1074,
      "zone_comment": "just a test"
    }
  ]}
```

The following shows an example of how to import nat rules that are combined access/nat rules, here only translating destination (not showing irrelevant fields for brevity's sake):

```json
{
  "rules": [
    {
      "rule_num": 0,
      "rule_uid": "828b0f42-4b18-4352-8bdf-c9c864d692eb",
      "rule_src": "test-ext-vpn-gw|test-interop-device|BeeW10|wsus",
      "rule_dst": "sting-gw",
      "rule_svc": "IPSEC",
      "rule_src_refs": "a580c5a3-379c-479b-b49d-487faba2442e|98bc04fc-b88b-4283-83ad-7b6899bc1876|2ad18398-e004-4324-af79-634be66941d6|2661ec9f-293f-4c82-8150-4bb6c883ca79",
      "rule_dst_refs": "cbdd1e35-b6e9-4ead-b13f-fd6389e34987",
      "rule_svc_refs": "97aeb475-9aea-11d5-bd16-0090272ccb30",
      "rule_type": "combined",    // this rule is both an access and a nat rule
      "xlate_rule: "123abcdef-4b18-4352-8bdf-c9c864d692eb"
    },
    {
      "rule_num": 1,
      "rule_uid": "123abcdef-4b18-4352-8bdf-c9c864d692eb",
      "rule_src": "test-ext-vpn-gw|test-interop-device|BeeW10|wsus",
      "rule_dst": "sting-gw_xlate",
      "rule_svc": "IPSEC",
      "rule_src_refs": "a580c5a3-379c-479b-b49d-487faba2442e|98bc04fc-b88b-4283-83ad-7b6899bc1876|2ad18398-e004-4324-af79-634be66941d6|2661ec9f-293f-4c82-8150-4bb6c883ca79",
      "rule_dst_refs": "123d1e35-b6e9-4ead-b13f-fd6389e34987",
      "rule_svc_refs": "97aeb475-9aea-11d5-bd16-0090272ccb30",
      "rule_type": "xlate",
    }

  ],
  ```


## NAT Rules

here we describe the representation of the rule in the rule table

- "original" is the keyword for no translation - a special object needs to be created for this
- algorithm for importing NAT rules: 
  - a NAT rule is stored as two rules, for both of which nat_rule is set to "true"
  - the first rule contains the packet match information (original packet) and a pointer (in xlate_rule) to the translation rule
  - the translation rule has access_rule==false and xlate_rule==null set and defines how the packet is to be translated
  - for check point the NAT rules are pure NAT rules, meaning that access_rule is false
  - for other systems a rule can both be an access and an access rule
- rule_action for the xlate_rule can be any of the following (new) values: hide, hide_pool, static_src, static_dst, static_src_and_dst

- all existing reporting needs to be restricted to access rules (exception receritfication)
- recertification should be possible for both NAT and access rules (should be configurable both per tenant and globally)

#### final db rule tables

The following gives an overview of the nat rule presentation as read via FWO API:

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
    "rule_to_zone": null,                                   // string: destination zone (if applicable) of the rule
    "access_rule": true,
    "nat_rule": true,
    "xlate_packet":  {
        "source": "ip_123.1.0.1",
        "source_refs": "76aeb369-9aea-11d5-bd16-0090272ccb33",
        "destination": "original",
        "destination_refs": "original",
        "service": "original",
        "service_refs": "original"
    },
}
```

#### final db rule tables with self ref to xlate rule
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
    "rule_to_zone": null,                                   // string: destination zone (if applicable) of the rule
    "access_rule": true,                                    // string: is this an access rule
    "nat_rule": true,                                       // string: is this a NAT rule
    "xlate_rule": 1234                                      // string: for NAT rules this is the link to the xlate rule which contains the translated fileds (src, dst, svc)
}
```
- additional values for rule_action for xlate rules: hide, hide_pool, static_src, static_dst, static_src_and_dst

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
