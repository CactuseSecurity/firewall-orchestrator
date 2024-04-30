
-- adding report owner (do not allow for sharing of generated reports yet)
Alter table "report" add column "report_owner_id" Integer Not Null;

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'report_report_owner_id_fkey')
  THEN
    Alter table "report" add foreign key ("report_owner_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
  END IF;
END $$;
