Authentication (OpenLDAP) is provisioned by the Ansible role in `roles/openldap-server`, with sample tenants/users/groups seeded by `roles/sample-auth-data`. These files define how LDAP is installed, configured, upgraded, and populated for tests or demo environments. The summaries below describe each code file under those roles so agents can quickly find the right entry points.

## OpenLDAP server role

### `roles/openldap-server/meta/main.yml`
Ansible Galaxy metadata for the role. It lists supported platforms and basic role metadata.

### `roles/openldap-server/defaults/main.yml`
Default OpenLDAP configuration variables. It sets domain, TLS paths, default ports, and baseline settings used by the tasks.

### `roles/openldap-server/vars/main.yml`
Role vars file that defines a small environment map. It is used for system-level configuration defaults.

### `roles/openldap-server/vars/Debian.yml`
OS-specific variables for Debian/Ubuntu. It defines package names and base paths for OpenLDAP on Debian-family systems.

### `roles/openldap-server/vars/RedHat.yml`
OS-specific variables for RedHat/CentOS. It defines package names and base paths for OpenLDAP on RedHat-family systems.

### `roles/openldap-server/handlers/main.yml`
Handler to restart the `slapd` service. It is triggered by configuration changes requiring a reload.

### `roles/openldap-server/files/DB_CONFIG`
Berkeley DB configuration template for the LDAP database. It sets cache size and log parameters used by the `slapd` backend.

### `roles/openldap-server/tasks/main.yml`
Primary task file to install and configure OpenLDAP. It creates the LDAP directory, generates root credentials, applies configuration templates, and starts `slapd` for new installs.

### `roles/openldap-server/tasks/run-upgrades.yml`
Upgrade orchestrator that selects and runs versioned upgrade tasks. It computes the applicable upgrade list based on installed and target versions.

### `roles/openldap-server/templates/config.ldif.j2`
LDIF template for initial `slapd` configuration. It defines schema includes, database setup, access controls, and overlays.

### `roles/openldap-server/templates/ldap.conf.j2`
Client-side LDAP configuration template. It sets the base DN, URI, and TLS settings based on role variables.

### `roles/openldap-server/templates/override.conf.j2`
Systemd override to start `slapd` with the configured LDAP/LDAPS listeners. It ensures correct runtime directories and user ownership.

## Sample auth data role

### `roles/sample-auth-data/defaults/main.yml`
Defaults for the sample auth data role. It provides flags to toggle sample data behavior for test scenarios.

### `roles/sample-auth-data/tasks/main.yml`
Main task file to seed sample tenants, LDAP tree entries, and owner data. It conditions execution on installation mode or test runs and restarts the middleware if LDAP connections change.

### `roles/sample-auth-data/tasks/auth_sample_data.yml`
Seeds sample tenants and mappings in PostgreSQL. It inserts tenant records and device/management mappings and adds demo tenant networks when requested.

### `roles/sample-auth-data/tasks/modify_ldap_tree.yml`
Applies LDIF templates to build the sample LDAP tree. It loads sample tenants, operators, and groups, and optionally assigns roles and group memberships.

### `roles/sample-auth-data/tasks/sample_owner_data.yml`
Seeds demo owner records and owner networks in PostgreSQL. It adds ownership entries tied to sample LDAP users and groups.

### `roles/sample-auth-data/templates/tree_sample_tenants.ldif.j2`
LDIF template for sample tenant OUs. It creates or deletes tenant organizational units based on the requested change type.

### `roles/sample-auth-data/templates/tree_sample_operators.ldif.j2`
LDIF template for sample operator users. It defines two test users with passwords and basic person attributes.

### `roles/sample-auth-data/templates/tree_sample_groups.ldif.j2`
LDIF template for sample groups and owner groups. It defines group entries with ownergroup metadata for recertification workflows.

### `roles/sample-auth-data/templates/tree_roles_for_sample_operators.ldif.j2`
LDIF template assigning sample users and owner groups to roles. It adds uniqueMember entries for reporter, recertifier, and modeller roles.

### `roles/sample-auth-data/templates/tree_groups_for_sample_operators.ldif.j2`
LDIF template assigning sample users to groups. It adds users to standard and owner groups for demo access.
