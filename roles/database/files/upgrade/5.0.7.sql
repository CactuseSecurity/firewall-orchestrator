
Alter table "report_schedule" ADD COLUMN IF NOT EXISTS "report_schedule_name" Varchar;

Alter table "report_template" DROP COLUMN IF EXISTS "report_typ_id";

DROP FUNCTION IF EXISTS get_report_typ_list(REFCURSOR);

Alter table "report_template" drop CONSTRAINT if exists report_template_report_typ_id_fkey;

drop index if exists "IX_Relationship201"; -- on "report_template" ("report_typ_id");

DROP table if exists "stm_report_typ";
