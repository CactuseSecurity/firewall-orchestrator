
-- Grant ALL on "import_service" to group "configimporters";
-- Grant ALL on "import_object" to group "configimporters";
-- Grant ALL on "import_user" to group "configimporters";
-- Grant ALL on "import_rule" to group "configimporters";
-- Grant ALL on "import_control" to group "configimporters";
-- Grant ALL on "import_zone" to group "configimporters";
-- Grant ALL on "import_changelog" to group "configimporters";

CREATE TABLE IF NOT EXISTS "import_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("import_id")
);

CREATE TABLE "import_full_config" (
    "import_id" bigint NOT NULL,
    "mgm_id" integer NOT NULL,
    "config" jsonb NOT NULL,
    PRIMARY KEY ("import_id")
);

ALTER TABLE "import_config"
    DROP CONSTRAINT IF EXISTS "import_config_import_id_f_key" CASCADE;
ALTER TABLE "import_config"
    ADD CONSTRAINT "import_config_import_id_f_key" FOREIGN KEY ("import_id") REFERENCES "import_control" ("control_id") ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE "import_config"
    DROP CONSTRAINT IF EXISTS "import_config_mgm_id_f_key" CASCADE;
ALTER TABLE "import_config"
    ADD CONSTRAINT "import_config_mgm_id_f_key" FOREIGN KEY ("mgm_id") REFERENCES "management" ("mgm_id") ON UPDATE RESTRICT ON DELETE CASCADE;

ALTER TABLE "import_full_config"
    DROP CONSTRAINT IF EXISTS "import_full_config_import_id_f_key" CASCADE;
ALTER TABLE "import_full_config"
    ADD CONSTRAINT "import_full_config_import_id_f_key" FOREIGN KEY ("import_id") REFERENCES "import_control" ("control_id") ON UPDATE RESTRICT ON DELETE CASCADE;
ALTER TABLE "import_full_config"
    DROP CONSTRAINT IF EXISTS "import_full_config_mgm_id_f_key" CASCADE;
ALTER TABLE "import_full_config"
    ADD CONSTRAINT "import_full_config_mgm_id_f_key" FOREIGN KEY ("mgm_id") REFERENCES "management" ("mgm_id") ON UPDATE RESTRICT ON DELETE CASCADE;

--- create index to enforce max 1 stop_time=null import per mgm
DROP INDEX IF EXISTS import_control_only_one_null_stop_time_per_mgm_when_null;
CREATE UNIQUE INDEX import_control_only_one_null_stop_time_per_mgm_when_null ON import_control (mgm_id) WHERE stop_time IS NULL;

-------------------
-- the following helper trigger creates the bigserial obj_id as it does not seem to be set automatically, 
-- when insert via jsonb function and specifying no obj_id

CREATE OR REPLACE FUNCTION import_object_obj_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.obj_id = coalesce(NEW.obj_id, nextval('import_object_obj_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS import_object_obj_id_seq ON import_object CASCADE;
CREATE TRIGGER import_object_obj_id_seq BEFORE INSERT ON import_object FOR EACH ROW EXECUTE PROCEDURE import_object_obj_id_seq();

-------------------
--  trigger that copies network objects from import_config to import_object
CREATE OR REPLACE FUNCTION import_config_from_jsonb ()
    RETURNS TRIGGER
    AS $BODY$
DECLARE
    import_id BIGINT;
BEGIN
    INSERT INTO import_object
    SELECT
        *
    FROM
        jsonb_populate_recordset(NULL::import_object, NEW.config -> 'network_objects');
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;

ALTER FUNCTION public.import_config_from_jsonb () OWNER TO fworch;

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;

CREATE TRIGGER import_config_insert
    BEFORE INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_jsonb ();
