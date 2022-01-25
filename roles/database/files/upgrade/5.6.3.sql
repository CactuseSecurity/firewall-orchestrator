
-- migrate config import tables (deleting all old configs)
DELETE FROM "import_config";
DELETE FROM "import_full_config";
ALTER TABLE "import_config" DROP COLUMN IF exists "config";
ALTER TABLE "import_config" ADD COLUMN "config" json NOT NULL;
ALTER TABLE "import_full_config" DROP COLUMN IF exists "config";
ALTER TABLE "import_full_config" ADD COLUMN "config" json NOT NULL;

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;
DROP FUNCTION IF EXISTS import_config_from_jsonb ();

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
        json_populate_recordset(NULL::import_object, NEW.config -> 'network_objects');

    INSERT INTO import_service
    SELECT
        *
    FROM
        json_populate_recordset(NULL::import_service, NEW.config -> 'service_objects');

    INSERT INTO import_user
    SELECT
        *
    FROM
        json_populate_recordset(NULL::import_user, NEW.config -> 'user_objects');

    INSERT INTO import_zone
    SELECT
        *
    FROM
        json_populate_recordset(NULL::import_zone, NEW.config -> 'zone_objects');

    INSERT INTO import_rule
    SELECT
        *
    FROM
        json_populate_recordset(NULL::import_rule, NEW.config -> 'rules');

    -- finally start the stored procedure import
    PERFORM import_all_main(NEW.import_id);


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