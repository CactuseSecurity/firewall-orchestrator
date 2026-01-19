Middleware server (FWO.Middleware.Server) is the ASP.NET API and background job host that brokers UI and data layer traffic, issuing JWTs and coordinating scheduled tasks. It exposes REST endpoints for authentication, compliance, reporting, external requests, and tenant/group/user management. The summaries below describe each code file under roles/middleware/files/FWO.Middleware.Server/ to quickly find the right entry points.

## Startup and shared infrastructure

### `roles/middleware/files/FWO.Middleware.Server/Program.cs`
Entry point that configures server URLs, JWT writer, GraphQL connection, and LDAP subscriptions. It starts background schedulers, wires ASP.NET controllers/auth/Swagger, and runs the web host.

### `roles/middleware/files/FWO.Middleware.Server/SchedulerBase.cs`
Abstract base for timed jobs with config subscriptions, start/recurring timers, and standard exception handling. It also provides helper methods to log alerts and write log entries through the API.

### `roles/middleware/files/FWO.Middleware.Server/MiddlewareServerServices.cs`
Static helper functions used by middleware services, currently to pull internal owner groups from LDAP. It translates LDAP group membership into `UserGroup` objects with user DNs.

### `roles/middleware/files/FWO.Middleware.Server/JwtWriter.cs`
Generates JWTs for users and service roles with Hasura-specific claims. It builds claims from `UiUser` details, chooses default roles, and supports middleware/reporter tokens.

### `roles/middleware/files/FWO.Middleware.Server/UiUserHandler.cs`
Handles local UI user lifecycle tasks such as first-login upsert, session timeout lookup, and ownership mapping. It queries GraphQL config and uses LDAP group naming conventions to assign owner IDs.

### `roles/middleware/files/FWO.Middleware.Server/NotificationService.cs`
Loads notification rules and decides when owners should be notified. It builds emails with optional report attachments, resolves recipients, and updates last-sent timestamps.

## Schedulers

### `roles/middleware/files/FWO.Middleware.Server/AutoDiscoverScheduler.cs`
Scheduled job for device autodiscovery, triggered by config subscriptions. It runs autodiscovery per eligible management, creates alerts for actions, and logs per-management outcomes.

### `roles/middleware/files/FWO.Middleware.Server/ComplianceCheckScheduler.cs`
Runs compliance checks on a configurable interval. It constructs a compliance checker, persists results, and raises alerts on failures.

### `roles/middleware/files/FWO.Middleware.Server/DailyCheckScheduler.cs`
Daily job that inspects demo data, import health, and recertification tasks. It can refresh recert ownerships on startup and emits alerts/logs for issues found.

### `roles/middleware/files/FWO.Middleware.Server/ExternalRequestScheduler.cs`
Periodic job that sends pending external requests. It delegates to `ExternalRequestSender` and raises alerts when requests fail.

### `roles/middleware/files/FWO.Middleware.Server/ImportAppDataScheduler.cs`
Runs application data imports and optional DNS-based app server name adjustments. It uses `AppDataImport` and `AppServerHelper`, logging alerts on failures.

### `roles/middleware/files/FWO.Middleware.Server/ImportChangeNotifyScheduler.cs`
Triggers import-change notification processing at short intervals. It instantiates `ImportChangeNotifier` and logs alerts if notification processing fails.

### `roles/middleware/files/FWO.Middleware.Server/ImportIpDataScheduler.cs`
Schedules area IP/subnet imports from configured sources. It runs `AreaIpDataImport` and reports failures via alerts.

### `roles/middleware/files/FWO.Middleware.Server/ReportScheduler.cs`
Continuously checks report schedules and generates reports when due. It builds user-context JWTs, adapts filters, saves report files, and sends emails as configured.

### `roles/middleware/files/FWO.Middleware.Server/VarianceAnalysisScheduler.cs`
Scheduled job for modelling variance analysis. It generates a connections report and runs variance checks per owner, logging errors when analysis fails.

## Imports and modelling

### `roles/middleware/files/FWO.Middleware.Server/DataImportBase.cs`
Shared base class for importers with file reading, script execution, and log writing. It centralizes import logging to the API.

### `roles/middleware/files/FWO.Middleware.Server/AppDataImport.cs`
Imports application owners and app servers from JSON and optionally runs a preprocessing script. It reconciles apps with existing owners, manages LDAP groups, and logs counts for create/update/deactivate actions.

### `roles/middleware/files/FWO.Middleware.Server/AreaIpDataImport.cs`
Imports network area IP data from one or more JSON sources and merges them. It normalizes IP ranges, updates areas, deactivates missing ones, and logs outcomes.

### `roles/middleware/files/FWO.Middleware.Server/ImportChangeNotifier.cs`
Detects completed imports needing notification and optionally generates a change report. It prepares email content/attachments and marks imports as notified.

### `roles/middleware/files/FWO.Middleware.Server/ZoneMatrixDataImport.cs`
Handles compliance matrix imports from uploaded JSON. It creates or updates matrices and zones, manages zone connections, and logs detailed results.

### `roles/middleware/files/FWO.Middleware.Server/ImportNwZoneMatrixData.cs`
DTOs for compliance matrix import payloads, including matrix metadata, zones, and allowed communications. Used by `ZoneMatrixDataImport` for JSON deserialization.

### `roles/middleware/files/FWO.Middleware.Server/ModellingImportAppData.cs`
DTOs for imported application owners and app servers. Includes metadata fields and helper conversion to modelling app server objects.

### `roles/middleware/files/FWO.Middleware.Server/ModellingImportNwData.cs`
DTOs for imported network areas and subnets with cloning helpers. Serves as the payload model for area IP imports.

## LDAP handling

### `roles/middleware/files/FWO.Middleware.Server/LdapBasic.cs`
Core LDAP connection and search logic, including bind handling and filter construction. It retrieves user entries, validates credentials, and parses common attributes.

### `roles/middleware/files/FWO.Middleware.Server/LdapGroupHandling.cs`
LDAP group/role helpers for membership discovery and listing roles/groups. It normalizes DNs, queries group containers, and logs LDAP errors.

### `roles/middleware/files/FWO.Middleware.Server/LdapTenantHandling.cs`
LDAP tenant helpers for creating and deleting tenant organizational units. It performs write-user binds and logs failures.

## External requests and workflows

### `roles/middleware/files/FWO.Middleware.Server/ExternalRequestHandler.cs`
Coordinates external change request creation and progression based on internal workflow tickets. It resolves tickets, maps task state, patches request states, and triggers next requests or rejections.

### `roles/middleware/files/FWO.Middleware.Server/ExternalRequestSender.cs`
Processes and sends queued external requests to external ticket systems. It handles retries, status refresh, lock release, and updates request state/metadata.

## Recertification

### `roles/middleware/files/FWO.Middleware.Server/RecertCheck.cs`
Recertification workflow runner that calculates next check dates and sends notifications. It can run rule-by-rule checks or notification-based checks depending on configuration.

## Controllers

### `roles/middleware/files/FWO.Middleware.Server/Controllers/AuthenticationTokenController.cs`
REST endpoints to issue JWTs for user logins and admin-issued user tokens. It validates credentials via LDAP, resolves groups/roles/tenant info, and returns signed tokens.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/AuthenticationServerController.cs`
CRUD and test endpoints for LDAP connection configuration. It reads/writes LDAP connections via GraphQL and keeps the in-memory LDAP list in sync.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/ComplianceController.cs`
Endpoints for importing compliance matrices and generating compliance reports. It invokes `ZoneMatrixDataImport` and runs compliance checks to return CSV output.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/ExternalRequestController.cs`
Endpoints to create external requests and patch their state. It instantiates `ExternalRequestHandler` with a user config and forwards request operations.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/GroupController.cs`
Endpoints for listing, creating, deleting, updating, and searching LDAP groups. It operates on internal writable LDAPs and audits changes.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/NormalizedConfigController.cs`
Endpoint to generate normalized configuration snapshots. It builds a user-context GraphQL connection and returns serialized normalized config data.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/ReportController.cs`
Endpoint for ad-hoc report generation based on API parameters. It constructs user context, converts filters, and returns report JSON.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/RoleController.cs`
Endpoints to list roles and add/remove users from roles. It operates across LDAPs that support role handling and logs audit events.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/TenantController.cs`
Endpoints to list, create, update, and delete tenants across LDAP and the local database. It coordinates LDAP OU changes with GraphQL updates.

### `roles/middleware/files/FWO.Middleware.Server/Controllers/UserController.cs`
Endpoints to list known users, search LDAP users, and add/update/delete users. It synchronizes LDAP changes with local database records and audit logs.
