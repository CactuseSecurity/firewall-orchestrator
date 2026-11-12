# Firewall Orchestrator – Version / Feature Overview

This document lists each Firewall Orchestrator version from **9.0** onwards (newest
first) and the features it introduced. It is compiled from the
[revision history](revision-history.md).

Entries focus on newly introduced features and capabilities; pure bug fixes,
dependency bumps and Hasura/.NET maintenance upgrades are generally omitted. A
`(DEVELOP)` marker indicates a develop-branch interim release; unmarked versions
are `main` releases.

For a feature-centric, thematically grouped view see
[feature-catalogue.md](feature-catalogue.md).

---

### 10.0.0 — 30.11.2026
- New **labelling** schema for generic key/value labels (e.g. Guardicore labels, app roles, stages) with a full change history; labels can be assigned to rules, connections, owner networks and access flows.
- New Akamai Guardicore Segmentation import module (Guardicore Management / Gateway): labels become network object groups, rules are split into rulebases following the Guardicore section order, and imported rules carry their source and destination labels.
- App data import accepts labels per app server (also via `label:<key>` csv columns); every rule and app server gets the **App label** of its owner (CMDB as master, key configurable via the global setting `appLabelKey`).
- Rule-based reports (rules, resolved/technical, unused, NAT, recertification, app rules and change reports) show rule labels in html, pdf, csv, json and the UI, configurable under Settings - Reporting.
- Label-based modelling: labels can be used as source and destination of connections, interfaces and common services, switched on by the new global setting "Label-based Modelling".

### 9.9.1 - 05.11.2026
- Workflow state matrices can configure whether phases after request are promoted from the ticket, from individual tasks, or both. The request phase remains ticket-driven.
- Access-task elements reference their ticket-local object-create task, keeping object changes consistent.
- Approval-phase tickets provide a consolidated task-details view with read-only, technical and resolved task displays, configurable field editing, and task-specific editors for access, object, group and time-object tasks.
- The promotion-mode setting supports an action-buttons-only option in addition to ticket, task and combined promotion.

### 9.9.0 — 04.11.2026
- Azure Firewall import module fetches firewall policies (network rules) and IP groups directly via the Azure Resource Manager API (api version 2026-01-01 with fallback to 2024-05-01).
- Azure parent policies (policy hierarchy) are imported; their rules precede the rules of the child policy.
- Azure gateway autodiscovery: finds the Azure Firewalls of the subscription and maps each to its firewall policy.
- New importer setting "Skip unsupported rules": rules that cannot be represented (currently Azure geo-location filters) are skipped with a warning or fail the import.

### 9.8.7 — 02.11.2026
- Rule positioning calculates and persists semantic positions for Check Point access-rule implementation tasks, including placement at the end of a uniquely matched zone-pair section.
- Auto-calculated undefined-internal zones cover complete IPv6 networks, and shared IP range parsing accepts dotted IPv4 netmasks.

### 9.8.6 — 02.11.2026
- The compliance check fails closed: a criterion that cannot be evaluated makes the rule not assessable instead of compliant, also in the conditional auto-promote of the workflow.
- The Cisco ASA importer offers the SHA-1 SSH key exchange only when the new device specific setting "Allow SHA-1 SSH key exchange" is switched on.
- Security hardening of the API permissions, password policy enforcement in the middleware, CSV and HTML exports, installer secrets and local privileges (database, LDAP, browser location, sudoers handling).
- Updated NuGet packages; the sort, hide, clear search and column filter labels of the FancyTable based tables are shown in the user's language.

### 9.8.5 — 30.10.2026
- The installer handles operating system upgrades: it stops with migration instructions when PostgreSQL moved to a new major version, and rebuilds the python venvs of the importer and the customizing scripts for a new python version.
- The Docker role was removed; upgrades start from 9.0.16 or newer, older installations upgrade with v9.8.4 first.
- The Tufin SecureChange ticket templates are device specific settings that can be adapted globally, per device type and per management; the upgrade moves the templates stored with the ticket systems.

### 9.8.4 — 29.10.2026
- Every list endpoint of the middleware REST API is paged (`limit`, `offset`, `X-Has-More`) with one central page size, the new setting "Max objects per REST API response page" (`maxRestResponseObjectLimit`, default 10000).
- Admins see a warning on every page while certificate checking is switched off for firewall connections, external ticket systems or the email server, and the upgrade lists the unchecked connection types.
- The Cisco ASA importer checks the SSH host key of the device against a known hosts file on the importer host; admins see the managements whose key is unknown or changed together with instructions.
- Hardening of report filters, the Report/Get REST call, external ticket and notification email templates, HTML report exports and firewall API error logging; the network syslog receiver is no longer opened.

### 9.8.3 — 26.10.2026
- Implementation tasks are created by the state action "Create implementation tasks"; the device specific settings "Auto plan implementation" and "Auto plan implementation for rules" decide per gateway and management which tasks it creates. Workflow Customizing offers the device input in the request and the path analysis algorithms "For each device" and "First device (if available)".

### 9.8.2 — 24.10.2026
- Workflow configuration transfer supports selected-phase exports, custom file names, imports into existing configurations, and safe state-definition validation.

### 9.6.1 — 01.10.2026 (DEVELOP)
- The Rules report can show management rulebases instead of gateways: start rulebases are selected per management, including rulebases without a gateway link, and reported together with the layers and sections following them.
- Configurable default rules report view (gateway rules or management rulebases), globally and personally. Personal settings override the global gateway-based default; saved report templates retain their view.

### 9.4.5 — 27.08.2026
- Compliance diff reports can optionally show only rules that newly became non-compliant.

### 9.3 — 30.07.2026 (MAIN)
- New OPNsense standalone (25ff) import module: imports OPNsense firewall configs via the full config.xml core backup API.

### 9.2.3 — 17.07.2026 (DEVELOP)
- Standard rules report generation now scopes flat rule paging to selected rulebases and adds a supporting database index.

### 9.2.2 — 15.07.2026 (MAIN)
- Added generic firewall import.
- Enabled Azure 2022 firewall support for normalized config import.

### 9.2.1 — 12.07.2026 (DEVELOP)
- Migrated firewall tables to the firewall schema.

### 9.2.0 — 10.07.2026
- Updated supported operating systems: Ubuntu 22.04/24.04/26.04, Debian 11/12/13, Red Hat 9, and Rocky 9.
- Ubuntu versions older than 22.04 and Debian versions older than 11 are no longer supported.

### 9.1.7 — 08.06.2026 (DEVELOP)
- Security-hardening release: restricted app-data import file/script paths, safer installer secret handling, tightened Hasura config permissions, LDAP/install idempotency, removal of the obsolete webhook role.
- Importer/customizing-script HTTP calls now use connect/read timeouts.
- FortiOS (REST) VIP/destination-NAT objects normalized to their external IP.
- Explicit `[Authorize]` on the password-change REST endpoint.

### 9.1.5 — 05.06.2026 (DEVELOP)
- Asynchronous initial JWT bootstrap in the UI.
- Subscription-aware reconnect logic after JWT refresh; separate GraphQL subscription client path.

### 9.1.3 — 27.05.2026 (DEVELOP)
- Optional workflow flow merging for Flow DB creation.

### 9.1.1 — 21.05.2026 (DEVELOP)
- Workflow: configurable execution order for actions assigned to states.

### 9.1.0 — 20.05.2026 (DEVELOP)
- JWT refresh token.
- Introduce flow schema.

### 9.0.24 — 27.04.2026 (DEVELOP)
- New modelling integration mode: WorkflowNotifications.

### 9.0.23 — 27.04.2026 (DEVELOP)
- Notifications by BCC.
- Display-only workflow label report column option.
- Default template for workflow tickets approved last week.

### 9.0.20 — 11.04.2026 (DEVELOP)
- Extended notification handling.

### 9.0.19 — 09.04.2026 (DEVELOP)
- Owner `additional_info` JSONB field incl. owner edit UI support.

### 9.0.18 — 03.04.2026 (DEVELOP)
- New column `automatic_only` on workflow states.

### 9.0.16 — 26.03.2026
- Move from Docker to Podman.
- Full re-initialize of RuleOwner mapping for IP-based rules; `matched_objects` field in `rule_owner`.

### 9.0.14 — 17.03.2026 (DEVELOP)
- Owner decommission notification (preparation).

### 9.0.13 — 12.03.2026 (DEVELOP)
- Mark lifecycle states as active.

### 9.0.12 — 12.03.2026 (DEVELOP)
- New config values for rule-expiry notification.

### 9.0.11 — 04.03.2026 (DEVELOP)
- New config value for requesting only own objects.

### 9.0.10 — 28.02.2026 (DEVELOP)
- New config value for user synchronization in owner data import.

### 9.0.8 — 25.02.2026 (DEVELOP)
- New config value for removed app-server handling.

### 9.0.7 — 25.02.2026 (DEVELOP)
- `changelog_owner` table.

### 9.0.6 — 20.02.2026 (DEVELOP)
- Import of time objects.

### 9.0.5 — 18.02.2026 (DEVELOP)
- `rule_owner` mapping for `custom_field` via button and service/job.

### 9.0.3 — 12.02.2026 (DEVELOP)
- Interface permissions.

### 9.0.1 — 07.02.2026
- `rule_owner` table for REST API.
- `import_control` reworked for flexible tracking of different import types.
- Generalized owner responsibles with configurable responsible types.
- `allow_write_access` on responsible types (controls modelling and recertification).

### 9.0 — 27.01.2026
- Complete 80K-line rework of FWO:
  - Database changes to deduplicate rules (rule-to-gateway mapping now 1:n via `rulebase` and `rulebase_link` tables).
  - Import module migrated from mixed python/pgsql to pure python.
