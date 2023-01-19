/* 
 data structure:
	rule_<objtype>_resolved table for each object contained in a rule - used for quick reporting of objects used in a reported ruleset
 	
	rule table:
 		rule.rule_from/to/svc/usr - textual overview of rule elements - filled directly from import_xxx tables without ref integrity checking

	import_changelog table only used for audit log entries - currently not used at all - can be deleted or maybe used for recording ref integrity issues?

	changelog_rule/svc/nwobj tables
		record all changes - should also contain unreferenced changes?! then we can use these tables for reporting ref issues

	new: log_data_issue for storing broken references etc. but not rolling back the import


 (error handling) structure:

	import_all_main(mgm_id) RETURNS boolean --> string with errors instead?
	{
		TRY:
			all VOID returns: import_zone_main, import_nwobj_main, import_svc_main, import_usr_main
			FOR all devices: 
				IF import_rules() 
					import_rules_set_rule_num_numeric 

			if import_global_refhandler_main THEN raise returned exception error_string
				-- here exception handling needs to be fixed
			else 
				if get_active_rules_with_broken_refs_per_mgm() <>'' THEN raise returned exception error_string
					-- here exception handling already seems to be working

			import_changelog_sync (mainly logging hitherto unnoticed deletes in import_change- tables)
		CATCH:
			enrich import_control.import_errors with errors from above
	
	--------- creating foreign key references for each element of a rule (and also object groups)
		the _flat tables in addition contain all resolved group members (redundant information to speed up reporting)

		import_global_refhandler_main: (exception handling not working)
			import_nwobj_refhandler_main
				import_nwobj_refhandler_insert
					import_nwobj_refhandler_objgrp_add_group
				import_nwobj_refhandler_change
					import_nwobj_refhandler_change_objgrp_member_refs
					import_nwobj_refhandler_objgrp_add_group
				import_nwobj_refhandler_insert_flat
					import_nwobj_refhandler_objgrp_flat_add_group
				import_nwobj_refhandler_change_flat
					import_nwobj_refhandler_change_objgrp_flat_member_refs
					import_nwobj_refhandler_change_rule_from_refs
					import_nwobj_refhandler_change_rule_to_refs	
			
			import_svc_refhandler_main (same sub functions as nwobj above)
				...
			import_usr_refhandler_main (same sub functions as nwobj above)
				...

			for each device:
				import_rule_refhandler_main:
					for each rule:
						resolve_rule_list(rule_from):
							for each rule-from element:
								f_add_single_rule_from_element
									import_rule_resolved_nwobj
						resolve_rule_list(rule_to)
							for each rule-to element:
								f_add_single_rule_to_element
									import_rule_resolved_nwobj
						resolve_rule_list(rule_service)
							for each rule-service element:
								f_add_single_rule_svc_element
									import_rule_resolved_svc

	--------- Q&A the following code just checks for broken references and reports them
	    broken refs can occur if the element is not the only element in a field?

	get_active_rules_with_broken_refs_per_mgm: (exception handling working)
		for each device:
			get_active_rules_with_broken_refs_per_dev := 
				get_active_rules_with_broken_src_refs_per_dev (v_delimiter, b_heal, i_dev_id) || 
				get_active_rules_with_broken_dst_refs_per_dev (v_delimiter, b_heal, i_dev_id) ||
				get_active_rules_with_broken_svc_refs_per_dev

				the above functions check all refs 
					if "healing mode" is set (not used during import), the missing refs are inserted into rule_from/rule_to/rule_svc
					all broken refs are returned as a string
	}  */

DROP FUNCTION IF EXISTS public.import_all_main(BIGINT);
DROP FUNCTION IF EXISTS public.import_all_main(BIGINT, BOOLEAN);
CREATE OR REPLACE FUNCTION public.import_all_main(BIGINT, BOOLEAN)
  RETURNS VARCHAR AS
$BODY$
DECLARE
	i_current_import_id ALIAS FOR $1; -- ID of the current import
	b_debug_mode ALIAS FOR $2; -- should we output debug info?
	i_mgm_id INTEGER;
	r_dev RECORD;
	b_force_initial_import BOOLEAN;
	b_is_initial_import BOOLEAN;
	b_do_not_import BOOLEAN;
	v_err_pos VARCHAR;
	v_err_str VARCHAR;
	v_err_str_refs VARCHAR;
	b_result BOOLEAN;
	r_obj RECORD;
	v_exception_message VARCHAR;
	v_exception_details VARCHAR;
	v_exception_hint VARCHAR;
	v_exception VARCHAR;
	t_import_start TIMESTAMP;
	t_last_measured_timestamp TIMESTAMP;	
BEGIN
	BEGIN -- catch exception block
		t_import_start := now();
		v_err_pos := 'start';
		SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO b_is_initial_import is_initial_import FROM import_control WHERE control_id=i_current_import_id;
		IF NOT b_is_initial_import THEN -- pruefen, ob force_flag des Mangements gesetzt ist
			SELECT INTO b_force_initial_import force_initial_import FROM management WHERE mgm_id=i_mgm_id;
			IF b_force_initial_import THEN b_is_initial_import := TRUE; END IF;
		END IF;

		-- import base objects
		v_err_pos := 'import_zone_main';
		PERFORM import_zone_main	(i_current_import_id, b_is_initial_import);
		v_err_pos := 'import_nwobj_main';
		PERFORM import_nwobj_main	(i_current_import_id, b_is_initial_import);	
		v_err_pos := 'import_svc_main';
		PERFORM import_svc_main		(i_current_import_id, b_is_initial_import);
		v_err_pos := 'import_usr_main';
		PERFORM import_usr_main		(i_current_import_id, b_is_initial_import);
		RAISE  DEBUG 'after usr_import';
		v_err_pos := 'rulebase_import_start';

		t_last_measured_timestamp := debug_show_time('import of base objects', t_import_start);	
		-- import rulebases
		FOR r_dev IN
			SELECT * FROM device WHERE mgm_id=i_mgm_id AND NOT do_not_import
		LOOP
			SELECT INTO b_do_not_import do_not_import FROM device WHERE dev_id=r_dev.dev_id;
			IF NOT b_do_not_import THEN		--	RAISE NOTICE 'importing %', r_dev.dev_name;
				v_err_pos := 'import_rules of device ' || r_dev.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ')';
				IF (import_rules(r_dev.dev_id, i_current_import_id)) THEN  				-- returns true if rule order needs to be changed
																						-- currently always returns true as each import needs a rule reordering
					v_err_pos := 'import_rules_set_rule_num_numeric of device ' || r_dev.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ')';
					-- in case of any changes - adjust rule_num values in rulebase
					PERFORM import_rules_set_rule_num_numeric (i_current_import_id,r_dev.dev_id);
				END IF;
			END IF;
		END LOOP;

		t_last_measured_timestamp := debug_show_time('import of rules', t_last_measured_timestamp);	

		v_err_pos := 'ImpGlobRef';
		SELECT INTO b_result * FROM import_global_refhandler_main(i_current_import_id);
		IF NOT b_result THEN --  alle Referenzen aendern (basiert nur auf Eintraegen in changelog_xxx	
			SELECT INTO v_err_str_refs import_errors FROM import_control WHERE control_id=i_current_import_id;
			RAISE NOTICE 'notice. error in import_global_refhandler_main';
			RAISE EXCEPTION 'error in import_global_refhandler_main';
		ELSE  -- no error so far
			v_err_pos := 'get_active_rules_with_broken_refs_per_mgm';
			SELECT INTO v_err_str_refs * FROM get_active_rules_with_broken_refs_per_mgm ('|', FALSE, i_mgm_id);
			IF NOT are_equal(v_err_str_refs, '') THEN
				RAISE EXCEPTION 'error in get_active_rules_with_broken_refs_per_mgm: %', v_err_str_refs;
--				RAISE NOTICE 'found broken references in get_active_rules_with_broken_refs_per_mgm: %', v_err_str_refs;
			END IF;
		END IF;
		IF b_force_initial_import THEN UPDATE management SET force_initial_import=FALSE WHERE mgm_id=i_mgm_id; END IF; 	-- evtl. gesetztes management.force_initial_import-Flag loeschen	
		v_err_pos := 'import_changelog_sync';
		PERFORM import_changelog_sync (i_current_import_id, i_mgm_id); -- Abgleich zwischen import_changelog und changelog_xxx	
		v_err_pos := 'recert_refresh_per_management';
		PERFORM recert_refresh_per_management (i_mgm_id);
	EXCEPTION
		WHEN OTHERS THEN -- read error from import_control and rollback
			GET STACKED DIAGNOSTICS v_exception_message = MESSAGE_TEXT,
                          v_exception_details = PG_EXCEPTION_DETAIL,
                          v_exception_hint = PG_EXCEPTION_HINT;
			v_exception := v_exception_message || v_exception_details || v_exception_hint;
			v_err_pos := 'ERR-ImpMain@' || v_err_pos;
			RAISE DEBUG 'import_all_main - Exception block entered with v_err_pos=%', v_err_pos;
			SELECT INTO v_err_str import_errors FROM import_control WHERE control_id=i_current_import_id;
			IF v_err_str IS NULL THEN
				UPDATE import_control SET import_errors = v_err_pos || v_exception WHERE control_id=i_current_import_id;
			ELSE 
				UPDATE import_control SET import_errors = v_err_str || v_err_pos || v_exception WHERE control_id=i_current_import_id;				
			END IF;
			IF NOT v_err_str_refs IS NULL THEN
				SELECT INTO v_err_str import_errors FROM import_control WHERE control_id=i_current_import_id;
				UPDATE import_control SET import_errors = v_err_str || ';' || v_err_str_refs WHERE control_id=i_current_import_id;
			END IF;
			RAISE NOTICE 'ERROR: import_all_main failed';
			RETURN v_err_str;
			-- RETURN FALSE;
	END;
	RETURN '';
END;
$BODY$
  LANGUAGE plpgsql VOLATILE
  COST 100;
ALTER FUNCTION public.import_all_main(BIGINT, BOOLEAN) OWNER TO fworch;


CREATE OR REPLACE FUNCTION debug_show_time (VARCHAR, TIMESTAMP)
    RETURNS TIMESTAMP
    AS $BODY$
DECLARE
	v_event ALIAS FOR $1; -- description of the processed time
	t_import_start ALIAS FOR $2; -- start time of the import
BEGIN

    RAISE NOTICE '% duration: %s', v_event, CAST(now()- t_import_start AS VARCHAR);
--    RAISE NOTICE 'duration of last step: %s', CAST(now()- t_import_start AS VARCHAR);
    RETURN now();
END;
$BODY$
LANGUAGE plpgsql
VOLATILE
COST 100;

----------------------------------------------------
-- FUNCTION:  import_global_refhandler_main
-- Zweck:     ueberall dort, wo Elemente veraendert (changed,inserted,deleted) wurden,
-- Zweck:	  muessen die Referenzen entweder:
-- Zweck:     - vom alten auf das neue Element umgebogen werden
-- Zweck:     - fuer das Element geloescht werden
-- Zweck:     - fuer das Element hinzugefuegt werden
-- Parameter: current_import_id
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_global_refhandler_main (BIGINT) RETURNS BOOLEAN AS $$
DECLARE
	i_current_import_id ALIAS FOR $1; -- ID des laufenden Imports
	i_mgm_id INTEGER;
	r_device RECORD;
	b_do_not_import BOOLEAN;
	v_err_pos VARCHAR;
	v_err_str VARCHAR;
BEGIN
	-- BEGIN -- exception block
		-- adjust references for objects for current management
		v_err_pos := 'import_nwobj_refhandler_main';
		PERFORM import_nwobj_refhandler_main(i_current_import_id);
		v_err_pos := 'import_svc_refhandler_main';
		PERFORM import_svc_refhandler_main(i_current_import_id);
		v_err_pos := 'import_usr_refhandler_main';
		PERFORM import_usr_refhandler_main(i_current_import_id);
		
		-- adjust rule references or all devices of the current management
		SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
		FOR r_device IN
			SELECT * FROM device WHERE mgm_id=i_mgm_id
		LOOP 
			SELECT INTO b_do_not_import do_not_import FROM device WHERE dev_id=r_device.dev_id;
			IF NOT b_do_not_import THEN
				v_err_pos := 'import_rule_refhandler_main of device ' || r_device.dev_name || ' (Management: ' || CAST (i_mgm_id AS VARCHAR) || ')';
				PERFORM import_rule_refhandler_main(i_current_import_id, r_device.dev_id);
			END IF;
		END LOOP;
	-- EXCEPTION
	-- 	WHEN OTHERS THEN
	-- 		v_err_pos := 'ERR-ImpGlobRef@' || v_err_pos;
	-- 		RAISE NOTICE 'referr %', v_err_pos;
	-- 		-- SELECT INTO v_err_str import_errors FROM import_control WHERE control_id=i_current_import_id;
	-- 		-- IF v_err_str IS NULL THEN
	-- 		-- 	UPDATE import_control SET import_errors = v_err_pos WHERE control_id=i_current_import_id;
	-- 		-- ELSE 
	-- 		-- 	UPDATE import_control SET import_errors = v_err_str || v_err_pos WHERE control_id=i_current_import_id;				
	-- 		-- END IF;
	-- 		-- RETURN FALSE;
	-- END;
	RETURN TRUE;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_changelog_sync
-- Zweck:     Abgleich zwischen Auditlog Eintraegen (import_changelog) und
-- Zweck:	  den generierten Eintraegen in changelog_xxx
-- Parameter: current_import_id
-- Parameter: mgmg_id
-- RETURNS:   VOID
--
/*
Example import_changelog entries:

fworchdb=# select * from import_changelog where control_id=14;
     change_time     | management_name | changed_object_name |          changed_object_uid          | changed_object_type | change_action | change_admin | control_id | import_changelog_nr | import_changelog_id 
---------------------+-----------------+---------------------+--------------------------------------+---------------------+---------------+--------------+------------+---------------------+---------------------
 2007-09-25 11:08:02 | fw1             | Standard            | 4C6B3F06-B6B0-4287-AF4B-A7B553925F02 |  rule               | C             | tim          |         14 |                   8 |                 311
 2007-09-25 11:08:02 | fw1             | abc                 | B4BD0D06-43F7-4FA4-AAE0-5567D45E313C |  network_object     | I             | tim          |         14 |                   7 |                 310
 2007-09-25 11:01:27 | fw1             |                     |                                      |                     | Log In        | tim          |         14 |                   1 |                 304
 2007-09-25 11:00:33 | fw1             |                     |                                      |                     | Log In        | localhost    |         14 |                   0 |                 303
(10 Zeilen)


fworchdb=# select * from view_changes;
 abs_change_id | local_change_id | change_request_info | change_element | change_element_order | old_id | new_id | change_documented | change_type_id | change_type | change_comment | obj_comment |        change_time         | mgm_name | mgm_id | dev_name | dev_id | change_admin | change_admin_id | doku_admin | doku_admin_id | security_relevant |                               unique_name                               | change_diffs | change_new_element 
---------------+-----------------+---------------------+----------------+----------------------+--------+--------+-------------------+----------------+-------------+----------------+-------------+----------------------------+----------+--------+----------+--------+--------------+-----------------+------------+---------------+-------------------+-------------------------------------------------------------------------+--------------+--------------------
           460 |               9 |                     | rule           | rule_element         |      4 |      8 | f                 |              3 | C           |                |             | 2007-09-25 11:04:41.951275 | fw1      |    444 | fw1      |    444 | Tim Purschke |               4 |            |               | t                 | Standard__uid__26E762E9-4DF2-406C-B3D4-F57C2C19F7A9, Rulebase: Standard |              | 
           457 |               8 |                     | rule           | rule_element         |      7 |        | f                 |              3 | D           |                |             | 2007-09-25 11:42:45.101601 | fw1      |    444 | fw1      |    444 |              |                 |            |               | t                 | Standard__uid__8ED0AE3D-F6E5-485A-81D8-C22D21410491, Rulebase: Standard |              | 
           458 |              13 |                     | object         | basic_element        |      2 |        | f                 |              3 | D           |                |             | 2007-09-25 11:45:45.972244 | fw1      |    444 |          |        |              |                 |            |               | t                 | test1-inserted                                                          |              | 
(9 Zeilen)
*/

CREATE OR REPLACE FUNCTION import_changelog_sync (BIGINT, INTEGER) RETURNS VOID AS $$
DECLARE
	i_current_import_id ALIAS FOR $1; -- ID des laufenden Imports
	i_mgm_id ALIAS FOR $2;			 -- mgm_id
	r_auditlog RECORD;
	r_changelog RECORD;	
	i_admin_id INTEGER;
	r_user_id RECORD;
	i_delete_import_id BIGINT;
	i_last_seen_import_id BIGINT;
BEGIN
	FOR r_auditlog IN
		SELECT * FROM import_changelog WHERE control_id=i_current_import_id
	LOOP
		RAISE NOTICE 'change: %', r_auditlog.change_action;
		IF r_auditlog.change_action = 'C' OR r_auditlog.change_action = 'I' OR r_auditlog.change_action = 'D' THEN	-- real changes, no log-ins/outs
			SELECT uiuser_id INTO i_admin_id FROM uiuser WHERE uiuser_username=r_auditlog.change_admin;	-- change_admin ID holen
			RAISE NOTICE '   object change, name=%', r_auditlog.changed_object_name || ', type=' || r_auditlog.changed_object_type || ', action=' || r_auditlog.change_action || '.';
		-- now processing unnoticed user deletes (which exist for checkpoint firewalls)
			IF r_auditlog.changed_object_type='user' AND r_auditlog.change_action='D' THEN
				RAISE NOTICE '      user delete change found: %', r_auditlog.changed_object_name;
				SELECT view_changes.*, uiuser_username INTO r_changelog FROM view_changes LEFT JOIN uiuser ON (change_admin_id=uiuser_id)
					WHERE unique_name=r_auditlog.changed_object_name AND change_type='D';
				IF NOT FOUND THEN
--					RAISE NOTICE '            found unnoticed user delete: %', r_auditlog.changed_object_name;
					SELECT user_id INTO r_user_id FROM usr WHERE user_name=r_auditlog.changed_object_name AND active;
					IF FOUND THEN 
						SELECT MAX(control_id) INTO i_last_seen_import_id FROM import_control WHERE mgm_id=i_mgm_id AND start_time<r_auditlog.change_time;
						IF FOUND THEN -- 
							SELECT MIN(control_id) INTO i_delete_import_id FROM import_control WHERE mgm_id=i_mgm_id AND control_id>i_last_seen_import_id;
							IF FOUND THEN 
								INSERT INTO changelog_user (control_id, old_user_id, change_action, mgm_id, change_type_id, change_time, import_admin, unique_name)
									VALUES (i_current_import_id, r_user_id.user_id, 'D', i_mgm_id, 3, r_auditlog.change_time, i_admin_id, r_auditlog.changed_object_uid);
								UPDATE usr SET active=FALSE, user_last_seen=i_last_seen_import_id WHERE active AND usr.user_id=r_user_id.user_id;
							ELSE
								RAISE WARNING 'ERROR: no previous import for deleted user % found', r_auditlog.changed_object_name;
							END IF;
						ELSE -- der user wurde vor dem ersten Import des Managements geloescht --> also wenn moeglich nicht in die DB aufnehmen
							RAISE NOTICE 'User % already deleted before initial import', r_auditlog.changed_object_name;
							DELETE FROM changelog_user WHERE new_user_id=r_user_id.user_id;
							-- catch errors
							DELETE FROM usr WHERE user_id=r_user_id.user_id;
							-- catch errors
						END IF;
					ELSE
						RAISE EXCEPTION 'ERROR: deleted user % not found', r_auditlog.changed_object_name;
					END IF;
				ELSE 
					RAISE NOTICE '            found noticed user delete: %', r_auditlog.changed_object_name;
				END IF;
			END IF;
		END IF;		
	END LOOP;
	RETURN;
END;
$$ LANGUAGE plpgsql;
