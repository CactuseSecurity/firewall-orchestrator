# Creating a new FWORCH version

## When a new version is necessary
if you make any changes to
- the database model
- the internal LDAP
- if you introduce a major feature

## How to create a new version
- All version number should be of the format a.b.c
- edit [documentation/revision-history.md](documentation/revision-history.md) and add the new version at the bottom
- edit [inventory/group_vars/all.yml](inventory/group_vars/all.yml) - set the variable product_name in line 5 to the new version number
- add an upgrade script as appropriate with the name of the version, e.g.  for a database change create

        roles/database/files/upgrade/5.5.2.sql
- this file should contain all changes in an idempotent manner, eg. for adding a new report template:

        INSERT INTO "report_template" ("report_filter","report_template_name","report_template_comment","report_template_owner") 
        VALUES ('type=natrules and time=now ','Current NAT Rules','T0105', 0) ON CONFLICT DO NOTHING;

- similarly for adding a new firewall device type:

        insert into stm_dev_typ (dev_typ_name,dev_typ_version,dev_typ_manufacturer,dev_typ_predef_svc) VALUES ('FortiManager','5ff','Fortinet','') ON CONFLICT DO NOTHING;

## Installation

If you want to upgrade an existing installation, see [advanced installer documentation](documentation/installer/install-advanced.md).