
-- default report templates belong to user 0 
UPDATE "report_template" SET "report_template_owner" = 0; -- defining all templates to be default templates
-- make schedule owner mandatory
Alter table "report_schedule" ALTER COLUMN "report_schedule_owner" SET NOT NULL;

Alter table "report" ALTER COLUMN "report_pdf" TYPE TEXT;
