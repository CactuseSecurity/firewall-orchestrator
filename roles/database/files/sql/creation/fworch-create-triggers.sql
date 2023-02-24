
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
BEGIN
    -- networking
    IF NEW.chunk_number=0 THEN -- delete all networking data only when starting import, not for each chunk
        SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=NEW.import_id;
        -- before importing, delete all old interfaces and routes belonging to the current management:
        DELETE FROM gw_route WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
        DELETE FROM gw_interface WHERE routing_device IN 
            (SELECT dev_id FROM device LEFT JOIN management ON (device.mgm_id=management.mgm_id) WHERE management.mgm_id=i_mgm_id);
    END IF;

	-- now re-insert the currently found interfaces: 
    INSERT INTO gw_interface SELECT * FROM jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
	-- now re-insert the currently found routes: 
    INSERT INTO gw_route SELECT * FROM jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routing');

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
VOLATILE
COST 100;
ALTER FUNCTION public.import_config_from_json () OWNER TO fworch;


DROP TRIGGER IF EXISTS import_config_insert ON import_config CASCADE;

CREATE TRIGGER import_config_insert
    BEFORE INSERT ON import_config
    FOR EACH ROW
    EXECUTE PROCEDURE import_config_from_json ();



---------------------------------------------------------------------------
-- owner changes


-- fundamental function to check owner <--> rule mapping using the existing view
-- "view_rule_with_owner"
CREATE OR REPLACE FUNCTION recert_owner_responsible_for_rule (i_owner_id INTEGER, i_rule_id BIGINT) RETURNS BOOLEAN AS $$
DECLARE
	i_id BIGINT;
BEGIN
	-- check if this is the super owner:
	SELECT INTO i_id id FROM owner WHERE id=i_owner_id AND is_default;
	IF FOUND THEN -- this is the super owner
		SELECT INTO i_id rule_id FROM view_rule_with_owner WHERE owner_id IS NULL AND rule_id=i_rule_id;
		IF FOUND THEN
			RAISE DEBUG '%', 'rule found for super owner ' || i_rule_id;
			RETURN TRUE;
		ELSE
			RETURN FALSE;
		END IF;
	ELSE -- standard owner
		SELECT INTO i_id rule_id FROM view_rule_with_owner WHERE owner_id=i_owner_id AND rule_id=i_rule_id;
		IF FOUND THEN
			RETURN TRUE;
		ELSE
			RETURN FALSE;
		END IF;
	END IF;
END;
$$ LANGUAGE plpgsql;

-- this function deletes existing (future) open recert entries and inserts the new ones into the recertificaiton table
-- the new recert date will only replace an existing one, if it is closer (smaller)
CREATE OR REPLACE FUNCTION recert_refresh_one_owner_one_mgm
	(i_owner_id INTEGER, i_mgm_id INTEGER, t_requested_next_recert_date TIMESTAMP) RETURNS VOID AS $$
DECLARE
	r_rule   RECORD;
	i_recert_entry_id BIGINT;
	b_super_owner BOOLEAN := FALSE;
	t_rule_created TIMESTAMP;
	t_current_next_recert_date TIMESTAMP;
	t_next_recert_date_by_interval TIMESTAMP;
	t_rule_last_recertified TIMESTAMP;
	t_next_recert_date TIMESTAMP;
	i_recert_inverval INTEGER;
	b_never_recertified BOOLEAN := FALSE;
	b_no_current_next_recert_date BOOLEAN := FALSE;
	b_super_owner_exists BOOLEAN := FALSE;
	i_previous_import BIGINT;
	i_current_import_id BIGINT;
	i_super_owner_id INT;
	i_current_owner_id_tmp INT;
BEGIN
	IF i_owner_id IS NULL OR i_mgm_id IS NULL THEN
		IF i_owner_id IS NULL THEN
			RAISE WARNING 'found undefined owner_id in recert_refresh_one_owner_one_mgm';
		ELSE -- mgm_id NULL
			RAISE WARNING 'found undefined mgm_id in recert_refresh_one_owner_one_mgm';
		END IF;
	ELSE
		-- get id of previous import:
		SELECT INTO i_current_import_id control_id FROM import_control WHERE mgm_id=i_mgm_id AND stop_time IS NULL;
		SELECT INTO i_previous_import * FROM get_previous_import_id_for_mgmt(i_mgm_id,i_current_import_id);
		IF NOT FOUND OR i_previous_import IS NULL THEN
			i_previous_import := -1;	-- prevent match for previous import
		END IF;

		SELECT INTO i_super_owner_id id FROM owner WHERE is_default;
		IF FOUND THEN 
			b_super_owner_exists := TRUE;
		END IF;

		SELECT INTO i_current_owner_id_tmp id FROM owner WHERE id=i_owner_id AND is_default;
		IF FOUND THEN 
			b_super_owner := TRUE;
		END IF;

		SELECT INTO i_recert_inverval recert_interval FROM owner WHERE id=i_owner_id;

		FOR r_rule IN
		SELECT rule_uid, rule_id FROM rule WHERE mgm_id=i_mgm_id AND (active OR NOT active AND rule_last_seen=i_previous_import)
		LOOP

			IF recert_owner_responsible_for_rule (i_owner_id, r_rule.rule_id) THEN

				-- collects dates
				SELECT INTO t_current_next_recert_date next_recert_date FROM recertification 
				WHERE owner_id=i_owner_id AND rule_id=r_rule.rule_id AND recert_date IS NULL;

				IF NOT FOUND THEN
					b_no_current_next_recert_date := TRUE;
				END IF;

				SELECT INTO t_rule_last_recertified MAX(recert_date)
					FROM recertification
					WHERE rule_id=r_rule.rule_id AND NOT recert_date IS NULL;

				IF NOT FOUND OR t_rule_last_recertified IS NULL THEN	-- no prior recertification, use initial rule import date 
					b_never_recertified := TRUE;
					SELECT INTO t_rule_created rule_metadata.rule_created
						FROM rule
						LEFT JOIN rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
						WHERE rule_id=r_rule.rule_id;
				END IF;

				IF t_requested_next_recert_date IS NULL THEN
					-- if the currenct next recert date is before the intended fixed input date, ignore it
					IF b_never_recertified THEN
						t_next_recert_date := t_rule_created + make_interval (days => i_recert_inverval);
					ELSE
						t_next_recert_date := t_rule_last_recertified + make_interval (days => i_recert_inverval);
					END IF;
				ELSE
					t_next_recert_date := t_requested_next_recert_date;
				END IF;

				-- do not set next recert date later than actually calculated date
				IF NOT b_no_current_next_recert_date THEN
					IF t_next_recert_date>t_current_next_recert_date THEN
						t_next_recert_date := t_current_next_recert_date;
					END IF;
				END IF;

				-- delete old recert entry:
				DELETE FROM recertification WHERE owner_id=i_owner_id AND rule_id=r_rule.rule_id AND recert_date IS NULL;

				-- add new recert entry:
				IF b_super_owner THEN	-- special case for super owner (convert NULL to ID)
					INSERT INTO recertification (rule_metadata_id, next_recert_date, rule_id, ip_match, owner_id)
						SELECT rule_metadata_id, 
							t_next_recert_date AS next_recert_date,
							rule_id, 
							matches as ip_match, 
							i_owner_id AS owner_id
						FROM view_rule_with_owner 
						LEFT JOIN rule USING (rule_id)
						LEFT JOIN rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
						WHERE view_rule_with_owner.rule_id=r_rule.rule_id AND view_rule_with_owner.owner_id IS NULL;
				ELSE
					INSERT INTO recertification (rule_metadata_id, next_recert_date, rule_id, ip_match, owner_id)
						SELECT rule_metadata_id, 
							t_next_recert_date AS next_recert_date,
							rule_id, 
							matches as ip_match, 
							i_owner_id AS owner_id
						FROM view_rule_with_owner 
						LEFT JOIN rule USING (rule_id)
						LEFT JOIN rule_metadata ON (rule.rule_uid=rule_metadata.rule_uid AND rule.dev_id=rule_metadata.dev_id)
						WHERE view_rule_with_owner.rule_id=r_rule.rule_id AND view_rule_with_owner.owner_id=i_owner_id;
				END IF;
			ELSE
				-- delete old outdated recert entry if owner is not responsible any more 
				DELETE FROM recertification WHERE owner_id=i_owner_id AND rule_id=r_rule.rule_id AND recert_date IS NULL;
			END IF;
		END LOOP;

		-- finally, when not super user - recalculate super user recert entries - since these might change with each owner change
		IF NOT b_super_owner AND b_super_owner_exists THEN
			PERFORM recert_refresh_one_owner_one_mgm (i_super_owner_id, i_mgm_id, t_requested_next_recert_date);
		END IF;
	END IF;
END;
$$ LANGUAGE plpgsql;


-- function used during import of a single management config
CREATE OR REPLACE FUNCTION recert_refresh_per_management (i_mgm_id INTEGER) RETURNS VOID AS $$
DECLARE
	r_owner   RECORD;
BEGIN
	BEGIN		
		FOR r_owner IN
			SELECT id, name FROM owner
		LOOP
			PERFORM recert_refresh_one_owner_one_mgm (r_owner.id, i_mgm_id, NULL::TIMESTAMP);
		END LOOP;
	EXCEPTION WHEN OTHERS THEN
		RAISE EXCEPTION 'Exception caught in recert_refresh_per_management while handling owner %', r_owner.name;
	END;
	RETURN;
END;
$$ LANGUAGE plpgsql;


-- function used during import of owner data
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


CREATE OR REPLACE FUNCTION owner_change_triggered ()
    RETURNS TRIGGER
    AS $BODY$
BEGIN
    IF NOT NEW.id IS NULL THEN
        PERFORM recert_refresh_per_owner(NEW.id);
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;
ALTER FUNCTION public.owner_change_triggered () OWNER TO fworch;


DROP TRIGGER IF EXISTS owner_change ON owner CASCADE;

CREATE TRIGGER owner_change
    AFTER INSERT OR UPDATE OR DELETE ON owner
    FOR EACH ROW
    EXECUTE PROCEDURE owner_change_triggered ();


CREATE OR REPLACE FUNCTION owner_network_change_triggered ()
    RETURNS TRIGGER
    AS $BODY$
BEGIN
    IF NOT NEW.owner_id IS NULL THEN
        PERFORM recert_refresh_per_owner(NEW.owner_id);
    END IF;
    RETURN NEW;
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;
ALTER FUNCTION public.owner_network_change_triggered () OWNER TO fworch;

DROP TRIGGER IF EXISTS owner_network_change ON owner_network CASCADE;

CREATE TRIGGER owner_network_change
    AFTER INSERT OR UPDATE OR DELETE ON owner_network
    FOR EACH ROW
    EXECUTE PROCEDURE owner_network_change_triggered ();
