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
