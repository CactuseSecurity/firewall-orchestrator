
Alter table "ldap_connection" ADD COLUMN IF NOT EXISTS "ldap_searchpath_for_groups" Varchar;
Alter table "ldap_connection" ADD COLUMN IF NOT EXISTS "ldap_type" Integer NOT NULL Default 0;
Alter table "ldap_connection" ADD COLUMN IF NOT EXISTS "ldap_pattern_length" Integer NOT NULL Default 0;
