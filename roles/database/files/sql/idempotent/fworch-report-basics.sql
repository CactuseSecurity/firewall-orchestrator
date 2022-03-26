----------------------------------------------------
-- generate report functions
----------------------------------------------------
------- basics ------------
-- is_obj_group (obj_id) RETURNS BOOLEAN
-- is_rule_src_negated (rule_id) RETURNS BOOLEAN
-- is_rule_dst_negated (rule_id) RETURNS BOOLEAN
-- explode_objgrp (obj_id,import_id) RETURNS SETOF INTEGER
-- explode_objgrp (obj_id) RETURNS SETOF INTEGER
-- get_previous_import_id (dev_id, time)
-- get_previous_import_ids (time) 
-- get_matching_import_id(device_id, zeitpunkt) RETURNS INTEGER
-- get_exploded_src_of_rule(rule_id,import_id) RETURNS SETOF INTEGER
-- get_exploded_dst_of_rule(rule_id,import_id) RETURNS SETOF INTEGER
-- get_mgmt_dev_list(name-of-REFCURSOR) RETURNS REFCURSOR

------- advanced div ------------
-- get_rule_action (rule_id) RETURNS RECORD (action_id, action_name)
-- DROP FUNCTION get_request_str(VARCHAR,BIGINT);
CREATE OR REPLACE FUNCTION get_request_str(VARCHAR,BIGINT) RETURNS VARCHAR AS $$
DECLARE
	v_table	ALIAS FOR $1;
	i_id	ALIAS FOR $2;
	r_request RECORD;
	v_tbl	VARCHAR;
	v_result VARCHAR;
	v_id_name VARCHAR;
	v_sql_statement VARCHAR;
BEGIN
	v_result := '';
	-- IF v_table='object' THEN v_tbl := 'obj'; END IF;
	-- IF v_table='service' THEN v_tbl := 'svc'; END IF;
	-- IF v_table='user' THEN v_tbl := 'usr'; END IF;
	-- IF v_table='rule' THEN v_tbl := 'rule'; END IF;
	-- v_id_name := 'log_' || v_tbl || '_id';
	-- v_sql_statement := 'SELECT request_number, tenant_name, request_type_name FROM request_' ||
	-- 	v_table || '_change LEFT JOIN request USING (request_id) LEFT JOIN tenant USING (tenant_id) ' ||
	-- 	 ' LEFT JOIN request_type using (request_type_id) ' ||
	-- 	' WHERE ' || v_id_name || '=' || CAST(i_id AS VARCHAR);
	-- FOR r_request IN EXECUTE v_sql_statement
	-- LOOP
	-- 	IF v_result<>'' THEN v_result := v_result || '<br>'; END IF;
	-- 	IF NOT r_request.tenant_name IS NULL THEN
	-- 		v_result := v_result || r_request.tenant_name || ': ';
	-- 	END IF;
	-- 	IF NOT r_request.request_type_name IS NULL THEN
	-- 		v_result := v_result || r_request.request_type_name || '-';
	-- 	END IF;
	-- 	v_result := v_result || r_request.request_number;
		
	-- END LOOP;
	RETURN v_result;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    get_last_change_admin_of_rulebase_change(import_id, dev_id)
-- Zweck:       liefert den change_admin fuer einen Zeitpunkt und ein Device zurueck (fuer rule_deletes
--              benoetigt fuer rule_deletes
--              Annahme: ein Admin hat alle Rule-Changes an einer Rulebase zu einem Zeitpunkt gemacht
-- Parameter1:  import id
-- Parameter2:  device id
-- RETURNS:     id des change_admins
--

-- DROP FUNCTION get_last_change_admin_of_rulebase_change (BIGINT, INTEGER);
CREATE OR REPLACE FUNCTION get_last_change_admin_of_rulebase_change (BIGINT, INTEGER) RETURNS INTEGER AS
$BODY$
DECLARE
    i_import_id			ALIAS FOR $1;
    i_dev_id			ALIAS FOR $2;
    r_rule				RECORD;
    i_admin_counter		INTEGER;
BEGIN 

	SELECT INTO i_admin_counter COUNT(distinct import_admin) FROM changelog_rule
		WHERE control_id=i_import_id AND dev_id=i_dev_id AND NOT import_admin IS NULL GROUP BY import_admin;
	IF (i_admin_counter=1) THEN
		SELECT INTO r_rule import_admin FROM changelog_rule
			WHERE control_id=i_import_id AND dev_id=i_dev_id AND NOT import_admin IS NULL GROUP BY import_admin;
--		RAISE NOTICE 'Found last_change_admin %', r_rule.import_admin;
		IF FOUND THEN
			RETURN r_rule.import_admin;
		ELSE
			RETURN NULL;
		END IF;
	ELSE
		RETURN NULL;
	END IF;
END; 
$BODY$
  LANGUAGE 'plpgsql' VOLATILE;

----------------------------------------------------
-- FUNCTION:    get_last_change_admin_of_obj_delete(import_id, mgm_id)
-- Zweck:       liefert den change_admin fuer einen Import zurueck (fuer svc- nwobj u. usr_deletes
--              benoetigt fuer obj / svc / usr _deletes
--              Annahme: ein Admin hat alle Changes an einem Management zu einem Zeitpunkt gemacht
--						 wenn nicht, dann wird NULL zurueckgeliefert
-- Parameter1:  import id
-- RETURNS:     id des change_admins
--

-- DROP FUNCTION get_last_change_admin_of_obj_delete (BIGINT);
CREATE OR REPLACE FUNCTION get_last_change_admin_of_obj_delete (BIGINT) RETURNS INTEGER AS
$BODY$
DECLARE
    i_import_id			ALIAS FOR $1;
    r_obj				RECORD;
    i_admin_counter		INTEGER;
    i_admin_id			INTEGER;
BEGIN 
	i_admin_counter := 0;
	FOR r_obj IN
		SELECT import_admin FROM changelog_object WHERE control_id=i_import_id AND NOT import_admin IS NULL
		UNION
		SELECT import_admin FROM changelog_service WHERE control_id=i_import_id AND NOT import_admin IS NULL
		UNION		
		SELECT import_admin FROM changelog_user WHERE control_id=i_import_id AND NOT import_admin IS NULL
	LOOP
		i_admin_counter := i_admin_counter + 1;
		i_admin_id := r_obj.import_admin;
	END LOOP;
	IF (i_admin_counter=1) THEN
		RETURN i_admin_id;
	ELSE
		RETURN NULL;
	END IF;
END; 
$BODY$
LANGUAGE 'plpgsql' VOLATILE;

----------------------------------------------------
-- FUNCTION:    get_mgmt_dev_list(name-of-refcursor)
-- Zweck:       liefert Cursor mit allen Managements und Devices zurueck (Name u. ID)
-- Parameter1:  Name des zurueckzuliefernden Pointers
-- RETURNS:     Cursor mit Tabelle (mgmt_id,mgmt_name,dev_id,dev_name,manufacturer)
--
--DROP FUNCTION get_mgmt_dev_list(REFCURSOR);
CREATE OR REPLACE FUNCTION get_mgmt_dev_list(REFCURSOR) RETURNS REFCURSOR AS $$
DECLARE
BEGIN
	OPEN $1 FOR
		SELECT management.mgm_id,management.mgm_name,device.dev_id,device.dev_name,stm_dev_typ.dev_typ_manufacturer
			FROM management, device, stm_dev_typ
			WHERE management.mgm_id=device.mgm_id AND stm_dev_typ.dev_typ_id=device.dev_typ_id
			ORDER BY dev_typ_manufacturer,mgm_name,dev_name;
    RETURN $1;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    get_mgmt_list(name-of-refcursor)
-- Zweck:       liefert Cursor mit allen Managements zurueck (Name u. ID)
-- Parameter1:  Name des zurueckzuliefernden Pointers
-- RETURNS:     Cursor mit Tabelle (mgmt_id,mgmt_name,manufacturer)
--

-- DROP FUNCTION get_mgmt_list(REFCURSOR);
CREATE OR REPLACE FUNCTION get_mgmt_list(REFCURSOR) RETURNS REFCURSOR AS $$
DECLARE
BEGIN
	OPEN $1 FOR
		SELECT management.mgm_id,management.mgm_name,stm_dev_typ.dev_typ_manufacturer
			FROM management, stm_dev_typ
			WHERE management.dev_typ_id=stm_dev_typ.dev_typ_id
			ORDER BY dev_typ_manufacturer,mgm_name;
    RETURN $1;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    get_dev_list(name-of-refcursor,mgm_id)
-- Zweck:       liefert Cursor mit allen Device-Ids der zum Management gehoerigen Devices zurueck (ID)
-- Parameter1:  Name des zurueckzuliefernden Pointers
-- RETURNS:     Cursor mit Tabelle (dev_id)
--
-- DROP FUNCTION get_dev_list(REFCURSOR,INTEGER);
CREATE OR REPLACE FUNCTION get_dev_list(REFCURSOR,INTEGER) RETURNS REFCURSOR AS $$
DECLARE
	i_mgm_id ALIAS FOR $2;
BEGIN
	OPEN $1 FOR
		SELECT dev_id
			FROM device
			WHERE mgm_id=i_mgm_id;
    RETURN $1;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    get_report_typ_list(name-of-refcursor)
-- Zweck:       liefert Cursor mit allen Reporttypen zurueck (Name u. ID)
-- Parameter1:  Name des zurueckzuliefernden Pointers
-- RETURNS:     Cursor mit Tabelle (report_typ_id,report_typ_name)
--
-- DROP FUNCTION get_report_typ_list(REFCURSOR);
-- CREATE OR REPLACE FUNCTION get_report_typ_list(REFCURSOR) RETURNS REFCURSOR AS $$
-- DECLARE
-- 	r_config RECORD;
-- BEGIN
-- 	SELECT INTO r_config * FROM config;
-- 	IF r_config.language='german' THEN
-- 		OPEN $1 FOR
-- 			SELECT report_typ_id, report_typ_name
-- 				FROM stm_report_typ
-- 				ORDER BY report_typ_id;
-- --				ORDER BY report_typ_name;
-- 	ELSE
-- 		OPEN $1 FOR
-- 			SELECT report_typ_id, report_typ_name
-- 				FROM stm_report_typ
-- 				ORDER BY report_typ_id;
-- --				ORDER BY report_typ_name;
-- 	END IF;
--     RETURN $1;
-- END;
-- $$ LANGUAGE plpgsql;

-- CREATE OR REPLACE FUNCTION get_report_typ_list(REFCURSOR) RETURNS REFCURSOR AS $$
-- DECLARE
-- 	r_config RECORD;
-- BEGIN
-- 	SELECT INTO r_config * FROM config;
-- 	OPEN $1 FOR
-- 		SELECT report_typ_id,report_typ_name as report_typ_name
-- 			FROM stm_report_typ
-- 			ORDER BY report_typ_id;
--     RETURN $1;
-- END;
-- $$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:    get_tenant_list(name-of-refcursor)
-- Zweck:       liefert Cursor mit allen tenants zurueck (Name u. ID)
-- Parameter1:  Name des zurueckzuliefernden Pointers
-- Parameter2:  tenant-Id fuer spaetere Anzeige direkt fuer tenant
-- RETURNS:     Cursor mit Tabelle (tenant_id,tenant_name)
--
CREATE OR REPLACE FUNCTION get_tenant_list(REFCURSOR) RETURNS REFCURSOR AS $$
DECLARE
BEGIN
	OPEN $1 FOR SELECT tenant_id,tenant_name FROM tenant ORDER BY tenant_name;
    RETURN $1;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_exploded_src_of_rule(rule_id)
-- Zweck:		liefert alle in den Quellen enthalten object IDs zurueck
-- Zweck:		auch fuer alle Gruppen rekursiv
-- Parameter1:	rule_id der Regel
-- Parameter2:	relevante import_id
-- RETURNS:		alle obj_ids als Tabelle
--
CREATE OR REPLACE FUNCTION get_exploded_src_of_rule(BIGINT) RETURNS SETOF BIGINT AS $$
DECLARE
	i_rule_id ALIAS FOR $1;
--	i_import_id ALIAS FOR $2;
	r_obj	RECORD;
	r_obj2	RECORD;
BEGIN
	FOR r_obj IN
		SELECT obj_id FROM rule_from WHERE rule_id=i_rule_id -- AND rf_create<=i_import_id AND rf_last_seen>=i_import_id
	LOOP
		FOR r_obj2 IN
--			SELECT explode_objgrp AS obj_id FROM explode_objgrp(r_obj.obj_id,i_import_id)
			SELECT explode_objgrp AS obj_id FROM explode_objgrp(r_obj.obj_id)
		LOOP
			RETURN NEXT r_obj2.obj_id;
		END LOOP;
	END LOOP;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_exploded_dst_of_rule(rule_id,import_id)
-- Zweck:		liefert alle in den Zielen enthalten objeckt IDs zurueck
-- Zweck:		auch fuer alle Gruppen rekursiv
-- Parameter1:	rule_id der Regel
-- Parameter1:	relevante import_id
-- RETURNS:		alle obj_ids als Tabelle
--
CREATE OR REPLACE FUNCTION get_exploded_dst_of_rule(BIGINT) RETURNS SETOF BIGINT AS $$
DECLARE
	i_rule_id ALIAS FOR $1;
--	i_import_id ALIAS FOR $2;
	r_obj	RECORD;
	r_obj2	RECORD;
BEGIN
	FOR r_obj IN
		SELECT obj_id FROM rule_to WHERE rule_id=i_rule_id -- AND rt_create<=i_import_id AND rt_last_seen>=i_import_id
	LOOP
		FOR r_obj2 IN
--			SELECT explode_objgrp AS obj_id FROM explode_objgrp(r_obj.obj_id,i_import_id)
			SELECT explode_objgrp AS obj_id FROM explode_objgrp(r_obj.obj_id)
		LOOP
			RETURN NEXT r_obj2.obj_id;
		END LOOP;
	END LOOP;
	RETURN;
END;
$$ LANGUAGE plpgsql;
-- version ohne import_id
----------------------------------------------------
-- FUNCTION:	get_exploded_dst_of_rule(rule_id)
-- Zweck:		liefert alle in den Zielen enthalten objeckt IDs zurueck
-- Zweck:		auch fuer alle Gruppen rekursiv
-- Parameter1:	rule_id der Regel
-- RETURNS:		alle obj_ids als Tabelle
--
CREATE OR REPLACE FUNCTION get_exploded_dst_of_rule(BIGINT) RETURNS SETOF BIGINT AS $$
DECLARE
	i_rule_id ALIAS FOR $1;
	r_obj	RECORD;
	r_obj2	RECORD;
BEGIN
	FOR r_obj IN
		SELECT obj_id FROM rule_to WHERE rule_id=i_rule_id -- AND rt_create<=i_import_id AND rt_last_seen>=i_import_id
	LOOP
		FOR r_obj2 IN
--			SELECT explode_objgrp AS obj_id FROM explode_objgrp(r_obj.obj_id,i_import_id)
			SELECT explode_objgrp AS obj_id FROM explode_objgrp(r_obj.obj_id)
		LOOP
			RETURN NEXT r_obj2.obj_id;
		END LOOP;
	END LOOP;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_previous_import_id(devid, zeitpunkt)
-- Zweck:		liefert zu einem Device + Zeitpunkt die Import ID des vorherigen Imports
-- Parameter1:	Device_id (INTEGER)
-- Parameter2:	Time (timestamp)
-- RETURNS:		ID des vorherigen Imports
--
CREATE OR REPLACE FUNCTION get_previous_import_id(INTEGER,TIMESTAMP) RETURNS BIGINT AS $$
DECLARE
	i_device_id ALIAS FOR $1;
	t_report_time_in ALIAS FOR $2;
	t_report_time TIMESTAMP;
	i_mgm_id INTEGER;
	i_prev_import_id BIGINT;
BEGIN
	IF t_report_time_in IS NULL THEN
		t_report_time := now();
	ELSE
		t_report_time := t_report_time_in;
	END IF;
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_device_id;
	SELECT INTO i_prev_import_id max(control_id) FROM import_control WHERE mgm_id=i_mgm_id AND
		start_time<=t_report_time AND NOT stop_time IS NULL AND successful_import;
	IF NOT FOUND THEN
		RETURN NULL;
	ELSE
--		RAISE NOTICE 'found get_previous_import_id: %', i_prev_import_id;
	    RETURN i_prev_import_id;
	END IF;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_previous_import_ids(time)
-- Zweck:		liefert zu einem Zeitpunkt die Import ID aller Systeme des vorherigen Imports
-- Parameter1:	Time (timestamp)
-- RETURNS:		string mit Import-Ids, eg.: (1, 2, 5, 7)
--
CREATE OR REPLACE FUNCTION get_previous_import_ids(TIMESTAMP) RETURNS VARCHAR AS $$
DECLARE
	t_report_time_in ALIAS FOR $1;
	t_report_time TIMESTAMP;
	i_mgm_id INTEGER;
	r_dev RECORD;
	v_id_string VARCHAR;
	i_prev_import_id BIGINT;
BEGIN
	IF t_report_time_in IS NULL THEN
		t_report_time := now();
	ELSE
		t_report_time := t_report_time_in;
	END IF;
	v_id_string := ' (';
	FOR r_dev IN
		SELECT dev_id FROM device
	LOOP
		i_prev_import_id := get_previous_import_id(r_dev.dev_id, t_report_time);
		IF NOT i_prev_import_id IS NULL THEN
			IF NOT v_id_string=' (' THEN
				v_id_string := v_id_string || ', ';
			END IF;
			v_id_string := v_id_string || CAST(i_prev_import_id AS VARCHAR);
		END IF;
	END LOOP;
	v_id_string := v_id_string || ') ';
    RETURN v_id_string;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_next_import_id($devid)
-- Zweck:		liefert zu einem Device + Zeitpunkt die Import ID des naechst folgenden Imports
-- Parameter1:	Device_id (INTEGER)
-- Parameter2:	Time (timestamp)
-- RETURNS:		ID des naechsten Imports
--
CREATE OR REPLACE FUNCTION get_next_import_id(INTEGER,TIMESTAMP) RETURNS BIGINT AS $$
DECLARE
	i_device_id ALIAS FOR $1;
	t_report_time_in ALIAS FOR $2;
	t_report_time TIMESTAMP;
	i_mgm_id INTEGER;
	i_next_import_id BIGINT;
BEGIN
	IF t_report_time_in IS NULL THEN
		t_report_time := now();
	ELSE
		t_report_time := t_report_time_in;
	END IF;
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_device_id;
	SELECT INTO i_next_import_id min(control_id) FROM import_control WHERE mgm_id=i_mgm_id
		AND start_time>=t_report_time AND NOT stop_time IS NULL AND successful_import;
    RETURN i_next_import_id;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_matching_import_id
-- Zweck:		liefert zu einem Zeitpunkt die ID des unmittelbar davor
-- Zweck:		stattgefunden habenden Imports fuer das Device zurueck
-- Parameter1:	Device_id (INTEGER)
-- Parameter2:	Zeitpunkt (TIMESTAMP)
-- RETURNS:		ID des Imports
--
CREATE OR REPLACE FUNCTION get_matching_import_id(INTEGER, TIMESTAMP) RETURNS BIGINT AS $$
DECLARE
	i_device_id ALIAS FOR $1;
	t_report_time_in ALIAS FOR $2;
	i_import_id BIGINT;
	i_mgm_id INTEGER;
	t_report_time TIMESTAMP;
BEGIN
	IF t_report_time_in IS NULL THEN
		t_report_time := now();
	ELSE t_report_time := t_report_time_in;
	END IF;
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_device_id;
	SELECT INTO i_import_id control_id FROM import_control
			WHERE mgm_id=i_mgm_id AND start_time<=t_report_time AND NOT stop_time IS NULL AND successful_import  -- get only completed imports
			ORDER BY control_id desc
			LIMIT 1;
--	RAISE EXCEPTION 'device_id: %, time: %, import_id: %', i_device_id, t_report_time, i_import_id;
    RETURN i_import_id;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	explode_objgrp
-- Zweck:		liefert alle obj_ids die in der Gruppe (auch rekursiv) enthalten sind, zurueck
-- Zweck:		wenn keine Gruppe, dann nur das object selbst
-- Parameter1:	obj_id
-- RETURNS:		wahr, wenn das Komplement von object zum tenant mit tenant_id gehoert
--
CREATE OR REPLACE FUNCTION explode_objgrp (BIGINT) RETURNS SETOF BIGINT AS $$
DECLARE
    i_obj_id ALIAS FOR $1;
    r_obj	RECORD;				-- zu pruefendes Objekt
BEGIN
	IF is_obj_group(i_obj_id) THEN  -- keine Gruppe
		FOR r_obj IN
			SELECT objgrp_flat_member_id FROM object LEFT JOIN objgrp_flat ON objgrp_flat_id=object.obj_id
			WHERE object.obj_id=i_obj_id
		LOOP
			RETURN NEXT r_obj.objgrp_flat_member_id;
		END LOOP;
	ELSE -- Gruppe
		RETURN NEXT i_obj_id;
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	is_rule_src_negated
-- Zweck:		liefert TRUE, wenn die Quelle der Regel negiert ist
-- Parameter1:	rule_id
-- RETURNS:		BOOLEAN
--
CREATE OR REPLACE FUNCTION is_rule_src_negated (BIGINT) RETURNS BOOLEAN AS $$
DECLARE
    i_rule_id ALIAS FOR $1;
    r_rule_src_neg	BOOLEAN; -- result
BEGIN
	SELECT INTO r_rule_src_neg rule_src_neg FROM rule WHERE rule_id=i_rule_id;
	RETURN r_rule_src_neg;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	is_rule_dst_negated
-- Zweck:		liefert TRUE, wenn das Ziel der Regel negiert ist
-- Parameter1:	rule_id
-- RETURNS:		BOOLEAN
--
CREATE OR REPLACE FUNCTION is_rule_dst_negated (BIGINT) RETURNS BOOLEAN AS $$
DECLARE
    i_rule_id ALIAS FOR $1;
    r_rule_dst_neg	BOOLEAN; -- result
BEGIN
	SELECT INTO r_rule_dst_neg rule_dst_neg FROM rule WHERE rule_id=i_rule_id;
	RETURN r_rule_dst_neg;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:	get_rule_action
-- Zweck:		liefert die Aktion der Regel zur Anzeige (als ID und als String)
-- Parameter1:	rule_id
-- RETURNS:		action_id und string der Aktion
--
CREATE OR REPLACE FUNCTION get_rule_action (BIGINT) RETURNS RECORD AS $$
DECLARE
    i_rule_id ALIAS FOR $1;
    r_rule	RECORD; -- record to be returned
BEGIN
	SELECT INTO r_rule rule.action_id,action_name FROM rule,stm_action
		WHERE rule.action_id=stm_action.action_id AND rule_id=i_rule_id;
	RETURN r_rule;
END;
$$ LANGUAGE plpgsql;