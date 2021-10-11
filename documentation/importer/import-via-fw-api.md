# Importing firewall configs via API

Use a tool like insomnia for testing.


## object interface - (import_config)

Use the API to write the whole firewall config as JSON into import_config). The format is described here.
Currently CPR8x can be importer both via PERL and python importer without changing source code.

Overview:

```json
{
    "import_id": 1,
    "mgm_id": 12,
    "config": {
        "network_objects": [...],
        "network_services": [...],
        "users": [...],
        "network_zones": [...],
        "rules": [...],
        "nat_rules": [...]
    }
}
```

- In the following, all fields that are null, are optional.
- Note that you need some of them depending on the object type (e.g. group needs members).
- todo: change network_services.ip_proto from string to integer
- The following fields will be dropped in v5.5:
  - network_objects.obj_sw
  - network_objects.obj_location
  - network_objects.last_change_admin
  - network_objects.last_change_time
  - network_services.svc_prod_specific - seems to be just a redundant copy of svc_typ
  - network_services.last_change_admin
  - network_services.last_change_time
  - users.last_change_admin
  - rules.rule_last_change_admin


### network_objects

```json
{
    "control_id": 1,
    "obj_ip": "1.5.1.1",
    "obj_typ": "host",
    "obj_uid": "8a5bc8fb-8a10-4fd2-b4b7-825ee57bd6bd",
    "obj_name": "host112",
    "obj_color": null,
    "obj_ip_end": "1.5.1.1",
    "obj_comment": null,
    "obj_member_refs": null,
    "obj_member_names": null,
    "obj_zone": null
}
```

### network_services
```json
{
    "control_id": 1,
    "svc_name": "AOL",
    "svc_typ": "simple",
    "svc_uid": "97aeb44f-9aea-11d5-bd16-0090272ccb30",
    "ip_proto": "6",
    "svc_port": "5190",
    "svc_timeout": "3600",
    "svc_port_end": "5190",
    "svc_source_port": null,
    "svc_source_port_end": null,
    "svc_color": null,
    "svc_comment": null,
    "svc_member_refs": null,
    "svc_member_names": null,
    "rpc_nr": null
},
```

### users

```json
{
    "control_id": 1,
    "user_typ": "simple",
    "user_uid": "c4d28191-bd44-4d45-8887-df94f594a8ef",
    "user_name": "IA_User1",
    "user_member_names": null,
    "user_member_refs": null,
    "user_color": null,
    "user_comment": null,
    "user_valid_until": null
}
```

### zones

```json
{
    "control_id": 1,
    "zone_name": "zone1"
}
```

### rules

```json
{
    "control_id": 1,
    "rulebase_name": "FirstLayer shared with inline layer",
    "rule_num": 1,
    "rule_uid": "acc044f6-2a4f-459b-b78c-9e7afee92621",
    "rule_dst": "Any",
    "rule_src": "test-net-1.2.3.0_24",
    "rule_svc": "Any",
    "rule_track": "None",
    "rule_action": "Inner Layer",
    "rule_dst_neg": false,
    "rule_implied": false,
    "rule_src_neg": false,
    "rule_svc_neg": false,
    "rule_disabled": false,
    "rule_dst_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",
    "rule_src_refs": "9aa8e391-f811-4067-86f3-82646bd47d40",
    "rule_svc_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",
    "rule_time": null,
    "rule_installon": null,
    "parent_rule_uid": null, - for layers, the uid of the rule of layer above
    "rule_ruleid": null,    - string - id (unique within gateway, but not globally)
    "rule_name": null,
    "rule_comment": null,
    "rule_head_text": null,
    "rule_from_zone": null,
    "rule_to_zone": null
}
```

### nat_rules

```json
{
    "control_id": 1,
    "rulebase_name": "global nat rules",
    "rule_num": 1,
    "rule_uid": "bcc044f6-2a4f-459b-b78c-9e7afee92621",
    "rule_src": "Any",
    "rule_src_refs": "9aa8e391-f811-4067-86f3-82646bd47d40",
    "rule_src_neg": false,
    "rule_dst": "test-net-1.2.3.0_24",
    "rule_dst_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",
    "rule_dst_neg": false,
    "rule_svc": "Any",
    "rule_svc_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",
    "rule_svc_neg": false,
    "rule_src_xlate": "interface_ip_4.5.6.7",
    "rule_src_xlate_refs": "97aeb369-9aea-11d5-bd16-0090272ccb38",
    "rule_dst_xlate": "original",
    "rule_dst_xlate_refs": "97aeb369-9aea-11d5-bd16-0090272ccb31",
    "rule_svc_xlate": "Any",
    "rule_svc_xlate_refs": "97aeb369-9aea-11d5-bd16-0090272ccb30",
    "rule_action": "srcnat|dstnat|bothnat",
    "rule_implied": false,
    "rule_disabled": false,
    "rule_installon": null,
    "rule_ruleid": null,    - string - id (unique within gateway, but not globally)
    "rule_name": null,
    "rule_comment": null,
    "rule_head_text": null,
    "rule_from_zone": null,
    "rule_to_zone": null
}
```