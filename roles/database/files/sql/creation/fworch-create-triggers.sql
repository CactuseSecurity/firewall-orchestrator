
-------------------
-- the following trigger creates the bigserial obj_id as it does not seem to be set automatically, 
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
