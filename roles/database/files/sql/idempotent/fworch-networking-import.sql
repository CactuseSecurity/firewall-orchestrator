
CREATE OR REPLACE FUNCTION import_networking_main (BIGINT, BOOLEAN) RETURNS VOID AS $$
DECLARE
	i_current_import_id ALIAS FOR $1;
	b_is_initial_import ALIAS FOR $2;
	i_mgm_id  INTEGER;
	r_interface  RECORD;
	r_route  RECORD; 
BEGIN 
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;

	-- first delete all old interfaces belonging to the current management:
	DELETE FROM gw_interface WHERE routing_device IN (SELECT dev_id FROM device LEFT JOIN management USING (mgm_id) WHERE device.mgm_id=i_mgm_id); 

	-- first delete all old routes belonging to the current management:
	-- DELETE FROM gw_route WHERE routing_device IN (SELECT dev_id FROM device LEFT JOIN management USING (mgm_id) WHERE device.mgm_id=i_mgm_id); 

	-- now re-insert the currently found interfaces: 
    INSERT INTO gw_interface SELECT * FROM jsonb_populate_recordset(NULL::gw_interface, NEW.config -> 'interfaces');
	-- now re-insert the currently found routes: 
    -- INSERT INTO gw_route SELECT * FROM jsonb_populate_recordset(NULL::gw_route, NEW.config -> 'routes');

	RETURN;
END; 
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION import_zone_single(BIGINT,INTEGER,VARCHAR,BIGINT) RETURNS VOID AS $$
DECLARE
	control_id	ALIAS FOR $1;
	i_mgm_id	ALIAS FOR $2;
	v_zone_name	ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
	r_existing_zone	RECORD;
BEGIN
	RAISE DEBUG 'start import_zone_single for zone %', v_zone_name;
	-- zones need update of zone_last_seen
	SELECT INTO r_existing_zone zone_name FROM zone WHERE mgm_id=i_mgm_id AND zone_name=v_zone_name;
	IF FOUND THEN
		UPDATE zone set zone_last_seen=i_current_import_id, active=TRUE where mgm_id=i_mgm_id AND zone_name=v_zone_name;
	ELSE
		INSERT INTO zone (mgm_id,zone_name,zone_create,zone_last_seen)
		VALUES (i_mgm_id,v_zone_name,i_current_import_id,i_current_import_id);
	END IF;
	RAISE DEBUG 'end import_zone_single for zone %', v_zone_name;
    RETURN;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_zone_mark_deleted(BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
    i_current_import_id	ALIAS FOR $1;
    i_mgm_id			ALIAS FOR $2;
    i_import_admin_id	BIGINT;
	i_previous_import_id  BIGINT; -- zum Holen der import_ID des vorherigen Imports fuer das Mgmt
BEGIN
	i_previous_import_id := get_previous_import_id_for_mgmt(i_mgm_id,i_current_import_id);
	IF NOT i_previous_import_id IS NULL THEN -- wenn das Management nicht zum ersten Mal importiert wurde
		UPDATE zone SET active='FALSE' WHERE mgm_id=i_mgm_id AND active AND zone_last_seen<i_current_import_id;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;
