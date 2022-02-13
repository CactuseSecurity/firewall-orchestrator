-- $Id: iso-usr-refs.sql,v 1.1.2.5 2011-09-28 21:14:05 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-usr-refs.sql,v $

/*
 import_usr_refhandler_main (INTEGER) RETURNS VOID
 import_usr_refhandler_change(INTEGER, INTEGER, INTEGER)
 import_usr_refhandler_insert (integer,varchar) RETURNS VOID
 import_usr_refhandler_usergrp_add_group (integer,varchar,varchar,integer)
 import_usr_refhandler_usergrp_add_single_groupmember(varchar,integer,INTEGER) RETURNS VOID
 import_usr_refhandler_usergrp_flat_add_group (INTEGER,INTEGER,INTEGER) RETURNS VOID
 import_usr_refhandler_change_usergrp_member_refs(INTEGER, INTEGER) RETURNS VOID
 import_usr_refhandler_change_usergrp_flat_member_refs(INTEGER, INTEGER) RETURNS VOID
 import_usr_refhandler_change_rule_from_refs (INTEGER, INTEGER) RETURNS VOID
 import_usr_refhandler_change_rule_to_refs (INTEGER, INTEGER) RETURNS VOID
*/

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_main
-- Zweck:     ueberall dort, wo ein usr veraendert (changed,inserted,deleted) wurde,
-- Zweck:	  muessen die Referenzen entweder:
-- Zweck:     - vom alten auf das neue Element umgebogen werden
-- Zweck:     - fuer das Element geloescht werden
-- Zweck:     - fuer das Element hinzugefuegt werden
-- Parameter: current_import_id
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_main (BIGINT) RETURNS VOID AS $$
DECLARE
	i_current_import_id   ALIAS FOR $1; -- ID des laufenden Imports
	r_user	RECORD;	-- temp usr
	r_ctrl		RECORD;	-- zum Holen des group-delimiters
	v_debug		VARCHAR; --debug-output
	v_user_name	VARCHAR; --debug-output
	i_previous_import_id BIGINT;
	i_mgm_id INTEGER;
BEGIN
	BEGIN
		RAISE DEBUG 'import_usr_refhandler_main - 1 starting';
		SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;
		i_previous_import_id := get_previous_import_id_for_mgmt (i_mgm_id, i_current_import_id);

		SELECT INTO r_ctrl delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		FOR r_user IN -- neue Member-Beziehungen von i_new_id eintragen
			SELECT old_user_id,new_user_id,change_action FROM changelog_user
				WHERE control_id=i_current_import_id AND NOT change_action='D'
		LOOP
			v_debug :=  'old_id: ';
			IF r_user.old_user_id IS NULL THEN v_debug := v_debug || 'NULL'; ELSE v_debug := v_debug || CAST(r_user.old_user_id AS VARCHAR); END IF;
			v_debug :=  v_debug || ', new_id: ';
			IF r_user.new_user_id IS NULL THEN v_debug := v_debug || 'NULL'; ELSE
				SELECT INTO v_user_name user_name FROM usr WHERE user_id=r_user.new_user_id;
				v_debug := v_debug || CAST(r_user.new_user_id AS VARCHAR) || ', new_user_name=' || v_user_name;
			END IF;
			IF r_user.change_action = 'I' THEN
				RAISE DEBUG 'import_usr_refhandler_main - 2 inserting - %', v_debug;
				PERFORM import_usr_refhandler_insert(r_user.new_user_id,r_ctrl.delimiter_group,i_current_import_id);
			ELSIF r_user.change_action = 'C' THEN
				RAISE DEBUG 'import_usr_refhandler_main - 2 changing - %', v_debug;
				PERFORM import_usr_refhandler_change(r_user.old_user_id,r_user.new_user_id,i_current_import_id);
			END IF;
		END LOOP;
		FOR r_user IN -- neue Member-Beziehungen von i_new_id eintragen
			SELECT old_user_id,new_user_id,change_action FROM changelog_user
				WHERE control_id=i_current_import_id AND NOT change_action='D'
		LOOP
			IF r_user.change_action = 'I' THEN
				PERFORM import_usr_refhandler_insert_flat(r_user.new_user_id,r_ctrl.delimiter_group,i_current_import_id);
			ELSIF r_user.change_action = 'C' THEN
				PERFORM import_usr_refhandler_change_flat(r_user.old_user_id,r_user.new_user_id,i_current_import_id);
			END IF;
		END LOOP;
		----------------------------------------------------------------------------------------------
		-- die alten (nicht mehr gueltigen) Objekte auf non-active setzen
		UPDATE usergrp SET active=FALSE WHERE usergrp_id IN
			(SELECT old_user_id FROM changelog_user WHERE control_id=i_current_import_id GROUP BY old_user_id);
		UPDATE usergrp_flat SET active=FALSE WHERE usergrp_flat_id IN
			(SELECT old_user_id FROM changelog_user WHERE control_id=i_current_import_id GROUP BY old_user_id);
		UPDATE usergrp SET active=FALSE WHERE usergrp_member_id IN
			(SELECT old_user_id FROM changelog_user WHERE control_id=i_current_import_id GROUP BY old_user_id);
		UPDATE usergrp_flat SET active=FALSE WHERE usergrp_flat_member_id IN
			(SELECT old_user_id FROM changelog_user WHERE control_id=i_current_import_id GROUP BY old_user_id);
	--	UPDATE rule_from SET active=FALSE WHERE user_id IN
	--		(SELECT old_user_id FROM changelog_user WHERE control_id=i_current_import_id GROUP BY old_user_id);
		UPDATE rule_from SET active=FALSE WHERE user_id IN
			(SELECT old_user_id FROM changelog_user WHERE control_id=i_current_import_id AND NOT old_user_id IS NULL);
			
		UPDATE rule_from	SET rf_last_seen=i_current_import_id WHERE rule_id IN
			(SELECT rule_id FROM rule WHERE mgm_id=i_mgm_id AND active) AND active;
		UPDATE usergrp		SET import_last_seen=i_current_import_id WHERE usergrp_id IN
			(SELECT user_id FROM usr WHERE mgm_id=i_mgm_id AND active) AND active;
		UPDATE usergrp_flat	SET import_last_seen=i_current_import_id WHERE usergrp_flat_id IN
			(SELECT user_id FROM usr WHERE mgm_id=i_mgm_id AND active) AND active;
			
		FOR r_user IN -- loop for rule_svc_resolved
			SELECT old_user_id,new_user_id,change_action FROM changelog_user WHERE control_id=i_current_import_id -- AND (change_action = 'C' OR change_action = 'D')
		LOOP
			PERFORM import_rule_resolved_usr (i_mgm_id, NULL, r_user.old_user_id, r_user.new_user_id, i_current_import_id, r_user.change_action, 'U');
		END LOOP;
		RAISE DEBUG 'import_usr_refhandler_main - 3 completed';
	EXCEPTION
	    WHEN others THEN
            raise notice 'import_user_refhandler_main - uncommittable state. Rolling back';
            raise EXCEPTION '% %', SQLERRM, SQLSTATE;    
	END;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_change
-- Zweck:     ueberall dort, wo ein usr geaendert wurde,
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_user_id, new_user_id, current_import_id
-- RETURNS:   VOID
-- an allen Stellen, an denen das alte Objekt in einem aktiven Datensatz referenziert wird,
-- muss es durch das neue ersetzt werden
CREATE OR REPLACE FUNCTION import_usr_refhandler_change(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus usr
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus usr
	i_current_import_id ALIAS FOR $3; -- zum Heraussuchen des group_delimiters
	r_user_info RECORD;			-- Record zum Sammeln diverser usr-Infos (mgm_id, user_member)
	r_import_info RECORD;		-- Record zum Sammeln von import-Infos (group_del)
BEGIN
	IF are_equal(i_old_id,i_new_id) THEN
		RAISE EXCEPTION 'old and new user id are identical!';
	END IF;
	PERFORM import_usr_refhandler_change_usergrp_member_refs		(i_old_id, i_new_id, i_current_import_id);
	PERFORM import_usr_refhandler_change_rule_usr_refs				(i_old_id, i_new_id, i_current_import_id);
	-- jetzt noch die Aufloesung der Gruppen
	IF is_user_group(i_old_id) THEN -- wenn Gruppe, dann werden die Beziehungen der moeglicherweise
			-- neuen Mitglieder komplett neu eingetragen
--		IF NOT is_user_group(i_new_id) THEN
--			RAISE EXCEPTION 'trying to replace group with non-group usr';
--		END IF;
		-- Daten zum Beschicken der Funktion insert_user_group_relations sammeln
		SELECT INTO r_import_info delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO r_user_info mgm_id,user_member_names,user_member_refs FROM usr WHERE user_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_usr_refhandler_usergrp_add_group
			(i_new_id,r_user_info.user_member_refs,r_import_info.delimiter_group,r_user_info.mgm_id,i_current_import_id);
	END IF;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_insert
-- Zweck:     pruefen ob Gruppe, wenn ja Gruppenzugehoerigkeiten der Gruppe aufloesen
-- Zweck:     und in usergrp  eintragen
-- Parameter: $1: user_id des neuen Objekts
-- Parameter: $2: Trennzeichen zwischen Gruppenmitgliedern
-- verwendete
-- Funktionen: add_references_for_inserted_group_user
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_insert (BIGINT,varchar,BIGINT) RETURNS VOID AS $$
DECLARE
	i_new_id ALIAS FOR $1;
	v_delimiter ALIAS FOR $2;
	i_current_import_id ALIAS FOR $3;
    r_user_info RECORD;
BEGIN 
--	RAISE NOTICE 'entering import_usr_refhandler_insert with user_id %', i_new_id;
	-- Aufloesung der Gruppen
	IF is_user_group(i_new_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und
		-- Mitgliedern eingetragen
		SELECT INTO r_user_info mgm_id,user_member_refs FROM usr WHERE user_id=i_new_id;
		-- neue Member-Beziehungen von i_new_id eintragen
		PERFORM import_usr_refhandler_usergrp_add_group
			(i_new_id,r_user_info.user_member_refs,v_delimiter,r_user_info.mgm_id,i_current_import_id);
	END IF;
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_change_flat
-- Zweck:     ueberall dort, wo ein usr geaendert wurde,
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_user_id, new_user_id, current_import_id
-- RETURNS:   VOID
-- an allen Stellen, an denen das alte Objekt in einem aktiven Datensatz referenziert wird,
-- muss es durch das neue ersetzt werden
CREATE OR REPLACE FUNCTION import_usr_refhandler_change_flat(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus usr
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus usr
	i_current_import_id ALIAS FOR $3; -- zum Heraussuchen des group_delimiters
	r_user_info RECORD;			-- Record zum Sammeln diverser usr-Infos (mgm_id, user_member)
	r_import_info RECORD;		-- Record zum Sammeln von import-Infos (group_del)
BEGIN
	IF are_equal(i_old_id,i_new_id) THEN
		RAISE EXCEPTION 'old and new user id are identical!';
	END IF;
	PERFORM import_usr_refhandler_change_usergrp_flat_member_refs	(i_old_id, i_new_id, i_current_import_id);
	IF is_user_group(i_old_id) THEN -- wenn Gruppe, dann werden die Beziehungen der moeglicherweise
		SELECT INTO r_import_info delimiter_group FROM import_control WHERE control_id=i_current_import_id;
		SELECT INTO r_user_info mgm_id,user_member_names,user_member_refs FROM usr WHERE user_id=i_new_id;
		PERFORM import_usr_refhandler_usergrp_flat_add_group(i_new_id,i_new_id,0,i_current_import_id);
	END IF;
	PERFORM import_usr_refhandler_usergrp_flat_add_self(i_new_id,i_current_import_id);			
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_insert_flat
-- Zweck:     pruefen ob Gruppe, wenn ja Gruppenzugehoerigkeiten der Gruppe aufloesen
-- Zweck:     und in usergrp_flat eintragen
-- Parameter: $1: user_id des neuen Objekts
-- Parameter: $2: Trennzeichen zwischen Gruppenmitgliedern
-- verwendete
-- Funktionen: add_references_for_inserted_group_user
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_insert_flat (BIGINT,varchar,BIGINT) RETURNS VOID AS $$
DECLARE
	i_new_id ALIAS FOR $1;
	v_delimiter ALIAS FOR $2;
	i_current_import_id ALIAS FOR $3;
    r_user_info RECORD;
BEGIN 
	IF is_user_group(i_new_id) THEN -- wenn Gruppe, dann werden die Beziehungen zwischen Gruppe und
		SELECT INTO r_user_info mgm_id,user_member_refs FROM usr WHERE user_id=i_new_id;
		PERFORM import_usr_refhandler_usergrp_flat_add_group (i_new_id,i_new_id,0,i_current_import_id);
	END IF;
	PERFORM import_usr_refhandler_usergrp_flat_add_self(i_new_id,i_current_import_id);			
	RETURN;
END;
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_usergrp_add_group
-- Zweck:     die Gruppenzugehoerigkeiten der Gruppe $1 aufloesen
-- Zweck:     und in usergrp eintragen
-- Parameter: $1: user_id der Gruppe
-- Parameter: $2: String mit Gruppenmitgliedern
-- Parameter: $3: Trennzeichen zwischen den Gruppenmitgliedern in $1
-- Parameter: $4: Management-ID
-- Parameter: $5: ID des aktuellen Imports
-- verwendete
-- Funktionen: f_add_single_group_member_usr, insert_user_group_relations (rekursiv)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_usergrp_add_group (BIGINT,varchar,varchar,integer,BIGINT) RETURNS VOID AS $$
DECLARE
	i_group_id ALIAS FOR $1;
	v_member_string ALIAS FOR $2;
	v_delimiter ALIAS FOR $3;
	i_mgm_id ALIAS FOR $4;
	i_current_import_id ALIAS FOR $5;
 	v_current_member varchar;
BEGIN
	RAISE DEBUG 'import_usr_refhandler_usergrp_add_group - 1 starting, v_member_string=%', v_member_string;
	IF v_member_string IS NULL OR v_member_string='' THEN RETURN; END IF;
	FOR v_current_member IN SELECT member FROM regexp_split_to_table(v_member_string, E'\\' || v_delimiter) AS member LOOP
		RAISE DEBUG 'import_usr_refhandler_usergrp_add_group - 2 adding group member ref for %', v_current_member;
		PERFORM import_usr_refhandler_usergrp_add_single_groupmember(v_current_member, i_group_id, i_mgm_id, i_current_import_id);
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:   import_usr_refhandler_usergrp_add_single_groupmember
-- Zweck:      die Gruppenzugehoerigkeiten der Gruppe $2 aufloesen
-- Zweck:      und in usergrp eintragen
-- Parameter:  $1: Name eines einzelnen Gruppenmitglieds
-- Parameter:  $2: user_id der Gruppe
-- Parameter:  $3: Management-ID
-- Parameter:  $4: ID des aktuellen Imports
-- verwendete
-- Funktionen: KEINE
-- RETURNS:    VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_usergrp_add_single_groupmember(VARCHAR,BIGINT,INTEGER,BIGINT) RETURNS VOID AS $$
DECLARE
	v_member_name ALIAS FOR $1;
	i_group_id ALIAS FOR $2;
	i_mgm_id ALIAS FOR $3;
	i_current_import_id ALIAS FOR $4;
	i_user_id BIGINT;
	r_group RECORD;
BEGIN 
	SELECT INTO i_user_id user_id FROM usr WHERE mgm_id=i_mgm_id AND user_uid = v_member_name AND active;
	IF NOT FOUND THEN
		SELECT INTO r_group user_name FROM usr WHERE user_id = i_group_id;
		PERFORM error_handling('ERR_GRP_MISS_USR', r_group.user_name || ', ' || v_member_name);
	END IF;
	INSERT INTO usergrp (usergrp_id,usergrp_member_id,import_created,import_last_seen)
		VALUES (i_group_id,i_user_id,i_current_import_id,i_current_import_id);
    RETURN;
END; 
$$ LANGUAGE plpgsql;
 
----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_change_usergrp_member_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in usergrp als member auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_user_id, new_user_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_usr_refhandler_change_usergrp_member_refs(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus usr
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus usr
	i_current_import_id ALIAS FOR $3;
	r_usergrp usergrp%ROWTYPE;
BEGIN
	FOR r_usergrp IN
--        SELECT * FROM usergrp WHERE usergrp_member_id=i_old_id AND import_created<=i_current_import_id AND import_last_seen>=i_current_import_id
        SELECT * FROM usergrp WHERE usergrp_member_id=i_old_id AND active
	LOOP -- die neue Beziehung wird eingefuegt
		INSERT INTO usergrp (usergrp_id,usergrp_member_id,import_created,import_last_seen)
			VALUES (r_usergrp.usergrp_id, i_new_id,i_current_import_id,i_current_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;


----------------- ab hier kommen die flat-Funktionen -----------------------


----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_usergrp_flat_add_self
-- Zweck:     der simple User $1 (keine Gruppe) wird als sein eigenes usergrp_flat-Mitglied eingetragen
--            Dient dazu, einfacher nach geschachtelten Benutzern zu suchen
-- Parameter: $1: user_id des Benutzers
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_usergrp_flat_add_self (BIGINT,BIGINT) RETURNS VOID AS $$
DECLARE
	i_user_id	ALIAS FOR $1;
	i_current_import_id	ALIAS FOR $2;
	
--	v_temp VARCHAR;
BEGIN 
--	SELECT INTO v_temp user_name FROM usr WHERE user_id=i_user_id;
--	v_temp := v_temp || ', id: ' || CAST(i_user_id AS VARCHAR);
--	RAISE NOTICE 'adding usergrp_flat_self: %', v_temp;
	INSERT INTO usergrp_flat (usergrp_flat_id,usergrp_flat_member_id,import_created,import_last_seen)
		VALUES (i_user_id,i_user_id,i_current_import_id,i_current_import_id);
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_usergrp_flat_add_group
-- Zweck:     die Gruppenzugehoerigkeiten der Gruppe $2 aufloesen
-- Zweck:     und in usergrp_flat als Teil von $1 eintragen
-- Parameter: $1: user_id der Top-Level-Gruppe
-- Parameter: $2: user_id der Unter-Gruppe
-- Parameter: $3: Rekursionsebene - fuer Abbruch nach 20 Rekursionen
-- verwendete
-- Funktionen: insert_user_group_relations_flat (rekursiv)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_refhandler_usergrp_flat_add_group (BIGINT,BIGINT,INTEGER,BIGINT) RETURNS VOID AS $$
DECLARE
    i_top_group_id	ALIAS FOR $1;
    i_group_id		ALIAS FOR $2;
    i_rec_level		ALIAS FOR $3;
    i_current_import_id ALIAS FOR $4;
    r_member		RECORD;
    r_user			RECORD;
	r_member_exists usergrp_flat%ROWTYPE;
BEGIN 
	IF i_rec_level>20 THEN
		SELECT INTO r_member * FROM usr WHERE user_id = i_top_group_id;
--		RAISE NOTICE 'user group recursion reached its limit for top group %:', r_member.user_name;
		SELECT INTO r_member * FROM usr WHERE user_id = i_group_id;
--		RAISE NOTICE 'user group recursion reached its limit for member group %:', r_member.user_name;
		PERFORM error_handling('ERR_OBJ_GROUP_REC_LVL_EXCEEDED');
	END IF;
	FOR r_member IN
		SELECT usergrp_member_id FROM usergrp WHERE usergrp_id=i_group_id
	LOOP
		SELECT INTO r_member_exists * FROM usergrp_flat WHERE
			usergrp_flat_id=i_top_group_id AND usergrp_flat_member_id=r_member.usergrp_member_id;
		IF NOT FOUND THEN
			INSERT INTO usergrp_flat (usergrp_flat_id,usergrp_flat_member_id,import_created,import_last_seen)
				VALUES (i_top_group_id,r_member.usergrp_member_id,i_current_import_id,i_current_import_id);
		END IF;
		IF is_user_group(r_member.usergrp_member_id) THEN
			PERFORM import_usr_refhandler_usergrp_flat_add_group
				(i_top_group_id, r_member.usergrp_member_id, i_rec_level+1, i_current_import_id);
		END IF;
	END LOOP;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_change_usergrp_flat_member_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in usergrp_flat als member auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_user_id, new_user_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_usr_refhandler_change_usergrp_flat_member_refs(BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus usr
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus usr
	i_current_import_id ALIAS FOR $3;
	r_usergrp_flat usergrp_flat%ROWTYPE;
	r_member_exists usergrp_flat%ROWTYPE;
--	v_temp VARCHAR;
--	v_temp1 VARCHAR;
BEGIN
	FOR r_usergrp_flat IN
        SELECT * FROM usergrp_flat WHERE usergrp_flat_member_id=i_old_id AND active
	LOOP -- es wird die neue Beziehung eingefuegt, wenn sie noch nicht existiert
		SELECT INTO r_member_exists * FROM usergrp_flat WHERE
			usergrp_flat.usergrp_flat_id=r_usergrp_flat.usergrp_flat_id AND usergrp_flat_member_id=i_new_id;
		IF NOT FOUND THEN
--			SELECT INTO v_temp user_name FROM usr WHERE user_id=i_new_id;
--			v_temp := 'adding usergrp_flat_self: ' || v_temp;
--			SELECT INTO v_temp1 user_name FROM usr WHERE user_id=r_usergrp_flat.usergrp_flat_id;
--			v_temp := v_temp || ' into flat group ' || v_temp1;
--			RAISE NOTICE '%', v_temp;
			INSERT INTO usergrp_flat (usergrp_flat_id,usergrp_flat_member_id,import_created,import_last_seen)
				VALUES (r_usergrp_flat.usergrp_flat_id, i_new_id, i_current_import_id, i_current_import_id);
		END IF;
	END LOOP;
	RETURN;
END;
$$ language plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_refhandler_change_rule_usr_refs
-- Zweck:     ueberall dort, wo das alte (geaenderte) Objekt in rule_from auftaucht
-- Zweck:	  muessen die Referenzen vom alten auf das neue Objekt umgebogen werden
-- Parameter: old_user_id, new_user_id, import_id
-- RETURNS:   VOID
CREATE OR REPLACE FUNCTION import_usr_refhandler_change_rule_usr_refs (BIGINT, BIGINT, BIGINT) RETURNS VOID AS $$
DECLARE
	i_old_id ALIAS FOR $1; -- id des bestehenden Datensatzes aus usr
	i_new_id ALIAS FOR $2; -- id des neuen Datensatzes aus usr
	i_current_import_id ALIAS FOR $3;
	r_usr rule_from%ROWTYPE;
BEGIN
	FOR r_usr IN
        SELECT * FROM rule_from WHERE user_id=i_old_id AND active
	LOOP -- zusaetzlich wird die neue Beziehung eingefuegt
		INSERT INTO rule_from (rule_id,obj_id,user_id,rf_create,rf_last_seen)
			VALUES (r_usr.rule_id, r_usr.obj_id, i_new_id, i_current_import_id, i_current_import_id);
	END LOOP;
	RETURN;
END;
$$ language plpgsql;
