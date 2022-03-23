
ALTER TABLE "ldap_connection" ADD column IF NOT EXISTS "active" Boolean NOT NULL Default TRUE;

ALTER TABLE "report" ADD column IF NOT EXISTS "report_type" Integer;
ALTER TABLE "report" ADD column IF NOT EXISTS "description" varchar;

ALTER TABLE "report_schedule" ADD column IF NOT EXISTS "report_schedule_counter" Integer Not NULL Default 0;

DO $$
BEGIN
IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='management' and column_name='ssh_private_key')
THEN
    ALTER TABLE "management" RENAME COLUMN "ssh_private_key" TO "secret";
END IF;
END $$;
