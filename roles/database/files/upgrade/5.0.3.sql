Alter table "config" add  column if not exists "config_user" Integer;
Alter table "config" drop constraint if exists "config_pkey";
Alter table "config" add primary key ("config_key","config_user");

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'config_config_user_fkey')
  THEN
    Alter table "config" add foreign key ("config_user") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
  END IF;
END $$;
