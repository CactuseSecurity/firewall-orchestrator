----------------------------------------------------
-- FUNCTION:  resolve_all_rule_lists
-- Zweck:     Generiert die Beziehungen in rule_from, rule_to, rule_svc
-- Zweck:     fuer alle aktuell importierten, geaenderten Regelteile des Device mit dev-id
-- Zweck:     orientiert sich dabei an den aktuellen changelog-Eintraegen
-- Parameter1: import_id
-- Parameter2: device_id
-- verwendete
-- Funktionen: resolve_rule_list_obj, resolve_rule_list_svc, resolve_rule_list_user
-- RETURNS:   nix
--
CREATE OR REPLACE FUNCTION import_rule_refhandler_main(BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
	i_current_import_id  ALIAS FOR $1; --  Import-ID
	i_dev_id  ALIAS FOR $2; -- Device ID
	r_liste   RECORD; -- Record fuer Liste
	r_control RECORD; -- Record fuer import control (Trennzeichen, Mangement)
	v_local_error VARCHAR;
	v_error_str VARCHAR;
	v_error_in_element VARCHAR;
BEGIN
	SELECT INTO r_control mgm_id,delimiter_group,delimiter_user,delimiter_zone,delimiter_list FROM import_control
		WHERE control_id=i_current_import_id;
	-- fuer alle neuen Regeln
	FOR r_liste IN
	SELECT rule.rule_uid, rule.rule_from_zone,rule.rule_to_zone,rule.rule_id,rule.rule_src,rule.rule_dst,rule.rule_svc,
				rule.rule_src_refs,rule.rule_dst_refs,rule.rule_svc_refs
			FROM changelog_rule,rule WHERE
				changelog_rule.dev_id = i_dev_id AND
				changelog_rule.new_rule_id = rule.rule_id AND
				changelog_rule.control_id = i_current_import_id AND
				changelog_rule.change_action <> 'D'   -- create new entries in rule_XXX for inserted or changed rules
	LOOP
		BEGIN
			RAISE DEBUG '%', 'processing rule no. ' || r_liste.rule_uid || ': ' || r_liste.rule_src || ' --> ' || r_liste.rule_dst || ' with services: ' || r_liste.rule_svc; 
			v_error_in_element := 'rule source';
	--      PERFORM error_handling('DEBUG_GENERAL_INFO', CAST(r_liste.rule_id AS VARCHAR));
			PERFORM resolve_rule_list (r_liste.rule_id, 'rule_from',r_liste.rule_src_refs, r_control.mgm_id,
				r_liste.rule_from_zone, r_control.delimiter_list,i_current_import_id);
			v_error_in_element := 'rule destination';
			PERFORM resolve_rule_list (r_liste.rule_id, 'rule_to',  r_liste.rule_dst_refs, r_control.mgm_id,
				r_liste.rule_to_zone, r_control.delimiter_list,i_current_import_id);
			v_error_in_element := 'rule service';
			PERFORM resolve_rule_list (r_liste.rule_id, 'rule_service', r_liste.rule_svc_refs, r_control.mgm_id,
				0, r_control.delimiter_list, i_current_import_id);
		EXCEPTION WHEN OTHERS THEN
			v_local_error := 'ERR-import_rule_refhandler@rule_uid: ' || r_liste.rule_uid;
			RAISE WARNING '%', v_local_error;
			-- adding the error to potential other errors already in import_control.import_errors
			SELECT INTO v_error_str import_errors FROM import_control WHERE control_id=i_current_import_id;
			IF NOT v_error_str IS NULL THEN
				v_error_str := v_error_str || '\n' || v_local_error;
			ELSE
				v_error_str := v_local_error;
			END IF;
			UPDATE import_control SET import_errors = v_error_str WHERE control_id=i_current_import_id;
			--                        1                        2                    3              4               5              6           7
			PERFORM add_data_issue(i_current_import_id,  r_liste.rule_uid, r_liste.rule_uid, r_liste.rule_uid, r_liste.rule_id, i_dev_id, 'import_rule_refhandler_main', 
				'Exception: broken ref in rule with UID "' || r_liste.rule_uid || '" in ' || v_error_in_element, NULL);
		END;
	END LOOP;

	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  resolve_rule_list
-- Zweck:     die Liste $3 aufloesen
-- Zweck:     und in rule_from,rule_to,rule_svc eintragen
-- Parameter: $1: rule_id
-- Parameter: $2: Zieltabelle (rule_from|rule_to|rule_svc)
-- Parameter: $3: Liste von Elementen in Stringform
-- Parameter: $4: mgm_id des aktuellen imports
-- Parameter: $5: zone_id der aktuellen Regel (entweder from oder to, bei svc dummy=0)
-- Parameter: $6: Trennzeichen zwischen den Listenmitgliedern in $3
-- Parameter: $7: current import id
-- verwendete
-- Funktionen: f_add_single_list_element, resolve_rule_list (rekursiv)
-- RETURNS:   VOID
--

CREATE OR REPLACE FUNCTION resolve_rule_list (BIGINT,varchar,varchar,integer,BIGINT,varchar,BIGINT) RETURNS VOID AS $$
DECLARE
	i_rule_id ALIAS FOR $1;
	v_dst_table ALIAS FOR $2;
	v_member_string ALIAS FOR $3;
    i_mgm_id ALIAS FOR $4;
	i_zone_id ALIAS FOR $5;
	v_delimiter ALIAS FOR $6;
	i_current_import_id ALIAS FOR $7;
	v_current_member varchar;
BEGIN
	RAISE DEBUG 'resolve_rule_list - 1 starting, v_member_string=%', v_member_string;
	RAISE DEBUG 'resolve_rule_list - 2 dst_table=%', v_dst_table;
	IF v_member_string IS NULL OR v_member_string='' THEN RETURN; END IF;
	FOR v_current_member IN SELECT member FROM regexp_split_to_table(v_member_string, E'\\' || v_delimiter) AS member LOOP
		IF NOT (v_current_member IS NULL OR v_current_member='') THEN 
			RAISE DEBUG 'resolve_rule_list - 3 adding list refs for %.', v_current_member;
			IF v_dst_table = 'rule_from' THEN
				PERFORM f_add_single_rule_from_element(i_rule_id, v_current_member, i_mgm_id, i_zone_id, i_current_import_id);
			ELSIF v_dst_table = 'rule_to' THEN
				PERFORM f_add_single_rule_to_element(i_rule_id, v_current_member, i_mgm_id, i_zone_id, i_current_import_id);
			ELSIF v_dst_table = 'rule_service' THEN
				PERFORM f_add_single_rule_svc_element(i_rule_id, v_current_member, i_mgm_id, i_current_import_id);
			END IF;
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   f_add_single_rule_from_element
-- Zweck:      die Elemente der Liste $2 aufloesen
-- Zweck:      und in rule_from eintragen
-- Parameter:  $1: rule_id
-- Parameter:  $2: einzelnes Listenelement
-- Parameter:  $3: Management-ID
-- Parameter:  $4: Zone-ID (netscreen only)
-- Parameter:  $5: current import id
-- verwendete
-- Funktionen: KEINE
-- RETURNS:    VOID
--
CREATE OR REPLACE FUNCTION f_add_single_rule_from_element(BIGINT,varchar,integer,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
    i_rule_id  ALIAS FOR $1;
    v_element  ALIAS FOR $2;
    i_mgm_id   ALIAS FOR $3;
    i_zone_id  ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
    i_obj      			BIGINT;
    i_usr      			BIGINT;
    i_at_sign_pos 		INTEGER;
    v_usergroup_name	VARCHAR;
    v_src_obj			VARCHAR;
    r_debug				RECORD;
    v_error_str			VARCHAR;
	i_dev_id 			INTEGER;
BEGIN 
	RAISE DEBUG 'f_add_single_rule_from_element - 1 starting for %', v_element;
	i_at_sign_pos := instr(v_element,'@');
	IF i_at_sign_pos > 0 THEN -- User-Gruppen enthalten
		v_usergroup_name := substr(v_element,0,i_at_sign_pos);
		v_src_obj := substr(v_element,i_at_sign_pos+1);
		SELECT INTO i_usr user_id FROM usr WHERE user_uid=v_usergroup_name AND mgm_id=i_mgm_id AND active;
		IF NOT FOUND THEN
		SELECT INTO i_dev_id dev_id FROM rule where rule_id=i_rule_id;
		SELECT INTO r_debug user_name,user_uid FROM usr WHERE user_id = i_usr;
		PERFORM add_data_issue(i_current_import_id, v_usergroup_name, r_debug.user_uid, r_debug.rule_uid, i_rule_id, i_dev_id, 'user object', 'undefined user obj in rule source', NULL);
		PERFORM error_handling('ERR_LST_EL_MISS', 'User: ' || v_usergroup_name);
		END IF;
	ELSE
		v_src_obj := v_element;
		i_usr := NULL; -- auf 'nouser' gesetzt
	END IF;

	IF i_zone_id IS NULL THEN -- wenn zoneID leer: ignoriere das Feld
		SELECT INTO i_obj obj_id FROM object WHERE obj_uid=v_src_obj AND mgm_id=i_mgm_id AND active;
	ELSE
		SELECT INTO i_obj obj_id FROM object WHERE obj_uid=v_src_obj AND zone_id=i_zone_id AND mgm_id=i_mgm_id AND active;
		IF NOT FOUND THEN  -- trying without zone (assuming global zone for junos)
			SELECT INTO i_obj obj_id FROM object WHERE obj_uid=v_src_obj AND mgm_id=i_mgm_id AND active;
		END IF;
	END IF;
	IF NOT FOUND THEN
		SELECT INTO i_dev_id dev_id FROM rule where rule_id=i_rule_id;
		SELECT INTO r_debug user_name,user_uid FROM usr WHERE user_id = i_usr;
		PERFORM add_data_issue(i_current_import_id, v_element, v_element, r_debug.rule_uid, i_rule_id, i_dev_id, 'user object', 'undefined user obj in rule source', NULL);
		PERFORM error_handling('ERR_LST_EL_MISS', 'Obj: ' || v_src_obj);
	END IF;
	IF i_usr IS NULL THEN
		SELECT INTO r_debug rule_id,obj_id,user_id FROM rule_from
			WHERE rule_id=i_rule_id AND obj_id=i_obj AND rf_create=i_current_import_id AND rf_last_seen=i_current_import_id;
	ELSE
		SELECT INTO r_debug rule_id,obj_id,user_id FROM rule_from
			WHERE rule_id=i_rule_id AND obj_id=i_obj AND user_id=i_usr AND rf_create=i_current_import_id AND rf_last_seen=i_current_import_id;
	END IF;
		
	IF FOUND THEN -- debug duplicate objects
		SELECT INTO r_debug obj_name,obj_uid FROM object WHERE obj_id = r_debug.obj_id;
		v_error_str := '';
		IF NOT r_debug.obj_name IS NULL THEN
			v_error_str := 'object: ' || r_debug.obj_name || '(uid: ' || r_debug.obj_uid || '), ';
		ELSE
			v_error_str := 'unknown object, ';
		END IF;
		SELECT INTO r_debug rule_uid FROM rule WHERE rule_id = i_rule_id;
		IF NOT r_debug.rule_uid IS NULL THEN
			v_error_str := 'rule: ' || r_debug.rule_uid;
		ELSE
			v_error_str := 'unknown rule';
		END IF;
		PERFORM error_handling('ERR_RULE_DBL_OBJ', v_error_str);
	ELSE 
		RAISE DEBUG 'f_add_single_rule_from_element - 3 before inserting';
		IF (NOT i_obj IS NULL) THEN
			IF i_usr IS NULL THEN
					INSERT INTO rule_from (rule_id,obj_id,rf_create,rf_last_seen)
						VALUES (i_rule_id,i_obj,i_current_import_id,i_current_import_id);
			ELSE
				INSERT INTO rule_from (rule_id,obj_id,user_id,rf_create,rf_last_seen)
					VALUES (i_rule_id,i_obj,i_usr,i_current_import_id,i_current_import_id);
				BEGIN
					PERFORM import_rule_resolved_usr(i_mgm_id, i_rule_id, NULL, i_usr, i_current_import_id, 'I', 'R');
				EXCEPTION
					WHEN others THEN
						raise notice 'f_add_single_rule_from_element - rule_from with user import_rule_resolved_usr - uncommittable state. Rolling back';
						raise notice '% %', SQLERRM, SQLSTATE;    
				END;

			END IF;
			BEGIN
				PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, NULL, i_obj, i_current_import_id, 'I', 'R');
			EXCEPTION
				WHEN others THEN
					raise notice 'f_add_single_rule_from_element - rule_from import_rule_resolved_nwobj - uncommittable state. Rolling back';
					raise EXCEPTION '% %', SQLERRM, SQLSTATE;
			END;

		END IF;
	END IF;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   f_add_single_rule_to_element
-- Zweck:      die Elemente der Liste $2 aufloesen
-- Zweck:      und in rule_to eintragen
-- Parameter:  $1: rule_id
-- Parameter:  $2: einzelnes Listenelement
-- Parameter:  $3: Management-ID
-- Parameter:  $4: Zone-ID (netscreen only)
-- Parameter:  $5: current import id

-- verwendete
-- Funktionen: KEINE
-- RETURNS:    VOID
--
CREATE OR REPLACE FUNCTION f_add_single_rule_to_element(BIGINT,varchar,integer,BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_rule_id  ALIAS FOR $1;
	v_element  ALIAS FOR $2;
	i_mgm_id   ALIAS FOR $3;
	i_zone_id  ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
	r_obj      RECORD;
	i_dev_id INTEGER;
	v_error_str VARCHAR;
	r_debug RECORD;
BEGIN
	IF i_zone_id IS NULL THEN
		SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_element AND mgm_id=i_mgm_id AND active;
	ELSE
		SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_element AND zone_id=i_zone_id AND mgm_id=i_mgm_id AND active;
		IF NOT FOUND THEN
			SELECT INTO r_obj obj_id FROM object WHERE obj_uid=v_element AND mgm_id=i_mgm_id AND active;
		END IF;
	END IF;
	IF NOT FOUND THEN
		SELECT INTO i_dev_id dev_id FROM rule where rule_id=i_rule_id;
		SELECT INTO r_debug obj_name,obj_uid FROM object WHERE obj_id = r_obj.obj_id;
		PERFORM add_data_issue(i_current_import_id, v_element, v_element, r_debug.rule_uid, i_rule_id, i_dev_id, 'nw object', 'undefined nw obj in rule destination', NULL);
		PERFORM error_handling('ERR_LST_EL_MISS', v_element);
	ELSE
-- 		TODO: check if exactly one hit found
	END IF;
	
	SELECT INTO r_debug rule_id,obj_id FROM rule_to WHERE rule_id=i_rule_id AND obj_id=r_obj.obj_id AND active;
	IF FOUND THEN -- debug duplicate objects
		SELECT INTO r_debug obj_name,obj_uid FROM object WHERE obj_id = r_debug.obj_id;
		v_error_str := '';
		IF NOT r_debug.obj_name IS NULL THEN
			v_error_str := 'object: ' || r_debug.obj_name || '(uid: ' || r_debug.obj_uid || '), ';
		ELSE
			v_error_str := 'unknown object, ';
		END IF;
		SELECT INTO r_debug rule_uid FROM rule WHERE rule_id = i_rule_id;
		IF NOT r_debug.rule_uid IS NULL THEN
			v_error_str := 'rule: ' || r_debug.rule_uid;
		ELSE
			v_error_str := 'unknown rule';
		END IF;
		SELECT INTO i_dev_id dev_id FROM rule where rule_id=i_rule_id;
		SELECT INTO r_debug obj_name,obj_uid FROM object WHERE obj_id = r_obj.obj_id;
		PERFORM add_data_issue(i_current_import_id, v_element, v_element, r_debug.rule_uid, i_rule_id, i_dev_id, 'nw object', 'duplicate nw obj in rule destination', NULL);
		PERFORM error_handling('ERR_RULE_DBL_OBJ', v_error_str);
	ELSE 
		INSERT INTO rule_to (rule_id,obj_id,rt_create,rt_last_seen)
			VALUES (i_rule_id,r_obj.obj_id,i_current_import_id,i_current_import_id);
		PERFORM import_rule_resolved_nwobj(i_mgm_id, i_rule_id, NULL, r_obj.obj_id, i_current_import_id, 'I', 'R');
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   f_add_single_rule_svc_element
-- Zweck:      die Elemente der Liste $2 aufloesen
-- Zweck:      und in rule_svc eintragen
-- Parameter:  $1: rule_id
-- Parameter:  $2: einzelnes Listenelement
-- Parameter:  $3: Management-ID
-- Parameter:  $4: current import id
-- verwendete
-- Funktionen: KEINE
-- RETURNS:    VOID
--
CREATE OR REPLACE FUNCTION f_add_single_rule_svc_element(BIGINT,varchar,integer,BIGINT) RETURNS VOID AS $$
DECLARE
    i_rule_id  ALIAS FOR $1;
    v_element  ALIAS FOR $2;
    i_mgm_id   ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
	i_dev_id INTEGER;
	r_svc      RECORD;
	v_error_str	VARCHAR;
	r_debug		RECORD;
BEGIN
	SELECT INTO r_svc svc_id FROM service WHERE svc_uid=v_element AND mgm_id=i_mgm_id AND active;
	IF NOT FOUND THEN
		SELECT INTO i_dev_id dev_id FROM rule where rule_id=i_rule_id;
		SELECT INTO r_debug svc_name,svc_uid FROM service WHERE svc_id = r_svc.svc_id;
		PERFORM add_data_issue(i_current_import_id, v_element, v_element, r_debug.rule_uid, i_rule_id, i_dev_id, 'service', 'undefined service in rule', NULL);
		PERFORM error_handling('ERR_LST_EL_MISS', v_element);
	END IF;
	
	SELECT INTO r_debug * FROM rule_service WHERE rule_id=i_rule_id AND
		svc_id=r_svc.svc_id AND rs_create=i_current_import_id AND
		rs_last_seen=i_current_import_id;
	IF FOUND THEN
--		RAISE NOTICE 'Error: found duplicate service in rule: %', v_element;
		SELECT INTO r_debug svc_name,svc_uid FROM service WHERE svc_id = r_debug.svc_id;
		SELECT INTO i_dev_id dev_id FROM rule where rule_id=i_rule_id;
		v_error_str := '';
		IF NOT r_debug.svc_name IS NULL THEN
			v_error_str := 'service: ' || r_debug.svc_name || '(uid: ' || r_debug.svc_uid || '), ';
		ELSE
			v_error_str := 'unknown service, ';
		END IF;
		SELECT INTO r_debug rule_uid FROM rule WHERE rule_id = i_rule_id;
		IF NOT r_debug.rule_uid IS NULL THEN
			v_error_str := 'rule: ' || r_debug.rule_uid;
		ELSE
			v_error_str := 'unknown rule';
		END IF;
		PERFORM error_handling('ERR_RULE_DBL_OBJ', v_error_str);
		PERFORM add_data_issue(i_current_import_id, v_element, v_element, r_debug.rule_uid, i_rule_id, i_dev_id, 'service', 'duplicate service in rule', NULL);
	ELSE
		INSERT INTO rule_service (rule_id,svc_id,rs_create,rs_last_seen)
			VALUES (i_rule_id,r_svc.svc_id,i_current_import_id,i_current_import_id);
		PERFORM import_rule_resolved_svc(i_mgm_id, i_rule_id, NULL, r_svc.svc_id, i_current_import_id, 'I', 'R');
	END IF;	
	RETURN;
END;
$$ LANGUAGE plpgsql;


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