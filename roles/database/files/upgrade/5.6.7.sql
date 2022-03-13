
ALTER TABLE "ldap_connection" ADD column IF NOT EXISTS "active" Boolean NOT NULL Default TRUE;

ALTER TABLE "report" ADD column IF NOT EXISTS "report_type" Integer;
ALTER TABLE "report" ADD column IF NOT EXISTS "description" varchar;

ALTER TABLE "report_schedule" ADD column IF NOT EXISTS "report_schedule_counter" Integer Not NULL Default 0;
