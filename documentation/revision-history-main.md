# Firewall Orchestrator Revision History MAIN branch

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

### 6.0.2 - 24.12.2022
- bugfix release with hasura API upgrade due to security bug in hasura

### 6.2 - 16.03.2023 MAIN
- enhanced recertification module: adding ip-base recertification
- adding import modules for Palo Alto and Azure Firewall
- Workflow Module: adding delete rule request and integrated path analysis into workflow

### 6.2.1 18.03.2023 MAIN
- fix ldap issues - closes ldap bugs #2023
- reduced logging in release mode
- hasura v2.21.0 upgrade

### 6.3 24.04.2023 MAIN
- adding CP R8X object types
  - application categories
  - updatable objects
  - domain names

### 6.3.1 27.04.2023 MAIN
- hotfix adding CP R8X object type application site

### 6.3.2 05.05.2023 MAIN
- hotfix UI and fortigate importer credential handling
- checkpoint R8X importer adding support for Internet object type
- reporting - CSV export for change report

### 6.4 25.05.2023 MAIN
- New importer module for importing FortiGate directly via FortiOS REST API
- Reporting: new lean export format JSON for resolved and tech reports
- hotfix FortiGate FortiOS REST importer: removing reference to gw_networking
- hotfix CPR8x importer: handling of empty section headers

### 6.4.1 02.06.2023 MAIN
- FortiOS importer: add support for internet services

### 6.4.2 05.06.2023 MAIN
- Hotfix - log locking UI hangs on prod systems due to infrequent log entries

### 6.4.3 05.06.2023 MAIN
- Hotfix - global config subsription timout after 12h

### 7.0 26.07.2023 MAIN
- new features
   - UI adding compliance matrix module
   - UI Reporting - unused rules report including delete ticket integration
   - importer new email notification on security relevant import changes
   - importer CPR8x: basic support for importing inline layers

- maintenance / bug-fixing
   - API: upgrading hasura api to 2.30.1
   - importer Fortigate API: hotfix NAT rules
   - UI: cleanup around buttons and logout session handling
   - UI Reporting: fixes links to objects, template name display, UI visibility for fw-admin role (multiple pages)
   - UI (re-)login: allow enter as submit
   - UI reporting: filter objects properly in rule report
   - UI updating help pages: email & importer settings, archive, scheduling)
   - installer: supress csharp test results on success
   - demo data: fix sample group role path
   - adding demo video in github README.MD
   - splitting revision history into develop and main

### 7.3 22.10.2023 MAIN
- new features
    - recertification: new rule ownership
    - customizable UI texts
    - starting target state module with introducing new role "modeller"
    - adding tenant ip filtering
    - adding tenant simulation (exluding statistical report and recertification) including scheduling
- maintenance / bug-fixing
  - complete re-work: all ip addresses are now internally represented as ranges, including all networks
  - UI:
    - do not show super managers in RSB all tab
    - Use production / development based on the build type instead of always using development.
    - do not show detailed errors in production mode + use the custom error page in the production environment
    - bug fix jwt expiry, jwt expiry timer now works as intended
    - unifying IP addresses display method across all parts
    - fix filtering for rules with negated source / destination or single negated ip ranges
  - Database:
    - removing unused materialized view for tenant ip filtering
  - Installer
    - fix upgrade become issue in middleware ldif files
    - fix client/server db sort order mismatch (collate)
    - fix postgresql_query module reference
    - adding simulated changes to fwodemodata (fortigate)
    - add check for successful publishing dotnet (mw, ui)
  - Importer
    - fortiOS: fix importer action field
    - fortimanager: ignore missing negate fields
    - Check Point: adding Inform action
    - Check Point: adding new network object type 'external-gateway' (for interoperable-dervice)
    - Check Point: adding network object type support for 'CpmiVsClusterNetobj' (for VSX virtual switches)
  - API:
    - upgrade hasura to 2.34.0
- restrictions
  - since tenant filtering is not done in the API but in the UI, the API should not be exposed to the tenants
  
### 8.0 19.02.2024 MAIN
- Introducing new Network Modelling module
  - allows your organisation to define the target state of all network connection on a per-application basis (or other distributed ownerships)
- Backend
  - Introducing Scheduled import change notification including inline or attached change report (replacing simple import notification from import module)
  - upgrade hasura graphql API to 2.37.0
- UI
  - New look and feel: Moving to vanilla bootstrap css v5.3.2 (allowing for future up to date css usage)
  - ip based tenant filtering: introducing unfiltered_managements and devices and adding extended tenant to device mapping settings
- Installer (breaking change!)
  - introducing venv for newer ansible versions and thereby removing annoying ansible version handling in installer (see https://github.com/CactuseSecurity/firewall-orchestrator/blob/main/documentation/installer/basic-installation.md for details)
- bugfixes for
  - import log locking
  - integration tests with credentials when installing without demo data
  - pdf creation on debian testing plattform (trixie)

# 8.1 - 10.04.2024 MAIN
- UI: iconifying modelling UI buttons (can now use icons instead of text buttons - configurable per user)
- Importer: first version of VMware NSX import module
- API: adding customizing script for bulk configs via API
- Database security: all credentials in the database are now encrypted - breaking change (for developer debugging only): add the following local file when using -e testkeys=true:
  /etc/fworch/secrets/main_key with content "not4production..not4production.."
- Importer fix: remove log locking from importer due to stalling importer stops

# 8.2 - 30.04.2024 MAIN
- new workflow for modelling: interface request
  - adding all imported modelling users to local db (uiuser) - to enable email notification
- new features for modelling
  - display NAs in Report LSB and Export
  - count and display members of areas in selection list  
- upgrade to dotnet 8.0 (middleware and UI server)
- encrypt emailPassword in config
- fixes:
  - demo managements (change import from deactivated to activated - does not affect test managements)

# 8.3 - 25.06.2024 MAIN
Maintenance release
- fix misleading login error message when authorisation is missing
- fix email credential decryption
- start of Tufin SecureChange integration
- remove cascading delete for used interfaces 
- owner-filtering for new report type
- new setting for email recipients
- owner-import custom script improvements#

# 8.3.1 - 14.08.24 MAIN
Hotfix:
- in CheckPoint importer: fix missing group members

# 8.4 - 30.09.24 MAIN
Stability release
- various small bug fixes
  - installer (redundant code deleting test user)
  - importer (switching from full details to standard, re-adding VSX gateway support, voip domain handling in cp parser)
  - reporting (app-rule report containing multiple objects)
  - middleware (config subscriptions)
  - reporting (temporarily highlight linked to object in rsb)
  - modelling (sync connections - not always part of overview table after creation)
  - RBA (role picking when user has multiple roles)
  - UI various: adding missing pager control
  - UI various: spinner clean-up
- features/upgrades
  - Added login page welcome message and settings
  - Added last hit information in app-rule report
  - API - upgrading to 2.43.0
  - various security upgrades dotnet (restsharp, jwt, ...)

# 8.4.1 - 30.10.24 MAIN
Network Modelling feature update
- import of app server IP addresses via CSV upload
- import of multiple sources for area IP data 
- new option email notification: fall-back to main owner if group is empty
Fixes
- corrections in displaying UI messages
- converting owner network ip data to standard format "range"
- importer 
  - check point - fix import of all VSX instances
  - fortinet - add hit counts and install on information

# 8.5 - 13.11.24 MAIN
Network Modelling feature update
- modelling can be requested as firewall change via external ticketing tool
- includes all approle handling
- simple form of rule change request (always request all connections as rules)
Fixes
- various small UI fixes
 