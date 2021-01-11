
ALTER TABLE "report_schedule_format" DROP CONSTRAINT IF EXISTS report_schedule_format_report_schedule_id_fkey; 

Alter table if exists "report_schedule_format" 
    add constraint report_schedule_format_report_schedule_id_fkey foreign key ("report_schedule_id") references "report_schedule" ("report_schedule_id") 
    on update restrict on delete cascade;
