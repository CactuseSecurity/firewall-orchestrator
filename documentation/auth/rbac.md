# Role-based access control Firewall Orchetrator

## Architecture and basic funtionality
- All users including password and roles a user belong to (role-mapping) are defined within ldap directories.
- The ldap directory can either a the locally installed one (based on open-ldap) or an external (company, eg. active directory) ldap directory.
- All roles and their permissions are defined within the database.
- A user can have multiple roles - permissions from these roles are combined (added).

## Basic roles
The following (basic) database roles are defined in ascending order of user rights:
- anonymous - anonymous users can only access the login page and the roles tables to get more granular permissions
- reporter - reporters have basic reporting rights (regarding e.g. basic tables, not object/rule tables)
- reporter-\<tenant-x\> - granular rights may be assigned using specific roles that can only view certain managements and/or devices (e.g. belonging to tenant x)
- reporter-viewall - reporter role for full read access to all devices
- importer - users can import config changes into the database
- dbbackup - users that are able to read data tables for backup purposes
- auditor - users that can view all data & settings but cannot make any changes
- workflow user - (for future use) all users who can request firewall changes
- workflow admin - (for future use) all users who can create change request workflows
- fw-admin - all users who can document open changes
- administrator - all users who have full access rights to firewall orchestrator

The above mentioned access rights are implemented on two levels 

a) as grants within the database. E.g. a reporter does not have the right to change any of the following tables:

        - rule
        - object
        - service
        - ...
b) on a per-device level allowing access only to specifice managements and devices and objects/roles defined there (see next section)


## Custom role based permissions
In addition there is the possiblity to restrict certain users to specific devices or managements. These granular rights are enforced via API access control for all tables that contain references to either management or device tables.

This has to be defined in the following database tables:
- role
- role_to_device

## LDAP - remote vs. local
- When using only the local LDAP server, the user <--> role matching is implemented with LDAP groups managed via the web user interface.
- When using a remote LDAP server, the user <--> role matching can be either
- In both cases the roles need to be added to the role table.

### Adding users

#### When creating a new user locally
The user does not get any role assignments to avoid any unwanted access rights.

#### When adding a new user or user group from a remote LDAP server

The user or user group does not get any role assignments to avoid any unwanted access rights.

The user is created in the local LDAP server as well but does not get any password to make sure, the credential checking is done remotely every time the user logs in (TODO: needs to be checked if this works!).

## Default users
- On each system an "admin" user is created with full access to everything. This user gets assigned the role "administrator". The default password of the "admin" user is "fworch.1" and needs to be changed when logging in for the first time.
- For test installations (only when using the install switch -e "auth_add_test_user=<username>") a user called "username" is created with restricted access. This user gets assigned the roles "reporter" and "fg_reporter" allowing only access to the test fortigate system data and not to the check point system data. The default password of the user is "fworch.1" and needs to be changed when logging in for the first time.