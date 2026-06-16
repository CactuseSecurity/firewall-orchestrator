# Firewall Orchestrator – Feature Catalogue

This catalogue lists the features of Firewall Orchestrator grouped by functional
area, together with the version in which they were first introduced. It is
compiled from the revision histories
([main](revision-history-main.md), [develop](revision-history-develop.md)).

Where a feature was first introduced on the `develop` branch and later shipped in
a `main` release, the released (main) version is given as the introduction
version and the develop version is noted in parentheses.

For a chronological, version-by-version listing (8.0 onwards) see
[version-feature-overview.md](version-feature-overview.md).

---

## Core platform & architecture

| Feature | Introduced in |
| --- | --- |
| First public open-source release | 5.3.1 |
| Move to Hasura GraphQL API v2.0 | 5.3.4 |
| Full API-based importer modules | 5.4.1 |
| NAT rules support in the data model | 5.5.1 |
| Internal representation of all IP addresses (incl. networks) as ranges | 7.3 (7.2.2) |
| Complete 80K-line rework: rule deduplication via `rulebase` / `rulebase_link` tables (1:n rule-to-gateway mapping) | 9.0 |
| Import module migrated from mixed python/pgsql to pure python | 9.0 |
| Flow schema | 9.1.0 |
| Optional workflow flow merging for Flow DB creation | 9.1.3 |

## Importers & firewall support

| Feature | Introduced in |
| --- | --- |
| Multi device manager for auto-discovery | 5.5.6 |
| Legacy FortiGate all-in-one device type (SSH) | 5.6.2 |
| Check Point R8x auto-discovery | 5.6.6 |
| Import firewall configs directly from URL | 5.6.9 |
| FortiManager import module (initial) | 5.4.2 |
| Cisco FirePower import module | 5.7.1 |
| Central credentials handling for import | 5.7.1 |
| Routing / interface import and path analysis (FortiNet) | 5.7.2 |
| Palo Alto import module | 6.2 (6.1.2) |
| Azure Firewall import module | 6.2 |
| Check Point R8X object types: application categories, updatable objects, domain names | 6.3 |
| Check Point R8X object type: application site | 6.3.1 |
| Check Point R8X object type: Internet | 6.3.2 |
| FortiGate import via FortiOS REST API | 6.4 (6.3.3) |
| FortiOS internet services support | 6.4.1 |
| Check Point R8x basic inline-layer support | 7.0 (6.4.4) |
| Check Point external-gateway / VSX cluster object support | 7.3 (7.2.5/7.2.6) |
| VMware NSX import module | 8.1 (8.0.2) |
| Custom (user-defined) import fields | 8.1 (8.0.3) |
| Tufin SecureChange integration (start) | 8.3 (8.2.2) |
| Check Point DLP actions (ask, inform) | 8.8.4 |
| Check Point `stm_track` "extended log" / "detailed log" | 8.8.6 |
| Import of time objects | 9.0 (9.0.6) |
| FortiOS VIP / destination-NAT objects normalized to external IP | 9.1.7 |

## Network Modelling

| Feature | Introduced in |
| --- | --- |
| Network Modelling module (target state per application / owner) | 8.0 (7.3.2) |
| Modeller role | 7.3 (7.2.4) |
| Interface request workflow | 8.2 (8.1.1) |
| Display NAs in Report LSB and export; area member counts | 8.2 |
| Import of app server IP addresses via CSV upload | 8.4.1 |
| Import of multiple sources for area IP data | 8.4.1 |
| Request modelling as firewall change via external ticketing tool | 8.5 |
| Application Zones | 8.6 |
| Monitoring / re-initialization / consolidation of external requests | 8.6 |
| Access request on behalf of UI user | 8.6 |
| Live update of external task/ticket status | 8.6 |
| Owner groups as external LDAP groups | 8.6 |
| App server naming via reverse DNS (fall-back to prefix + IP) | 8.7 (8.6.3) |
| Sortable application-role objects (by IP or name) | 8.7 |
| Deactivation of connections | 8.8 (8.8.3) |
| Decommissioning of interfaces | 8.8.x (8.8.9) |
| Interface permissions | 9.0 (9.0.3) |
| New modelling integration mode: WorkflowNotifications | 9.0.24 |

## Workflow & change requests

| Feature | Introduced in |
| --- | --- |
| Workflow module for requesting changes | 5.7.1 |
| Delete rule request with integrated path analysis | 6.2 |
| External state handling | 8.3 (8.3.1) |
| `automatic_only` workflow states | 9.0.18 |
| Configurable execution order for actions assigned to states | 9.1.1 |

## Recertification

| Feature | Introduced in |
| --- | --- |
| Recertifier role and recertification prototype | 5.2.5 |
| Owner-based recertification | 6.2 (6.1.1) |
| IP-based recertification | 6.2 |
| Rule ownership recertification | 7.3 (7.2.1) |
| Owner-recertification report type | 8.9.1 (8.8.10) |
| Owner lifecycle state (incl. manageable menu) | 8.9.2 |
| Owner additional_info JSONB field with owner edit UI | 9.0 (9.0.19) |
| Generalized owner responsibles with configurable responsible types | 9.0.1 |
| `allow_write_access` on responsible types (controls modelling & recertification) | 9.0.1 |

## Reporting

| Feature | Introduced in |
| --- | --- |
| Report scheduling | 5.0.1 |
| Default report templates | 5.1.04 |
| First compliance report template | 5.1.05 |
| NAT rules default report template | 5.5.2 |
| Resolved rules report (no group objects) | 5.7.2 |
| Resolved tech-info report (no names) | 5.8.2 |
| CSV export for change report | 6.3.2 |
| Lean JSON export for resolved / tech reports | 6.4 |
| Unused rules report incl. delete-ticket integration | 7.0 (6.5.1) |
| Last hit information in app-rule report | 8.4 |
| Connection report refinements (common service, app role, network area) | 8.6 |
| Variance report (first version) | 8.8 |
| Displayed state via variance analysis | 8.8 (8.8.2) |
| Display-only workflow label report column; "approved last week" default template | 9.0.23 |

## Compliance

| Feature | Introduced in |
| --- | --- |
| Compliance matrix module | 7.0 (6.5.0) |

## Tenant management

| Feature | Introduced in |
| --- | --- |
| Dedicated LDAP connections per tenant | 5.0.6 |
| Tenant network UI | 7.1 |
| Tenant IP-based filtering | 7.3 (7.2 beta) |
| Tenant simulation (incl. scheduling) | 7.3 |
| `unfiltered_managements` / `unfiltered` devices + extended tenant-to-device mapping | 8.0 (7.3.1/7.3.3) |

## Notifications

| Feature | Introduced in |
| --- | --- |
| Alerting on import attempts | 5.6.9 |
| Email notification on security-relevant import changes | 7.0 (6.4.6) |
| Scheduled import change notification with inline / attached change report | 8.0 (7.3.4) |
| Email recipients setting | 8.3 (8.2.4) |
| Email notification fall-back to main owner if group empty | 8.4.1 |
| Notification service | 8.8 (8.8.9) |
| Configurable rule-expiry notification | 9.0 (9.0.12) |
| Owner decommission notification | 9.0 (9.0.14) |
| Extended notification handling | 9.0 (9.0.20) |
| BCC on notifications | 9.0.23 |
| New notification parameters | 8.9.6 |

## Owner / application-data import

| Feature | Introduced in |
| --- | --- |
| Owner-import custom script | 8.3 |
| Owner LDAP selectable (internal / external) | 8.5 (8.5.3) |
| CSV app data import (stats, improvements) | 8.8 |
| Custom scripts for IIQ and CMDB import | 8.9.4 |
| User synchronization in owner data import (config value) | 9.0 (9.0.10) |
| `changelog_owner` table | 9.0 (9.0.7) |

## API

| Feature | Introduced in |
| --- | --- |
| Swagger REST API for user management (interactive docs) | 5.5.1 |
| Endpoint for creating JWTs with arbitrary lifetime | 5.7.2 |
| Customizing script for bulk configs via API | 8.1 |
| Endpoint for getting rules | 8.8 |
| `rule_owner` table for REST API | 9.0.1 |
| JWT refresh token | 9.1.0 |

## User interface

| Feature | Introduced in |
| --- | --- |
| Customizable UI texts | 7.3 (7.2.4) |
| Vanilla Bootstrap CSS v5.3.2 (new look & feel) | 8.0 (7.3.3) |
| Iconified modelling UI buttons (configurable per user) | 8.1 (8.0.1) |
| Login page welcome message and settings | 8.4 (8.3.2) |
| PDF generation engine moved from wkhtml to Puppeteer | 8.7 |
| Asynchronous initial JWT bootstrap; subscription-aware reconnect after JWT refresh | 9.1.5 |

## Security & hardening

| Feature | Introduced in |
| --- | --- |
| HTTPS reverse proxy in front of middleware server | 5.1.07 |
| Randomly generated secrets | 5.1.14 |
| All database credentials encrypted | 8.1 (8.0.3) |
| Encrypt emailPassword in config | 8.2 (8.1.2) |
| Read-only DB user `fwo_ro` | 8.8.8 |
| Hardening: apache config, listener restriction, log sanitisation | 8.8.8 |
| Security-hardening release (app-data path restrictions, secret handling, LDAP, Hasura perms, timeouts, webhook role removal) | 9.1.7 |

## Installer & platform

| Feature | Introduced in |
| --- | --- |
| Move temp dir from /tmp to /var/fworch/tmp | 5.2.3 |
| Support for Debian testing | 5.7.1 |
| venv for newer Ansible versions (installer rework) | 8.0 (7.3.5) |
| Maintenance page shown during upgrade | 8.0 (8.0.3) |
| Upgrade to .NET 8.0 (middleware & UI server) | 8.2 (8.1.2) |
| Move from Docker to Podman | 9.0.16 |
