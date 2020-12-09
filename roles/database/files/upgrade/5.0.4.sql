
-- adding report owner (do not allow for sharing of generated reports yet)
Alter table "report" add column "report_owner_id" Integer Not Null;
Alter table "report" add foreign key ("report_owner_id") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
