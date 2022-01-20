
-------------------
-- the following triggers creates the bigserial obj_id as it does not seem to be set automatically, 
-- when insert via json function and specifying no obj_id

CREATE OR REPLACE FUNCTION import_object_obj_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.obj_id = coalesce(NEW.obj_id, nextval('import_object_obj_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS import_object_obj_id_seq ON import_object CASCADE;
CREATE TRIGGER import_object_obj_id_seq BEFORE INSERT ON import_object FOR EACH ROW EXECUTE PROCEDURE import_object_obj_id_seq();

CREATE OR REPLACE FUNCTION import_service_svc_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.svc_id = coalesce(NEW.svc_id, nextval('import_service_svc_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS import_service_svc_id_seq ON import_service CASCADE;
CREATE TRIGGER import_service_svc_id_seq BEFORE INSERT ON import_service FOR EACH ROW EXECUTE PROCEDURE import_service_svc_id_seq();

CREATE OR REPLACE FUNCTION import_user_user_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.user_id = coalesce(NEW.user_id, nextval('import_user_user_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS import_user_user_id_seq ON import_user CASCADE;
CREATE TRIGGER import_user_user_id_seq BEFORE INSERT ON import_user FOR EACH ROW EXECUTE PROCEDURE import_user_user_id_seq();

CREATE OR REPLACE FUNCTION import_rule_rule_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.rule_id = coalesce(NEW.rule_id, nextval('import_rule_rule_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS import_rule_rule_id_seq ON import_rule CASCADE;
CREATE TRIGGER import_rule_rule_id_seq BEFORE INSERT ON import_rule FOR EACH ROW EXECUTE PROCEDURE import_rule_rule_id_seq();

-------------------

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

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;

CREATE TRIGGER import_config_insert
    BEFORE INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_json ();
