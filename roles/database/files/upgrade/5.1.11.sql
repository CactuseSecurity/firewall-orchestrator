
Create table IF NOT EXISTS  "rule_metadata"
(
	"rule_metadata_id" BIGSERIAL,
	"mgm_id" Integer NOT NULL,
	"rule_uid" Text,
	"rule_created" Timestamp NOT NULL Default now(),
	"rule_last_modified" Timestamp NOT NULL Default now(),
	"rule_first_hit" Timestamp,
	"rule_last_hit" Timestamp,
	"rule_hit_counter" BIGINT,
	"rule_last_certified" Timestamp,
	"rule_last_certifier" Integer,
	"rule_owner" Integer,
	"rule_group_owner" Varchar, -- distinguished name pointing to ldap group
	"rule_to_be_removed" Boolean NOT NULL Default FALSE,
	"last_change_admin" Integer,
 primary key ("rule_metadata_id")
);

Grant update,insert on "rule_metadata" to group "configimporters";

CREATE OR REPLACE FUNCTION initial_rule_metadata_filling () RETURNS BOOLEAN AS $$
DECLARE
    r_rule RECORD;
    v_rule_uid VARCHAR;
BEGIN
	FOR r_rule IN -- for every existing rule, add entry to rule_metadata table (just once per uid)
		SELECT rule_uid, mgm_id FROM rule 
		LOOP
			SELECT INTO v_rule_uid rule_uid FROM rule_metadata 
                WHERE rule_metadata.rule_uid=r_rule.rule_uid AND rule_metadata.mgm_id=r_rule.mgm_id;
            IF NOT FOUND THEN
                INSERT INTO rule_metadata (rule_uid, mgm_id) VALUES (r_rule.rule_uid, r_rule.mgm_id);
            END IF;
	END LOOP;
	RETURN TRUE;
END; 
$$ LANGUAGE plpgsql;

-- initially add all rule_uid/mgm_id pairs to rule_metadata, then add constraints
SELECT * FROM initial_rule_metadata_filling();

DROP FUNCTION IF EXISTS initial_rule_metadata_filling();

Alter table "rule_metadata" drop constraint if exists "rule_metadata_alt_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_management_mgm_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_rule_last_certifier_uiuser_uiuser_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_rule_owner_uiuser_uiuser_id_f_key" CASCADE;
Alter table "rule_metadata" drop constraint if exists "rule_metadata_mgm_id_rule_uid_f_key" CASCADE;

Alter Table "rule_metadata" add Constraint "rule_metadata_alt_key" UNIQUE ("rule_uid","mgm_id");

Alter table "rule_metadata" add constraint "rule_metadata_management_mgm_id_f_key"
    foreign key ("mgm_id") references "management" ("mgm_id") on update restrict on delete cascade;

Alter table "rule_metadata" add constraint "rule_metadata_rule_last_certifier_uiuser_uiuser_id_f_key"
  foreign key ("rule_last_certifier") references "uiuser" ("uiuser_id") on update restrict on delete cascade;

Alter table "rule_metadata" add constraint "rule_metadata_rule_owner_uiuser_uiuser_id_f_key"
  foreign key ("rule_owner") references "uiuser" ("uiuser_id") on update restrict on delete cascade;

Alter table "rule" add constraint "rule_metadata_mgm_id_rule_uid_f_key"
  foreign key ("mgm_id", "rule_uid") references "rule_metadata" ("mgm_id", "rule_uid") on update restrict on delete cascade;
