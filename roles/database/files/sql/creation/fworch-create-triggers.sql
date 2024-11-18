
-------------------
-- the following triggers create the bigserial obj_id as it does not seem to be set automatically, 
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

CREATE OR REPLACE FUNCTION gw_interface_id_seq() RETURNS TRIGGER AS $$
BEGIN
  NEW.id = coalesce(NEW.id, nextval('gw_interface_id_seq'));
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS gw_interface_id_seq ON gw_interface CASCADE;
CREATE TRIGGER gw_interface_id_seq BEFORE INSERT ON gw_interface FOR EACH ROW EXECUTE PROCEDURE gw_interface_id_seq();

CREATE OR REPLACE FUNCTION gw_route_add() RETURNS TRIGGER AS $$
BEGIN
  NEW.id = coalesce(NEW.id, nextval('gw_route_id_seq'));
  SELECT INTO NEW.interface_id id FROM gw_interface 
    WHERE gw_interface.routing_device=NEW.routing_device AND gw_interface.name=NEW.interface AND gw_interface.ip_version=NEW.ip_version;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS gw_route_add ON gw_route CASCADE;
CREATE TRIGGER gw_route_add BEFORE INSERT ON gw_route FOR EACH ROW EXECUTE PROCEDURE gw_route_add();

-------------------

CREATE OR REPLACE FUNCTION import_config_from_json ()
    RETURNS TRIGGER
    AS $BODY$
DECLARE
    i_mgm_id INTEGER;
    i_count INTEGER;
BEGIN
    -- networking
    SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=NEW.import_id;
    -- before importing, delete all old interfaces and routes belonging to the current management:

	-- now re-insert the currently found interfaces: 
    SELECT INTO i_count COUNT(*) FROM  jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
    IF COUNT>0 THEN
        DELETE FROM gw_interface WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
        INSERT INTO gw_interface SELECT * FROM jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
    END IF;

    SELECT INTO i_count COUNT(*) FROM  jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routing');
    IF COUNT>0 THEN
        DELETE FROM gw_route WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
        -- now re-insert the currently found routes: 
        INSERT INTO gw_route SELECT * FROM jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routing');
    END IF;

    -- firewall objects and rules

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
        PERFORM import_all_main(NEW.import_id, NEW.debug_mode);        
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE;
ALTER FUNCTION public.import_config_from_json () OWNER TO fworch;

DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;

CREATE TRIGGER import_config_insert
    BEFORE INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_json ();

-------------------------
-- recert refresh trigger

-- create or replace function refresh_view_rule_with_owner()
-- returns trigger language plpgsql
-- as $$
-- begin
--     refresh materialized view view_rule_with_owner;
--     return null;
-- end $$;

-- drop trigger IF exists refresh_view_rule_with_owner_delete_trigger ON recertification CASCADE;

-- create trigger refresh_view_rule_with_owner_delete_trigger
-- after delete on recertification for each statement 
-- execute procedure refresh_view_rule_with_owner();

-- CREATE OR REPLACE FUNCTION refresh_view_rule_with_owner_deferred()
-- RETURNS TRIGGER AS $$
-- BEGIN
--     -- Call the refresh function only once at the end of the transaction
--     PERFORM refresh_view_rule_with_owner();
--     RETURN NULL;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE CONSTRAINT TRIGGER refresh_view_rule_with_owner_delete_trigger
-- AFTER DELETE ON recertification
-- DEFERRABLE INITIALLY DEFERRED
-- FOR EACH ROW
-- EXECUTE FUNCTION refresh_view_rule_with_owner_deferred();

-- -- function used during import of owner data
CREATE OR REPLACE FUNCTION recert_refresh_per_owner(i_owner_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_mgm    RECORD;
BEGIN
	BEGIN
		FOR r_mgm IN
			SELECT mgm_id, mgm_name FROM management
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (i_owner_id, r_mgm.mgm_id, NULL::TIMESTAMP);
		END LOOP;

	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_owner while handling management %', r_mgm.mgm_name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


-- CREATE OR REPLACE FUNCTION owner_change_triggered ()
--     RETURNS TRIGGER
--     AS $BODY$
-- BEGIN
--     IF NOT NEW.id IS NULL THEN
--         PERFORM recert_refresh_per_owner(NEW.id);
--     END IF;
--     RETURN NEW;
-- END;
-- $BODY$
-- LANGUAGE plpgsql
-- VOLATILE;
-- ALTER FUNCTION public.owner_change_triggered () OWNER TO fworch;


-- DROP TRIGGER IF EXISTS owner_change ON owner CASCADE;

-- CREATE TRIGGER owner_change
--     AFTER INSERT OR UPDATE OR DELETE ON owner
--     FOR EACH ROW
--     EXECUTE PROCEDURE owner_change_triggered ();


-- CREATE OR REPLACE FUNCTION owner_network_change_triggered ()
--     RETURNS TRIGGER
--     AS $BODY$
-- BEGIN
--     IF NOT NEW.owner_id IS NULL THEN
--         PERFORM recert_refresh_per_owner(NEW.owner_id);
--     END IF;
--     RETURN NEW;
-- END;
-- $BODY$
-- LANGUAGE plpgsql
-- VOLATILE;
-- ALTER FUNCTION public.owner_network_change_triggered () OWNER TO fworch;

-- DROP TRIGGER IF EXISTS owner_network_change ON owner_network CASCADE;

-- CREATE TRIGGER owner_network_change
--     AFTER INSERT OR UPDATE OR DELETE ON owner_network
--     FOR EACH ROW
--     EXECUTE PROCEDURE owner_network_change_triggered ();

-- -- LargeOwnerChange: uncomment to disable triggers (e.g. for large installations without recert needs)
-- -- ALTER TABLE owner DISABLE TRIGGER owner_change;
-- -- ALTER TABLE owner_network DISABLE TRIGGER owner_network_change;

