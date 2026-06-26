# Firewall Orchestrator Revision History

pre-5, a product called IT Security Organizer and was closed source. It was developed starting in 2005.
In 2020 we decided to re-launch a new

## 8.0 - 19.02.2024 MAIN
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

## 8.0.1 - 20.02.2024
- iconify modelling
- add missing config values

## 8.0.2 - 11.03.2024
- first version of NSX import module

## 8.0.3 - 08.04.2024
- add maintenance page during upgrade
- sample customizing py script with sample data, closes  Installer customizable config (settings) #2275
- remove log locking from importer due to stalling importer stops
- credentials encryption, closes encrypt passwords and keys #1508
  - breaking change for developer debugging: add the following local file when using -e testkeys=true:
    /etc/fworch/secrets/main_key with content "not4production..not4production.."
- add custom (user-defined) fields to import
  - cp only so far, other fw types missing
  - user-defined fields are not part of reports yet

## 8.1 - 10.04.2024 MAIN
- UI: iconifying modelling UI buttons (can now use icons instead of text buttons - configurable per user)
- Importer: first version of VMware NSX import module
- API: adding customizing script for bulk configs via API
- Database security: all credentials in the database are now encrypted - breaking change (for developer debugging only): add the following local file when using -e testkeys=true:
  /etc/fworch/secrets/main_key with content "not4production..not4production.."
- Importer fix: remove log locking from importer due to stalling importer stops

## 8.1.1 - 15.04.2024
- interface request workflow first version

## 8.1.2 - 22.04.2024
- encrypt emailPassword in config
- fix demo managements (change import from deactivated to activated - does not affect test managements)
- upgrade to dotnet 8.0
- adding all imported modelling users to uiuser

## 8.2 - 30.04.2024 MAIN
- new workflow for modelling: interface request
  - adding all imported modelling users to local db (uiuser) - to enable email notification
- new features for modelling
  - display NAs in Report LSB and Export
  - count and display members of areas in selection list
- upgrade to dotnet 8.0 (middleware and UI server)
- encrypt emailPassword in config
- fixes:
  - demo managements (change import from deactivated to activated - does not affect test managements)

## 8.2.1 - 03.05.2024
- fix misleading login error message when authorisation is missing

## 8.2.2 - 14.05.2024
- fix email credential decryption
- start of Tufin SecureChange integration

## 8.2.3 - 26.05.2024
- remove cascading delete for used interfaces
- new properties field in connections

## 8.2.4 - 19.06.2024
- owner-filtering for new report type
- new setting for email recipients

## 8.3 - 25.06.2024 MAIN
Maintenance release
- fix misleading login error message when authorisation is missing
- fix email credential decryption
- start of Tufin SecureChange integration
- remove cascading delete for used interfaces
- owner-filtering for new report type
- new setting for email recipients
- owner-import custom script improvements#

## 8.3.1 - 08.07.2024
- workflow: external state handling
- fix config value
- remove uniqueness of owner names

## 8.3.1 - 14.08.24 MAIN
Hotfix:
- in CheckPoint importer: fix missing group members

## 8.3.2 - 09.09.2024
- Added welcome message and settings

## 8.4 - 30.09.24 MAIN
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

## 8.4.1 - 15.10.24
- Add missing FK connection.proposed_app_id #2591

## 8.4.2 - 17.10.2024
- external request

## 8.4.1 - 30.10.24 MAIN
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

## 8.4.3 - 05.11.2024
- extra parameters in modelling connection

## 8.5 - 13.11.24 MAIN
Network Modelling feature update
- modelling can be requested as firewall change via external ticketing tool
- includes all approle handling
- simple form of rule change request (always request all connections as rules)
- api hasura upgrade to 2.44.0
Fixes
- various small UI fixes
- importer (CP: handle None objects)

## 8.5.1 - 18.11.2024
- reporting - fixing PDF generation on various platforms
- modelling - fixing AR editing: strict prevention of all area mixing

## 8.5.2 - 27.11.2024
- some check point importer fixes
  - 4 new colors
  - added Internet object
  - added voip one more object

## 8.5.3 - 27.11.2024
- owner import - make ldap selectable (internal/external)
- small fixes regarding missing config data for two schedulers (daily, app data import)

## 8.5.4 - 04.12.2024
- external request: introduce wait cycles

## 8.6 - 11.12.2024 MAIN
Features
- Modelling
  - Create Application Zones
  - Add monitoring for external requests for admins
  - Add re-initialization for external requests
  - consolidation modelling external requests
  - adding optional access requst on behalf of UI user
  - adding live update of external task/ticket status
  - app server name handling rework (NONAME --> <prefix>_<IP address>)
  - owner groups can now also be external LDAP groups
- Reporting
  - refining connection report (adding Common service, app role, network area details)
Fixes
- Importer
  - adding missing colors in Check Point importer
  - new VOIP service object and Internet object
- UI
  - SECURITY: updating System.Text.Encodings.Web v4.5.0 --> v8.0.0

## 8.6.1 - 12.12.2024
- external request: introduce locks

## 8.6.1 - 17.12.2024 MAIN
Fixes network modelling
- lock external requests to avoid multiple external tickets
- fix missing comments
- wait cycles for access request after group changes
- save publish flag at interface creation
- disregard dummyAppRole for status determination
- inherit extra configs from interface
- sanitize extra configs
- sort tasks for connection Id and show already adapted name of new members
- small monitoring adaptations
- some cleanup + removal of compiler warnings
- fix ldap group creation regression
- restrict owner_network uniqness constraint to same import source
- UI interface search pop-up transformed into filterable table

Upgrade Hasura API to v2.45.1

## 8.6.2 - 03.01.2025 MAIN
Hotfix for network modelling:
- fix: when visiting the library for the second time, app servers were missing due to uninitialized area data.

## 8.6.3 - 20.02.2025
- dns lookup for app server names

## 8.7 - 03.03.2025 MAIN
- General UI
  - pop-up unification and clean-up
  - removing unnecessary scroll-bars
- PDF generation: replacing engine wkhtml with puppeteer
- Modelling
  - Edit application role (AR): make objects sortable by IP or name
  - adding change requests to history
  - adding option to name all application servers by reverse DNS and fall-back to prefix + ip
- API: upgrade Hasura to 2.45.2
- Workflow: some performance improvements

## 8.7.1 - 05.03.2025
- ldap writepath for groups

## 8.7.1 - 07.03.2025 MAIN
- fix modelling select existing interfac
- fix modelling settings ldap selection
- fix workflow ticket close spinner

## 8.7.2 - 20.03.2025
- new config values
- external request: attempt counter

## 8.8 - 17.04.2025 MAIN
* fix stm_action by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2844
* add missing rulebase_link constraints by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2845
* fix rule_metadata creation by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2865
* remove dev_id fk constraint from rule_metadata by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2909
* fix missing rule_metadata.rulebase_id by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2911
* fix warnings and rule normalize bug by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2912
* fix missing upgrade scripts from pre 9 by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2938
* Cactus develop fix importer main level bug by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3009
* Endpoint for getting rules by @abarz722 in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3027
* ExtRequest - increase logging by @abarz722 in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3029
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3038
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3042
* fix(ui): ip filtering in app report by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3040
* Preventing use of NA objects in connections by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3043
* fix(ui rsb): ui crash likely caused by duplicates in query result by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3046
* LDAP Nuget Update changes by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3056
* Defer AZ creation until second button click by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/2856
* Removing minor py-re deprecation warnings  by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3053
* feat(ui): rsb enhancements by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3073
* User UI glitch by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3089
* Modelling new AR drop down strange initial value by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3091
* Verify modelled services for empty groups by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3087
* adding app servers fails without name by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3088
* Modelling - no NA should be usable for selected interfaces by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3086
* new customized app data import script by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3101
* adding csv appdata import stats by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3103
* reformatting app server ip struct by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3105
* css cache changes by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3108
* show more clearly if everything is (horizontally) displayed by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3096
* Fixed connection object duplication by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3118
* Modelling csv import improvements by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3113
* IP check improvements by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3133
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3136
* Some report generation improvements by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3117
* Config change subscribe add "autoReplaceAppServer"  #3138 by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3148
* Nuget Updates by @SolidProgramming in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3143
* External ticket timout fix by @NilsPur in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3151
* feat(ui): ip filter line observes negation in rules by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3164
* allow for flexible ldap group name templating, fix #3114 by @tpurschke in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3165
* Variance Report First Throw by @abarz722 in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3080
* feat(ui rsb): show ip/port of flat members by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3172
* fix(ui report): ip filter on negated rule to/from by @Y4nnikH in https://github.com/CactuseSecurity/firewall-orchestrator/pull/3173

## 8.8.2 - 07.05.2025
- displayed state via variance analysis

## 8.8.3 - 15.05.2025
- deactivation of connections

## 8.8.4 - 02.06.2025
- hotfix for Check Point importer suppor for DLP actions (ask, inform)

## 8.8.5 - 17.06.2025
- new enum values for Request Element Field Types
- hotfix change recognition: separate rule changes and "all changes" to make object version handling work properly

## 8.8.6 - 08.07.2025
- hotfix CP importer new stm_track: "extended log" and "detailed log"

## 8.8.6 - 22.07.2025 MAIN
hotfix release
- CP importer new
  - stm_track: "extended log" and "detailed log"
  - fixing services-other ip proto import
- improved quality control with stricter automated checks
- various fixes in modelling module

## 8.8.8 - 21.08.2025
- add read-only db user fwo_ro
- also reducing db listener to localhost and other hardening changes

## 8.8.8 - 23.08.2025 MAIN
- add read-only db user fwo_ro
- hadening changes
  - apache config (information leakage)
  - listeners (hasura, postgres)
  - log santisation

## 8.8.9 - 27.08.2025
- prepare tables + settings for owner recert + first throw recert popup
- notification service
- decommissioning of interfaces
- iconification of modelling and related modules
- fix overwrite of objects with interface

## 8.8.10 - 07.09.2025
- new report type owner-recertification

## 8.9.1 - 02.10.2025 MAIN
- owner-recertification

## 8.9.2 - 17.10.2025 MAIN
- add ownerLifeCycleState
- add manageable ownerLifeCycleState menu
- fix two modelling ui glitches

## 8.9.3 - 05.11.2025 MAIN
- hotfix missing permissions for app data import in certain constellations

## 8.9.4 - 09.12.2025 MAIN
- bugfix release: common service connection not editable
- new custom scripts for iiq and cmdb import

## 8.9.5 - 10.12.2025 MAIN
- bugfix release: modelling - change planning showed duplicate NA elements for rule delete requests

## 8.9.6 - 05.01.2026 MAIN
- new parameters for notifications

## 9.0 - 24.01.2026 MAIN
A complete 80K lines rework of FWO, including
- database changes to deduplicate rules (rule to gateway mapping now 1:n by introducing rulebase and rulebase_link tables)
- migrating import module from mixed python/pgsql to pure python

## 9.0.1 - 07.02.2026 MAIN
- generalized owner responsibles with configurable responsible types
- add allow_write_access to responsible types to control modelling and recertification

## 9.0.2 - 10.02.2026
- importer: call api chunked where needed

**Breaking changes**
- Due to introduction of venv for all imports, the following steps have to be taken to manually import a config:

```shell
  sudo -u fworch -i
  cd importer
  source importer-venv/bin/activate
  python3 ./import_mgm.py -m xy -fs -d1
```
  As we now need support for pip, in installations behind url filter, make sure that all sub-domains of "pythonhosted.org" are also allowed.

- Limiting database listener to localhost for security reasons

## 9.0.3 - 12.02.2026
- introduce interface permissions

## 9.0.4 - 13.02.2026 MAIN
- maintenance release with explicit 9.0.4 upgrade step

## 9.0.5 - 18.02.2026
- update rule_owner table for REST api
- update import_control to allow flexible tracking of different import types
- create rule_owner mapping for custom_field via button and service/job
- update import_control to allow flexible tracking of different import types

## 9.0.6 - 20.02.2026
- add import of time objects

## 9.0.7 - 25.02.2026
- add import of time objects
- create changelog_owner table

## 9.0.8 - 25.02.2026
- new config value for removed App Server handling

## 9.0.9 - 25.02.2026
- remove stale v8 code

## 9.0.10 - 28.02.2026
- new config value for User synchronization in owner data import

## 9.0.11 - 04.03.2026
- new config value for requesting only own objects

## 9.0.12 - 12.03.2026
- new config values for rule expiry notification

## 9.0.13 - 12.03.2026
- mark lifecycle states as active

## 9.0.14 - 17.03.2026
- prepare owner decommission notification

## 9.0.15 - 19.03.2026
- rename OwnerSourceCustomFieldKey to CustomFieldOwnerKey in config

## 9.0.16 - 26.03.2026 MAIN
- bug fixing
- moving from docker to podman

## 9.0.16 - 31.03.2026
- remove not needed stm_owner_mapping_source
- add Full re-initialize of RuleOwner mapping for IP-based rules
- add matched_objects field in rule_owner table for track matched objects - IpBased

## 9.0.18 - 03.04.2026
- add new column automatic_only to workflow states

## 9.0.19 - 09.04.2026
- add owner additional_info jsonb field including owner edit UI support

## 9.0.20 - 11.04.2026
- extend notification handling

## 9.0.21 - 21.04.2026 MAIN
- fix ldap users with special chars not being processed correctly in role handling
- fix empty mail being sent for orphaned rule report
- update dependencies (notably closing mailkit and pytest vuln)
- fix time zone issues in importer

## 9.0.22 - 26.04.2026 MAIN
- fixes missing source or destination in rule expiry notification report
- fixes time zone issues with checkpoint time objects
- fixes python tests failing on python 3.10
- fixes owner import from custom file

## 9.0.23 - 27.04.2026
- enhance notifications by bcc
- add display-only workflow label report column option
- add default template for workflow tickets approved last week
Removed deprecated configuration keys:
- updateRuleOwnerMappingActive
- updateRuleOwnerMappingStartAt
These settings are no longer used due to the full automation of UpdateRuleOwner.

## 9.0.24 - 27.04.2026
- introduce new modelling integration mode WorkflowNotifications

## 9.1.0 - 20.05.2026
- JWT refresh token
- introduce flow schema

## 9.1.1 - 21.05.2026
- Workflow: add configurable execution order for actions assigned to states.

## 9.1.2 - 26.05.2026 MAIN
- database: fix flow foreign key duplication on fresh install plus upgrade path

## 9.1.3 - 27.05.2026
- add optional workflow flow merging for Flow DB creation

## 9.1.4 - 03.06.2026
- remove legacy ownerLdapGroupNames owner mapping fallback

## 9.1.5 - 05.06.2026
- remove old jwt token lifetime config values
- asynchronous initial JWT bootstrap in the UI
- subscription-aware reconnect logic after JWT refresh
- a separate GraphQL subscription client path
- improved cancellation and JWT-expiry handling
- a small cleanup of exception logging for subscription errors

## 9.1.6 - 08.06.2026
- remove obsolete database last_seen fields

## 9.1.7 - 08.06.2026
security patch

This PR hardens FWO installation and security-sensitive workflows. It restricts app data import file/script paths, reduces installer secret exposure, tightens Hasura config permissions, improves LDAP/install test idempotency, removes the obsolete webhook role, and fixes related installer/test reliability issues. It also includes targeted documentation and version updates.

- Import file/script handling now restricts app data sources to allowed .json and .py files under the customizing directory, rejecting unsafe paths and logging file hashes.
- Installer secret handling now uses no_log, avoids passing Hasura secrets on command lines, and prints secret file locations instead of secret values.
- LDAP installation now uses password files, tolerates existing entries, seeds missing test LDAP parents, and avoids premature middleware restarts.
- Hasura permissions now prevent scoped users from modifying global config rows while giving middleware-server dedicated global config access.
- Installer and integration tests now wait for required services, tolerate missing optional customizing scripts, and improve cleanup/reliability behavior.
- The obsolete webhook role and its docs, service files, templates, syslog, logrotate, and playbook wiring were removed.
- The app data import UI now selects allowed import stems instead of accepting arbitrary free-form paths.
- Product documentation and revision history were updated for this security-hardening release.
- The password-change REST endpoint now has an explicit [Authorize] requirement so anonymous callers are rejected before password-change logic runs.
- Importer and customizing-script HTTP calls now use connect/read timeouts so a stalled firewall or API endpoint can no longer hang an importer worker indefinitely.
- FortiOS (REST) VIP/destination-NAT objects are now normalized to their external IP, so policies referencing VIP objects resolve instead of failing the import or losing coverage.
- Tenant settings role handling now mirrors the backend per operation: the page stays viewable for admin/auditor/fw-admin, adding/deleting tenants and saving device visibility are admin-only (matching the REST and Hasura permissions), and editing existing tenants is allowed for admin/fw-admin.
- Remaining hardcoded strings on the scheduler monitoring page were moved into the localization texts.
- The shared confirm dialogs now raise DisplayChanged(false) after a successful action, so the parent's bound visibility state no longer remains stale.

## 9.1.8 - 16.06.2026
- flow db: access flows now include time objects and allow/deny flag in their functional definition
- flow sync: add hash consistency check
- this update resets the flow db!

## 9.1.9 - 18.06.2026
- request workflow: add locked tickets and request tasks for automatically created change requests
- further integration flow into workflow

# 9.1.10 - 21.06.2026
- change internal logic to handle src/dst zones as security-relevant
- backfill existing rule source and destination zone text fields from rule zone links

# 9.1.11 - 24.06.2026 - MAIN stabelizing
- fix: rule_owner_mapping - standardize constraint name

# 9.1.12 - 24.06.2026
- remove deprecated, unused rule.rule_num column (rule ordering is handled by rule_num_numeric)
- remove deprecated, unused direct rule zone columns (rule_from_zone, rule_to_zone); rule zones remain available through the rule_from_zone and rule_to_zone link tables
