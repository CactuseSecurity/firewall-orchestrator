Alter table "ldap_connection" add column "ldap_tenant_id" Integer;

Alter table "ldap_connection" add  foreign key ("ldap_tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;

