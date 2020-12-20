Alter table "ldap_connection" ADD COLUMN "tenant_id" INTEGER;
-- add foreign key ldap_connection --> tenant
Alter table "ldap_connection" add foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
