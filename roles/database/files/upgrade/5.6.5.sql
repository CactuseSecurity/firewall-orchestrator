/*
  plan to avoid creating big json data for large managements:
    - replace import_config.import_id as primary key to be able to have more than one entry for an import_id
    - add max X entries per table (object/rule) into a single import_control entry
    - after adding all data, set a signal (e.g. import_control entry = { 'start_import': 'yes'} )
    - this will start the trigger for the import (need to collect data from all entries with import_id first)
*/

-- migrate config import tables (deleting all old configs)
DELETE FROM "import_config";
DELETE FROM "import_full_config";

ALTER TABLE "import_config" DROP CONSTRAINT IF EXISTS "import_config_pkey"; -- we now will have more than one entry per import
ALTER TABLE "import_config" DROP COLUMN IF exists "config";
ALTER TABLE "import_config" ADD COLUMN "config" jsonb NOT NULL;
ALTER TABLE "import_config" ADD COLUMN IF NOT EXISTS "start_import_flag" BOOLEAN DEFAULT FALSE;

ALTER TABLE "import_full_config" DROP COLUMN IF exists "config";
ALTER TABLE "import_full_config" ADD COLUMN "config" jsonb NOT NULL;

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;
-- DROP FUNCTION IF EXISTS import_config_from_json ();

CREATE OR REPLACE FUNCTION import_config_from_json ()
    RETURNS TRIGGER
    AS $BODY$
DECLARE
    import_id BIGINT;
    r_import_result RECORD;
BEGIN
    INSERT INTO import_object
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_object, NEW.config -> 'network_objects');

    INSERT INTO import_service
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_service, NEW.config -> 'service_objects');

    INSERT INTO import_user
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_user, NEW.config -> 'user_objects');

    INSERT INTO import_zone
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_zone, NEW.config -> 'zone_objects');

    INSERT INTO import_rule
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_rule, NEW.config -> 'rules');

    IF NEW.start_import_flag THEN
        -- finally start the stored procedure import
        PERFORM import_all_main(NEW.import_id);        
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;

ALTER FUNCTION public.import_config_from_json () OWNER TO fworch;

CREATE TRIGGER import_config_insert
    BEFORE INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_json ();



Create table IF NOT EXISTS "changelog_data_issue"
(
	"data_issue_id" BIGSERIAL,
	"import_id" BIGINT NOT NULL,
	"object_name" Varchar,
	"object_uid" Varchar,
	"rule_uid" Varchar,				-- if a rule ref is broken
	"rule_id" BIGINT,				-- if a rule ref is broken
	"dev_id" BIGINT,
	"object_type" Varchar,
	"suspected_cause" VARCHAR,
	"description" VARCHAR,
 primary key ("data_issue_id")
);


ALTER TABLE "changelog_data_issue"
    DROP CONSTRAINT IF EXISTS "changelog_data_issue_import_control_control_id_fkey" CASCADE;
ALTER TABLE "changelog_data_issue"
    ADD CONSTRAINT changelog_data_issue_import_control_control_id_fkey FOREIGN KEY ("import_id") REFERENCES "import_control" ("control_id") ON UPDATE RESTRICT; -- ON DELETE CASCADE;

CREATE OR REPLACE FUNCTION add_data_issue(BIGINT,varchar,varchar,varchar,varchar,BIGINT,INT,varchar,varchar,varchar) RETURNS VOID AS $$
DECLARE
	i_current_import_id ALIAS FOR $1;
	v_obj_name ALIAS FOR $2;
	v_obj_uid ALIAS FOR $3;
	v_rule_uid ALIAS FOR $4;
    i_rule_id  ALIAS FOR $5;
    i_dev_id   ALIAS FOR $6;
	v_obj_type ALIAS FOR $7;
	v_suspected_cause ALIAS FOR $8;
	v_description ALIAS FOR $9;
BEGIN
	INSERT INTO changelog_data_issue (import_id, object_name, object_uid, rule_uid, rule_id, dev_id, object_type, suspected_cause, description) 
	VALUES (i_current_import_id, v_obj_name, v_obj_uid, v_rule_uid, i_rule_id, v_obj_type, v_suspected_cause, v_description);
	RETURN;
END;
$$ LANGUAGE plpgsql;
