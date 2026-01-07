Data API and database provisioning are handled by the Ansible roles in `roles/api` (Hasura + Apache proxy) and `roles/database` (PostgreSQL). These files define how the API is installed, how metadata and scripts are managed, and how schema and upgrades are applied. The summaries below describe each code file under those roles.

## API role orchestration

### `roles/api/handlers/main.yml`
Rollback handler for API upgrades that restores backups and reruns the Hasura install tasks. It toggles rollback state, cleans backup data, and prints failure guidance when upgrades fail.

### `roles/api/tasks/main.yml`
Top-level API role tasks that stop services during upgrades, back up the API directory, and recreate install paths. It installs Apache and Hasura, runs upgrade tasks, and optionally generates API documentation.

### `roles/api/tasks/api-apache-install-and-setup.yml`
Installs Apache packages and required modules, optionally creating TLS certificates for new installs. It renders the API virtual host config, enables the site, opens the port, and restarts Apache.

### `roles/api/tasks/hasura-install.yml`
Installs Hasura dependencies, reads secrets, configures environment variables, and starts the Hasura container. It installs the Hasura CLI, writes a systemd unit, waits for readiness, and imports metadata.

### `roles/api/tasks/run-upgrades.yml`
Determines which API upgrade tasks apply between the installed and target versions. It includes each upgrade task file in version order.

### `roles/api/tasks/api-create-docu.yml`
Installs graphdoc via npm and uses it to generate GraphQL API documentation. The output is written to the UI's api schema directory for browsing.

## API templates and metadata

### `roles/api/templates/httpd.conf.j2`
Apache virtual host template that proxies /api to the local Hasura endpoint. It configures SSL termination, WebSocket upgrades, and security headers.

### `roles/api/templates/fworch-hasura-docker-api.service.j2`
Systemd unit template for running the Hasura Docker container as a service. It attaches to or starts the container on boot and defines stop handling.

### `roles/api/files/replace_metadata.json`
Hasura metadata payload used to replace API metadata during installation or rollback. It defines tracked tables, relationships, permissions, and other GraphQL settings.

## API scripts

### `roles/api/files/scripts/common_scripts.py`
Shared Python helpers for API scripts, defining logging setup and common delimiters. It standardizes debug logging behavior across the CLI tools.

### `roles/api/files/scripts/fwo-execute-graphql-query-with-vars.py`
CLI helper that executes a GraphQL query with variables loaded from JSON. It logs in via the middleware to obtain a JWT and calls the Hasura endpoint.

### `roles/api/files/scripts/fwo-execute-graphql.py`
CLI helper that authenticates via the middleware and executes a GraphQL query or mutation from a file. It reads API endpoints from fworch.json and supports optional SSL verification.

### `roles/api/files/scripts/fwo-export-config.py`
Exports device and management configuration via GraphQL into JSON or GraphQL mutation format. It authenticates via the middleware and formats output for later import.

### `roles/api/files/scripts/fwo-migrate-itsecorg-devices.py`
Converts CSV exports from itsecorg or fworch databases into GraphQL mutations for device import. It normalizes device types, filters invalid rows, and writes a mutation file.

## Database role orchestration

### `roles/database/tasks/main.yml`
Main database role tasks that install PostgreSQL, configure logging and pg_hba, and copy schema files. It then creates or upgrades the database and reapplies idempotent SQL scripts.

### `roles/database/tasks/install-database.yml`
Initializes database users and secrets, creates the database, and runs base schema scripts. It loads seed CSV data and triggers creation of the read-only user.

### `roles/database/tasks/upgrade-database.yml`
Selects the SQL upgrade scripts that apply between installed and target versions. It copies the relevant scripts and runs them in order via postgresql_script.

### `roles/database/tasks/create-users.yml`
Creates database groups and service users with appropriate role flags. It assigns group memberships for backup, importer, and admin accounts.

### `roles/database/tasks/create-ro-user.yml`
Creates the read-only database user and grants access across schemas. It also sets default privileges for future tables and sequences.

### `roles/database/tasks/run-unit-tests.yml`
Copies database SQL test scripts to the install directory and runs them against the database. It prints the aggregated unit test results and cleanup output.

### `roles/database/tasks/redhat_preps.yml`
Prepares RedHat systems by installing the PostgreSQL repo and locale prerequisites. It ensures RPM sources and UTF-8 support are ready before installation.

### `roles/database/tasks/unused-add-tablespace.yml`
Commented reference task file for tablespace creation and migration. It documents possible tablespace workflows but is not invoked by the role.

## Database utilities

### `roles/database/files/get_pg_version.sh`
Shell helper that extracts the installed PostgreSQL version string. It returns only the major version for Postgres 10+ to simplify comparisons.

### `roles/database/files/remove_all_containers.sh`
Utility script that stops and removes all Docker containers if Docker is installed. Used to clean up stale containers before database operations.

## Database schema creation

### `roles/database/files/sql/creation/fworch-create-constraints.sql`
Adds table constraints for the FWO schema. Run after tables are created to enforce data integrity.

### `roles/database/files/sql/creation/fworch-create-foreign-keys.sql`
Defines foreign key relationships across the schema. Applied after table creation to enforce referential integrity.

### `roles/database/files/sql/creation/fworch-create-indices.sql`
Creates indexes used to optimize query performance. Run after tables and constraints are in place.

### `roles/database/files/sql/creation/fworch-create-tables.sql`
Creates the core database tables for the FWO schema. Executed during new installs before constraints and indexes are applied.

### `roles/database/files/sql/creation/fworch-create-triggers.sql`
Creates database triggers used by the application. These triggers support auditing and derived data maintenance.

### `roles/database/files/sql/creation/fworch-fill-stm.sql`
Populates baseline STM or lookup data required by the platform. Executed during new installs after core tables are created.

### `roles/database/files/sql/creation/fworch-views-materialized.sql`
Defines materialized views used for reporting or performance. Built during initial schema creation.

## Database idempotent SQL

### `roles/database/files/sql/idempotent/fworch-api-funcs.sql`
Idempotent SQL script defining API helper functions. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-basic-procs.sql`
Idempotent SQL script defining basic procedures. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-encryption.sql`
Idempotent SQL script defining encryption helpers. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-grants.sql`
Idempotent SQL script defining role and schema grants. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-import-main.sql`
Idempotent SQL script defining main import orchestration helpers. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-import.sql`
Idempotent SQL script defining core import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-networking-import.sql`
Idempotent SQL script defining network import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-obj-import.sql`
Idempotent SQL script defining object import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-obj-refs.sql`
Idempotent SQL script defining object reference views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-path-analysis.sql`
Idempotent SQL script defining path analysis views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-qa.sql`
Idempotent SQL script defining QA and validation helpers. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-rule-import.sql`
Idempotent SQL script defining rule import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-rule-recert.sql`
Idempotent SQL script defining rule recertification helpers. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-rule-refs.sql`
Idempotent SQL script defining rule reference views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-rule-resolved.sql`
Idempotent SQL script defining resolved rule views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-svc-import.sql`
Idempotent SQL script defining service import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-svc-refs.sql`
Idempotent SQL script defining service reference views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-texts.sql`
Idempotent SQL script defining localization and text resources. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-usr-import.sql`
Idempotent SQL script defining user import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-usr-refs.sql`
Idempotent SQL script defining user reference views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-views-changes.sql`
Idempotent SQL script defining change reporting views. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/fworch-zone-import.sql`
Idempotent SQL script defining zone import routines. Reapplied during installs or upgrades to keep related objects in sync.

### `roles/database/files/sql/idempotent/unused_fworch-views-tenant.sql`
Unused idempotent SQL script for tenant views. Kept for reference but not applied unless explicitly invoked.

## Database maintenance SQL

### `roles/database/files/sql/maintenance/fworch-change-to-delete-cascade.sql`
Maintenance SQL that adjusts delete behavior to cascade across related tables. Used when migrating referential behavior during maintenance.

### `roles/database/files/sql/maintenance/fworch-cleanup.sql`
Maintenance SQL that cleans up database artifacts and stale records. Run manually or during maintenance windows as needed.

## Database test SQL

### `roles/database/files/sql/test/hasura-test.sql`
SQL test script focused on Hasura-facing schema objects. Executed by the database unit test task.

### `roles/database/files/sql/test/unit-test-cleanup.sql`
Cleanup SQL to remove test data created by unit tests. Ensures the database returns to a clean state after tests run.

### `roles/database/files/sql/test/unit-tests.sql`
Core database unit test script with assertions for schema behavior. Used by the automated database unit test task.

## Database seed data

### `roles/database/files/csv/color.csv`
Seed data listing STM color names and RGB values. Loaded during new database installs.

### `roles/database/files/csv/error.csv`
Seed data containing error identifiers and localized messages. Loaded during new database installs.

### `roles/database/files/csv/ip-protocol-list.csv`
Seed data listing IP protocol identifiers and names. Loaded during new database installs.

### `roles/database/files/csv/ns-predefined-services.csv`
Seed data defining predefined network services. Used to populate service catalogs during database setup.
