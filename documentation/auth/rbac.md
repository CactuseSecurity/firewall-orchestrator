# Role-based access control Firewall Orchetrator

## Architecture and basic funtionality
- All users including password and roles a user belong to (role-mapping) are defined within ldap directories.
- All roles and their permissions are defined within the API.
- The user-tenant mapping is implemented in the LDAP hierarchy (either locally or on the external ldap).
- The user-role mapping is defined in the local LDAP hierarchy under role, adding users to a role with the "uniqueMember" attribute.
- The ldap directory can either be locally installed (based on open-ldap) or an external (eg. ActiveDirectory) ldap directory.
- Roles are non-additive (meaning a user can only have permissions belonging to a single role at a given time). But the user can switch roles during a workflow without changing the JWT by sending an addtional HTTP header x-hasura-role.

## Roles

The following roles are defined in ascending order of permissions:
- anonymous - anonymous users can only access the login page and health statistics
- middleware-server - allows the middleware server to read necessary tables (ldap_connection)
- reporter - reporters have access to basic tables (stm_...) and limited rights for object and rule tables depending on the visible devices for the tenant the user belongs to.
- reporter-viewall - reporter role for full read access to all devices
- importer - users can import config changes into the database
- dbbackup - users that are able to read data tables for backup purposes
- auditor - users that can view all data & settings (in the UI) but cannot make any changes
- workflow-user - (for future use) users who can request firewall changes
- workflow-admin - (for future use) users who can create change request workflows
- recertifier - users who can re-certify or de-certify firewall rules
- fw-admin - users who can document open changes
- admin - users with full access rights to firewall orchestrator (this is also the pre-defined hasura role 'admin')

The above mentioned access rights are implemented on the following levels 
1. as grants within the database. E.g. a reporter does not have the right to change any of the tables rule, object, service.
2. in the api as "permissions without restrictions"
3. in the api as "permissions with restrictions" on a tenant-level allowing access only to specifice managements and devices and objects/roles defined there (see next section)
4. in the UI controlling access to certain (admin-only) menus.

Just having the reporter role would mean a user can view basic tables like device types, service types in full but can only view those devices that are assigned via the tenant_to_device relation.

## Tenants
In addition there is the possiblity to restrict certain users to specific devices or managements. These granular rights are enforced via API access control for all tables that contain references to either management or device tables.

The default tenant "tenant0" is always defined and has access rights to all devices. The access rights have to be explicitly present in tenant_to_device table.

These tenant-based permissions are assigned during login as follows:
- The tenant(s) a user belongs to are read from an ldap directory.
- The devices a tenant has access to are read from the database table tenent_to_device.
- This information is written to a JWT (visible_devices, visible_managements) and signed by the Middleware-Module.

## LDAP - remote vs. local
- When using only the local LDAP server, the user <--> role matching is implemented with LDAP groups managed via the web user interface.
- When using a remote LDAP server, the user <--> role matching is done on the local ldap.

### Adding users

#### When creating a new user locally
The user does not get any role assignments to avoid any unwanted access rights.

#### When adding a new user or tenant from a remote LDAP server

Initially the user or tenant group does not get any role assignments to avoid any unwanted access rights.
An admin needs to manually assign role(s) to the user.

## Default users
- The default password of all users is "fworch.1" and needs to be changed when logging in for the first time.
- On each system an "admin" user, belonging to tenant0, is created with full access to everything. This user gets assigned the role "administrator". 
- For test installations (only when using the install switch -e "auth_add_test_user=<username>") a user called "username" is created with restricted access. This user gets assigned the roles "reporter" and "tenant1" allowing only access to the test fortigate system data and not to the check point system data.
