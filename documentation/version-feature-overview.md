# Firewall Orchestrator – Version / Feature Overview

This document lists each Firewall Orchestrator version from **8.0** onwards and
the features it introduced. It is compiled from the
[revision history](revision-history.md).

Entries focus on newly introduced features and capabilities; pure bug fixes,
dependency bumps and Hasura/.NET maintenance upgrades are generally omitted. A
`(DEVELOP)` marker indicates a develop-branch interim release; unmarked versions
are `main` releases.

For a feature-centric, thematically grouped view see
[feature-catalogue.md](feature-catalogue.md).

---

## 8.x

### 8.0 — 19.02.2024
- New **Network Modelling** module: define the target state of all network connections per application (or other distributed ownerships).
- Scheduled import change notification with inline or attached change report (replaces the simple import notification from the import module).
- New look & feel: vanilla Bootstrap CSS v5.3.2.
- IP-based tenant filtering: `unfiltered_managements` / `unfiltered` devices and extended tenant-to-device mapping settings.
- Installer: introduction of venv for newer Ansible versions (breaking change).
- Hasura GraphQL API upgraded to 2.37.0.

### 8.1 — 10.04.2024
- Iconified modelling UI buttons (icons instead of text buttons, configurable per user).
- First version of the **VMware NSX** import module.
- API: customizing script for bulk configs.
- All database credentials are now encrypted (breaking change for developer debugging).
- Custom (user-defined) import fields (Check Point only initially).

### 8.2 — 30.04.2024
- New workflow for modelling: **interface request** (all imported modelling users added to local `uiuser` to enable email notification).
- Modelling: display NAs in Report LSB and export; count and display members of areas in selection list.
- Upgrade to .NET 8.0 (middleware and UI server).
- Encrypt emailPassword in config.

### 8.3 — 25.06.2024
- Start of **Tufin SecureChange** integration.
- Owner-filtering for new report type.
- New setting for email recipients.
- Owner-import custom script improvements.
- Workflow: external state handling.

### 8.4 — 30.09.2024
- Login page welcome message and settings.
- Last hit information in app-rule report.
- API upgraded to 2.43.0; various dotnet security upgrades.

### 8.4.1 — 30.10.2024
- Network Modelling: import of app server IP addresses via CSV upload.
- Import of multiple sources for area IP data.
- Email notification option: fall-back to main owner if group is empty.
- Importer: FortiNet hit counts and install-on information.

### 8.5 — 13.11.2024
- Network Modelling can be requested as a firewall change via an external ticketing tool (incl. all approle handling).
- Simple form of rule change request (always request all connections as rules).
- Owner LDAP selectable (internal / external).
- API Hasura upgrade to 2.44.0.

### 8.6 — 11.12.2024
- Modelling: create **Application Zones**.
- Monitoring of external requests for admins; re-initialization and consolidation of external requests.
- Optional access request on behalf of UI user.
- Live update of external task/ticket status.
- App server name handling rework (`NONAME` → `<prefix>_<IP address>`).
- Owner groups can now be external LDAP groups.
- Reporting: refined connection report (common service, app role, network area details).
- Importer: new VOIP service object and Internet object.

### 8.6.1 — 17.12.2024
- Network modelling hardening: locks on external requests, wait cycles after group changes, inherit/sanitize extra configs, UI interface search as filterable table.

### 8.7 — 03.03.2025
- PDF generation: engine replaced (wkhtml → Puppeteer).
- Modelling: sortable application-role objects (by IP or name); change requests added to history; option to name all app servers by reverse DNS with fall-back to prefix + IP.
- General UI pop-up unification and clean-up.

### 8.8 — 17.04.2025
- New API endpoint for getting rules.
- Variance report (first version).
- Prevention of using NA objects in connections.
- RSB enhancements; IP filter line observes negation in rules.
- Flexible LDAP group name templating.
- New customized app data import script with import stats.

### 8.8.2 — 07.05.2025 (DEVELOP)
- Displayed state via variance analysis.

### 8.8.3 — 15.05.2025 (DEVELOP)
- Deactivation of connections.

### 8.8.4 — 02.06.2025 (DEVELOP)
- Check Point importer support for DLP actions (ask, inform).

### 8.8.5 — 17.06.2025 (DEVELOP)
- New enum values for Request Element Field Types.

### 8.8.6 — 22.07.2025
- Check Point importer: `stm_track` "extended log" and "detailed log".
- Stricter automated quality-control checks.

### 8.8.8 — 23.08.2025
- Read-only DB user `fwo_ro`.
- Hardening: apache config, listener restriction (Hasura, Postgres), log sanitisation.

### 8.8.9 — 27.08.2025 (DEVELOP)
- Notification service.
- Decommissioning of interfaces.
- Iconification of modelling and related modules.
- Tables/settings prepared for owner recert + first-throw recert popup.

### 8.8.10 — 07.09.2025 (DEVELOP)
- New report type: owner-recertification.

### 8.9.1 — 02.10.2025
- Owner-recertification.

### 8.9.2 — 17.10.2025
- Add `ownerLifeCycleState` and a manageable lifecycle-state menu.

### 8.9.4 — 09.12.2025
- New custom scripts for IIQ and CMDB import.

### 8.9.6 — 05.01.2026
- New parameters for notifications.

## 9.x

### 9.0 — 27.01.2026
- Complete 80K-line rework of FWO:
  - Database changes to deduplicate rules (rule-to-gateway mapping now 1:n via `rulebase` and `rulebase_link` tables).
  - Import module migrated from mixed python/pgsql to pure python.

### 9.0.1 — 07.02.2026
- `rule_owner` table for REST API.
- `import_control` reworked for flexible tracking of different import types.
- Generalized owner responsibles with configurable responsible types.
- `allow_write_access` on responsible types (controls modelling and recertification).

### 9.0.3 — 12.02.2026 (DEVELOP)
- Interface permissions.

### 9.0.5 — 18.02.2026 (DEVELOP)
- `rule_owner` mapping for `custom_field` via button and service/job.

### 9.0.6 — 20.02.2026 (DEVELOP)
- Import of time objects.

### 9.0.7 — 25.02.2026 (DEVELOP)
- `changelog_owner` table.

### 9.0.8 — 25.02.2026 (DEVELOP)
- New config value for removed app-server handling.

### 9.0.10 — 28.02.2026 (DEVELOP)
- New config value for user synchronization in owner data import.

### 9.0.11 — 04.03.2026 (DEVELOP)
- New config value for requesting only own objects.

### 9.0.12 — 12.03.2026 (DEVELOP)
- New config values for rule-expiry notification.

### 9.0.13 — 12.03.2026 (DEVELOP)
- Mark lifecycle states as active.

### 9.0.14 — 17.03.2026 (DEVELOP)
- Owner decommission notification (preparation).

### 9.0.16 — 26.03.2026
- Move from Docker to Podman.
- Full re-initialize of RuleOwner mapping for IP-based rules; `matched_objects` field in `rule_owner`.

### 9.0.18 — 03.04.2026 (DEVELOP)
- New column `automatic_only` on workflow states.

### 9.0.19 — 09.04.2026 (DEVELOP)
- Owner `additional_info` JSONB field incl. owner edit UI support.

### 9.0.20 — 11.04.2026 (DEVELOP)
- Extended notification handling.

### 9.0.23 — 27.04.2026 (DEVELOP)
- Notifications by BCC.
- Display-only workflow label report column option.
- Default template for workflow tickets approved last week.

### 9.0.24 — 27.04.2026 (DEVELOP)
- New modelling integration mode: WorkflowNotifications.

### 9.1.0 — 20.05.2026 (DEVELOP)
- JWT refresh token.
- Introduce flow schema.

### 9.1.1 — 21.05.2026 (DEVELOP)
- Workflow: configurable execution order for actions assigned to states.

### 9.1.3 — 27.05.2026 (DEVELOP)
- Optional workflow flow merging for Flow DB creation.

### 9.1.5 — 05.06.2026 (DEVELOP)
- Asynchronous initial JWT bootstrap in the UI.
- Subscription-aware reconnect logic after JWT refresh; separate GraphQL subscription client path.

### 9.1.7 — 08.06.2026 (DEVELOP)
- Security-hardening release: restricted app-data import file/script paths, safer installer secret handling, tightened Hasura config permissions, LDAP/install idempotency, removal of the obsolete webhook role.
- Importer/customizing-script HTTP calls now use connect/read timeouts.
- FortiOS (REST) VIP/destination-NAT objects normalized to their external IP.
- Explicit `[Authorize]` on the password-change REST endpoint.

### 9.3 — 14.06.2026 (DEVELOP)
- New OPNsense standalone (25ff) import module: imports OPNsense firewall configs via the full config.xml core backup API.
