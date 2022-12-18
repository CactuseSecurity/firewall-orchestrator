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

### 5.5.3 - 08.12.2021
- add column global_tenant_name to ldap_connection

### 5.5.4 - 13.12.2021
- insert default config values

### 5.5.5 - 20.12.2021
- set ldap_tenant_level to 5

### 5.5.6 - 02.01.2022
- introducing multi device manager for auto discovery

### 5.5.7 - 04.01.2022
- add column report_parameters to report_template, update data for default templates

### 5.6.1 - 12.01.2022
- update data for default templates for time filter

### 5.6.2 - 17.01.2022
- adding new legacy fortigate all in one device type (ssh)
- clean separation of legacy and api importer

### 5.6.3 - 19.01.2022
- migrating jsonb import config fields (import_config and import_full_config tables) to json
- this allows for import of bigger configs but is only a workaround that will not help for configs with >40.000 rules

### 5.6.4 - 25.01.2022
- main release merge
- migrated api import-loop to a single python script without any sys executes of ext. scripts
- minor but fixes and vip nat for fortimanager

### 5.6.5 - 11.02.2022
- next planned release
- fixing migration scripts
- splitting import_config into chunks to enable import of big managements
- introducing fw-admin role (device admin without delete & auto-discovery rights)
- working fortinet src hide nat behind interface

### 5.6.6 - 07.03.2022
- allow for users in rule destination (CP)
- monitoring module
- CPR8x auto-discovery

### 5.6.7 - 04.04.2022
- allow deactivation of ldap connection
- rework of python logging
- db index optimization
- fixing CIDR filtering

### 5.6.8
- no end ip address for obj types <> range
- fixing range display in reporting

### 5.6.9 - 28.04.2022
- import of fw configs directly from URL (import-mgm.py -m 17 -i https://x.y/z.conf)
- ldap connection check improvements
- alerting - handle import attempts

### 5.7.1 - 13.10.2022
- new workflow module for requesting changes
- new Cisco FirePower import module 
- support for new operating system debian testing
- bugfix enrichable objects in CP NAT rules
- bugfix filter line brackets
- new central credentials handling for import

### 5.7.2 - 21.10.2022
- start routing/interface (implemented for fortinet only) import and path analysis
- also adding dummy router for testing and interconnecting routing clouds
- new report type: resolved rules (report without group objects, exporting into pure rule tables without additional object tables)
- new user management API call for creating JWTs with arbitrary lifetime

### 5.8 - 23.10.2022
- fix for CP R81 bug with certain installations - we now allow for domain UID as well as domain name for getting import data
- also adding domain UID in auto discovery module
- from now on reserving 3 digit version numbers for bug fixes only 

### 5.8.1 - 26.10.2022
- hotfix DB user group import

### 5.8.2 - 30.10.2022
- new report type resolved tech info (no names)
- fix for log file rotation issues (log lock)
- fix change report warning for empty reports

### 6.0 - 02.11.2022
- clean-up work and new major version

### 6.0.1 - 10.11.2022
- bugfix release with small issues (userconfig re-login, ldif upgrade bug, debian testing support)

### 6.1.0 - 16.11.2022 DEVELOP
- interactive network analysis prototype in UI
- integrate path analysis to workflow

### 6.1.1 - 15.12.2022 DEVELOP
- recertification on owner base
- preparation of new task types
