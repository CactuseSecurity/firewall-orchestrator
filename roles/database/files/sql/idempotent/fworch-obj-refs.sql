----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_main
-- Zweck:     ueberall dort, wo ein object veraendert (changed,inserted,deleted) wurde,
-- Zweck:	  muessen die Referenzen entweder:
-- Zweck:     - vom alten auf das neue Objekt umgebogen werden
-- Zweck:     - fuer das Objekt geloescht werden
-- Zweck:     - fuer das Objekt hinzugefuegt werden
-- Parameter: current_import_id
-- RETURNS:   VOID
--
-- Function: import_nwobj_refhandler_main(BIGINT)

-- DROP FUNCTION import_nwobj_refhandler_main(BIGINT);

CREATE OR REPLACE FUNCTION import_nwobj_refhandler_main(BIGINT)
  RETURNS void AS
$BODY$
DECLARE
	i_current_import_id   ALIAS FOR $1; -- ID des laufenden Imports
	r_obj 	RECORD;	-- temp object
	r_ctrl 	RECORD;	-- zum Holen des group-delimiters
	v_debug	VARCHAR; --debug-output
	v_obj_name VARCHAR;
	i_previous_import_id BIGINT;
	i_mgm_id INTEGER;
BEGIN
	BEGIN
		RAISE DEBUG 'import_nwobj_refhandler_main - starting ...';

		SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
		i_previous_import_id := get_previous_import_id_for_mgmt (i_mgm_id, i_current_import_id);

		SELECT INTO r_ctrl delimiter_group FROM import_control WHERE control_id=i_current_import_id;
			-- neue Member-Beziehungen von i_new_id eintragen

		RAISE DEBUG 'import_nwobj_refhandler_main - before first FOR loop (objgrp)';
		FOR r_obj IN -- loop for objgrp
			SELECT old_obj_id,new_obj_id,change_action FROM changelog_object WHERE control_id=i_current_import_id AND NOT change_action='D'
		LOOP
			v_debug :=  'old_id: ';
			IF r_obj.old_obj_id IS NULL THEN v_debug := v_debug || 'NULL'; ELSE v_debug := v_debug || CAST(r_obj.old_obj_id AS VARCHAR); END IF;
			v_debug :=  v_debug || ', new_id: ';
			IF r_obj.new_obj_id IS NULL THEN v_debug := v_debug || 'NULL'; ELSE
				SELECT INTO v_obj_name obj_name FROM object WHERE obj_id=r_obj.new_obj_id;
				v_debug := v_debug || CAST(r_obj.new_obj_id AS VARCHAR) || ', new_obj_name=' || v_obj_name;
			END IF;
			IF r_obj.change_action = 'I' THEN
				RAISE DEBUG 'import_nwobj_refhandler_main - calling import_nwobj_refhandler_insert for %', v_debug;
				PERFORM import_nwobj_refhandler_insert(r_obj.new_obj_id,r_ctrl.delimiter_group,i_current_import_id);
			ELSIF r_obj.change_action = 'C' THEN
				RAISE DEBUG 'import_nwobj_refhandler_main - calling import_nwobj_refhandler_change for %', v_debug;
				PERFORM import_nwobj_refhandler_change(r_obj.old_obj_id,r_obj.new_obj_id,i_current_import_id);
			END IF;
		END LOOP;
		RAISE DEBUG 'import_nwobj_refhandler_main - before second FOR loop (objgrp_flat)';
		FOR r_obj IN -- loop for objgrp_flat
			SELECT old_obj_id,new_obj_id,change_action FROM changelog_object WHERE control_id=i_current_import_id AND NOT change_action='D'
		LOOP
			v_debug :=  'old_id: ';
			IF r_obj.old_obj_id IS NULL THEN v_debug := v_debug || 'NULL'; ELSE v_debug := v_debug || CAST(r_obj.old_obj_id AS VARCHAR); END IF;
			v_debug :=  v_debug || ', new_id: ';
			IF r_obj.new_obj_id IS NULL THEN v_debug := v_debug || 'NULL'; ELSE v_debug := v_debug || CAST(r_obj.new_obj_id AS VARCHAR); END IF;
			IF    r_obj.change_action = 'I' THEN
				RAISE DEBUG 'import_nwobj_refhandler_main - calling import_nwobj_refhandler_insert_flat for %', v_debug;
				PERFORM import_nwobj_refhandler_insert_flat(r_obj.new_obj_id,r_ctrl.delimiter_group,i_current_import_id);
			ELSIF r_obj.change_action = 'C' THEN
				RAISE DEBUG 'import_nwobj_refhandler_main - calling import_nwobj_refhandler_change_flat for %', v_debug;
				PERFORM import_nwobj_refhandler_change_flat(r_obj.old_obj_id,r_obj.new_obj_id,i_current_import_id);
			END IF;
		END LOOP;
		----------------------------------------------------------------------------------------------
		-- die alten (nicht mehr gueltigen) Objekte auf non-active setzen
		RAISE DEBUG 'import_nwobj_refhandler_main - after second FOR loop';

		UPDATE objgrp SET active=FALSE WHERE objgrp_id IN
		(SELECT old_obj_id FROM changelog_object WHERE control_id=i_current_import_id AND NOT old_obj_id IS NULL GROUP BY old_obj_id);
		RAISE DEBUG 'import_nwobj_refhandler_main - after first objgrp UPDATE';
		UPDATE objgrp_flat SET active=FALSE WHERE objgrp_flat_id IN
		(SELECT old_obj_id FROM changelog_object WHERE control_id=i_current_import_id AND NOT old_obj_id IS NULL GROUP BY old_obj_id);
		RAISE DEBUG 'import_nwobj_refhandler_main - after first objgrp_flat UPDATE';
		UPDATE objgrp SET active=FALSE WHERE objgrp_member_id IN
		(SELECT old_obj_id FROM changelog_object WHERE control_id=i_current_import_id AND NOT old_obj_id IS NULL GROUP BY old_obj_id);
		RAISE DEBUG 'import_nwobj_refhandler_main - after secondt objgrp UPDATE';
		UPDATE objgrp_flat SET active=FALSE WHERE objgrp_flat_member_id IN
		(SELECT old_obj_id FROM changelog_object WHERE control_id=i_current_import_id AND NOT old_obj_id IS NULL GROUP BY old_obj_id);
		RAISE DEBUG 'import_nwobj_refhandler_main - after second objgrp_flat UPDATE';
			
		UPDATE rule_from SET active=FALSE WHERE obj_id IN
			(SELECT old_obj_id FROM changelog_object WHERE control_id=i_current_import_id AND NOT old_obj_id IS NULL);
			-- hier fehlen Eintraege auch fuer alle nicht Gruppen in objgrp_flat
			-- auch noch in rule_svc !!!
		RAISE DEBUG 'import_nwobj_refhandler_main - after first rule_from UPDATE';
		UPDATE rule_to SET active=FALSE WHERE obj_id IN
			(SELECT old_obj_id FROM changelog_object WHERE control_id=i_current_import_id AND NOT old_obj_id IS NULL);
		RAISE DEBUG 'import_nwobj_refhandler_main - after first rule_to UPDATE';

		-- bei allen neu angelegten Objektbeziehungen wurde last_seen auf i_current_import_id als Default gesetzt
		-- 		--> nix zu tun
		-- abschliessend bei allen nicht auf non-active gesetzten Relationen die last_seen_id aktualisieren
		UPDATE rule_from	SET rf_last_seen=i_current_import_id WHERE rule_id IN
			(SELECT rule_id FROM rule WHERE mgm_id=i_mgm_id AND active) AND active;
		RAISE DEBUG 'import_nwobj_refhandler_main - after second rule_from UPDATE';
		UPDATE rule_to		SET rt_last_seen=i_current_import_id WHERE rule_id IN
			(SELECT rule_id FROM rule WHERE mgm_id=i_mgm_id AND active) AND active;
		RAISE DEBUG 'import_nwobj_refhandler_main - after second rule_to UPDATE';
		UPDATE objgrp		SET import_last_seen=i_current_import_id WHERE objgrp_id IN
			(SELECT obj_id FROM object WHERE mgm_id=i_mgm_id AND active) AND active;
		RAISE DEBUG 'import_nwobj_refhandler_main - after objgrp UPDATE';
		UPDATE objgrp_flat	SET import_last_seen=i_current_import_id WHERE objgrp_flat_id IN
			(SELECT obj_id FROM object WHERE mgm_id=i_mgm_id AND active) AND active;
		RAISE DEBUG 'import_nwobj_refhandler_main - after objgrp_flat UPDATE finished import_nwobj_refhandler_main';

		FOR r_obj IN -- loop for rule_nwobj_resolved
			SELECT old_obj_id,new_obj_id,change_action FROM changelog_object WHERE control_id=i_current_import_id -- AND (change_action = 'C' OR change_action = 'D')
		LOOP
			-- RAISE NOTICE 'import_nwobj_refhandler_main - rule_nwobj_resolved loop';
			PERFORM import_rule_resolved_nwobj (i_mgm_id, NULL, r_obj.old_obj_id, r_obj.new_obj_id, i_current_import_id, r_obj.change_action, 'N');
		END LOOP;
	EXCEPTION
	    WHEN others THEN
            raise notice 'import_nwobj_refhandler_main - uncommittable state. Rolling back';
            raise EXCEPTION '% %', SQLERRM, SQLSTATE;    
	END;
	RETURN;
END; 
$BODY$
  LANGUAGE 'plpgsql';

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_change
-- Zweck:     ueberall dort, wo ein object geaendert wurde,
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_obj_id, new_obj_id, current_import_id
-- RETURNS:   VOID
-- an allen Stellen, an denen das alte Objekt in einem aktiven Datensatz referenziert wird,
-- muss es durch das neue ersetzt werden

CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus object
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus object
	i_current_import_id ALIAS FOR $3; -- zum Heraussuchen des group_delimiters
	r_obj_info RECORD;			-- Record zum Sammeln diverser object-Infos (mgm_id, obj_member)
	r_import_info RECORD;		-- Record zum Sammeln von import-Infos (group_del)
BEGIN
	IF are_equal(i_old_id,i_new_id) THEN
		RAISE EXCEPTION 'old and new obj id are identical!';
	END IF;
	PERFORM import_nwobj_refhandler_change_objgrp_member_refs		(i_old_id, i_new_id, i_current_import_id);
	-- jetzt noch die Aufloesung der Gruppen
	IF is_obj_group(i_old_id) THEN -- wenn Gruppe, dann werden die Beziehungen der moeglicherweise neuen Mitglieder komplett neu eingetragen
		-- Daten zum Beschicken der Funktion insert_nwobj_group_relations sammeln
		SELECT INTO r_import_info delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO r_obj_info mgm_id,obj_member_refs FROM object WHERE obj_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_nwobj_refhandler_objgrp_add_group
			(i_new_id,r_obj_info.obj_member_refs,r_import_info.delimiter_group,r_obj_info.mgm_id,i_current_import_id);
	END IF;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_insert
-- Zweck:     pruefen ob Gruppe, wenn ja Gruppenzugehoerigkeiten der Gruppe aufloesen
-- Zweck:     und in objgrp eintragen
-- Parameter: $1: obj_id des neuen Objekts
-- Parameter: $2: Trennzeichen zwischen Gruppenmitgliedern
-- verwendete
-- Funktionen: add_references_for_inserted_group_obj
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_insert (BIGINT,varchar,BIGINT) RETURNS VOID AS $$
DECLARE
	i_new_id ALIAS FOR $1;
	v_delimiter ALIAS FOR $2;
	i_current_import_id ALIAS FOR $3;
    r_obj_info RECORD;
BEGIN 
	-- Aufloesung der Gruppen
	RAISE DEBUG 'import_nwobj_refhandler_insert - starting';
	IF is_obj_group(i_new_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und
		RAISE DEBUG 'import_nwobj_refhandler_insert - is_obj_group = TRUE';
		-- Mitgliedern eingetragen
		SELECT INTO r_obj_info mgm_id,obj_member_refs FROM object WHERE obj_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		RAISE DEBUG 'import_nwobj_refhandler_insert - calling import_nwobj_refhandler_objgrp_add_group';
		PERFORM import_nwobj_refhandler_objgrp_add_group (i_new_id,r_obj_info.obj_member_refs,v_delimiter,r_obj_info.mgm_id,i_current_import_id);
	END IF;
	RAISE DEBUG 'import_nwobj_refhandler_insert - exiting normally';
	RETURN;
END;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change_flat (BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus object
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus object
	i_current_import_id ALIAS FOR $3; -- zum Heraussuchen des group_delimiters
	r_obj_info RECORD;			-- Record zum Sammeln diverser object-Infos (mgm_id, obj_member)
	r_import_info RECORD;		-- Record zum Sammeln von import-Infos (group_del)
BEGIN
	IF are_equal(i_old_id,i_new_id) THEN
		RAISE EXCEPTION 'old and new obj id are identical!';
	END IF;
	PERFORM import_nwobj_refhandler_change_objgrp_flat_member_refs	(i_old_id, i_new_id, i_current_import_id);
	PERFORM import_nwobj_refhandler_change_rule_from_refs			(i_old_id, i_new_id, i_current_import_id);
	PERFORM import_nwobj_refhandler_change_rule_to_refs				(i_old_id, i_new_id, i_current_import_id);
	-- jetzt noch die Aufloesung der Gruppen
	IF is_obj_group(i_old_id) THEN -- wenn Gruppe, dann werden die Beziehungen der moeglicherweise neuen Mitglieder komplett neu eingetragen
		-- Daten zum Beschicken der Funktion insert_nwobj_group_relations sammeln
		SELECT INTO r_import_info delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO r_obj_info mgm_id,obj_member_refs FROM object WHERE obj_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_nwobj_refhandler_objgrp_flat_add_group (i_new_id,i_new_id,0,i_current_import_id);
	END IF;
	PERFORM import_nwobj_refhandler_objgrp_flat_add_self(i_new_id, i_current_import_id);	
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_insert_flat
-- Zweck:     pruefen ob Gruppe, wenn ja Gruppenzugehoerigkeiten der Gruppe aufloesen
-- Zweck:     und in objgrp_flat eintragen
-- Parameter: $1: obj_id des neuen Objekts
-- Parameter: $2: Trennzeichen zwischen Gruppenmitgliedern
-- verwendete
-- Funktionen: add_references_for_inserted_group_obj
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_insert_flat (BIGINT,varchar,BIGINT) RETURNS VOID AS $$
DECLARE
	i_new_id ALIAS FOR $1;
	v_delimiter ALIAS FOR $2;
	i_current_import_id ALIAS FOR $3;
    r_obj_info RECORD;
BEGIN 
	-- Aufloesung der Gruppen
	IF is_obj_group(i_new_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und Mitgliedern eingetragen
		SELECT INTO r_obj_info mgm_id,obj_member_refs FROM object WHERE obj_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_nwobj_refhandler_objgrp_flat_add_group (i_new_id,i_new_id,0,i_current_import_id);
	END IF;
	PERFORM import_nwobj_refhandler_objgrp_flat_add_self(i_new_id,i_current_import_id);	
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_objgrp_add_group
-- Zweck:     die Gruppenzugehoerigkeiten der Gruppe $1 aufloesen
-- Zweck:     und in objgrp eintragen
-- Parameter: $1: obj_id der Gruppe
-- Parameter: $2: String mit Gruppenmitgliedern
-- Parameter: $3: Trennzeichen zwischen den Gruppenmitgliedern in $1
-- Parameter: $4: Management-ID
-- verwendete
-- Funktionen: f_add_single_group_member_object, insert_nwobj_group_relations (rekursiv)
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_objgrp_add_group (BIGINT,varchar,varchar,integer,BIGINT) RETURNS VOID AS $$
DECLARE
	i_group_id ALIAS FOR $1;
	v_member_string ALIAS FOR $2;
	v_delimiter ALIAS FOR $3;
	i_mgm_id ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
 	v_current_member varchar;
BEGIN
	RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_group - 1 starting, v_member_string=%', v_member_string;
	IF v_member_string IS NULL OR v_member_string='' THEN RETURN; END IF;
	FOR v_current_member IN SELECT member FROM regexp_split_to_table(v_member_string, E'\\' || v_delimiter) AS member LOOP
		RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_group - 2 adding group member ref for %', v_current_member;
		PERFORM import_nwobj_refhandler_objgrp_add_single_groupmember(v_current_member, i_group_id, i_mgm_id, i_current_import_id);
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   import_nwobj_refhandler_objgrp_add_single_groupmember
-- Zweck:      die Gruppenzugehoerigkeiten der Gruppe $2 aufloesen
-- Zweck:      und in objgrp eintragen
-- Parameter:  $1: Name eines einzelnen Gruppenmitglieds
-- Parameter:  $2: obj_id der Gruppe
-- Parameter:  $3: Management-ID
-- verwendete
-- Funktionen: KEINE
-- RETURNS:    VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_objgrp_add_single_groupmember(varchar,BIGINT,INTEGER,BIGINT)
	RETURNS VOID AS $$
DECLARE
	v_member_name ALIAS FOR $1;
	i_group_id ALIAS FOR $2;
	i_mgm_id ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
 	i_obj_id integer;
	r_group RECORD;
	v_error_str VARCHAR;
	r_debug RECORD;
BEGIN 
--	RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_single_groupmember - starting';
	SELECT INTO i_obj_id obj_id FROM object WHERE mgm_id=i_mgm_id AND obj_uid = v_member_name AND active;
--	RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_single_groupmember - after first select into';
	-- hier sollte ev. noch die Zonenzugehoerigkeit beruecksichtigt werden
	IF NOT FOUND THEN
--		RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_single_groupmember - obj_uid not found: normal case, no dups';
		SELECT INTO r_group obj_name,obj_uid FROM object WHERE obj_id = i_group_id;
--		RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_single_groupmember - after second select into (group)';
		v_error_str := '';
		IF NOT r_group.obj_name IS NULL THEN
			v_error_str := 'group: ' || r_group.obj_name || ' (uid: ' || r_group.obj_uid || '), ';
		ELSE
			v_error_str := 'unknown group, ';
		END IF;
		IF NOT v_member_name IS NULL THEN
			v_error_str := v_error_str || 'member: ' || v_member_name;
		ELSE
			v_error_str := v_error_str || 'unknown member';
		END IF;
		PERFORM error_handling('ERR_GRP_MISS_OBJ', v_error_str);
        -- PERFORM add_data_issue(i_current_import_id, r_group.obj_name, r_group.obj_uid, NULL, NULL, 'nw obj group member', 
		--	'non-existant nw obj ' || v_member_name || ' referenced in network object group ' || r_group.obj_name, NULL);
	ELSE
		RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_single_groupmember - obj_uid already exists: duplicate';
		-- debugging for duplicate members
		SELECT INTO r_debug objgrp_id,objgrp_member_id FROM objgrp 
			WHERE objgrp_id=i_group_id AND objgrp_member_id=i_obj_id AND active;
		IF FOUND THEN
			SELECT INTO r_group obj_name,obj_uid FROM object WHERE obj_id = i_group_id;
			v_error_str := '';
			IF NOT r_group.obj_name IS NULL THEN
				v_error_str := 'group: ' || r_group.obj_name || ' (uid: ' || r_group.obj_uid || '), ';
			ELSE
				v_error_str := 'unknown group, ';
			END IF;
			IF NOT v_member_name IS NULL THEN
				v_error_str := v_error_str || 'member: ' || v_member_name;
			ELSE
				v_error_str := v_error_str || 'unknown member';
			END IF;
			-- PERFORM add_data_issue(i_current_import_id, r_group.obj_name, r_group.obj_uid, NULL, NULL, 'nw obj group member', 
			--	'duplicate nw obj in group', 'nw obj ' || v_member_name || ' referenced more than once in network object group ' || r_group.obj_name);
			PERFORM error_handling('ERR_GRP_DBL_OBJ', v_error_str);
		ELSE 
			INSERT INTO objgrp (objgrp_id,objgrp_member_id,import_created,import_last_seen)
				VALUES (i_group_id,i_obj_id,i_current_import_id,i_current_import_id);
		END IF;
	END IF;
--	RAISE DEBUG 'import_nwobj_refhandler_objgrp_add_single_groupmember - exiting normally';
    RETURN;
END; 
$$ LANGUAGE plpgsql;
 

----------------- ab hier kommen die flat-Funktionen -----------------------

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_objgrp_flat_add_group
-- Zweck:     die Gruppenzugehoerigkeiten der Gruppe $2 aufloesen
-- Zweck:     und in objgrp_flat als Teil von $1 eintragen
-- Parameter: $1: obj_id der Top-Level-Gruppe
-- Parameter: $2: obj_id der Unter-Gruppe
-- Parameter: $3: Rekursionsebene - fuer Abbruch nach 20 Rekursionen
-- verwendete
-- Funktionen: insert_nwobj_group_relations_flat (rekursiv)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_objgrp_flat_add_group (BIGINT,BIGINT,INTEGER,BIGINT) RETURNS VOID AS $$
DECLARE
	i_top_group_id		ALIAS FOR $1;
	i_group_id			ALIAS FOR $2;
	i_rec_level			ALIAS FOR $3;
	i_current_import_id	ALIAS FOR $4;
	r_member			RECORD;
	r_obj				RECORD;
	r_member_exists		RECORD;
BEGIN 
--	RAISE DEBUG '%',  'objgrp_flat_add_group start with top lvl ' || i_top_group_id || ' and current group to add ' || i_group_id || ', rec_level=' || i_rec_level;
	IF i_rec_level>20 THEN
		RAISE DEBUG '%',  'exceeded rec-level (' || i_rec_level || ') check, top-lvl-group ' || i_top_group_id || ', current group to add ' || i_group_id;
		PERFORM error_handling('ERR_OBJ_GROUP_REC_LVL_EXCEEDED');
	END IF;
	FOR r_member IN SELECT objgrp_member_id FROM objgrp WHERE objgrp_id=i_group_id
	LOOP
--		RAISE DEBUG '%',  'in loop with current member to add: ' || r_member.objgrp_member_id;
		SELECT INTO r_member_exists * FROM objgrp_flat WHERE objgrp_flat_id=i_top_group_id AND objgrp_flat_member_id=r_member.objgrp_member_id;
		IF NOT FOUND THEN
--			RAISE DEBUG '%',  'adding ' || r_member.objgrp_member_id || ' from group ' || i_group_id || ' to top lvl group ' || i_top_group_id;
			INSERT INTO objgrp_flat (objgrp_flat_id,objgrp_flat_member_id,import_created,import_last_seen)
				VALUES (i_top_group_id,r_member.objgrp_member_id,i_current_import_id,i_current_import_id);
			IF is_obj_group(r_member.objgrp_member_id) THEN
--				RAISE DEBUG'%',  'found a nested group ' || r_member.objgrp_member_id || ' in group ' || i_group_id || ' (in top lvl group ' || i_top_group_id || ')';
				PERFORM import_nwobj_refhandler_objgrp_flat_add_group (i_top_group_id, r_member.objgrp_member_id, i_rec_level+1, i_current_import_id);
			END IF;
		ELSE
--			RAISE DEBUG '%',  'member ' || r_member.objgrp_member_id || ' already (FOUND) in group ' || i_group_id || ' (top lvl group ' || i_top_group_id || ') - skipping';
		END IF;
	END LOOP;
--	RAISE DEBUG '%',  'objgrp_flat_add_group stop top lvl=' || i_top_group_id || ', current group to add=' || i_group_id || ', rec_level=' || i_rec_level;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_objgrp_flat_add_self
-- Zweck:     das Objekt $1 (ob Gruppe oder simple-Object) wird als sein eigenes objgrp_flat-Mitglied eingetragen
-- Parameter: $1: obj_id eines NW-Objekts
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_objgrp_flat_add_self (BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
    i_obj_id	ALIAS FOR $1;
    i_current_import_id	ALIAS FOR $2;
BEGIN 
	INSERT INTO objgrp_flat (objgrp_flat_id,objgrp_flat_member_id,import_created,import_last_seen)
		VALUES (i_obj_id,i_obj_id,i_current_import_id,i_current_import_id);
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_change_objgrp_member_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in objgrp als member auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_obj_id, new_obj_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change_objgrp_member_refs(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus object
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus object
    i_current_import_id	ALIAS FOR $3;
	r_objgrp objgrp%ROWTYPE;
BEGIN
	FOR r_objgrp IN
        SELECT * FROM objgrp WHERE objgrp_member_id=i_old_id AND active
	LOOP -- die neue Beziehung wird eingefuegt
		INSERT INTO objgrp (objgrp_id,objgrp_member_id,import_created,import_last_seen)
			VALUES (r_objgrp.objgrp_id, i_new_id,i_current_import_id,i_current_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_change_objgrp_flat_member_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in objgrp_flat als member auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_obj_id, new_obj_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change_objgrp_flat_member_refs(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus object
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus object
    i_current_import_id	ALIAS FOR $3;
	r_objgrp_flat objgrp_flat%ROWTYPE;
	r_member_exists objgrp_flat%ROWTYPE;
BEGIN
	FOR r_objgrp_flat IN
        SELECT * FROM objgrp_flat WHERE objgrp_flat_member_id=i_old_id AND active
	LOOP -- die neue Beziehung wird eingefuegt
		SELECT INTO r_member_exists * FROM objgrp_flat WHERE
			objgrp_flat_id=r_objgrp_flat.objgrp_flat_id AND objgrp_flat_member_id=i_new_id;
		IF NOT FOUND THEN -- wenn das Mitglied noch nicht in der Gruppe ist: eintragen - verhindert doppelte EIntraege
			INSERT INTO objgrp_flat (objgrp_flat_id,objgrp_flat_member_id,import_created,import_last_seen)
				VALUES (r_objgrp_flat.objgrp_flat_id, i_new_id, i_current_import_id, i_current_import_id);
		ELSE
			RAISE NOTICE 'found double flat_group_member in group %', r_objgrp_flat.objgrp_flat_id;
		END IF;
	END LOOP;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_change_rule_from_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in rule_from als Quelle auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_obj_id, new_obj_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change_rule_from_refs (BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus object
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus object
	i_import_id ALIAS FOR $3; -- id des imports
	r_from rule_from%ROWTYPE;
BEGIN
	FOR r_from IN
        SELECT * FROM rule_from WHERE obj_id=i_old_id AND active
	LOOP -- die neue Beziehung wird eingefuegt
		INSERT INTO rule_from (rule_id,obj_id,user_id,rf_create,rf_last_seen)
			VALUES (r_from.rule_id, i_new_id, r_from.user_id, i_import_id, i_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_nwobj_refhandler_change_rule_to_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in rule_to als Ziel auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_obj_id, new_obj_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_nwobj_refhandler_change_rule_to_refs (BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus object
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus object
	i_import_id ALIAS FOR $3; -- id des imports
	r_to rule_to%ROWTYPE;
BEGIN
	FOR r_to IN
        SELECT * FROM rule_to WHERE obj_id=i_old_id AND active
	LOOP -- alte Zeiger auf rule_to-objekte mit i_old_id werden auf not active gesetzt
		-- zusaetzlich wird die neue Beziehung eingefuegt
		INSERT INTO rule_to (rule_id,obj_id, rt_create, rt_last_seen)
			VALUES (r_to.rule_id, i_new_id, i_import_id, i_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;
