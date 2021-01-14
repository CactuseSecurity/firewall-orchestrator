Alter table "report" DROP COLUMN IF EXISTS "report_filetype";

Alter table "report_schedule" ADD COLUMN IF NOT EXISTS "report_schedule_repetitions" Integer;
-- default = NULL = infinite
-- NULL --> infinite, 1 = just once, 2, ....
