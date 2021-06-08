
Alter table "rule_metadata" ALTER COLUMN "rule_uid" SET NOT NULL;

Alter table "rule_metadata" ADD COLUMN IF NOT EXISTS "rule_last_certifier_dn" Varchar;

DO $$
BEGIN
  IF EXISTS(SELECT *
    FROM information_schema.columns
    WHERE table_name='rule_metadata' and column_name='rule_group_owner')
  THEN
      ALTER TABLE "rule_metadata" RENAME COLUMN "rule_group_owner" TO "rule_owner_dn";
  END IF;
END;
$$ LANGUAGE plpgsql;
