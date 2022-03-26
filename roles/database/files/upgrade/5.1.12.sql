Alter table "rule_metadata" drop constraint if exists "rule_metadata_alt_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_management_mgm_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_device_dev_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_rule_last_certifier_uiuser_uiuser_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_rule_owner_uiuser_uiuser_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_mgm_id_rule_uid_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_dev_id_rule_uid_f_key" CASCADE;

Alter table "rule_metadata" ADD COLUMN IF NOT EXISTS "dev_id" Integer;

Alter table "rule_metadata" DROP COLUMN IF EXISTS "mgm_id";

-- Grant update,insert on "rule_metadata" to group "configimporters";

CREATE OR REPLACE FUNCTION initial_rule_metadata_filling () RETURNS BOOLEAN AS $$
DECLARE
    r_rule RECORD;
    v_rule_uid VARCHAR;
BEGIN
	DELETE FROM rule_metadata; -- delete old entries based on mgm_id mapping
	FOR r_rule IN -- for every existing rule, add entry to rule_metadata table (just once per uid)
		SELECT rule_uid, dev_id FROM rule 
		LOOP
			SELECT INTO v_rule_uid rule_uid FROM rule_metadata 
                WHERE rule_metadata.rule_uid=r_rule.rule_uid AND rule_metadata.dev_id=r_rule.dev_id;
            IF NOT FOUND THEN
                INSERT INTO rule_metadata (rule_uid, dev_id) VALUES (r_rule.rule_uid, r_rule.dev_id);
            END IF;
	END LOOP;
	RETURN TRUE;
END; 
$$ LANGUAGE plpgsql;

-- initially add all rule_uid/mgm_id pairs to rule_metadata, then add constraints
SELECT * FROM initial_rule_metadata_filling();

DROP FUNCTION IF EXISTS initial_rule_metadata_filling();

Alter table "rule_metadata" ALTER COLUMN "dev_id" SET NOT NULL;

Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","dev_id");

Alter table "rule_metadata" add constraint "rule_metadata_device_dev_id_f_key"
    foreign key ("dev_id") references "device" ("dev_id") on update restrict on delete cascade;

Alter table "rule" add constraint "rule_metadata_dev_id_rule_uid_f_key"
  foreign key ("dev_id", "rule_uid") references "rule_metadata" ("dev_id", "rule_uid") on update restrict on delete cascade;

------------- rule_order

DROP TABLE IF EXISTS "rule_order" CASCADE;
DROP table IF EXISTS "temp_table_for_tenant_filtered_rule_ids" CASCADE;
DROP table IF EXISTS "temp_filtered_rule_ids" CASCADE;
DROP table IF EXISTS "temp_mgmid_importid_at_report_time" CASCADE;
