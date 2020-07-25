-- $Id: iso-svc-refs.sql,v 1.1.2.4 2011-09-28 21:14:05 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-svc-refs.sql,v $

/*
 import_svc_refhandler_main (INTEGER) RETURNS VOID
 import_svc_refhandler_change(INTEGER, INTEGER, INTEGER)
 import_svc_refhandler_insert (integer,varchar) RETURNS VOID
 import_svc_refhandler_svcgrp_add_group (integer,varchar,varchar,integer)
 import_svc_refhandler_svcgrp_add_single_groupmember(varchar,integer,INTEGER) RETURNS VOID
 import_svc_refhandler_svcgrp_flat_add_group (INTEGER,INTEGER,INTEGER) RETURNS VOID
 import_svc_refhandler_change_svcgrp_member_refs(INTEGER, INTEGER) RETURNS VOID
 import_svc_refhandler_change_svcgrp_flat_member_refs(INTEGER, INTEGER) RETURNS VOID
 import_svc_refhandler_change_rule_from_refs (INTEGER, INTEGER) RETURNS VOID
 import_svc_refhandler_change_rule_to_refs (INTEGER, INTEGER) RETURNS VOID
*/

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_main
-- Zweck:     ueberall dort, wo ein service veraendert (changed,inserted,deleted) wurde,
-- Zweck:	  muessen die Referenzen entweder:
-- Zweck:     - vom alten auf das neue Objekt umgebogen werden
-- Zweck:     - fuer das Objekt geloescht werden
-- Zweck:     - fuer das Objekt hinzugefuegt werden
-- Parameter: current_import_id
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_main (BIGINT) RETURNS VOID AS $$
DECLARE
	i_current_import_id   ALIAS FOR $1; -- ID des laufenden Imports
	r_svc	RECORD;	-- temp service
	r_ctrl		RECORD;	-- zum Holen des group-delimiters
	v_debug		VARCHAR; --debug-output
	i_previous_import_id BIGINT;
	i_mgm_id INTEGER;
BEGIN
	RAISE DEBUG 'import_svc_refhandler_main - 1 start';
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
	RAISE DEBUG 'import_svc_refhandler_main - 2';
	i_previous_import_id := get_previous_import_id_for_mgmt (i_mgm_id, i_current_import_id);
	RAISE DEBUG 'import_svc_refhandler_main - 3';
	SELECT INTO r_ctrl delimiter_group FROM import_control WHERE control_id=i_current_import_id;
	RAISE DEBUG 'import_svc_refhandler_main - 4';
		-- neue Member-Beziehungen von i_new_id eintragen
	FOR r_svc IN -- jedes geloeschte Objekt wird in changelog_service eingetragen (standard groups)
		SELECT old_svc_id,new_svc_id,change_action FROM changelog_service WHERE control_id=i_current_import_id AND NOT change_action='D'
	LOOP
		RAISE DEBUG 'import_svc_refhandler_main - first loop (std grps): old_svc_id %', CAST(old_svc_id as VARCHAR);
		IF r_svc.change_action = 'I' THEN
			PERFORM import_svc_refhandler_insert(r_svc.new_svc_id,r_ctrl.delimiter_group,i_current_import_id);
		ELSIF r_svc.change_action = 'C' THEN
			PERFORM import_svc_refhandler_change(r_svc.old_svc_id,r_svc.new_svc_id,i_current_import_id);
		END IF;
	END LOOP;
	RAISE DEBUG 'import_svc_refhandler_main - 5';
	FOR r_svc IN -- jedes geloeschte Objekt wird in changelog_service eingetragen (flattened groups)
		SELECT old_svc_id,new_svc_id,change_action FROM changelog_service WHERE control_id=i_current_import_id AND NOT change_action='D'
	LOOP
		RAISE DEBUG 'import_svc_refhandler_main - second loop (flat grps): old_svc_id %', CAST(old_svc_id as VARCHAR);
		IF r_svc.change_action = 'I' THEN
			PERFORM import_svc_refhandler_insert_flat(r_svc.new_svc_id,r_ctrl.delimiter_group,i_current_import_id);
		ELSIF r_svc.change_action = 'C' THEN
			PERFORM import_svc_refhandler_change_flat(r_svc.old_svc_id,r_svc.new_svc_id,i_current_import_id);
		END IF;
	END LOOP;
	RAISE DEBUG 'import_svc_refhandler_main - 6';
	----------------------------------------------------------------------------------------------
	-- die alten (nicht mehr gueltigen) Objekte auf non-active setzen
	UPDATE svcgrp SET active=FALSE WHERE svcgrp_id IN
		(SELECT old_svc_id FROM changelog_service WHERE control_id=i_current_import_id GROUP BY old_svc_id);
	UPDATE svcgrp_flat SET active=FALSE WHERE svcgrp_flat_id IN
		(SELECT old_svc_id FROM changelog_service WHERE control_id=i_current_import_id GROUP BY old_svc_id);
	UPDATE svcgrp SET active=FALSE WHERE svcgrp_member_id IN
		(SELECT old_svc_id FROM changelog_service WHERE control_id=i_current_import_id GROUP BY old_svc_id);
	UPDATE svcgrp_flat SET active=FALSE WHERE svcgrp_flat_member_id IN
		(SELECT old_svc_id FROM changelog_service WHERE control_id=i_current_import_id GROUP BY old_svc_id);
	UPDATE rule_service SET active=FALSE WHERE svc_id IN
		(SELECT old_svc_id FROM changelog_service WHERE control_id=i_current_import_id AND NOT old_svc_id IS NULL);
	UPDATE rule_service	SET rs_last_seen=i_current_import_id WHERE rule_id IN	
		(SELECT rule_id FROM rule WHERE mgm_id=i_mgm_id AND active) AND active;		
	UPDATE svcgrp		SET import_last_seen=i_current_import_id WHERE svcgrp_id IN
		(SELECT svc_id FROM service WHERE mgm_id=i_mgm_id AND active) AND active;
	UPDATE svcgrp_flat	SET import_last_seen=i_current_import_id WHERE svcgrp_flat_id IN
		(SELECT svc_id FROM service WHERE service.mgm_id=i_mgm_id AND active) AND active;
	RAISE DEBUG 'import_svc_refhandler_main - 7 - end';
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_change
-- Zweck:     ueberall dort, wo ein service geaendert wurde,
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_svc_id, new_svc_id, current_import_id
-- RETURNS:   VOID
-- an allen Stellen, an denen das alte Objekt in einem aktiven Datensatz referenziert wird,
-- muss es durch das neue ersetzt werden
CREATE OR REPLACE FUNCTION import_svc_refhandler_change(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus service
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus service
	i_current_import_id ALIAS FOR $3; -- zum Heraussuchen des group_delimiters
	r_svc_info RECORD;			-- Record zum Sammeln diverser service-Infos (mgm_id, svc_member)
	r_import_info RECORD;		-- Record zum Sammeln von import-Infos (group_del)
BEGIN
	IF are_equal(i_old_id,i_new_id) THEN
		RAISE EXCEPTION 'old and new svc id are identical!';
	END IF;
	PERFORM import_svc_refhandler_change_svcgrp_member_refs			(i_old_id, i_new_id, i_current_import_id);
	PERFORM import_svc_refhandler_change_rule_service_refs			(i_old_id, i_new_id, i_current_import_id);
	-- jetzt noch die Aufloesung der Gruppen
	IF is_svc_group(i_old_id) THEN -- wenn Gruppe, dann werden die Beziehungen der moeglicherweise
		-- neuen Mitglieder komplett neu eingetragen und die alten Beziehungen auf inaktiv gesetzt
--		IF NOT is_svc_group(i_new_id) THEN
--			RAISE EXCEPTION 'trying to replace group with non-group service';
--		END IF;
		-- Daten zum Beschicken der Funktion insert_svc_group_relations sammeln
		SELECT INTO r_import_info delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO r_svc_info mgm_id,svc_member_refs FROM service WHERE svc_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_svc_refhandler_svcgrp_add_group
			(i_new_id,r_svc_info.svc_member_refs,r_import_info.delimiter_group,r_svc_info.mgm_id,i_current_import_id);
	END IF;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_insert
-- Zweck:     pruefen ob Gruppe, wenn ja Gruppenzugehoerigkeiten der Gruppe aufloesen
-- Zweck:     und in svcgrp eintragen
-- Parameter: $1: svc_id des neuen Objekts
-- Parameter: $2: Trennzeichen zwischen Gruppenmitgliedern
-- verwendete
-- Funktionen: add_references_for_inserted_group_svc
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_insert (BIGINT,varchar,BIGINT) RETURNS VOID AS $$
DECLARE
	i_new_id ALIAS FOR $1;
	v_delimiter ALIAS FOR $2;
	i_current_import_id ALIAS FOR $3;
    r_svc_info RECORD;
BEGIN 
	-- Aufloesung der Gruppen
	IF is_svc_group(i_new_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und
		-- Mitgliedern eingetragen
		SELECT INTO r_svc_info mgm_id,svc_member_refs FROM service WHERE svc_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_svc_refhandler_svcgrp_add_group
			(i_new_id,r_svc_info.svc_member_refs,v_delimiter,r_svc_info.mgm_id,i_current_import_id);
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_change_flat
-- Zweck:     flat group relations neu setzen
-- Parameter: old_svc_id, new_svc_id, current_import_id
-- RETURNS:   VOID
-- an allen Stellen, an denen das alte Objekt in einem aktiven Datensatz referenziert wird,
-- muss es durch das neue ersetzt werden
CREATE OR REPLACE FUNCTION import_svc_refhandler_change_flat(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus service
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus service
	i_current_import_id ALIAS FOR $3; -- zum Heraussuchen des group_delimiters
	r_svc_info RECORD;			-- Record zum Sammeln diverser service-Infos (mgm_id, svc_member)
	r_import_info RECORD;		-- Record zum Sammeln von import-Infos (group_del)
BEGIN
	IF are_equal(i_old_id,i_new_id) THEN
		RAISE EXCEPTION 'old and new svc id are identical!';
	END IF;
	PERFORM import_svc_refhandler_change_svcgrp_flat_member_refs	(i_old_id, i_new_id, i_current_import_id);
	IF is_svc_group(i_old_id) THEN -- wenn Gruppe, dann werden die Beziehungen der moeglicherweise
		SELECT INTO r_import_info delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO r_svc_info mgm_id,svc_member_refs FROM service WHERE svc_id=i_new_id;
		PERFORM import_svc_refhandler_svcgrp_flat_add_group
			(i_new_id,i_new_id,0,i_current_import_id);
	END IF;
	PERFORM import_svc_refhandler_svcgrp_flat_add_self(i_new_id, i_current_import_id);	
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_insert_flat
-- Zweck:     pruefen ob Gruppe, wenn ja Gruppenzugehoerigkeiten der Gruppe aufloesen
-- Zweck:     und in svcgrp_flat eintragen
-- Parameter: $1: svc_id des neuen Objekts
-- Parameter: $2: Trennzeichen zwischen Gruppenmitgliedern
-- verwendete
-- Funktionen: add_references_for_inserted_group_svc
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_insert_flat (BIGINT,varchar,integer) RETURNS VOID AS $$
DECLARE
	i_new_id ALIAS FOR $1;
	v_delimiter ALIAS FOR $2;
	i_current_import_id ALIAS FOR $3;
    r_svc_info RECORD;
BEGIN 
	-- Aufloesung der Gruppen
	IF is_svc_group(i_new_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und
		SELECT INTO r_svc_info mgm_id,svc_member_refs FROM service WHERE svc_id=i_new_id;
		PERFORM import_svc_refhandler_svcgrp_flat_add_group (i_new_id,i_new_id,0,i_current_import_id);
	END IF;
	PERFORM import_svc_refhandler_svcgrp_flat_add_self(i_new_id, i_current_import_id);	
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_svcgrp_add_group
-- Zweck:     die Gruppenzugehoerigkeiten der Gruppe $1 aufloesen
-- Zweck:     und in svcgrp eintragen
-- Parameter: $1: svc_id der Gruppe
-- Parameter: $2: String mit Gruppenmitgliedern
-- Parameter: $3: Trennzeichen zwischen den Gruppenmitgliedern in $1
-- Parameter: $4: Management-ID
-- verwendete
-- Funktionen: f_add_single_group_member_service, insert_svc_group_relations (rekursiv)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_svcgrp_add_group (BIGINT,varchar,varchar,integer,integer) RETURNS VOID AS $$
DECLARE
	i_group_id ALIAS FOR $1;
	v_member_string ALIAS FOR $2;
	v_delimiter ALIAS FOR $3;
	i_mgm_id ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
 	v_current_member varchar;
BEGIN
	RAISE DEBUG 'import_svc_refhandler_svcgrp_add_group - 1 starting, v_member_string=%', v_member_string;
	IF v_member_string IS NULL OR v_member_string='' THEN RETURN; END IF;
	FOR v_current_member IN SELECT member FROM regexp_split_to_table(v_member_string, E'\\' || v_delimiter) AS member LOOP
		RAISE DEBUG 'import_svc_refhandler_svcgrp_add_group - 2 adding group member ref for %', v_current_member;
		PERFORM import_svc_refhandler_svcgrp_add_single_groupmember(v_current_member, i_group_id, i_mgm_id, i_current_import_id);
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_svcgrp_flat_add_self
-- Zweck:     der Dienst$1 (ob Gruppe oder simple-Object) wird als sein eigenes svcgrp_flat-Mitglied eingetragen
--            Dient dazu, einfacher nach geschachtelten Diensten zu suchen
-- Parameter: $1: svc_id eines Dienstes
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_svcgrp_flat_add_self (BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
    i_svc_id	ALIAS FOR $1;
	i_current_import_id ALIAS FOR $2;
BEGIN 
	INSERT INTO svcgrp_flat (svcgrp_flat_id,svcgrp_flat_member_id,import_created,import_last_seen)
		 VALUES (i_svc_id,i_svc_id,i_current_import_id,i_current_import_id);
	RETURN;
END; 
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:   import_svc_refhandler_svcgrp_add_single_groupmember
-- Zweck:      die Gruppenzugehoerigkeiten der Gruppe $2 aufloesen
-- Zweck:      und in svcgrp eintragen
-- Parameter:  $1: Name eines einzelnen Gruppenmitglieds
-- Parameter:  $2: svc_id der Gruppe
-- Parameter:  $3: Management-ID
-- verwendete
-- Funktionen: KEINE
-- RETURNS:    VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_svcgrp_add_single_groupmember(VARCHAR,BIGINT,INTEGER,BIGINT) RETURNS VOID AS $$
DECLARE
	v_member_name ALIAS FOR $1;
	i_group_id ALIAS FOR $2;
	i_mgm_id ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
	i_svc_id integer;
	r_group RECORD;
	v_error_str VARCHAR;
	r_debug RECORD;	
BEGIN 
	SELECT INTO i_svc_id svc_id FROM service WHERE mgm_id=i_mgm_id AND svc_uid = v_member_name AND active;
	IF NOT FOUND THEN
		SELECT INTO r_group svc_name,svc_uid FROM service WHERE svc_id = i_group_id;
		PERFORM error_handling
			('ERR_GRP_MISS_SVC', r_group.svc_name || ' (uid of group: ' || r_group.svc_uid || '), ' || v_member_name);
	ELSE -- debugging for duplicate members
		SELECT INTO r_debug svcgrp_id,svcgrp_member_id FROM svcgrp
			WHERE svcgrp_id=i_group_id AND svcgrp_member_id=i_svc_id AND active;
		IF FOUND THEN
			SELECT INTO r_debug svc_name,svc_uid FROM service WHERE svc_id = i_group_id;
			v_error_str := '';
			IF NOT r_debug.svc_name IS NULL THEN
				v_error_str := 'group: ' || r_debug.svc_name || ' (group-uid: ' || r_debug.svc_uid || '), ' || ', ';
			ELSE
				v_error_str := 'unknown group, ';
			END IF;
			IF NOT v_member_name IS NULL THEN
				v_error_str := v_error_str || 'member_uid: ' || v_member_name;
			ELSE
				v_error_str := v_error_str || 'unknown member';
			END IF;
			PERFORM error_handling('ERR_GRP_DBL_SVC', v_error_str);
		ELSE 
			INSERT INTO svcgrp (svcgrp_id,svcgrp_member_id,import_created,import_last_seen)
				VALUES (i_group_id,i_svc_id,i_current_import_id,i_current_import_id);
		END IF;
	END IF;
    RETURN;
END; 
$$ LANGUAGE plpgsql;
 

----------------- ab hier kommen die flat-Funktionen -----------------------

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_svcgrp_flat_add_group
-- Zweck:     die Gruppenzugehoerigkeiten der Gruppe $2 aufloesen
-- Zweck:     und in svcgrp_flat als Teil von $1 eintragen
-- Parameter: $1: svc_id der Top-Level-Gruppe
-- Parameter: $2: svc_id der Unter-Gruppe
-- Parameter: $3: Rekursionsebene - fuer Abbruch nach 20 Rekursionen
-- verwendete
-- Funktionen: insert_svc_group_relations_flat (rekursiv)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_svc_refhandler_svcgrp_flat_add_group (BIGINT,BIGINT,INTEGER,BIGINT) RETURNS VOID AS $$
DECLARE
    i_top_group_id	ALIAS FOR $1;
    i_group_id		ALIAS FOR $2;
    i_rec_level		ALIAS FOR $3;
    i_current_import_id ALIAS FOR $4;
    r_member		RECORD;
    r_svc			RECORD;
    r_member_exists	RECORD;
BEGIN 
	IF i_rec_level>20 THEN
		PERFORM error_handling('ERR_SVC_GROUP_REC_LVL_EXCEEDED');
	END IF;
	FOR r_member IN
		SELECT svcgrp_member_id FROM svcgrp WHERE svcgrp_id=i_group_id
	LOOP
		SELECT INTO r_member_exists * FROM svcgrp_flat WHERE
            svcgrp_flat_id=i_top_group_id AND svcgrp_flat_member_id=r_member.svcgrp_member_id;
        IF NOT FOUND THEN
			INSERT INTO svcgrp_flat (svcgrp_flat_id,svcgrp_flat_member_id,import_created,import_last_seen)
				VALUES (i_top_group_id,r_member.svcgrp_member_id,i_current_import_id,i_current_import_id);
		END IF;
		IF is_svc_group(r_member.svcgrp_member_id) THEN
			PERFORM import_svc_refhandler_svcgrp_flat_add_group
				(i_top_group_id, r_member.svcgrp_member_id, i_rec_level+1, i_current_import_id);
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_change_svcgrp_member_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in svcgrp als member auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_svc_id, new_svc_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_svc_refhandler_change_svcgrp_member_refs(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus service
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus service
	i_current_import_id ALIAS FOR $3;
	r_svcgrp svcgrp%ROWTYPE;
BEGIN
	FOR r_svcgrp IN
        SELECT * FROM svcgrp WHERE svcgrp_member_id=i_old_id AND active
	LOOP -- die neue Beziehung wird eingefuegt
		INSERT INTO svcgrp (svcgrp_id, svcgrp_member_id, import_created, import_last_seen)
			VALUES (r_svcgrp.svcgrp_id, i_new_id, i_current_import_id, i_current_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_change_svcgrp_flat_member_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in svcgrp_flat als member auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_svc_id, new_svc_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_svc_refhandler_change_svcgrp_flat_member_refs(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus service
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus service
	i_current_import_id ALIAS FOR $3;
	r_svcgrp_flat svcgrp_flat%ROWTYPE;
	r_member_exists svcgrp_flat%ROWTYPE;
BEGIN
	FOR r_svcgrp_flat IN
        SELECT * FROM svcgrp_flat WHERE svcgrp_flat_member_id=i_old_id AND active
	LOOP -- es wird die neue Beziehung eingefuegt
		SELECT INTO r_member_exists * FROM svcgrp_flat WHERE
            svcgrp_flat_id=r_svcgrp_flat.svcgrp_flat_id AND svcgrp_flat_member_id=i_new_id;
		IF NOT FOUND THEN -- wenn das Mitglied noch nicht in der Gruppe ist: eintragen - verhindert doppelte Eintraege
			INSERT INTO svcgrp_flat (svcgrp_flat_id,svcgrp_flat_member_id,import_created,import_last_seen)
				VALUES (r_svcgrp_flat.svcgrp_flat_id, i_new_id, i_current_import_id, i_current_import_id);
		END IF;
	END LOOP;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_svc_refhandler_change_rule_service_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in rule_service auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_svc_id, new_svc_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_svc_refhandler_change_rule_service_refs (BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus service
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus service
	i_import_id ALIAS FOR $3; -- id des aktuellen Imports
	r_service rule_service%ROWTYPE;
BEGIN
	FOR r_service IN
        SELECT * FROM rule_service WHERE svc_id=i_old_id AND active
	LOOP -- die neue Beziehung wird eingefuegt
		INSERT INTO rule_service (rule_id,svc_id,rs_create,rs_last_seen)
			VALUES (r_service.rule_id, i_new_id, i_import_id, i_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;
