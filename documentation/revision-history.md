# Firewall Orchestrator Revision History

pre-5, a product called IT Security Organizer and was closed source. It was developed starting in 2005.
In 2020 we decided to re-launch a new 

## 5.0

### 5.0.0
rename isoadmin --> uiuser

### 5.0.1
create table report_schedule

### 5.0.2
Add column changes_found to import_control table - signifying if an import produced any changes

### 5.0.3
adding config_user column to config table for user specific config settings

### 5.0.4
add report_owner_id column to report table (but do not allow for sharing of generated reports yet)

### 5.0.5 - 13.12.2020
adjust all relevant tables to allow for growth beyond integer (turning into BIGINT)

### 5.0.6 - 18.12.2020
adding tenant_id to ldap_connection table (optional) to allow dedicated ldap_connections per tenant

### 5.0.7 - 22.12.2020
removing stm_report_typ table and references, adding report_schedule.report_schedule_name.

### 5.0.8 - 23.12.2020
adding report_name and report_filetype to report table

### 5.0.9 - 28.12.2020
remove unique constraint from uiuser_username in uiuser

### 5.1.01 - 30.12.2020
drop old functions to enable re-creation with bigint

### 5.1.02 - 30.12.2020
drop default value for uiuser_language in uiuser

### 5.1.03 - 31.12.2020
adding some reporting columns

### 5.1.04 - 01.01.2021
adding default report_templates

### 5.1.05 - 06.01.2021
adding first compliance report template (any rules)

### 5.1.06 - 10.01.2021
adding report template format fk and permissions

### 5.1.07 - 14.01.2021
- adding https reverse proxy in front of middleware server
- removing column report.report_filetype which has been replaced with relation report_schedule_format and extra fields report_json, report_pdf, report_csv, report_html
- adding report_schedule_repetitions

### 5.1.08 - 18.01.2021
- removing report table columns which are not needed:
  - start_import_id
  - stop_import_id
  - report_generation_time

### 5.1.09 - 20.01.2021
- report_template fixes
  - default templates get report_template_owner id=0 instead of null
  - fixing template edit/delete buttons
  - changing report.report_pdf type from bytea to TEXT

### 5.1.10 - 25.01.2021
- add debug_level to table management

### 5.1.11 - 27.01.2021
- add rule_metadata table and fill it during import
- removing rule_order from import rule process

### 5.1.12 - 29.01.2021
- replacing rule_metadata.mgm_id with dev_id
- removing rule_order commpletely

### 5.1.13 - 19.02.2021
- add ldap_searchpath_for_groups, ldap_type and ldap_pattern_length to ldap_connection

### 5.1.14 - 18.04.2021
- replacing all secrets with randomly generated ones - an upgrade is not possible - needs an uninstall/re-install!

### 5.1.15 - 23.04.2021
- adding culture info to language

### 5.1.16 - 10.05.2021
- adding direct link tables rule_[svc|nwobj|user]_resolved to make report object export easier

### 5.1.17 - 28.05.2021
- adding parent rule reference for layer and domain rule functionality

### 5.2.1 - 30.05.2021
- removing
  - php ui 
  - database components not needed for v5 (text_msg, get_text)

### 5.2.2 - 30.05.2021
- replace description texts of roles in ldap by codes for language translation

### 5.2.3 - 02.06.2021
- changing rule constraint from mgm_id to dev_id (for rulebases with identical global rules)
- debug improvements
- moving temp dir from /tmp to /var/fworch/tmp
- migrating get_config from text/dict mixed to cleaner dict-only approach

### 5.2.4 - 04.06.2021
- changing db column management.ssh_public_key to nullable
- adjusting api calls

### 5.2.5 - 04.06.2021
- new role recertifier
- adapt rule_metadata table for recertification prototype

### 5.2.6 - 07.06.2021
- report rule - object reference now time aware

### 5.2.7 - 14.06.2021
- add column rule_decert_date to rule_metadata

### 5.2.8 - 18.06.2021
- add column rule_recertification_comment to rule_metadata

### 5.3.1 - 01.07.2021
- first public open source version

### 5.3.2 - 05.07.2021
- some minor bufixes

### 5.3.3 - 10.07.2021
- add column ldap_name to ldap_connection
- add column ldap_connection_id to uiuser

### 5.3.4 - 29.07.2021
- moving to API hasura v2.0

### 5.4.1 - 10.09.2021
- moving towards full API-based importer modules
- in preparation for coming import changes

### 5.4.2 - 17.09.2021
- as a start of FortiManager importer only some network objects are imported (PoC)
- renaming fortimanager version
- adding importer loop for new API based imports 

### 5.5.1 - 27.10.2021
- preparing DB for NAT rules (transforming all existing rules)
- restricting all existing reports to access rules
- introducing swagger REST API for user management
- adding swagger REST API interactive documentation for user management
- moving to hasura 2.0.10 for slight performance boost (see https://github.com/hasura/graphql-engine/releases/tag/v2.0.10)

### 5.5.2 - 06.11.2021
- new default report template for NAT rules