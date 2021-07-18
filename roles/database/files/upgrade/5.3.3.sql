Alter table "ldap_connection" ADD COLUMN IF NOT EXISTS "ldap_name" Varchar;
Alter table "uiuser" ADD COLUMN IF NOT EXISTS "ldap_connection_id" BIGINT;
Alter table "uiuser" add foreign key ("ldap_connection_id") references "ldap_connection" ("ldap_connection_id") on update restrict on delete cascade;
