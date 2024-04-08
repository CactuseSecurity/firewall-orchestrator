# Firewall Orchestrator Revision History for DEVELOP branch only

pre-5, a product called IT Security Organizer and was closed source. It was developed starting in 2005.
In 2020 we decided to re-launch a new 

### 6.1.0 - 16.11.2022 DEVELOP
- interactive network analysis prototype in UI
- integrate path analysis to workflow

### 6.1.1 - 15.12.2022 DEVELOP
- recertification on owner base
- preparation of new task types

### 6.1.2 - 20.12.2022 DEVELOP
- start of Palo Alto import module

### 6.1.3 - xx.01.2023 DEVELOP
- enhance recertification

### 6.1.4 - 27.01.2023 DEVELOP
- prepare delete rule requests

### 6.2.2 22.03.2023 DEVELOP
- adding last hit of each rule for check point and FortiManager to recertification (report)

### 6.3.3 09.05.2023 DEVELOP
- new importer module for importing FortiGate directly via FortiOS REST API

### 6.4.4 19.06.2023 DEVELOP
- CPR8x importer: basic support for inline layers

### 6.4.5 22.06.2023 DEVELOP
- Fortigate API importer: hotfix NAT rules
- upgrade to hasura API 2.28.0

### 6.4.6 23.06.2023 DEVELOP
- new email notification on import changes

### 6.4.7 26.06.2023 DEVELOP
- hotfix fortiOS importer NAT IP addresses
- fixing issue during ubuntu OS upgrade with ldap 
- unifying all buttons in UI

### 6.4.8 29.06.2023 DEVELOP
- hotfix fortiOS importer: replacing ambiguous import statement

### 6.4.9 03.07.2023 DEVELOP
- fix sample group role path

### 6.4.10 07.07.2023 DEVELOP
- fixes in importer change mail notification for encrypted mails
- fixes for report links to objects
- fix template name display issue
- fix UI visibility for fw-admin role (multiple pages)
- UI login page: allow enter as submit
- UI reporting: filter objects in rule report
- adding demo video in github README.MD

### 6.4.11 10.07.2023 DEVELOP
- bugfix in importer change mail notification for missing mail server config

### 6.4.12 14.07.2023 DEVELOP
- UI settings: hotfix email port (default 25) was not written to config before
- splitting revision history into develop and main
- installer: supress csharp test results on success

### 6.4.13 20.07.2023 DEVELOP
- re-login now also with enter key
- fixing help pages (email & importer settings, archive, scheduling) [#2162](https://github.com/CactuseSecurity/firewall-orchestrator/issues/2162)

### 6.5.0 24.07.2023 DEVELOP
- UI: adding compliance matrix module
- UI: fix browser session persistence causing subscriptions to remain open after user logout; now api connection and web socket are disposed on logout
- API: removing obsolete graphql query repos
- API: upgrading hasura api to 2.30.0
- installer: replacing deprecated path_to_script option in postgresql_query

### 6.5.1 24.07.2023 DEVELOP
- New report type Unused Rules

### 7.0.1 - 28.07.2023 DEVELOP
- Compliance matrix edit fix
- Logout audit logging fix

### 7.0.2 - 28.07.2023 DEVELOP
- Default templates for new report types

### 7.1 - 11.08.2023 DEVELOP
- adding tenant network UI
- adding test import via URI in hostname field
- replacing legacy demo data import with standard imported data, closing #2197 (note: only for new installations, an upgrade will not touch the demo data)
- test imports can now be made from file (integrated in UI)
- improve debugging of imports (no errors for missing object parts)

### 7.1.1 - 15.08.2023 DEVELOP
- fixes upgrade bug on systems without demo data

### 7.1.2 - 16.08.2023 DEVELOP
- adding Check Point R8x Inform action

### 7.2 - 21.08.2023 DEVELOP
mostly version update summarizing latest PRs
- UI/API: adding tenant ip filtering beta version (clean-up and optiomazation necessary)
- API: updating hasura to 2.32.0
- UI: now not showing super managers in RSB all tab
- UI: bug fixes blazor environment settings
  - Use production / development based on the build type instead of always using development.
  - Do not show detailed errors in production mode.
  - Use the custom error page in the production environment.
  - Spelling mistake fix
- UI: bug fix jwt expiry
  - jwt expiry timer now works as intended
  - after the jwt expired no exception can be triggered anymore

### 7.2.1 - 11.09.2023 DEVELOP
- new settings option for rule ownership mode
### 7.2.2 - 15.09.2023 DEVELOP
- complete re-work: all ip addresses are now internally represented as ranges, including all networks
### 7.2.3 - 29.09.2023 DEVELOP
bugfix release:
- api - upgrade hasura to 2.33.4
- installer - fix client/server db sort order mismatch (collate)
- adding simulated changes to fwodemodata (fortiate)
- importer - fix in fortiOS importer action field
- UI
  - fix settings owner networks editing and displaying
  - recert report (and recert page) IP addresses now also simplified like an other reports
  - fix broken links in recert page
### 7.2.4 - 04.10.2023 DEVELOP
- new role modeller
- new mechanism for overwriting texts 
# 7.2.5 - 05.10.2023 DEVELOP
- importer
  - adding more error debugging in CPR8x importer 
  - adding new network object type 'external-gateway' (for interoperable-dervice in check point)
  - fix fortimanager importer: ignore missing negate fields
- middleware & ui: add check for successful publishing dotnet
- middlware: fix upgrade become issue in middleware ldif files
- database: fix postgresql_query module reference

# 7.2.6 - 06.10.2023 DEVELOP
- importer Checkpoint: adding network object type support for 'CpmiVsClusterNetobj' (for VSX virtual switches)

# 7.3 - 22.10.2023 DEVELOP
- cleanup unused database views and functions
- first working tenant ip-based filtering

# 7.3.1 - 26.10.23 DEVELOP
- introducing unfiltered_managements and devices for tenant filtering
- fixing missing api perms fw-admin (management)
- rename management & device tenat_id fields to unfiltered_tenant_id
- fixing UI device selector crashes

# 7.3.2 - 09.12.2023 DEVELOP
- Modelling first version

# 7.3.3 - 08.01.2024 DEVELOP
- Moving to vanilla bootstrap css v5.3.2
- adding extended tenant to device mapping settings (depending on latest bootstrap version) - closes  #2280
- fix for log locking for import process

# 7.3.4 - 09.01.2024 DEVELOP
- Scheduled import change notification

# 7.3.5 - 15.01.2024 DEVELOP
- importer log locking fix (only fixing import stopping so far)
- import change notification:
  - DB extensions import_control.security_relevant_changes_counter
  - removing python import notification
  - writing to change counter after import (inpreparation for notification enhancement)
- importer demo tenant device mapping additions (upgrade)
- installer: introducing venv for newer ansible versions and thereby removing version handling

# 7.3.6 - 23.01.2024 DEVELOP
- common service handling
- fixes credentials when installing without demo data
- fix error with pdf creation on debian testing

# 8.0.1 - 20.02.2024 DEVELOP
- iconify modelling
- add missing config values

# 8.0.2 - 11.03.2024 DEVELOP
- first version of NSX import module

# 8.0.3 - 08.04.2024 DEVELOP
- add maintenance page during upgrade
- sample customizing py script with sample data, closes  Installer customizable config (settings) #2275
- remove log locking from importer due to stalling importer stops
- credentials encryption, closes encrypt passwords and keys #1508
  - breaking change for developer debugging: add the following local file when using -e testkeys=true:
    /etc/fworch/secrets/main_key with content "not4production..not4production.."
- add custom (user-defined) fields to import
  - cp only so far, other fw types missing
  - user-defined fields are not part of reports yet
