Alter table "report" DROP COLUMN IF EXISTS "start_import_id";
Alter table "report" DROP COLUMN IF EXISTS "stop_import_id";
Alter table "report" DROP COLUMN IF EXISTS "report_generation_time";

-- foreign keys get dropped cascaded
-- ALTER TABLE "report" DROP CONSTRAINT IF EXISTS report_start_import_id_fkey; 
-- ALTER TABLE "report" DROP CONSTRAINT IF EXISTS report_stop_import_id_fkey;
