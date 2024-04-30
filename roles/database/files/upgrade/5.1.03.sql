
Create table IF NOT EXISTS "report_format"
(
	"report_format_name" varchar not null,
 	primary key ("report_format_name")
);
INSERT INTO "report_format" ("report_format_name") VALUES ('json');
INSERT INTO "report_format" ("report_format_name") VALUES ('pdf');
INSERT INTO "report_format" ("report_format_name") VALUES ('csv');
INSERT INTO "report_format" ("report_format_name") VALUES ('html');


Create table IF NOT EXISTS "report_schedule_format"
(
	"report_schedule_format_name" VARCHAR not null,
	"report_schedule_id" BIGSERIAL,
 	primary key ("report_schedule_format_name","report_schedule_id")
);

DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'report_schedule_format_report_schedule_format_name_fkey')
  THEN
	Alter table "report_schedule_format" add foreign key ("report_schedule_format_name") references "report_format" ("report_format_name") on update restrict on delete cascade;
  END IF;
END $$;


Alter table "report_template" ADD COLUMN IF NOT EXISTS "report_template_owner" Integer;
DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'report_template_report_template_owner_fkey')
  THEN
	Alter table "report_template" add foreign key ("report_template_owner") references "uiuser" ("uiuser_id") on update restrict on delete cascade;
  END IF;
END $$;

Alter table "report_schedule" ADD COLUMN IF NOT EXISTS "report_schedule_active" Boolean Default TRUE;

Alter table "report" ADD COLUMN IF NOT EXISTS "report_json" json NOT NULL;
Alter table "report" ADD COLUMN IF NOT EXISTS "report_pdf" bytea;
Alter table "report" DROP COLUMN IF EXISTS "report_document";
Alter table "report" ADD COLUMN IF NOT EXISTS "report_csv" text;
Alter table "report" ADD COLUMN IF NOT EXISTS "report_html" text;
Alter table "report" ALTER COLUMN "report_filetype" DROP NOT NULL;
Alter table "report" ADD COLUMN IF NOT EXISTS "tenant_wide_visible" Integer;
DO $$
BEGIN
  IF NOT EXISTS(select constraint_name 
    from information_schema.referential_constraints
    where constraint_name = 'report_tenant_wide_visible_fkey')
  THEN
	Alter table "report" add foreign key ("tenant_wide_visible") references "tenant" ("tenant_id") on update restrict on delete cascade;
  END IF;
END $$;

