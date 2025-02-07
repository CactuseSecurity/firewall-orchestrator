-- $Id: iso-usr-import.sql,v 1.1.2.6 2011-05-12 12:11:52 tim Exp $
-- $Source: /home/cvs/iso/package/install/database/Attic/iso-usr-import.sql,v $

----------------------------------------------------
-- FUNCTION:  import_users
-- Zweck:     fuegt alle Benutzer des aktuellen Imports in die usr-Tabelle
-- Zweck:     verwendet dazu die Funktionen insert_single_user und resolve_user_groups
-- Parameter: KEINE
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_main (BIGINT,BOOLEAN) RETURNS VOID AS $$
DECLARE
    i_current_import_id ALIAS FOR $1; -- ID des aktiven Imports
    b_is_initial_import ALIAS FOR $2; -- ID des aktiven Imports
    i_mgm_id  INTEGER; -- zum Holen der mgm_ID fuer Loeschen von Elementen
    r_usr  RECORD;  -- Datensatz mit einzelner svc_id aus import_service-Tabelle des zu importierenden Services
    v_group_del   VARCHAR; -- Trennzeichen fuer Gruppenmitglieder
	t_last_change_time TIMESTAMP;
	r_last_times RECORD;
BEGIN 
	SELECT INTO i_mgm_id mgm_id FROM import_control WHERE control_id=i_current_import_id;

/*
	IF NOT b_is_initial_import THEN	-- Objekte ausklammern, die vor dem vorherigen Import-Zeitpunkt geaendert wurden, Tuning-Masznahme
		SELECT INTO r_last_times MAX(start_time) AS last_import_time, MAX(last_change_in_config) AS last_change_time
			FROM import_control WHERE mgm_id=i_mgm_id AND NOT control_id=i_current_import_id AND successful_import;
		IF (r_last_times.last_change_time IS NULL) THEN t_last_change_time := r_last_times.last_import_time;
		ELSE 
			IF (r_last_times.last_import_time<r_last_times.last_change_time) THEN t_last_change_time := r_last_times.last_import_time;
		 	ELSE t_last_change_time := r_last_times.last_change_time;
		 	END IF;
	 	END IF;
		t_last_change_time := t_last_change_time - CAST('24 hours' AS INTERVAL); -- einen Tag abziehen, falls Zeitsync-Probleme
		UPDATE usr SET user_last_seen=i_current_import_id WHERE mgm_id=i_mgm_id AND active AND NOT user_uid IS NULL AND user_uid IN
			(SELECT user_uid FROM import_user WHERE NOT last_change_time IS NULL AND last_change_time<t_last_change_time );
		DELETE FROM import_user WHERE last_change_time<t_last_change_time AND NOT last_change_time IS NULL AND NOT user_uid IS NULL;
	END IF;
*/			
	-- import-loop for user_import
	FOR r_usr IN -- jedes Objekt wird mittels insert_single_nwobj eingefuegt
		SELECT user_id FROM import_user WHERE control_id = i_current_import_id
	LOOP
		RAISE DEBUG 'processing user %', r_usr.user_id;
		PERFORM import_usr_single(i_current_import_id, i_mgm_id, r_usr.user_id, b_is_initial_import);
	END LOOP;
	
	IF NOT b_is_initial_import THEN	
		PERFORM import_usr_mark_deleted (i_current_import_id, i_mgm_id); -- geloeschte Elemente markieren als NOT active
	END IF;
	RETURN;
END; 
$$ LANGUAGE plpgsql;

----------------------------------------------------
-- FUNCTION:  import_usr_mark_deleted
-- Zweck:     markiert alle nicht mehr vorhandenen User als not active
-- Parameter: current_control_id, mgm_id
-- Parameter: import_user.usr_id (die ID des zu importierenden Users)
-- RETURNS:   VOID
--
CREATE OR REPLACE FUNCTION import_usr_mark_deleted(BIGINT,INTEGER) RETURNS VOID AS $$
DECLARE
    i_current_import_id ALIAS FOR $1;
    i_mgm_id            ALIAS FOR $2;
    i_previous_import_id  BIGINT; -- zum Holen der import_ID des vorherigen Imports fuer das Mgmt
    r_usr  RECORD;  -- Datensatz mit einzelner svc_id aus import_service-Tabelle des zu importierenden Services
    i_import_admin_id	INTEGER;
BEGIN
    i_previous_import_id := get_previous_import_id_for_mgmt(i_mgm_id,i_current_import_id);
	i_import_admin_id := get_last_change_admin_of_obj_delete (i_current_import_id);
    RAISE DEBUG 'user_mark_deleted, previous_id: %', i_previous_import_id;
    IF NOT i_previous_import_id IS NULL THEN -- wenn das Management nicht zum ersten Mal importiert wurde
        -- alle nicht mehr vorhandenen Services in changelog_object als geloescht eintragen
        FOR r_usr IN -- jedes geloeschte Element wird in changelog_service eingetragen
            SELECT user_id,user_name FROM usr WHERE mgm_id=i_mgm_id AND user_last_seen=i_previous_import_id AND active
        LOOP
            INSERT INTO changelog_user
                (control_id,new_user_id,old_user_id,change_action,import_admin,documented,mgm_id)
                VALUES (i_current_import_id,NULL,r_usr.user_id,'D',	i_import_admin_id, FALSE, i_mgm_id);
            PERFORM error_handling('INFO_USR_DELETED', r_usr.user_name);
        END LOOP;
        -- active-flag von allen in diesem Import geloeschten Objekten loeschen
        UPDATE usr SET active='FALSE' WHERE mgm_id=i_mgm_id AND user_last_seen=i_previous_import_id AND active;
    END IF;
    RETURN;
END;
$$ LANGUAGE plpgsql;


----------------------------------------------------
-- FUNCTION:  import_usr_single(i_current_import_id,i_mgm_id,r_usr.user_id)
-- Zweck:     fuegt einen User des aktuellen Imports in die usr-Tabelle ein
-- Parameter: import_user.user_id (die ID des zu importierenden Users)
-- RETURNS:   TRUE (dummy)
--
-- Function: import_usr_single(BIGINT, integer, BIGINT, boolean)

-- DROP FUNCTION import_usr_single(BIGINT, integer, BIGINT, boolean);

CREATE OR REPLACE FUNCTION import_usr_single(BIGINT, integer, BIGINT, boolean)
  RETURNS void AS
$BODY$
DECLARE
    i_control_id ALIAS FOR $1;   -- Import-ID
    i_mgm_id	ALIAS FOR $2;   -- zugehoerige mgm_id
    id   		ALIAS FOR $3;	-- ID des Users in import_user
	b_is_initial_import ALIAS FOR $4; 
    i_admin_id	INTEGER;   -- ID des last_change_admins
    r_to_import	RECORD;    -- der zu importierende Datensatz aus import_user
    i_color_id	INTEGER;   -- enthaelt color_id
    v_user_name	VARCHAR;   
    i_typ_id	INTEGER;   -- der Typ (simple/group)
    v_farbe		VARCHAR;   
    v_vorname	VARCHAR;   
    v_nachname	VARCHAR;  
    v_authmethod	VARCHAR;  
    v_user_comment	VARCHAR;  
    v_members	VARCHAR;  
    ts_valid_from	TIMESTAMP;  
    ts_valid_until	TIMESTAMP;  
    existing_usr   RECORD; -- der ev. bestehende  Datensatz aus object
    b_insert	BOOLEAN; -- soll eingefuegt werden oder nicht?
    b_change	BOOLEAN; -- hat sich etwas geaendert?
    b_change_security_relevant	BOOLEAN; -- hat sich etwas sicherheitsrelevantes geaendert?
    v_change_id VARCHAR;	-- type of change
	b_is_documented BOOLEAN; 
	t_outtext TEXT; 
	i_change_type INTEGER;
	i_new_user_id  BIGINT;	-- id des neu eingefuegten Users
	v_comment	VARCHAR;
--	r_x RECORD;
BEGIN
    b_insert := FALSE;
    b_change := FALSE;
    b_change_security_relevant := FALSE;
    SELECT INTO r_to_import * FROM import_user WHERE user_id = id; -- zu importierenden Datensatz aus import_user einlesen
 	-- user_namen (username, firstname, lastname) holen
	v_user_name := del_surrounding_spaces(r_to_import.user_name);
	IF r_to_import.user_firstname IS NOT NULL THEN v_vorname := del_surrounding_spaces(r_to_import.user_firstname);
	ELSE v_vorname := NULL;	END IF;
	IF r_to_import.user_lastname IS NOT NULL THEN v_nachname := del_surrounding_spaces(r_to_import.user_lastname);
	ELSE v_nachname := NULL; END IF;
	-- authmethod
	IF r_to_import.user_valid_until IS NOT NULL THEN ts_valid_until := r_to_import.user_valid_until;
	ELSE ts_valid_until := NULL; END IF;
	IF r_to_import.user_comment IS NOT NULL THEN v_user_comment := r_to_import.user_comment;
	ELSE v_user_comment := NULL; END IF;
	IF r_to_import.user_authmethod IS NOT NULL THEN	v_authmethod := del_surrounding_spaces(r_to_import.user_authmethod);
	ELSE v_authmethod := NULL; END IF;
	-- Gruppenmitglieder
	IF r_to_import.user_member_refs IS NOT NULL THEN v_members := del_surrounding_spaces(r_to_import.user_member_refs);
	ELSE v_members := NULL; END IF;
	-- user_typ_id holen
	SELECT INTO i_typ_id usr_typ_id FROM stm_usr_typ WHERE usr_typ_name=r_to_import.user_typ; -- usr_typ holen
	IF NOT FOUND THEN PERFORM error_handling('ERR_USR_TYP_MISS', r_to_import.user_typ); END IF;
	-- color_id holen (normalisiert ohne SPACES und in Kleinbuchstaben)
	v_farbe := LOWER(r_to_import.user_color);
	IF v_farbe IS NULL OR char_length(v_farbe)=0 THEN
		v_farbe := 'black';
	END IF; 
	SELECT INTO i_color_id color_id FROM stm_color WHERE color_name LIKE v_farbe;
	IF NOT FOUND THEN
		RAISE NOTICE 'user color not found: %', v_user_name || ' color ' || v_farbe;
	END IF;
	IF NOT FOUND THEN PERFORM error_handling('ERR_COLOR_MISS', r_to_import.user_color);	END IF;
	SELECT INTO existing_usr * FROM usr WHERE user_name=v_user_name AND mgm_id=i_mgm_id AND active;

	IF FOUND THEN  -- user existiert bereits
		IF (NOT (   
			are_equal(existing_usr.usr_typ_id, i_typ_id) AND
--			are_equal(existing_usr.user_member, v_members) AND
			are_equal(existing_usr.user_member_refs, v_members) AND
			are_equal(existing_usr.user_name, v_user_name) AND
			are_equal(existing_usr.user_firstname, v_vorname) AND
			are_equal(existing_usr.user_lastname, v_nachname) AND
-- probleme mit utf-8			are_equal(existing_usr.user_authmethod, v_authmethod) AND
-- probleme mit utf-8			are_equal(existing_usr.user_valid_from, r_to_import.user_valid_from) AND
-- probleme mit utf-8			are_equal(existing_usr.user_valid_until, r_to_import.user_valid_until) AND
			are_equal(existing_usr.src_restrict, r_to_import.src_restrict) AND
			are_equal(existing_usr.dst_restrict, r_to_import.dst_restrict) AND
			are_equal(existing_usr.time_restrict, r_to_import.time_restrict) AND
			are_equal(existing_usr.user_uid, r_to_import.user_uid)
		))
		THEN
			b_change := TRUE;
			b_change_security_relevant := TRUE;
		END IF;

		IF (NOT( -- ab hier die nicht sicherheitsrelevanten Aenderungen
			are_equal(existing_usr.user_comment, r_to_import.user_comment) AND
			are_equal(existing_usr.user_color_id, i_color_id)
		))
		THEN 
			b_change := TRUE;
		END IF;
		IF (b_change) THEN
			v_change_id := 'INFO_USR_CHANGED';
		ELSE
			UPDATE usr SET user_last_seen = i_control_id WHERE user_id = existing_usr.user_id;
		END IF;
	ELSE
		b_insert := TRUE;
		v_change_id := 'INFO_USR_INSERTED'; 
	END IF;
	IF (b_change OR b_insert) THEN
		PERFORM error_handling(v_change_id, r_to_import.user_name);
		i_admin_id := get_admin_id_from_name(r_to_import.last_change_admin);
		INSERT INTO usr
			(mgm_id,usr_typ_id,user_name,user_firstname,user_lastname,user_valid_until,user_valid_from,
			user_authmethod,user_uid,user_color_id,user_member_names,user_member_refs,last_change_admin, user_last_seen, user_create, user_comment)
		VALUES (i_mgm_id,i_typ_id,v_user_name,v_vorname,v_nachname,ts_valid_until,ts_valid_from,
			v_authmethod,r_to_import.user_uid,i_color_id,r_to_import.user_member_names,v_members,i_admin_id, i_control_id, i_control_id, v_user_comment);
        -- changelog-Eintrag
        SELECT INTO i_new_user_id MAX(user_id) FROM usr WHERE mgm_id=i_mgm_id; -- ein bisschen fragwuerdig
		RAISE DEBUG 'single insert user record got id %', i_new_user_id;
		IF (b_insert) THEN  -- der User wurde neu angelegt
			IF b_is_initial_import THEN
				b_is_documented := TRUE;  t_outtext := 'INITIAL_IMPORT'; i_change_type := 2;
			ELSE
				b_is_documented := FALSE; t_outtext := NULL; i_change_type := 3;
			END IF;	-- fest verdrahtete Werte: weniger gut
			INSERT INTO changelog_user
				(control_id,new_user_id,old_user_id,change_action,import_admin,documented,changelog_user_comment,
					mgm_id,change_type_id)
				VALUES (i_control_id,i_new_user_id,NULL,'I',i_admin_id,b_is_documented,t_outtext,i_mgm_id,i_change_type);
		ELSE -- change
			IF (b_change_security_relevant) THEN
				v_comment := NULL;
				b_is_documented := FALSE;
			ELSE
				v_comment := 'NON_SECURITY_RELEVANT_CHANGE';
				b_is_documented := TRUE;
			END IF;
			INSERT INTO changelog_user
				(control_id,new_user_id,old_user_id,change_action,import_admin,documented,mgm_id,security_relevant,
					changelog_user_comment)
				VALUES (i_control_id,i_new_user_id,existing_usr.user_id,'C',i_admin_id,b_is_documented,i_mgm_id,
					b_change_security_relevant,v_comment);
			-- erst jetzt kann active beim alten user auf FALSE gesetzt werden
			UPDATE usr SET active = FALSE WHERE user_id = existing_usr.user_id; -- altes Objekt auf not active setzen
		END IF;
	END IF;
	RETURN;
END;
$BODY$
  LANGUAGE 'plpgsql' VOLATILE;
