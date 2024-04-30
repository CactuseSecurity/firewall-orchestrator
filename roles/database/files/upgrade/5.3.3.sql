Alter table "ldap_connection" ADD COLUMN IF NOT EXISTS "ldap_name" Varchar;
Alter table "uiuser" ADD COLUMN IF NOT EXISTS "ldap_connection_id" BIGINT;

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'uiuser_ldap_connection_id_fkey')
  THEN
        Alter table "uiuser" add foreign key ("ldap_connection_id") references "ldap_connection" ("ldap_connection_id") on update restrict on delete cascade;
  END IF;
END $$;

