
General:

```
- boolean fields (negated, disabled) can contain either 0/1 or true/false?
- directory:
- import arrays/fields are defined in CACTUS/ISO/import.pm:
  our @obj_import_fields
  our @svc_import_fields
  our @user_import_fields
  our @zone_import_fields
  our @rule_import_fields
  our @auditlog_import_fields
```

A) rule.csv:

```
name: <rulebase_name>_rulebase.csv
fields (total=25):
  control_id
  rule_num
  rulebase_name
  rule_ruleid
  rule_disabled
  rule_src_neg
  rule_src
  rule_src_refs
  rule_dst_neg
  rule_dst
  rule_dst_refs
  rule_svc_neg
  rule_svc
  rule_svc_refs
  rule_action
  rule_track
  rule_installon
  rule_time
  rule_comment
  rule_name
  rule_uid
  rule_head_text
  rule_from_zone
  rule_to_zone
  last_change_admin
```

B) network_objects.csv

```
name: <mgmt_name>_netzobjekte.csv
fields (total=15):
  control_id
  obj_name
  obj_typ
  obj_member_names
  obj_member_refs
  obj_sw
  obj_ip
  obj_ip_end
  obj_color
  obj_comment
  obj_location
  obj_zone
  obj_uid
  last_change_admin
  last_change_time
```

C) services

```
name: <mgmt_name>_services.csv
fields (total=19):
  control_id
  svc_name
  svc_typ
  svc_prod_specific      --> note: this seems to be just a redundant copy of svc_typ!
  svc_member_names
  svc_member_refs
  svc_color
  ip_proto
  svc_port
  svc_port_end
  svc_source_port
  svc_source_port_end
  svc_comment
  rpc_nr
  svc_timeout_std
  svc_timeout
  svc_uid
  last_change_admin
  last_change_time
```

D) users.csv

```
name: <mgmt_name>_users.csv
fields (total=10):
  control_id
  user_name
  user_typ
  user_member_names
  user_member_refs
  user_color
  user_comment
  user_uid
  user_valid_until
  last_change_admin
```

E) zones.csv

```
name: <mgmt_name>_zones.csv
fields (total=2):
  control_id
  zone_name
```
