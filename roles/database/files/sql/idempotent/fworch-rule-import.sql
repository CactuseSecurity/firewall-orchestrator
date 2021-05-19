-- $Id: iso-rule-import.sql,v 1.1.2.10 2013-02-12 09:48:08 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-rule-import.sql,v $

----------------------------------------------------
-- FUNCTION:  import_rules (device_id)
-- Zweck:     fuegt alle Regeln des aktuellen Imports in die rule-Tabelle
-- Zweck:     verwendet dazu die Funktionen insert_single_rule
-- Zweck:     zum Einfuegen saemtlicher Regeln
-- Parameter: device_id
-- Parameter2: import_id
-- RETURNS:   should rule_order be written (aka has anything been changed?)
--
CREATE OR REPLACE FUNCTION import_rules (INTEGER,BIGINT) RETURNS BOOLEAN AS $$
DECLARE
    i_dev_id  ALIAS FOR $1; -- zum Holen der dev_ID fuer Loeschen von Regeln
    i_current_import_id ALIAS FOR $2;
    i_mgm_id  INTEGER; -- zum Holen der mgm_ID fuer Loeschen von Regeln
    r_rule RECORD; -- Datensatz mit einzelner rule_id aus import_rule-Tabelle
    v_rulebase_name VARCHAR; -- Name des zur Device-ID gerhoerigen Regelsatzes
    b_is_initial_import BOOLEAN;
    v_rule_head_text VARCHAR;
    b_rule_order_to_be_written BOOLEAN;
    i_change_admin INTEGER;
BEGIN
	b_rule_order_to_be_written := FALSE; 
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
	SELECT INTO v_rulebase_name dev_rulebase FROM device WHERE dev_id=i_dev_id;
	-- SELECT INTO r_rule rule_id FROM rule_order WHERE dev_id=i_dev_id LIMIT 1;
	SELECT INTO r_rule rule_id FROM rule WHERE dev_id=i_dev_id LIMIT 1;
	IF FOUND THEN
		b_is_initial_import := FALSE;
		SELECT INTO r_rule force_initial_import FROM management WHERE mgm_id=i_mgm_id;
		IF r_rule.force_initial_import THEN
			b_is_initial_import := TRUE;
			b_rule_order_to_be_written := TRUE; 
		END IF;
		SELECT INTO r_rule force_initial_import FROM device WHERE dev_id=i_dev_id;
		IF r_rule.force_initial_import THEN
			b_is_initial_import := TRUE;
			UPDATE device SET force_initial_import=FALSE WHERE dev_id=i_dev_id;	-- jetzt zuruecksetzen
		END IF;
	ELSE
		b_is_initial_import := TRUE;
		b_rule_order_to_be_written := TRUE; 
	END IF;
	RAISE DEBUG 'import_rules - importing rulebase: %', v_rulebase_name;
	FOR r_rule IN -- jede Regel wird mittels insert_single_rule eingefuegt
		SELECT rule_id FROM import_rule WHERE control_id = i_current_import_id AND rulebase_name = v_rulebase_name
		LOOP
			b_rule_order_to_be_written :=
				insert_single_rule(r_rule.rule_id,i_dev_id,i_mgm_id,i_current_import_id,b_is_initial_import)
				 OR b_rule_order_to_be_written;
	END LOOP;
	RAISE DEBUG 'import_rules - after insert loop';
	IF NOT b_is_initial_import THEN	-- alle nicht mehr vorhandenen Regeln loeschen (active=false setzen)
		i_change_admin := get_last_change_admin_of_rulebase_change(i_current_import_id,i_dev_id);
--		RAISE DEBUG 'import_rules - change_admin_id = %', i_change_admin;
		FOR r_rule IN -- jede geloeschte Regel wird in changelog_rule eingetragen
			SELECT rule_id, rule_name, (rule_head_text is NULL) as is_security_relevant FROM rule 
				WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_last_seen<i_current_import_id
		LOOP
			RAISE DEBUG 'import_rules - changelog delete %', r_rule.rule_id; 
			INSERT INTO changelog_rule
				(control_id,new_rule_id,old_rule_id,change_action,import_admin,documented,mgm_id,dev_id,security_relevant)
				VALUES (i_current_import_id,NULL,r_rule.rule_id,'D',i_change_admin,FALSE,i_mgm_id,i_dev_id,r_rule.is_security_relevant);
			PERFORM error_handling('INFO_RULE_DELETED', r_rule.rule_name);
		END LOOP;
		RAISE DEBUG 'import_rules - after delete loop';
--		UPDATE rule SET active=FALSE WHERE rule_id IN
--			( SELECT rule.rule_id FROM rule_order LEFT JOIN rule USING (rule_id)
--			  WHERE rule.active AND rule_order.dev_id=i_dev_id AND rule.mgm_id=i_mgm_id AND rule.rule_last_seen<i_current_import_id GROUP BY rule.rule_id );
		UPDATE rule SET active=FALSE WHERE rule_id IN
			( SELECT rule.rule_id FROM rule
			  WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_last_seen<i_current_import_id );
	RAISE DEBUG 'import_rules - after active=false update';
	END IF;
	RETURN TRUE;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_rules_save_order
-- Zweck:     speichert die Regelreihenfolge in rule_order
-- Parameter: current_import_id::BIGINT
-- Parameter: device_id::INTEGER
-- RETURNS:   VOID
--
-- CREATE OR REPLACE FUNCTION import_rules_save_order (BIGINT,INTEGER) RETURNS VOID AS $$
-- DECLARE
-- 	i_current_control_id ALIAS FOR $1; -- ID des aktiven Imports
-- 	i_dev_id ALIAS FOR $2; -- ID des zu importierenden Devices
-- 	i_mgm_id INTEGER; -- ID des zugehoerigen Managements
-- 	b_existing_rulebase BOOLEAN;
-- BEGIN
-- 	RAISE DEBUG 'import_rules_save_order - start';
-- 	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
-- 	IF (TRUE) THEN
-- 		RAISE DEBUG 'import_rules_save_order - mgm_id=%, dev_id=%, before inserting', i_mgm_id, i_dev_id;
-- 		INSERT INTO rule_order (control_id,dev_id,rule_id,rule_number)
-- 			SELECT i_current_control_id AS control_id, i_dev_id as dev_id, rule.rule_id, import_rule.rule_num as rule_number
-- 			FROM device, import_rule LEFT JOIN rule ON (import_rule.rule_uid=rule.rule_uid AND rule.dev_id=i_dev_id) WHERE device.dev_id=i_dev_id 
-- 			AND rule.mgm_id = i_mgm_id AND rule.active AND import_rule.control_id=i_current_control_id 
-- 			AND import_rule.rulebase_name=device.dev_rulebase;
-- 	ELSE
-- 		RAISE DEBUG 'import_rules_save_order - policy already processed for other device: skipping';	
-- 	END IF;
-- 	RAISE DEBUG 'import_rules_save_order - end';
-- 	RETURN;
-- END;
-- $$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:  import_rules_set_rule_num_numeric (control_id, device_id)
-- purpose:   sets numeric rule order value in field rule_num_numeric for sorting rules in the correct order
-- Parameter1: import id (control_id)
-- Parameter2: device_id
-- RETURNS:   nothing
CREATE OR REPLACE FUNCTION import_rules_set_rule_num_numeric (BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
	i_current_control_id ALIAS FOR $1; -- ID des aktiven Imports
	i_dev_id ALIAS FOR $2; -- ID des zu importierenden Devices
	i_mgm_id INTEGER; -- ID des zugehoerigen Managements
	r_rule RECORD;
	i_prev_numeric_value BIGINT;
	i_next_numeric_value BIGINT;
	i_numeric_value BIGINT;
/*  function layout:
	for each rule in import_rule
		if rule changed
			get rule_num_numeric of previous and next rule from rule table
			if no prev & no next rule exists: rule_num_numeric = 0
			elsif no next rule exists: rule_num_numeric = prev + 1000
			elsif no prev rule exists: rule_num_numeric = next - 1000
			else set rule_num_numeric = (prev+next)/2
*/
BEGIN
	RAISE DEBUG 'import_rules_set_rule_num_numeric - start';
	SELECT INTO i_mgm_id mgm_id FROM device WHERE dev_id=i_dev_id;
	RAISE DEBUG 'import_rules_set_rule_num_numeric - mgm_id=%, dev_id=%, before inserting', i_mgm_id, i_dev_id;
	FOR r_rule IN -- set rule_num_numeric for changed (i.e. "new") rules
		SELECT rule.rule_id, rule_num_numeric FROM import_rule LEFT JOIN rule USING (rule_uid) WHERE
			active AND
			import_rule.control_id = i_current_control_id AND
			rule.dev_id=i_dev_id 
			ORDER BY import_rule.rule_num
	LOOP
		RAISE DEBUG 'import_rules_set_rule_num_numeric loop rule %', CAST(r_rule.rule_id AS VARCHAR );
		IF r_rule.rule_num_numeric IS NULL THEN
			RAISE DEBUG 'import_rules_set_rule_num_numeric found new rule %', CAST(r_rule.rule_id AS VARCHAR );
			-- get numeric value of next rule:
			SELECT INTO i_next_numeric_value rule_num_numeric FROM rule 
				WHERE active AND dev_id=i_dev_id AND mgm_id=i_mgm_id AND rule_num_numeric>i_prev_numeric_value ORDER BY rule_num_numeric LIMIT 1;
			RAISE DEBUG 'import_rules_set_rule_num_numeric next rule %', CAST(i_next_numeric_value AS VARCHAR);
			IF i_prev_numeric_value IS NULL AND i_next_numeric_value IS NULL THEN
				i_numeric_value := 0;
			ELSIF i_next_numeric_value IS NULL THEN
				i_numeric_value := i_prev_numeric_value + 1000;
			ELSIF i_prev_numeric_value IS NULL THEN
				i_numeric_value := i_next_numeric_value - 1000;
			ELSE
				i_numeric_value := (i_prev_numeric_value + i_next_numeric_value) / 2;
			END IF; 
			RAISE DEBUG 'import_rules_set_rule_num_numeric determined rule_num_numeric %', CAST(i_numeric_value AS VARCHAR);
			UPDATE rule SET rule_num_numeric = i_numeric_value WHERE rule.rule_id=r_rule.rule_id;
			r_rule.rule_num_numeric := i_numeric_value;
		END IF;
		i_prev_numeric_value := r_rule.rule_num_numeric;
	END LOOP;
	RAISE DEBUG 'import_rules_set_rule_num_numeric - end';
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  insert_single_rule
-- Zweck:     fuegt eine Regel des aktuellen Imports in die rule-Tabelle ein
-- Parameter1: import_rule.rule_id (die ID der zu importierenden Regel)
-- Parameter2: dev_id
-- Parameter3: mgm_id
-- Parameter4: control_id
-- Parameter5: b_is_initial_import

-- RETURNS:   b_rule_order_to_be_written (aka has anything been changed?)
--
CREATE OR REPLACE FUNCTION insert_single_rule(BIGINT,INTEGER,INTEGER,BIGINT,BOOLEAN) RETURNS BOOLEAN AS $$
DECLARE
    id   ALIAS FOR $1;
    i_dev_id   ALIAS FOR $2;
    i_mgm_id   ALIAS FOR $3;
    i_control_id ALIAS FOR $4;
    b_is_initial_import ALIAS FOR $5;
    r_to_import   RECORD;    -- der zu importierende Datensatz aus import_rule
    r_meta   RECORD;    -- rule meta data record
    i_rule_num    INTEGER;   -- fuer Regelnummer
    s_track       VARCHAR;   -- track-string
    i_action_id   INTEGER;   -- action_id
    i_track_id    INTEGER;    -- Record fuer Track
    i_fromzone    INTEGER;   -- Record fuer Quell-Zone
    i_tozone      INTEGER;   -- Record fuer Ziel-Zone
    i_admin_id    INTEGER;   -- ID des last_change_admins dieser Regel
    b_implied     BOOLEAN;   -- Regel ex- oder implizit
    r_existing	  RECORD;	 -- vorhandene Regel (bei Aenderungen)
    b_insert	  BOOLEAN;	 -- neue Regel
    b_change	  BOOLEAN;	 -- Regel geaendert
    b_change_sr   BOOLEAN;	 -- non-security-relevant Change
    v_change_id	  VARCHAR;	-- type of change
    i_new_rule_id BIGINT;  -- id of rule just about to be inserted
    i_old_rule_id BIGINT;  -- id of exisiting rule
	b_is_documented BOOLEAN; 
	t_outtext	  TEXT; 
	i_change_type INTEGER;
	v_change_action VARCHAR;    
    b_rule_order_to_be_written BOOLEAN;
	i_parent_rule_id BIGINT;
BEGIN
	b_rule_order_to_be_written := FALSE; 
    b_insert := FALSE;    b_change := FALSE;    b_change_sr := FALSE;
    SELECT INTO r_to_import * FROM import_rule WHERE rule_id = id; -- zu importierenden Datensatz aus import_rule einlesen
-- Zone-ID holen ------------------------------------------------------------------------------------------
    IF (r_to_import.rule_from_zone IS NULL) THEN	i_fromzone := NULL;
    ELSE SELECT INTO i_fromzone zone_id FROM zone WHERE zone_name = r_to_import.rule_from_zone AND zone.mgm_id = i_mgm_id; -- AND active;
	END IF;
    IF (r_to_import.rule_to_zone IS NULL) THEN i_tozone := NULL;
	ELSE SELECT INTO i_tozone zone_id FROM zone WHERE zone_name = r_to_import.rule_to_zone AND zone.mgm_id = i_mgm_id; -- AND active;
	END IF;
-- Track ID holen ------------------------------------------------------------------------------------------
	IF char_length(cast(r_to_import.rule_track as varchar))=0 THEN	s_track := 'none';
	ELSE	s_track := lower(r_to_import.rule_track);
	END IF;
    SELECT INTO i_track_id track_id FROM stm_track WHERE track_name = s_track; -- Track-ID holen
    IF NOT FOUND THEN	PERFORM error_handling('ERR_NO_TRACK', s_track);	END IF;
-- Action-ID holen ------------------------------------------------------------------------------------------
    SELECT INTO i_action_id action_id FROM stm_action WHERE action_name = lower(r_to_import.rule_action); -- Action-ID holen
	IF NOT FOUND THEN	PERFORM error_handling('ERR_NO_ACTION', r_to_import.rule_action);	END IF;
-- rule_num holen ------------------------------------------------------------------------------------------
    IF (r_to_import.rule_num IS NULL OR CAST(r_to_import.rule_num AS VARCHAR) = '') THEN  -- wenn keine Regelnummer vorhanden 
		i_rule_num := 0;  -- rule_num auf 0 setzen
    ELSE i_rule_num := CAST(r_to_import.rule_num AS INTEGER); -- TEXT in INTEGER umwandeln, TODO: Fehlerbehandlung
    END IF;    
-- Vergleich - hat sich die Regel geaendert? -----------------------------------------------------------------

	IF (r_to_import.rule_uid IS NULL) THEN -- removed char_length-check due to utf-8 problems
		PERFORM error_handling('ERR_RULE_NOT_IDENTIFYABLE');
	END IF;
	SELECT INTO r_existing * FROM rule WHERE
		rule_uid=r_to_import.rule_uid AND rule.mgm_id=i_mgm_id AND rule.dev_id=i_dev_id AND rule.active;
	IF FOUND THEN  -- Regel existiert schon
		IF ( NOT (
			are_equal(r_existing.rule_uid, r_to_import.rule_uid) AND
			are_equal(r_existing.rule_ruleid,r_to_import.rule_ruleid) AND
			are_equal(r_existing.rule_from_zone,i_fromzone) AND
			are_equal(r_existing.rule_to_zone,i_tozone) AND
			are_equal(r_existing.rule_disabled, r_to_import.rule_disabled) AND
			are_equal(r_existing.rule_src, r_to_import.rule_src) AND
			are_equal(r_existing.rule_dst, r_to_import.rule_dst) AND
			are_equal(r_existing.rule_svc, r_to_import.rule_svc) AND
			are_equal(r_existing.rule_src_refs,r_to_import.rule_src_refs) AND
			are_equal(r_existing.rule_dst_refs, r_to_import.rule_dst_refs) AND
			are_equal(r_existing.rule_svc_refs, r_to_import.rule_svc_refs) AND
			are_equal(r_existing.rule_src_neg, r_to_import.rule_src_neg) AND
			are_equal(r_existing.rule_dst_neg, r_to_import.rule_dst_neg) AND
			are_equal(r_existing.rule_svc_neg, r_to_import.rule_svc_neg) AND
			are_equal(r_existing.action_id, i_action_id) AND
			are_equal(r_existing.track_id, i_track_id) AND
			are_equal(r_existing.rule_installon, r_to_import.rule_installon) AND
			are_equal(r_existing.rule_time, r_to_import.rule_time) ))
		THEN
			b_change := TRUE;
			b_change_sr := TRUE;
		END IF;
		IF ( NOT (		--	ab hier die nicht sicherheitsrelevanten Aenderungen
			are_equal(r_existing.rule_name,r_to_import.rule_name) AND 
			are_equal(r_existing.rule_head_text, r_to_import.rule_head_text) AND
			are_equal(r_existing.rule_comment, r_to_import.rule_comment) ))
		THEN
			b_change := TRUE;
		END IF;
		IF (b_change)
		THEN	v_change_id := 'INFO_RULE_CHANGED';
		ELSE	UPDATE rule SET rule_last_seen = i_control_id WHERE rule_id = r_existing.rule_id;
		END IF;
	ELSE -- Regel geaendert
		b_insert := TRUE;
		v_change_id := 'INFO_RULE_INSERTED'; 
	END IF;
	IF (b_change OR b_insert) THEN
		b_rule_order_to_be_written := TRUE; 
		PERFORM error_handling(v_change_id, r_to_import.rule_uid);
		i_admin_id := get_admin_id_from_name(r_to_import.last_change_admin);   
		RAISE DEBUG 'rule_change_or_insert: %', r_to_import.rule_uid;
		-- INSERT statement absetzen
		IF r_to_import.rule_svc IS NULL OR r_to_import.rule_src IS NULL OR r_to_import.rule_dst IS NULL THEN
			RAISE NOTICE 'rule_change with svc, dst or svc = NULL: %', r_to_import.rule_uid;			
		ELSE
			RAISE DEBUG 'rule_change_or_insert_before_insert: %', r_to_import.rule_uid;

			SELECT INTO r_meta rule_metadata_id FROM rule_metadata WHERE dev_id=i_dev_id AND rule_uid=r_to_import.rule_uid;

			IF FOUND THEN
				UPDATE rule_metadata SET rule_last_modified=now() WHERE dev_id=i_dev_id AND rule_uid=CAST(r_to_import.rule_uid AS TEXT);
			ELSE
				INSERT INTO rule_metadata (rule_uid, dev_id) VALUES(r_to_import.rule_uid, i_dev_id);
			END IF;

			RAISE DEBUG 'rule_change_after_rule_metadata change: %', r_to_import.rule_uid;

			IF NOT r_to_import.parent_rule_id IS NULL THEN
				RAISE DEBUG 'rule_change parent uid is set to %', r_to_import.parent_rule_uid;
				SELECT INTO i_parent_rule_id rule_id FROM rule WHERE rule_uid=r_to_import.parent_rule_uid AND rule_last_seen=i_control_id;
				INSERT INTO rule
					(mgm_id,rule_name,rule_num,rule_ruleid,rule_uid,rule_disabled,rule_src_neg,rule_dst_neg,rule_svc_neg,
					action_id,track_id,rule_src,rule_dst,rule_svc,rule_src_refs,rule_dst_refs,rule_svc_refs,rule_action,rule_track,rule_installon,rule_time,
					rule_from_zone,rule_to_zone,rule_comment,rule_implied,rule_head_text,last_change_admin,
					rule_create,rule_last_seen, dev_id, parent_rule_id, parent_rule_type)
				VALUES (i_mgm_id,r_to_import.rule_name,i_rule_num,r_to_import.rule_ruleid,r_to_import.rule_uid,
					r_to_import.rule_disabled,r_to_import.rule_src_neg,r_to_import.rule_dst_neg,r_to_import.rule_svc_neg,
					i_action_id,i_track_id,r_to_import.rule_src,r_to_import.rule_dst,r_to_import.rule_svc,
					r_to_import.rule_src_refs,r_to_import.rule_dst_refs,r_to_import.rule_svc_refs,
					lower(r_to_import.rule_action),r_to_import.rule_track,r_to_import.rule_installon,r_to_import.rule_time,
					i_fromzone,i_tozone, r_to_import.rule_comment,r_to_import.rule_implied,r_to_import.rule_head_text,i_admin_id,
					i_control_id,i_control_id, i_dev_id, i_parent_rule_id, 3);  -- 3 = unguarded-layer
			END IF;

			RAISE DEBUG 'rule_change_after_parent change: %', r_to_import.rule_uid;

			INSERT INTO rule
				(mgm_id,rule_name,rule_num,rule_ruleid,rule_uid,rule_disabled,rule_src_neg,rule_dst_neg,rule_svc_neg,
				action_id,track_id,rule_src,rule_dst,rule_svc,rule_src_refs,rule_dst_refs,rule_svc_refs,rule_action,rule_track,rule_installon,rule_time,
			 	rule_from_zone,rule_to_zone,rule_comment,rule_implied,rule_head_text,last_change_admin,
			 	rule_create,rule_last_seen, dev_id)
			VALUES (i_mgm_id,r_to_import.rule_name,i_rule_num,r_to_import.rule_ruleid,r_to_import.rule_uid,
				r_to_import.rule_disabled,r_to_import.rule_src_neg,r_to_import.rule_dst_neg,r_to_import.rule_svc_neg,
				i_action_id,i_track_id,r_to_import.rule_src,r_to_import.rule_dst,r_to_import.rule_svc,
				r_to_import.rule_src_refs,r_to_import.rule_dst_refs,r_to_import.rule_svc_refs,
				lower(r_to_import.rule_action),r_to_import.rule_track,r_to_import.rule_installon,r_to_import.rule_time,
				i_fromzone,i_tozone, r_to_import.rule_comment,r_to_import.rule_implied,r_to_import.rule_head_text,i_admin_id,
				i_control_id,i_control_id, i_dev_id);
			
			-- changelog-Eintrag
			RAISE DEBUG 'rule_change_or_insert_before_select_into: %', r_to_import.rule_uid;
			SELECT INTO i_new_rule_id MAX(rule_id) FROM rule WHERE mgm_id=i_mgm_id; -- ein bisschen fragwuerdig
			IF (b_insert) THEN  -- die regel wurde neu angelegt und ist keine header-regel
				RAISE DEBUG 'rule_change_or_insert_insert_zweig: %', r_to_import.rule_uid;
				IF b_is_initial_import
				THEN	b_is_documented := TRUE;  /* t_outtext := get_text('INITIAL_IMPORT'); */ i_change_type := 2;
				ELSE	b_is_documented := FALSE; t_outtext := NULL; i_change_type := 3;
				END IF;
				RAISE DEBUG 'rule_change_or_insert_insert_zweig_after_initial_import: %', r_to_import.rule_uid;
				v_change_action := 'I';
				i_old_rule_id := NULL;
				IF r_to_import.rule_head_text IS NULL THEN
					RAISE DEBUG 'rule_change_or_insert_insert_zweig_rule_head_text_is_null: %', r_to_import.rule_uid;
					b_change_sr := TRUE;
					t_outtext := NULL;
					b_is_documented := FALSE;
				ELSE
					RAISE DEBUG 'rule_change_or_insert_insert_zweig_rule_head_text_is_not_null: %', r_to_import.rule_uid;
--					t_outtext := get_text('NON_SECURITY_RELEVANT_CHANGE');
					t_outtext := 'not sec relevant';
--					t_outtext := get_text('NON_SECURITY_RELEVANT_CHANGE');
					RAISE DEBUG 'rule_change_or_insert_insert_zweig_rule_head_text_is_not_null_after_NON_SECURITY_RELEVANT_CHANGE: %', r_to_import.rule_uid;
					b_is_documented := TRUE;
					b_change_sr := FALSE;
				END IF;
				RAISE DEBUG 'rule_change_or_insert_insert_zweig_END: %', r_to_import.rule_uid;
			ELSE -- change
				RAISE DEBUG 'rule_change_or_insert_change_zweig: %', r_to_import.rule_uid;
				i_change_type := 3;
				IF (b_change_sr) THEN
					t_outtext := NULL;
					b_is_documented := FALSE;
				ELSE
					t_outtext := get_text('NON_SECURITY_RELEVANT_CHANGE');
					b_is_documented := TRUE;
				END IF;
				-- hier noch Test einbauen, ob ausschliesslich Rename eines Elements vorliegt, dann kein changelog_rule-Eintrag
				v_change_action := 'C';
				i_old_rule_id := r_existing.rule_id;
				RAISE DEBUG 'rule_change_or_insert_change_zweig_before_update_rule: %', r_to_import.rule_uid;
				UPDATE rule SET active = FALSE WHERE rule_id = i_old_rule_id; -- alte Regel auf not active setzen
				RAISE DEBUG 'rule_change_or_insert_change_zweig_after_update_rule: %', r_to_import.rule_uid;
			END IF;
			
			RAISE DEBUG 'rule_change_or_insert_before_changelog_rule_insert: %', r_to_import.rule_uid;
			INSERT INTO changelog_rule
				(control_id,new_rule_id,old_rule_id,change_action,import_admin,documented,changelog_rule_comment,
				 mgm_id,dev_id,change_type_id,security_relevant)
			VALUES
				(i_control_id,i_new_rule_id,i_old_rule_id,v_change_action,i_admin_id,b_is_documented,
				 t_outtext,i_mgm_id,i_dev_id,i_change_type,b_change_sr);
		END IF;
	END IF;
	RETURN b_rule_order_to_be_written; 
END;
$$ LANGUAGE plpgsql;