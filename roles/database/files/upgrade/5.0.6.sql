Alter table "ldap_connection" ADD COLUMN "tenant_id" INTEGER;
-- add foreign key ldap_connection --> tenant
DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'ldap_connection_tenant_id_fkey')
  THEN
        Alter table "ldap_connection" add foreign key ("tenant_id") references "tenant" ("tenant_id") on update restrict on delete cascade;
  END IF;
END $$;
