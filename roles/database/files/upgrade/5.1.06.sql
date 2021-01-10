
Alter table "report_schedule_format" add foreign key ("report_schedule_id") references "report_schedule" ("report_schedule_id") on update restrict on delete cascade;
