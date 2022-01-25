# CSV interface of importer

## adjusting parser interface

All data is currently exchanged via a CSV interface.

### perl-based (parse-config file) import modules (old)
If a field needs to be added to the CSV interface, for all old perl-based parsers 
you need to add the name of the field to the respective _outlist in roles/importer/files/importer/CACTUS/FWORCH/import.pm, e.g.
 @rule_outlist	=(qw (	rule_id disabled src.op src src.refs dst.op dst dst.refs services.op services services.refs
   action track install time comments name UID header_text src.zone dst.zone last_change_admin parent_rule_uid));

### python-based (API access) import modules (new)

For python based importers, you need to adjust the roles/importer/files/importer/checkpointR8x/*_parser.py files, e.g. 
  csv_dump_svc_obj.py in parse_service.py

## General

```console
- boolean fields (negated, disabled) can contain either 0/1 or true/false?
- directory:
- import arrays/fields are defined in CACTUS/FWORCH/import.pm:
  our @obj_import_fields
  our @svc_import_fields
  our @user_import_fields
  our @zone_import_fields
  our @rule_import_fields
  our @auditlog_import_fields
```

## import_ tables

### rule.csv

```console
name: <rulebase_name>_rulebase.csv
fields (total=26):
  control_id        - bigint - id of currently running import (identical for all rules of an import)
  rule_num          - integer - number of the rule relative to the current import - used for sorting rules within this import
  rulebase_name     - string - name of the rulebase (used for matching rule to gateway)
  rule_ruleid       - string - id (unique within gateway, but not globally)
  rule_disabled     - boolean - is the rule disabled (inactive)?
  rule_src_neg      - boolean - is the source of the rule negated?
  rule_src          - string of CSVs with source objects of the rule (including users in format "user[-group]@object")
  rule_src_refs     - string of CSVs with UIDs of source objects of the rule (including users in format "user[-group]-uid@object-uid")
  rule_dst_neg      - boolean - is the destination of the rule negated?
  rule_dst          - string of CSVs with destination objects of the rule
  rule_dst_refs     - string of CSVs with UIDs of destination objects of the rule 
  rule_svc_neg      - boolean - is the service of the rule negated?
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
  last_change_admin - string containing name of the last admin having changed this rule (optional, checkpoint only)
  parent_rule_uid   - string - UID of a rule this rule belongs to (either layer, domain rules or section)
```

### network_objects.csv

```console
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

### services.csv

```console
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

### users.csv

```console
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

### zones.csv

```console
name: <mgmt_name>_zones.csv
fields (total=2):
  control_id
  zone_name
```
